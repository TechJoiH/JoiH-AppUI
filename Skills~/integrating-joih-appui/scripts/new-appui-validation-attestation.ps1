[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$BindingReportPath,
    [Parameter(Mandatory = $true)][string]$RuntimeTestResultPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [ValidateRange(1, 100000)][int]$MaxSourceFiles = 10000
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$schemaVersion = 'joih-appui-project-validation-attestation.v2'
$producer = 'integrating-joih-appui/new-appui-validation-attestation.ps1'
$owner = 'Project'
$digestAlgorithm = 'SHA-256'
$digestScope = 'validation-relevant-project-files-v2'
$digestCanonicalization = 'ordinal-portable-path-nul-lower-sha256-lf-v2'
$requiredRuntimeTests = @(
    'Joi.H.AppUI.Tests.Lifecycle.Open',
    'Joi.H.AppUI.Tests.Lifecycle.Refresh',
    'Joi.H.AppUI.Tests.Lifecycle.Close',
    'Joi.H.AppUI.Tests.Lifecycle.ReleaseScope',
    'Joi.H.AppUI.Tests.Lifecycle.SceneRebind',
    'Joi.H.AppUI.Tests.Lifecycle.Shutdown'
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

function Get-BindingReportFacts {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$UnityVersion,
        [Parameter(Mandatory = $true)][datetime]$NewestProjectInput
    )

    $file = Assert-OrdinaryReadableFile -Path $Path -Description 'Binding validation report' `
        -MaximumBytes 2097152
    if ($file.LastWriteTimeUtc -le $NewestProjectInput) {
        throw 'Binding validation report is not newer than every validation-relevant project input.'
    }
    $document = [System.IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json
    if ((Get-ExactPropertyValue -Object $document -Name 'schemaVersion') -cne
        'app-ui-binding-validation.v2' -or
        (Get-ExactPropertyValue -Object $document -Name 'tool') -cne 'AppUIBindingValidateAll' -or
        (Get-ExactPropertyValue -Object $document -Name 'unityVersion') -cne $UnityVersion -or
        -not ((Get-ExactPropertyValue -Object $document -Name 'success') -is [bool]) -or
        (Get-ExactPropertyValue -Object $document -Name 'success') -ne $true -or
        -not ((Get-ExactPropertyValue -Object $document -Name 'exitCode') -is [int]) -or
        (Get-ExactPropertyValue -Object $document -Name 'exitCode') -ne 0 -or
        -not ((Get-ExactPropertyValue -Object $document -Name 'errorCount') -is [int]) -or
        (Get-ExactPropertyValue -Object $document -Name 'errorCount') -ne 0) {
        throw 'Binding validation report does not satisfy app-ui-binding-validation.v2.'
    }
    return [pscustomobject][ordered]@{
        schemaVersion = 'app-ui-binding-validation.v2'
        tool = 'AppUIBindingValidateAll'
        unityVersion = $UnityVersion
        success = $true
        exitCode = 0
        errorCount = 0
        reportSha256 = Get-FileSha256 -Path $file.FullName
        reportLastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('o')
    }
}

function Get-RuntimeReportFacts {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][datetime]$NewestProjectInput
    )

    $file = Assert-OrdinaryReadableFile -Path $Path -Description 'Unity Test Runner NUnit result' `
        -MaximumBytes 10485760
    if ($file.LastWriteTimeUtc -le $NewestProjectInput) {
        throw 'Runtime test result is not newer than every validation-relevant project input.'
    }

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
    if ($null -eq $root -or $root.Name -cne 'test-run' -or
        $root.GetAttribute('result') -cne 'Passed') {
        throw 'Runtime test result is not a passing NUnit3 test-run.'
    }
    $counts = @{}
    foreach ($name in @('total', 'passed', 'failed', 'inconclusive', 'skipped')) {
        $parsed = 0
        if (-not [int]::TryParse($root.GetAttribute($name), [ref]$parsed) -or $parsed -lt 0) {
            throw ("Runtime test result has an invalid {0} count." -f $name)
        }
        $counts[$name] = $parsed
    }
    if ($counts['failed'] -ne 0 -or $counts['inconclusive'] -ne 0 -or
        $counts['skipped'] -ne 0 -or $counts['total'] -ne $counts['passed'] -or
        $counts['passed'] -lt $requiredRuntimeTests.Count) {
        throw 'Runtime test result includes a non-passing, skipped, or inconclusive test.'
    }
    foreach ($requiredName in $requiredRuntimeTests) {
        $matches = @($document.SelectNodes('//test-case') | Where-Object {
            $_.GetAttribute('fullname') -ceq $requiredName
        })
        if ($matches.Count -ne 1 -or $matches[0].GetAttribute('result') -cne 'Passed') {
            throw ("Runtime test result lacks one exact passing lifecycle test: {0}" -f $requiredName)
        }
    }

    return [pscustomobject][ordered]@{
        format = 'NUnit3'
        result = 'Passed'
        total = $counts['total']
        passed = $counts['passed']
        failed = $counts['failed']
        inconclusive = $counts['inconclusive']
        skipped = $counts['skipped']
        requiredTests = @($requiredRuntimeTests)
        reportSha256 = Get-FileSha256 -Path $file.FullName
        reportLastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('o')
    }
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

$unityRoot = Resolve-UnityProjectRoot -StartPath $ProjectPath
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
Assert-SafeOutputPath -Path $resolvedOutputPath -UnityRoot $unityRoot
$binding = Get-ProjectBinding -UnityRoot $unityRoot -MaximumFiles $MaxSourceFiles
$bindingReport = Get-BindingReportFacts -Path ([System.IO.Path]::GetFullPath($BindingReportPath)) `
    -UnityVersion $binding.unityVersion -NewestProjectInput $binding.newestInputWriteTimeUtc
$runtimeReport = Get-RuntimeReportFacts -Path ([System.IO.Path]::GetFullPath($RuntimeTestResultPath)) `
    -NewestProjectInput $binding.newestInputWriteTimeUtc

$attestation = [pscustomobject][ordered]@{
    schemaVersion = $schemaVersion
    producer = $producer
    owner = $owner
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
$json = $attestation | ConvertTo-Json -Depth 16
Assert-SafeOutputPath -Path $resolvedOutputPath -UnityRoot $unityRoot
Write-Utf8NoBomNewFile -Path $resolvedOutputPath -Content $json
Write-Output $json
