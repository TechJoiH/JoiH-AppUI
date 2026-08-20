[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [Parameter(Mandatory = $true)][string]$BindingSettingsPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)]
    [ValidateScript({
        -not [string]::IsNullOrWhiteSpace($_) -and $_.Length -le 200 -and
        $_ -cmatch '^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*){1,15}$'
    })]
    [string]$LifecycleTestFilter,
    [ValidateRange(1, 3600)][int]$TimeoutSeconds = 120,
    [ValidateRange(1, 100000)][int]$MaxSourceFiles = 10000
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$schemaVersion = 'joih-appui-project-validation-attestation.v3'
$producer = 'integrating-joih-appui/new-appui-validation-attestation.ps1'
$owner = 'Project'
$digestAlgorithm = 'SHA-256'
$digestScope = 'validation-relevant-project-files-v2'
$digestCanonicalization = 'ordinal-portable-path-nul-lower-sha256-lf-v2'
$requiredRuntimeTests = @(
    ($LifecycleTestFilter + '.Open'),
    ($LifecycleTestFilter + '.Refresh'),
    ($LifecycleTestFilter + '.Close'),
    ($LifecycleTestFilter + '.Shutdown')
)
$requiredRuntimeAnyOfTests = @(
    ($LifecycleTestFilter + '.ReleaseScope'),
    ($LifecycleTestFilter + '.SceneRebind')
)

function Get-ExactPropertyValue {
    param(
        [AllowNull()]$Object,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }
    foreach ($property in $Object.PSObject.Properties) {
        if ($property.Name -ceq $Name) {
            return $property.Value
        }
    }
    return $null
}

function Resolve-UnityProjectRoot {
    param([Parameter(Mandatory = $true)][string]$StartPath)

    $current = [System.IO.Path]::GetFullPath($StartPath)
    if (Test-Path -LiteralPath $current -PathType Leaf) {
        $current = Split-Path -Parent $current
    }
    elseif (-not (Test-Path -LiteralPath $current -PathType Container)) {
        throw ("ProjectPath does not exist: {0}" -f $StartPath)
    }

    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if ((Test-Path -LiteralPath (Join-Path $current 'Assets') -PathType Container) -and
            (Test-Path -LiteralPath (Join-Path $current 'Packages') -PathType Container) -and
            (Test-Path -LiteralPath (Join-Path $current 'ProjectSettings') -PathType Container)) {
            return [System.IO.Path]::GetFullPath($current).TrimEnd('\', '/')
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }
        $current = $parent
    }
    throw 'ProjectPath is not inside a Unity project.'
}

function Test-PathWithinRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $pathFull = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if ($pathFull.Equals($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }
    return $pathFull.StartsWith(
        $rootFull + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Convert-ToPortableRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    $prefix = $rootFull + [System.IO.Path]::DirectorySeparatorChar
    if (-not $pathFull.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw ("Validation input escaped the Unity project: {0}" -f $pathFull)
    }
    return $pathFull.Substring($prefix.Length).Replace('\', '/')
}

function Assert-NoReparsePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $current = [System.IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -Force -LiteralPath $current
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw ("Path cannot target or traverse a reparse point: {0}" -f $current)
            }
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or
            $parent.Equals($current, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $current = $parent
    }
}

function Assert-OrdinaryReadableFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description,
        [long]$MaximumBytes = 10485760
    )

    if (Test-ExcludedName -Name ([System.IO.Path]::GetFileName($Path))) {
        throw ("{0} uses a sensitive or excluded filename: {1}" -f $Description, $Path)
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw ("{0} does not exist: {1}" -f $Description, $Path)
    }
    Assert-NoReparsePath -Path $Path
    $file = Get-Item -Force -LiteralPath $Path
    if (($file.Attributes -band [System.IO.FileAttributes]::Hidden) -ne 0) {
        throw ("{0} cannot be hidden: {1}" -f $Description, $Path)
    }
    if ($file.Length -gt $MaximumBytes) {
        throw ("{0} exceeds the bounded read size: {1}" -f $Description, $Path)
    }
    return $file
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = $null
    $sha256 = $null
    try {
        $stream = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        $bytes = $sha256.ComputeHash($stream)
        return [System.BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
    }
    finally {
        if ($null -ne $sha256) {
            $sha256.Dispose()
        }
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Test-ValidationRelevantFile {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    return @('.cs', '.asmdef', '.asmref', '.asset', '.prefab', '.unity', '.meta',
        '.inputactions', '.controller', '.anim') -contains $File.Extension.ToLowerInvariant()
}

function Test-ExcludedName {
    param([Parameter(Mandatory = $true)][string]$Name)

    if ($Name.StartsWith('.', [System.StringComparison]::Ordinal)) {
        return $true
    }
    return ($Name -match '^(?i:library|temp|logs?|obj|secrets?|credentials?|privatekeys?|keys?)$' -or
        $Name -match '(?i:credential|secret|private.?key|token)' -or
        $Name -match '(?i:\.(env|secret|pem|pfx|p12|key|cer|crt)$)')
}

function Get-CanonicalAssetsBinding {
    param([Parameter(Mandatory = $true)]$Entries)

    $records = New-Object 'System.Collections.Generic.List[string]'
    foreach ($entry in $Entries) {
        $records.Add(([string]$entry.path + [char]0 + [string]$entry.sha256)) | Out-Null
    }
    $records.Sort([System.StringComparer]::Ordinal)
    $canonical = if ($records.Count -eq 0) { '' } else {
        [string]::Join("`n", $records.ToArray()) + "`n"
    }
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($canonical)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $digestBytes = $sha256.ComputeHash($bytes)
        $digest = [System.BitConverter]::ToString($digestBytes).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
    return [pscustomobject][ordered]@{
        digest = $digest
        fileCount = $records.Count
    }
}

function Get-ProjectBinding {
    param(
        [Parameter(Mandatory = $true)][string]$UnityRoot,
        [Parameter(Mandatory = $true)][int]$MaximumFiles
    )

    Assert-NoReparsePath -Path $UnityRoot
    $projectVersionPath = Join-Path $UnityRoot 'ProjectSettings\ProjectVersion.txt'
    $manifestPath = Join-Path $UnityRoot 'Packages\manifest.json'
    $lockPath = Join-Path $UnityRoot 'Packages\packages-lock.json'
    $projectSettingsPath = Join-Path $UnityRoot 'ProjectSettings\ProjectSettings.asset'
    $projectVersionFile = Assert-OrdinaryReadableFile -Path $projectVersionPath `
        -Description 'ProjectVersion.txt' -MaximumBytes 1048576
    $manifestFile = Assert-OrdinaryReadableFile -Path $manifestPath `
        -Description 'Packages/manifest.json' -MaximumBytes 2097152
    $lockFile = Assert-OrdinaryReadableFile -Path $lockPath `
        -Description 'Packages/packages-lock.json' -MaximumBytes 4194304

    $projectVersionText = [System.IO.File]::ReadAllText($projectVersionPath)
    if ($projectVersionText -notmatch '(?m)^m_EditorVersion:\s*(?<version>\S+)\s*$') {
        throw 'ProjectVersion.txt does not contain an exact m_EditorVersion fact.'
    }
    $unityVersion = $Matches['version']

    $manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
    $lock = [System.IO.File]::ReadAllText($lockPath) | ConvertFrom-Json
    $manifestDependencies = Get-ExactPropertyValue -Object $manifest -Name 'dependencies'
    $lockDependencies = Get-ExactPropertyValue -Object $lock -Name 'dependencies'
    $manifestAppUI = Get-ExactPropertyValue -Object $manifestDependencies -Name 'com.joih.appui'
    $lockAppUI = Get-ExactPropertyValue -Object $lockDependencies -Name 'com.joih.appui'
    if ([string]::IsNullOrWhiteSpace([string]$manifestAppUI) -and $null -eq $lockAppUI) {
        throw 'The Unity project does not have com.joih.appui installed.'
    }

    $newestInput = $projectVersionFile.LastWriteTimeUtc
    foreach ($knownFile in @($manifestFile, $lockFile)) {
        if ($knownFile.LastWriteTimeUtc -gt $newestInput) {
            $newestInput = $knownFile.LastWriteTimeUtc
        }
    }
    $projectSettingsSha256 = $null
    if (Test-Path -LiteralPath $projectSettingsPath -PathType Leaf) {
        $projectSettingsFile = Assert-OrdinaryReadableFile -Path $projectSettingsPath `
            -Description 'ProjectSettings/ProjectSettings.asset' -MaximumBytes 8388608
        $projectSettingsSha256 = Get-FileSha256 -Path $projectSettingsPath
        if ($projectSettingsFile.LastWriteTimeUtc -gt $newestInput) {
            $newestInput = $projectSettingsFile.LastWriteTimeUtc
        }
    }

    $assetsRoot = Join-Path $UnityRoot 'Assets'
    Assert-NoReparsePath -Path $assetsRoot
    $entries = New-Object 'System.Collections.Generic.List[object]'
    $directories = New-Object 'System.Collections.Generic.Queue[string]'
    $directories.Enqueue($assetsRoot)
    $visitedDirectories = 0
    $enumeratedEntries = 0
    $maxDirectories = [Math]::Max(32, ($MaximumFiles * 4))
    $maxEntries = [Math]::Max(64, ($MaximumFiles * 16))

    while ($directories.Count -gt 0) {
        if ($visitedDirectories -ge $maxDirectories) {
            throw ("Validation scan exceeded MaxDirectories={0}." -f $maxDirectories)
        }
        $directory = $directories.Dequeue()
        $visitedDirectories++
        $directoryInfo = Get-Item -Force -LiteralPath $directory
        if (($directoryInfo.Attributes -band [System.IO.FileAttributes]::Hidden) -ne 0 -or
            ($directoryInfo.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
            (Test-ExcludedName -Name $directoryInfo.Name)) {
            throw ("Validation scan cannot skip a hidden, reparse, or excluded directory: {0}" -f $directory)
        }

        $enumerator = $null
        try {
            $enumerator = [System.IO.Directory]::EnumerateFileSystemEntries($directory).GetEnumerator()
            while ($enumerator.MoveNext()) {
                if ($enumeratedEntries -ge $maxEntries) {
                    throw ("Validation scan exceeded MaxEnumeratedEntries={0}." -f $maxEntries)
                }
                $path = $enumerator.Current
                $enumeratedEntries++
                $attributes = [System.IO.File]::GetAttributes($path)
                if (($attributes -band [System.IO.FileAttributes]::Directory) -ne 0) {
                    $child = New-Object System.IO.DirectoryInfo($path)
                    if (($attributes -band [System.IO.FileAttributes]::Hidden) -ne 0 -or
                        ($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                        (Test-ExcludedName -Name $child.Name)) {
                        throw ("Validation scan cannot skip a hidden, reparse, or excluded directory: {0}" -f $path)
                    }
                    $directories.Enqueue($child.FullName)
                    continue
                }

                $file = New-Object System.IO.FileInfo($path)
                if (-not (Test-ValidationRelevantFile -File $file)) {
                    continue
                }
                if (($attributes -band [System.IO.FileAttributes]::Hidden) -ne 0 -or
                    ($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                    (Test-ExcludedName -Name $file.Name)) {
                    throw ("Validation scan cannot skip a hidden, reparse, or excluded relevant file: {0}" -f $path)
                }
                if ($entries.Count -ge $MaximumFiles) {
                    throw ("Validation scan exceeded MaxSourceFiles={0}." -f $MaximumFiles)
                }
                $relativePath = Convert-ToPortableRelativePath -Root $UnityRoot -Path $file.FullName
                $entries.Add([pscustomobject][ordered]@{
                    path = $relativePath
                    sha256 = Get-FileSha256 -Path $file.FullName
                }) | Out-Null
                if ($file.LastWriteTimeUtc -gt $newestInput) {
                    $newestInput = $file.LastWriteTimeUtc
                }
            }
        }
        catch {
            throw
        }
        finally {
            if ($null -ne $enumerator) {
                $enumerator.Dispose()
            }
        }
    }

    $assetsBinding = Get-CanonicalAssetsBinding -Entries $entries
    return [pscustomobject][ordered]@{
        scanComplete = $true
        unityVersion = $unityVersion
        manifestSha256 = Get-FileSha256 -Path $manifestPath
        packagesLockSha256 = Get-FileSha256 -Path $lockPath
        projectVersionSha256 = Get-FileSha256 -Path $projectVersionPath
        projectSettingsSha256 = $projectSettingsSha256
        assetsDigest = $assetsBinding.digest
        assetsFileCount = $assetsBinding.fileCount
        newestInputWriteTimeUtc = $newestInput
    }
}

function Assert-StableProjectBinding {
    param(
        [Parameter(Mandatory = $true)]$Before,
        [Parameter(Mandatory = $true)]$After
    )

    foreach ($propertyName in @(
        'scanComplete',
        'unityVersion',
        'manifestSha256',
        'packagesLockSha256',
        'projectVersionSha256',
        'projectSettingsSha256',
        'assetsDigest',
        'assetsFileCount',
        'newestInputWriteTimeUtc'
    )) {
        $beforeValue = Get-ExactPropertyValue -Object $Before -Name $propertyName
        $afterValue = Get-ExactPropertyValue -Object $After -Name $propertyName
        if ($beforeValue -cne $afterValue) {
            throw ("Unity project changed during validation: {0}." -f $propertyName)
        }
    }
    if ($Before.scanComplete -ne $true -or $After.scanComplete -ne $true) {
        throw 'Unity project binding was incomplete before or after validation.'
    }
}

function Convert-ToExactTimestamp {
    param(
        [AllowNull()]$Value,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $parsed = [datetimeoffset]::MinValue
    if ($null -eq $Value -or -not [datetimeoffset]::TryParseExact([string]$Value, 'o',
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind, [ref]$parsed)) {
        throw ("{0} is not an exact round-trip timestamp." -f $Description)
    }
    return $parsed.UtcDateTime
}

function Convert-ToWindowsCommandLineArgument {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Value)

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }
    $builder = New-Object System.Text.StringBuilder
    $builder.Append('"') | Out-Null
    $backslashes = 0
    for ($index = 0; $index -lt $Value.Length; $index++) {
        $character = $Value[$index]
        if ($character -eq '\') {
            $backslashes++
            continue
        }
        if ($character -eq '"') {
            $builder.Append(('\' * (($backslashes * 2) + 1))) | Out-Null
            $builder.Append('"') | Out-Null
            $backslashes = 0
            continue
        }
        if ($backslashes -gt 0) {
            $builder.Append(('\' * $backslashes)) | Out-Null
            $backslashes = 0
        }
        $builder.Append($character) | Out-Null
    }
    if ($backslashes -gt 0) {
        $builder.Append(('\' * ($backslashes * 2))) | Out-Null
    }
    $builder.Append('"') | Out-Null
    return $builder.ToString()
}

function Invoke-ExactProcess {
    param(
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][int]$Timeout
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $ExecutablePath
    $startInfo.Arguments = [string]::Join(' ', @($Arguments | ForEach-Object {
        Convert-ToWindowsCommandLineArgument -Value ([string]$_)
    }))
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $startedAtUtc = [datetime]::UtcNow
    try {
        if (-not $process.Start()) {
            throw 'The validation process did not start.'
        }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $completed = $process.WaitForExit($Timeout * 1000)
        if (-not $completed) {
            try {
                $process.Kill()
            }
            catch {
                # The process may have exited between timeout detection and Kill.
            }
            $process.WaitForExit()
        }
        $finishedAtUtc = [datetime]::UtcNow
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        return [pscustomobject][ordered]@{
            startedAtUtc = $startedAtUtc
            finishedAtUtc = $finishedAtUtc
            timedOut = -not $completed
            exitCode = if ($completed) { [int]$process.ExitCode } else { $null }
            stdoutLength = $stdout.Length
            stderrLength = $stderr.Length
        }
    }
    finally {
        $process.Dispose()
    }
}

function Assert-ReportWindow {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File,
        [Parameter(Mandatory = $true)]$ProcessResult,
        [Parameter(Mandatory = $true)][datetime]$NewestProjectInput,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if ($ProcessResult.timedOut) {
        throw ("{0} process timed out." -f $Description)
    }
    if ($File.LastWriteTimeUtc -le $NewestProjectInput) {
        throw ("{0} is not newer than every validation-relevant project input." -f $Description)
    }
    if ($File.LastWriteTimeUtc -lt $ProcessResult.startedAtUtc) {
        throw ("{0} predates its verified process run." -f $Description)
    }
    if ($File.LastWriteTimeUtc -gt $ProcessResult.finishedAtUtc.AddSeconds(1)) {
        throw ("{0} has a future timestamp outside its verified process run." -f $Description)
    }
}

function Get-BindingReportFacts {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$UnityVersion,
        [Parameter(Mandatory = $true)][string]$SettingsPath,
        [Parameter(Mandatory = $true)]$ProcessResult,
        [Parameter(Mandatory = $true)][datetime]$NewestProjectInput
    )

    $file = Assert-OrdinaryReadableFile -Path $Path -Description 'Binding validation report' `
        -MaximumBytes 2097152
    Assert-ReportWindow -File $file -ProcessResult $ProcessResult `
        -NewestProjectInput $NewestProjectInput -Description 'Binding validation report'
    $document = [System.IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json
    $reportStartedAtUtc = Convert-ToExactTimestamp `
        -Value (Get-ExactPropertyValue -Object $document -Name 'startedAtUtc') `
        -Description 'Binding report startedAtUtc'
    $reportFinishedAtUtc = Convert-ToExactTimestamp `
        -Value (Get-ExactPropertyValue -Object $document -Name 'finishedAtUtc') `
        -Description 'Binding report finishedAtUtc'
    $expectedReportPath = $file.FullName.Replace('\', '/')
    $success = Get-ExactPropertyValue -Object $document -Name 'success'
    $exitCode = Get-ExactPropertyValue -Object $document -Name 'exitCode'
    $errorCount = Get-ExactPropertyValue -Object $document -Name 'errorCount'
    if ((Get-ExactPropertyValue -Object $document -Name 'schemaVersion') -cne
            'app-ui-binding-validation.v2' -or
        (Get-ExactPropertyValue -Object $document -Name 'tool') -cne 'AppUIBindingValidateAll' -or
        (Get-ExactPropertyValue -Object $document -Name 'unityVersion') -cne $UnityVersion -or
        (Get-ExactPropertyValue -Object $document -Name 'settingsPath') -cne $SettingsPath -or
        (Get-ExactPropertyValue -Object $document -Name 'reportPath') -cne $expectedReportPath -or
        -not ($success -is [bool]) -or -not ($exitCode -is [int]) -or
        -not ($errorCount -is [int]) -or $errorCount -lt 0 -or
        $exitCode -ne $ProcessResult.exitCode -or $exitCode -eq 2 -or
        $reportStartedAtUtc -lt $ProcessResult.startedAtUtc -or
        $reportFinishedAtUtc -lt $reportStartedAtUtc -or
        $reportFinishedAtUtc -gt $ProcessResult.finishedAtUtc.AddSeconds(1)) {
        throw 'Binding validation report does not match the verified Unity process.'
    }
    $status = if ($success -eq $true -and $exitCode -eq 0 -and $errorCount -eq 0) {
        'Passed'
    }
    elseif ($success -eq $false -and $exitCode -eq 1 -and $errorCount -gt 0) {
        'Failed'
    }
    else {
        throw 'Binding validation report has an impossible success or exit-code combination.'
    }

    return [pscustomobject][ordered]@{
        status = $status
        schemaVersion = 'app-ui-binding-validation.v2'
        tool = 'AppUIBindingValidateAll'
        unityVersion = $UnityVersion
        settingsPath = $SettingsPath
        success = [bool]$success
        exitCode = [int]$exitCode
        errorCount = [int]$errorCount
        processStartedAtUtc = $ProcessResult.startedAtUtc.ToString('o')
        processFinishedAtUtc = $ProcessResult.finishedAtUtc.ToString('o')
        reportStartedAtUtc = $reportStartedAtUtc.ToString('o')
        reportFinishedAtUtc = $reportFinishedAtUtc.ToString('o')
        reportFileName = $file.Name
        reportSha256 = Get-FileSha256 -Path $file.FullName
        reportLastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('o')
    }
}

function New-RuntimeFactsWithoutReport {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('NotRun', 'Pending')][string]$Status,
        [Parameter(Mandatory = $true)][string]$Reason,
        [AllowNull()]$ProcessResult
    )

    return [pscustomobject][ordered]@{
        status = $Status
        reason = $Reason
        format = 'NUnit3'
        lifecycleTestFilter = $LifecycleTestFilter
        requiredTests = @($requiredRuntimeTests)
        requiredAnyOfTests = @($requiredRuntimeAnyOfTests)
        processStartedAtUtc = if ($null -ne $ProcessResult) {
            $ProcessResult.startedAtUtc.ToString('o')
        } else { $null }
        processFinishedAtUtc = if ($null -ne $ProcessResult) {
            $ProcessResult.finishedAtUtc.ToString('o')
        } else { $null }
        processExitCode = if ($null -ne $ProcessResult) { $ProcessResult.exitCode } else { $null }
        timedOut = if ($null -ne $ProcessResult) { [bool]$ProcessResult.timedOut } else { $false }
        result = $null
        total = $null
        passed = $null
        failed = $null
        inconclusive = $null
        skipped = $null
        reportFileName = $null
        reportSha256 = $null
        reportLastWriteTimeUtc = $null
    }
}

function Get-RuntimeReportFacts {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$ProcessResult,
        [Parameter(Mandatory = $true)][datetime]$NewestProjectInput
    )

    $file = Assert-OrdinaryReadableFile -Path $Path -Description 'Unity Test Runner NUnit result' `
        -MaximumBytes 10485760
    Assert-ReportWindow -File $file -ProcessResult $ProcessResult `
        -NewestProjectInput $NewestProjectInput -Description 'Unity Test Runner NUnit result'

    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = $null
    $document = New-Object System.Xml.XmlDocument
    $document.XmlResolver = $null
    try {
        $reader = [System.Xml.XmlReader]::Create($file.FullName, $settings)
        $document.Load($reader)
    }
    finally {
        if ($null -ne $reader) {
            $reader.Dispose()
        }
    }

    $root = $document.DocumentElement
    if ($null -eq $root -or $root.Name -cne 'test-run') {
        throw 'Runtime test result is not an NUnit3 test-run.'
    }
    $counts = @{}
    foreach ($name in @('total', 'passed', 'failed', 'inconclusive', 'skipped')) {
        $parsed = 0
        if (-not [int]::TryParse($root.GetAttribute($name), [ref]$parsed) -or $parsed -lt 0) {
            throw ("Runtime test result has an invalid {0} count." -f $name)
        }
        $counts[$name] = $parsed
    }

    $requiredPassed = $true
    foreach ($requiredName in $requiredRuntimeTests) {
        $matches = @($document.SelectNodes('//test-case') | Where-Object {
            $_.GetAttribute('fullname') -ceq $requiredName
        })
        if ($matches.Count -ne 1 -or $matches[0].GetAttribute('result') -cne 'Passed') {
            $requiredPassed = $false
        }
    }
    $alternativePassed = $false
    foreach ($alternativeName in $requiredRuntimeAnyOfTests) {
        $matches = @($document.SelectNodes('//test-case') | Where-Object {
            $_.GetAttribute('fullname') -ceq $alternativeName
        })
        if ($matches.Count -eq 1 -and $matches[0].GetAttribute('result') -ceq 'Passed') {
            $alternativePassed = $true
        }
    }
    $allPassed = ($root.GetAttribute('result') -ceq 'Passed' -and
        $counts['failed'] -eq 0 -and $counts['inconclusive'] -eq 0 -and
        $counts['skipped'] -eq 0 -and $counts['total'] -eq $counts['passed'] -and
        $counts['passed'] -ge ($requiredRuntimeTests.Count + 1) -and
        $requiredPassed -and $alternativePassed -and
        -not $ProcessResult.timedOut -and $ProcessResult.exitCode -eq 0)

    return [pscustomobject][ordered]@{
        status = if ($allPassed) { 'Passed' } else { 'Failed' }
        reason = if ($allPassed) { $null } else { 'LifecycleValidationFailed' }
        format = 'NUnit3'
        lifecycleTestFilter = $LifecycleTestFilter
        requiredTests = @($requiredRuntimeTests)
        requiredAnyOfTests = @($requiredRuntimeAnyOfTests)
        processStartedAtUtc = $ProcessResult.startedAtUtc.ToString('o')
        processFinishedAtUtc = $ProcessResult.finishedAtUtc.ToString('o')
        processExitCode = $ProcessResult.exitCode
        timedOut = [bool]$ProcessResult.timedOut
        result = $root.GetAttribute('result')
        total = $counts['total']
        passed = $counts['passed']
        failed = $counts['failed']
        inconclusive = $counts['inconclusive']
        skipped = $counts['skipped']
        reportFileName = $file.Name
        reportSha256 = Get-FileSha256 -Path $file.FullName
        reportLastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('o')
    }
}

function New-VerifiedRunDirectory {
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
    Assert-NoReparsePath -Path $tempRoot
    $runPath = Join-Path $tempRoot ("joih-appui-validation-{0}" -f [guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($runPath) | Out-Null
    if (-not (Test-PathWithinRoot -Path $runPath -Root $tempRoot)) {
        throw 'Validation run directory escaped the system temp directory.'
    }
    Assert-NoReparsePath -Path $runPath
    if ([System.IO.Directory]::GetFileSystemEntries($runPath).Length -ne 0) {
        throw 'Validation run directory was not uniquely empty.'
    }
    return $runPath
}

function Remove-VerifiedRunDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return
    }
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
    if (-not (Test-PathWithinRoot -Path $Path -Root $tempRoot)) {
        throw 'Refusing to clean a validation run outside the system temp directory.'
    }
    Assert-NoReparsePath -Path $Path
    foreach ($entry in [System.IO.Directory]::GetFileSystemEntries($Path)) {
        $attributes = [System.IO.File]::GetAttributes($entry)
        if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
            ($attributes -band [System.IO.FileAttributes]::Directory) -ne 0) {
            throw 'Validation run cleanup found an unexpected directory or reparse point.'
        }
        [System.IO.File]::Delete($entry)
    }
    [System.IO.Directory]::Delete($Path, $false)
}

function Assert-SafeOutputPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$UnityRoot
    )

    if (Test-Path -LiteralPath $Path) {
        throw ("OutputPath must not already exist: {0}" -f $Path)
    }
    foreach ($name in @('Assets', 'Packages', 'ProjectSettings')) {
        if (Test-PathWithinRoot -Path $Path -Root (Join-Path $UnityRoot $name)) {
            throw ("OutputPath cannot target the Unity {0} directory." -f $name)
        }
    }
    $parent = Split-Path -Parent $Path
    if ([string]::IsNullOrWhiteSpace($parent) -or
        -not (Test-Path -LiteralPath $parent -PathType Container)) {
        throw 'OutputPath parent directory must already exist.'
    }
    Assert-NoReparsePath -Path $Path
}

function Write-Utf8NoBomNewFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($Content)
    $stream = $null
    try {
        $stream = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        $stream.Write($bytes, 0, $bytes.Length)
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

$requestedProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
$unityRoot = Resolve-UnityProjectRoot -StartPath $ProjectPath
if (-not $requestedProjectPath.Equals($unityRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'ProjectPath must identify the exact Unity project root.'
}
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
Assert-SafeOutputPath -Path $resolvedOutputPath -UnityRoot $unityRoot

$resolvedUnityPath = [System.IO.Path]::GetFullPath($UnityPath)
$unityExecutable = Assert-OrdinaryReadableFile -Path $resolvedUnityPath `
    -Description 'Unity executable' -MaximumBytes 1073741824
if ($unityExecutable.Extension -cne '.exe') {
    throw 'UnityPath must identify an exact .exe file.'
}

$resolvedBindingSettingsPath = if ([System.IO.Path]::IsPathRooted($BindingSettingsPath)) {
    [System.IO.Path]::GetFullPath($BindingSettingsPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $unityRoot $BindingSettingsPath))
}
$bindingSettingsFile = Assert-OrdinaryReadableFile -Path $resolvedBindingSettingsPath `
    -Description 'UIBindingSettings asset' -MaximumBytes 8388608
if ($bindingSettingsFile.Extension -cne '.asset' -or
    -not (Test-PathWithinRoot -Path $bindingSettingsFile.FullName -Root (Join-Path $unityRoot 'Assets'))) {
    throw 'BindingSettingsPath must identify an exact .asset file under the Unity Assets directory.'
}
$bindingSettingsUnityPath = Convert-ToPortableRelativePath -Root $unityRoot `
    -Path $bindingSettingsFile.FullName

$runDirectory = $null
try {
    $initialBinding = Get-ProjectBinding -UnityRoot $unityRoot -MaximumFiles $MaxSourceFiles
    $runId = [guid]::NewGuid().ToString('D')
    $runStartedAtUtc = [datetime]::UtcNow
    $runDirectory = New-VerifiedRunDirectory
    $bindingReportPath = Join-Path $runDirectory 'app-ui-binding-validation.v2.json'
    $bindingLogPath = Join-Path $runDirectory 'binding-unity.log'
    $runtimeReportPath = Join-Path $runDirectory 'app-ui-lifecycle-tests.xml'
    $runtimeLogPath = Join-Path $runDirectory 'runtime-unity.log'

    $bindingArguments = @(
        '-batchmode', '-nographics', '-quit',
        '-projectPath', $unityRoot,
        '-executeMethod', 'Joi.H.AppUI.Editor.Binding.UIBindingValidationCommandLine.ValidateAll',
        '-appUIBindingSettingsPath', $bindingSettingsUnityPath,
        '-appUIValidationReportPath', $bindingReportPath,
        '-logFile', $bindingLogPath
    )
    $bindingProcess = Invoke-ExactProcess -ExecutablePath $resolvedUnityPath `
        -Arguments $bindingArguments -WorkingDirectory $unityRoot -Timeout $TimeoutSeconds
    if ($bindingProcess.timedOut) {
        throw 'Binding validation Unity process timed out.'
    }
    $provisionalBindingReport = Get-BindingReportFacts -Path $bindingReportPath `
        -UnityVersion $initialBinding.unityVersion -SettingsPath $bindingSettingsUnityPath `
        -ProcessResult $bindingProcess -NewestProjectInput $initialBinding.newestInputWriteTimeUtc

    $runtimeProcess = $null
    if ($provisionalBindingReport.status -ceq 'Passed') {
        $runtimeArguments = @(
            '-batchmode', '-nographics',
            '-projectPath', $unityRoot,
            '-runTests', '-testPlatform', 'EditMode',
            '-testFilter', $LifecycleTestFilter,
            '-testResults', $runtimeReportPath,
            '-logFile', $runtimeLogPath
        )
        try {
            $runtimeProcess = Invoke-ExactProcess -ExecutablePath $resolvedUnityPath `
                -Arguments $runtimeArguments -WorkingDirectory $unityRoot -Timeout $TimeoutSeconds
        }
        catch {
            $runtimeProcess = $null
        }
    }

    $binding = Get-ProjectBinding -UnityRoot $unityRoot -MaximumFiles $MaxSourceFiles
    Assert-StableProjectBinding -Before $initialBinding -After $binding
    $bindingReport = Get-BindingReportFacts -Path $bindingReportPath `
        -UnityVersion $binding.unityVersion -SettingsPath $bindingSettingsUnityPath `
        -ProcessResult $bindingProcess -NewestProjectInput $binding.newestInputWriteTimeUtc

    if ($bindingReport.status -ceq 'Failed') {
        $runtimeReport = New-RuntimeFactsWithoutReport -Status 'NotRun' `
            -Reason 'BindingValidationFailed' -ProcessResult $null
    }
    elseif ($null -eq $runtimeProcess) {
        $runtimeReport = New-RuntimeFactsWithoutReport -Status 'Pending' `
            -Reason 'RuntimeProcessDidNotStart' -ProcessResult $null
    }
    elseif ($runtimeProcess.timedOut) {
        $runtimeReport = New-RuntimeFactsWithoutReport -Status 'Pending' `
            -Reason 'RuntimeProcessTimedOut' -ProcessResult $runtimeProcess
    }
    elseif (-not (Test-Path -LiteralPath $runtimeReportPath -PathType Leaf)) {
        $runtimeReport = New-RuntimeFactsWithoutReport -Status 'Pending' `
            -Reason 'RuntimeReportMissing' -ProcessResult $runtimeProcess
    }
    else {
        try {
            $runtimeReport = Get-RuntimeReportFacts -Path $runtimeReportPath `
                -ProcessResult $runtimeProcess -NewestProjectInput $binding.newestInputWriteTimeUtc
        }
        catch {
            $runtimeReport = New-RuntimeFactsWithoutReport -Status 'Pending' `
                -Reason 'RuntimeReportRejected' -ProcessResult $runtimeProcess
        }
    }

    $runFinishedAtUtc = [datetime]::UtcNow
    $attestation = [pscustomobject][ordered]@{
        schemaVersion = $schemaVersion
        producer = $producer
        owner = $owner
        run = [pscustomobject][ordered]@{
            runId = $runId
            startedAtUtc = $runStartedAtUtc.ToString('o')
            finishedAtUtc = $runFinishedAtUtc.ToString('o')
            unityExecutableSha256 = Get-FileSha256 -Path $resolvedUnityPath
            bindingSettingsPath = $bindingSettingsUnityPath
            lifecycleTestFilter = $LifecycleTestFilter
        }
        unityVersion = $binding.unityVersion
        manifestSha256 = $binding.manifestSha256
        packagesLockSha256 = $binding.packagesLockSha256
        projectVersionSha256 = $binding.projectVersionSha256
        projectSettingsSha256 = $binding.projectSettingsSha256
        assetsDigestAlgorithm = $digestAlgorithm
        assetsDigestScope = $digestScope
        assetsDigestCanonicalization = $digestCanonicalization
        assetsDigest = $binding.assetsDigest
        assetsFileCount = $binding.assetsFileCount
        newestInputWriteTimeUtc = $binding.newestInputWriteTimeUtc.ToString('o')
        binding = $bindingReport
        runtime = $runtimeReport
    }
    $json = $attestation | ConvertTo-Json -Depth 20

    Remove-VerifiedRunDirectory -Path $runDirectory
    $runDirectory = $null
    Assert-SafeOutputPath -Path $resolvedOutputPath -UnityRoot $unityRoot
    Write-Utf8NoBomNewFile -Path $resolvedOutputPath -Content $json
    Write-Output $json
}
finally {
    if ($null -ne $runDirectory -and (Test-Path -LiteralPath $runDirectory -PathType Container)) {
        Remove-VerifiedRunDirectory -Path $runDirectory
    }
}
