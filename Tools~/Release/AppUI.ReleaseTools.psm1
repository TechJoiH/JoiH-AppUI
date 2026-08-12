Set-StrictMode -Version 2.0

function Invoke-AppUIGitText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$AllowFailure
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & git -C $RepositoryPath @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $text = ($output -join "`n").Trim()
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "Git command failed ($exitCode): git $($Arguments -join ' ')`n$text"
    }

    return [PSCustomObject]@{
        ExitCode = $exitCode
        Text = $text
    }
}

function Write-AppUIUtf8NoBom {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [AllowEmptyString()]
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Value,
        (New-Object System.Text.UTF8Encoding($false)))
}

function Get-AppUISha256 {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hash = $sha.ComputeHash($stream)
        }
        finally {
            $sha.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
}

function Resolve-AppUIRepositoryName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath
    )

    $remoteResult = Invoke-AppUIGitText -RepositoryPath $RepositoryPath -Arguments @('remote', 'get-url', 'origin') -AllowFailure
    if ($remoteResult.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($remoteResult.Text)) {
        $remote = $remoteResult.Text.Trim()
        $match = [regex]::Match(
            $remote,
            '^(?:https://github\.com/|git@github\.com:)(?<name>[^/]+/[^/]+?)(?:\.git)?$')
        if ($match.Success) {
            return $match.Groups['name'].Value
        }

        return $remote
    }

    return 'local/' + (Split-Path -Leaf $RepositoryPath)
}

function Resolve-AppUIGitIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,

        [Parameter(Mandatory = $true)]
        [string]$SourceRef
    )

    $resolvedRepository = [System.IO.Path]::GetFullPath($RepositoryPath)
    if (-not (Test-Path -LiteralPath $resolvedRepository -PathType Container)) {
        throw "Repository path does not exist: $resolvedRepository"
    }

    $commitResult = Invoke-AppUIGitText -RepositoryPath $resolvedRepository -Arguments @('rev-parse', '--verify', "$SourceRef^{commit}")
    $sourceCommit = $commitResult.Text
    if ($sourceCommit -notmatch '^[0-9a-f]{40}$') {
        throw "Source ref did not resolve to a 40-character commit: $SourceRef"
    }

    $treeResult = Invoke-AppUIGitText -RepositoryPath $resolvedRepository -Arguments @('rev-parse', "$sourceCommit^{tree}")
    $sourceTree = $treeResult.Text
    if ($sourceTree -notmatch '^[0-9a-f]{40}$') {
        throw "Commit did not resolve to a 40-character tree: $sourceCommit"
    }

    $packageResult = Invoke-AppUIGitText -RepositoryPath $resolvedRepository -Arguments @('show', "$sourceCommit`:package.json")
    try {
        $package = $packageResult.Text | ConvertFrom-Json
    }
    catch {
        throw "Commit package.json is not valid JSON: $sourceCommit. $($_.Exception.Message)"
    }

    if ($null -eq $package -or [string]::IsNullOrWhiteSpace([string]$package.version)) {
        throw "Commit package.json does not define a version: $sourceCommit"
    }

    return [PSCustomObject]@{
        Repository = Resolve-AppUIRepositoryName -RepositoryPath $resolvedRepository
        SourceCommit = $sourceCommit
        SourceTree = $sourceTree
        PackageVersion = [string]$package.version
    }
}

function Invoke-AppUIGitBinaryToFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    $gitCommand = Get-Command git -ErrorAction Stop
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $gitCommand.Source
    $startInfo.WorkingDirectory = $RepositoryPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = ($Arguments -join ' ')

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Failed to start Git: $($Arguments -join ' ')"
    }

    $output = [System.IO.File]::Create($OutputPath)
    try {
        $process.StandardOutput.BaseStream.CopyTo($output)
    }
    finally {
        $output.Dispose()
    }

    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $exitCode = $process.ExitCode
    $process.Dispose()
    if ($exitCode -ne 0) {
        throw "Git command failed ($exitCode): git $($Arguments -join ' ')`n$standardError"
    }
}

function Get-AppUIGitTreeEntries {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,

        [Parameter(Mandatory = $true)]
        [string]$SourceCommit,

        [Parameter(Mandatory = $true)]
        [string]$TemporaryPath
    )

    Invoke-AppUIGitBinaryToFile -RepositoryPath $RepositoryPath -Arguments @('ls-tree', '-rz', '--full-tree', $SourceCommit) -OutputPath $TemporaryPath
    $bytes = [System.IO.File]::ReadAllBytes($TemporaryPath)
    $entries = New-Object System.Collections.Generic.List[object]
    $start = 0
    for ($index = 0; $index -le $bytes.Length; $index++) {
        if ($index -lt $bytes.Length -and $bytes[$index] -ne 0) {
            continue
        }

        if ($index -eq $start) {
            $start = $index + 1
            continue
        }

        $record = [System.Text.Encoding]::UTF8.GetString($bytes, $start, $index - $start)
        $tabIndex = $record.IndexOf("`t", [System.StringComparison]::Ordinal)
        if ($tabIndex -lt 0) {
            throw "Malformed git ls-tree record."
        }

        $metadata = $record.Substring(0, $tabIndex).Split(' ')
        if ($metadata.Count -lt 3) {
            throw "Malformed git ls-tree metadata: $record"
        }

        if ($metadata[1] -eq 'commit') {
            throw "Git submodules are not supported in candidate snapshots: $($record.Substring($tabIndex + 1))"
        }

        $entries.Add([PSCustomObject]@{
            GitMode = $metadata[0]
            Path = $record.Substring($tabIndex + 1).Replace('\', '/')
        })
        $start = $index + 1
    }

    return $entries
}

function Export-AppUICandidateSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,

        [Parameter(Mandatory = $true)]
        [string]$SourceRef,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    $resolvedRepository = [System.IO.Path]::GetFullPath($RepositoryPath)
    $resolvedDestination = [System.IO.Path]::GetFullPath($DestinationPath)
    if (Test-Path -LiteralPath $resolvedDestination) {
        throw "Destination already exists: $resolvedDestination"
    }

    $identity = Resolve-AppUIGitIdentity -RepositoryPath $resolvedRepository -SourceRef $SourceRef
    [System.IO.Directory]::CreateDirectory($resolvedDestination) | Out-Null
    $createdDestination = $true
    try {
        $candidateRoot = Join-Path $resolvedDestination 'candidate'
        $packageRoot = Join-Path $candidateRoot 'package'
        $evidenceRoot = Join-Path $candidateRoot 'evidence'
        [System.IO.Directory]::CreateDirectory($packageRoot) | Out-Null
        [System.IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null

        $archivePath = Join-Path $candidateRoot 'package.zip'
        $archiveResult = Invoke-AppUIGitText -RepositoryPath $resolvedRepository -Arguments @('archive', '--format=zip', "--output=$archivePath", $identity.SourceCommit)
        if ($archiveResult.ExitCode -ne 0) {
            throw "Failed to export candidate archive."
        }

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $packageRoot)
        Remove-Item -LiteralPath $archivePath -Force

        $treeOutput = Join-Path $evidenceRoot 'git-tree.bin'
        $treeEntries = Get-AppUIGitTreeEntries -RepositoryPath $resolvedRepository -SourceCommit $identity.SourceCommit -TemporaryPath $treeOutput
        Remove-Item -LiteralPath $treeOutput -Force

        $manifestEntries = New-Object System.Collections.Generic.List[object]
        foreach ($entry in $treeEntries) {
            $candidateFile = Join-Path $packageRoot ($entry.Path.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
            if (-not (Test-Path -LiteralPath $candidateFile -PathType Leaf)) {
                throw "Tracked candidate file was not exported: $($entry.Path)"
            }

            $manifestEntries.Add([PSCustomObject][ordered]@{
                path = $entry.Path
                gitMode = $entry.GitMode
                sha256 = Get-AppUISha256 -Path $candidateFile
            })
        }

        $entryByPath = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([System.StringComparer]::Ordinal)
        foreach ($entry in $manifestEntries) {
            $entryByPath.Add($entry.path, $entry)
        }

        $orderedPaths = [string[]]@($entryByPath.Keys)
        [System.Array]::Sort($orderedPaths, [System.StringComparer]::Ordinal)
        $orderedEntries = @(
            foreach ($path in $orderedPaths) {
                $entryByPath[$path]
            }
        )
        $canonicalBuilder = New-Object System.Text.StringBuilder
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        foreach ($entry in $orderedEntries) {
            $pathByteLength = $utf8.GetByteCount($entry.path)
            [void]$canonicalBuilder.Append($pathByteLength)
            [void]$canonicalBuilder.Append(':')
            [void]$canonicalBuilder.Append($entry.path)
            [void]$canonicalBuilder.Append("`t")
            [void]$canonicalBuilder.Append($entry.gitMode)
            [void]$canonicalBuilder.Append("`t")
            [void]$canonicalBuilder.Append($entry.sha256)
            [void]$canonicalBuilder.Append("`n")
        }

        $canonicalPath = Join-Path $evidenceRoot 'package-manifest.canonical.txt'
        Write-AppUIUtf8NoBom -Path $canonicalPath -Value $canonicalBuilder.ToString()
        $packageManifestSha256 = Get-AppUISha256 -Path $canonicalPath

        $manifest = [ordered]@{
            packageManifestSha256 = $packageManifestSha256
            files = $orderedEntries
        }
        $manifestPath = Join-Path $evidenceRoot 'package-manifest.json'
        Write-AppUIUtf8NoBom -Path $manifestPath -Value (($manifest | ConvertTo-Json -Depth 8) + "`n")

        $generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        $identityDocument = [ordered]@{
            repository = $identity.Repository
            sourceCommit = $identity.SourceCommit
            sourceTree = $identity.SourceTree
            packageVersion = $identity.PackageVersion
            packageManifestSha256 = $packageManifestSha256
            generatedAtUtc = $generatedAtUtc
        }
        $identityPath = Join-Path $evidenceRoot 'candidate-identity.json'
        Write-AppUIUtf8NoBom -Path $identityPath -Value (($identityDocument | ConvertTo-Json -Depth 4) + "`n")

        return [PSCustomObject]@{
            Repository = $identity.Repository
            SourceCommit = $identity.SourceCommit
            SourceTree = $identity.SourceTree
            PackageVersion = $identity.PackageVersion
            PackageManifestSha256 = $packageManifestSha256
            GeneratedAtUtc = $generatedAtUtc
            CandidateRoot = $candidateRoot
            PackageRoot = $packageRoot
            EvidenceRoot = $evidenceRoot
            IdentityPath = $identityPath
            ManifestPath = $manifestPath
        }
    }
    catch {
        if ($createdDestination -and
            (Test-Path -LiteralPath $resolvedDestination) -and
            $resolvedDestination -ne [System.IO.Path]::GetPathRoot($resolvedDestination)) {
            Remove-Item -LiteralPath $resolvedDestination -Recurse -Force
        }
        throw
    }
}

function Test-AppUIConsumerTemplate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$TemplatePath
    )

    $resolvedTemplate = [System.IO.Path]::GetFullPath($TemplatePath)
    if (-not (Test-Path -LiteralPath $resolvedTemplate -PathType Container)) {
        throw "Consumer template does not exist: $resolvedTemplate"
    }

    $forbiddenDirectoryNames = @(
        'Library',
        'Temp',
        'Logs',
        'Obj',
        'UserSettings',
        'Builds'
    )
    $directories = @(Get-ChildItem -LiteralPath $resolvedTemplate -Recurse -Force -Directory)
    foreach ($directory in $directories) {
        if ($forbiddenDirectoryNames -contains $directory.Name) {
            throw "Consumer template contains forbidden directory: $($directory.FullName)"
        }
    }

    $templateManifestPath = Join-Path $resolvedTemplate 'Packages\manifest.template.json'
    if (-not (Test-Path -LiteralPath $templateManifestPath -PathType Leaf)) {
        throw "Consumer template manifest is missing: $templateManifestPath"
    }

    $materializedManifestPath = Join-Path $resolvedTemplate 'Packages\manifest.json'
    if (Test-Path -LiteralPath $materializedManifestPath) {
        throw "Consumer template must not contain a materialized manifest.json."
    }

    $templateManifestText = Get-Content -LiteralPath $templateManifestPath -Raw -Encoding UTF8
    $token = '__APPUI_PACKAGE_REFERENCE__'
    $tokenCount = ([regex]::Matches($templateManifestText, [regex]::Escape($token))).Count
    if ($tokenCount -ne 1) {
        throw "Consumer template package token must appear exactly once; found $tokenCount."
    }

    try {
        $templateManifest = $templateManifestText | ConvertFrom-Json
    }
    catch {
        throw "Consumer template manifest is invalid JSON: $($_.Exception.Message)"
    }

    if ($null -eq $templateManifest.dependencies -or
        [string]$templateManifest.dependencies.'com.joih.appui' -ne $token) {
        throw "Consumer template must use the AppUI package token as com.joih.appui dependency."
    }

    $suspiciousPatterns = @(
        '(?i)(?:^|["''\s])(?:[a-z]:[\\/])',
        '(?i)file:(?:\.\.|[a-z]:|/)',
        '(?i)github_pat_[a-z0-9_]+',
        '(?i)ghp_[a-z0-9]+',
        '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
    )
    $textFiles = @(Get-ChildItem -LiteralPath $resolvedTemplate -Recurse -Force -File | Where-Object {
        $_.Length -le 10MB -and $_.Extension -match '^\.(?:json|txt|md|cs|asmdef|meta|asset|unity|yml|yaml|gitignore)$'
    })
    foreach ($file in $textFiles) {
        $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        foreach ($pattern in $suspiciousPatterns) {
            if ($content -match $pattern) {
                throw "Consumer template contains a forbidden local path or secret pattern: $($file.FullName)"
            }
        }
    }

    return [PSCustomObject]@{
        TemplatePath = $resolvedTemplate
        ManifestTemplatePath = $templateManifestPath
        ManifestTemplate = $templateManifest
    }
}

function Resolve-AppUIPackageReference {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageReference
    )

    if ([string]::IsNullOrWhiteSpace($PackageReference)) {
        throw "Package reference is empty."
    }

    if ($PackageReference -match '^https://github\.com/TechJoiH/JoiH-AppUI\.git#(?:[0-9a-f]{40}|v[0-9A-Za-z][0-9A-Za-z.+-]*)$') {
        return $PackageReference
    }

    $pathValue = $PackageReference
    if ($pathValue.StartsWith('file:', [System.StringComparison]::OrdinalIgnoreCase)) {
        $pathValue = $pathValue.Substring(5)
    }

    $resolvedPath = [System.IO.Path]::GetFullPath($pathValue)
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Container)) {
        throw "Local package reference does not exist: $resolvedPath"
    }

    return 'file:' + $resolvedPath.Replace('\', '/')
}

function New-AppUIConsumerWorkspace {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$TemplatePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,

        [Parameter(Mandatory = $true)]
        [string]$PackageReference
    )

    $resolvedDestination = [System.IO.Path]::GetFullPath($DestinationPath)
    if (Test-Path -LiteralPath $resolvedDestination) {
        throw "Consumer destination already exists: $resolvedDestination"
    }

    $template = Test-AppUIConsumerTemplate -TemplatePath $TemplatePath
    $resolvedPackageReference = Resolve-AppUIPackageReference -PackageReference $PackageReference
    [System.IO.Directory]::CreateDirectory($resolvedDestination) | Out-Null
    try {
        Get-ChildItem -LiteralPath $template.TemplatePath -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $resolvedDestination -Recurse -Force
        }

        $materializedTemplatePath = Join-Path $resolvedDestination 'Packages\manifest.template.json'
        $manifestText = Get-Content -LiteralPath $materializedTemplatePath -Raw -Encoding UTF8
        $manifest = $manifestText | ConvertFrom-Json
        $manifest.dependencies.'com.joih.appui' = $resolvedPackageReference

        $manifestPath = Join-Path $resolvedDestination 'Packages\manifest.json'
        Write-AppUIUtf8NoBom -Path $manifestPath -Value (($manifest | ConvertTo-Json -Depth 16) + "`n")

        return [PSCustomObject]@{
            TemplatePath = $template.TemplatePath
            WorkspacePath = $resolvedDestination
            PackageReference = $resolvedPackageReference
            ManifestPath = $manifestPath
        }
    }
    catch {
        if ((Test-Path -LiteralPath $resolvedDestination) -and
            $resolvedDestination -ne [System.IO.Path]::GetPathRoot($resolvedDestination)) {
            Remove-Item -LiteralPath $resolvedDestination -Recurse -Force
        }
        throw
    }
}

function New-AppUIPolicyCheck {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [bool]$Success,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Details
    )

    return [PSCustomObject]@{
        Name = $Name
        Status = if ($Success) { 'Passed' } else { 'Error' }
        Details = $Details
    }
}

function Get-AppUIProductionFiles {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageRoot
    )

    $files = New-Object System.Collections.Generic.List[object]
    foreach ($relativeRoot in @('Runtime', 'Editor')) {
        $root = Join-Path $PackageRoot $relativeRoot
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            continue
        }

        Get-ChildItem -LiteralPath $root -Recurse -Force -File | Where-Object {
            $_.Extension -in @('.cs', '.asmdef', '.json')
        } | ForEach-Object {
            $files.Add($_)
        }
    }

    return $files
}

function Test-AppUIUnityMetaIntegrity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageRoot
    )

    $issues = New-Object System.Collections.Generic.List[string]
    $metaFiles = New-Object System.Collections.Generic.List[object]
    $roots = @(
        @{ Path = 'Runtime'; IncludeRoot = $true },
        @{ Path = 'Editor'; IncludeRoot = $true },
        @{ Path = 'Tests'; IncludeRoot = $true },
        @{ Path = 'Validation~/Unity6000.0Consumer/Assets'; IncludeRoot = $false }
    )

    foreach ($rootDefinition in $roots) {
        $root = Join-Path $PackageRoot $rootDefinition.Path
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        $targets = New-Object System.Collections.Generic.List[object]
        if ($rootDefinition.IncludeRoot) {
            $targets.Add((Get-Item -LiteralPath $root))
        }
        Get-ChildItem -LiteralPath $root -Recurse -Force | ForEach-Object {
            if (-not $_.Name.EndsWith('.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
                $targets.Add($_)
            }
            else {
                $metaFiles.Add($_)
            }
        }

        foreach ($target in $targets) {
            $metaPath = $target.FullName + '.meta'
            if (-not (Test-Path -LiteralPath $metaPath -PathType Leaf)) {
                $issues.Add("Missing meta: $($target.FullName.Substring($PackageRoot.Length + 1).Replace('\', '/'))")
            }
        }
    }

    $guidOwner = @{}
    foreach ($metaFile in $metaFiles) {
        $targetPath = $metaFile.FullName.Substring(0, $metaFile.FullName.Length - 5)
        if (-not (Test-Path -LiteralPath $targetPath)) {
            $issues.Add("Orphan meta: $($metaFile.FullName.Substring($PackageRoot.Length + 1).Replace('\', '/'))")
        }

        $match = Select-String -LiteralPath $metaFile.FullName -Pattern '^guid:\s*([0-9a-fA-F]{32})\s*$' -Encoding UTF8 | Select-Object -First 1
        if ($null -eq $match) {
            $issues.Add("Missing or invalid GUID: $($metaFile.FullName.Substring($PackageRoot.Length + 1).Replace('\', '/'))")
            continue
        }

        $guid = $match.Matches[0].Groups[1].Value.ToLowerInvariant()
        if ($guidOwner.ContainsKey($guid)) {
            $issues.Add("Duplicate GUID $guid`: $($guidOwner[$guid]) and $($metaFile.FullName.Substring($PackageRoot.Length + 1).Replace('\', '/'))")
        }
        else {
            $guidOwner[$guid] = $metaFile.FullName.Substring($PackageRoot.Length + 1).Replace('\', '/')
        }
    }

    return @($issues)
}

function Test-AppUIMarkdownLinks {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageRoot
    )

    $issues = New-Object System.Collections.Generic.List[string]
    $markdownFiles = New-Object System.Collections.Generic.List[object]
    foreach ($rootFile in @('README.md', 'CONTRIBUTING.md', 'CHANGELOG.md', 'SECURITY.md')) {
        $path = Join-Path $PackageRoot $rootFile
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $markdownFiles.Add((Get-Item -LiteralPath $path))
        }
    }

    $documentationRoot = Join-Path $PackageRoot 'Documentation~'
    if (Test-Path -LiteralPath $documentationRoot -PathType Container) {
        Get-ChildItem -LiteralPath $documentationRoot -File -Filter '*.md' | ForEach-Object {
            $markdownFiles.Add($_)
        }
    }

    foreach ($file in $markdownFiles) {
        $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        $fenceCount = ([regex]::Matches($text, '(?m)^```')).Count
        if ($fenceCount % 2 -ne 0) {
            $issues.Add("Unbalanced code fences: $($file.FullName.Substring($PackageRoot.Length + 1).Replace('\', '/'))")
        }

        foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\(([^)]+)\)')) {
            $target = $match.Groups[1].Value
            if ($target -match '^(?:https?:|mailto:|#)') {
                continue
            }

            $relativeTarget = $target.Split('#')[0]
            if ([string]::IsNullOrWhiteSpace($relativeTarget)) {
                continue
            }

            $decodedTarget = [System.Uri]::UnescapeDataString($relativeTarget)
            $resolvedTarget = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName $decodedTarget))
            if (-not $resolvedTarget.StartsWith($PackageRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
                -not (Test-Path -LiteralPath $resolvedTarget)) {
                $issues.Add("Broken relative link: $($file.FullName.Substring($PackageRoot.Length + 1).Replace('\', '/')) -> $target")
            }
        }
    }

    return @($issues)
}

function Test-AppUIPackagePolicy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,

        [Parameter(Mandatory = $true)]
        [string]$SourceRef
    )

    $tempBase = [System.IO.Path]::GetTempPath()
    $auditRoot = Join-Path $tempBase ('joih-appui-policy-' + [Guid]::NewGuid().ToString('N'))
    $checks = New-Object System.Collections.Generic.List[object]
    try {
        $snapshot = Export-AppUICandidateSnapshot -RepositoryPath $RepositoryPath -SourceRef $SourceRef -DestinationPath $auditRoot
        $packageRoot = $snapshot.PackageRoot
        $packagePath = Join-Path $packageRoot 'package.json'
        $package = Get-Content -LiteralPath $packagePath -Raw -Encoding UTF8 | ConvertFrom-Json

        $dependencyProperties = @($package.dependencies.PSObject.Properties)
        $manifestSuccess = [string]$package.name -eq 'com.joih.appui' -and
            -not [string]::IsNullOrWhiteSpace([string]$package.version) -and
            [string]$package.unity -eq '6000.0' -and
            $dependencyProperties.Count -eq 1 -and
            $dependencyProperties[0].Name -eq 'com.unity.ugui' -and
            [string]$dependencyProperties[0].Value -eq '2.0.0'
        $checks.Add((New-AppUIPolicyCheck -Name 'PackageManifest' -Success $manifestSuccess -Details $(
            if ($manifestSuccess) { 'Package ID, version, Unity and UGUI dependency match the official line.' }
            else { 'Expected com.joih.appui, Unity 6000.0 and only com.unity.ugui 2.0.0.' }
        )))

        $productionMatches = New-Object System.Collections.Generic.List[string]
        $tokenPattern = '(?i)Cysharp\.Threading\.Tasks|\bUniTask\b|\bSirenix\b|ResourcesUIAssetProvider|Resources\.Load|\bAnnals\b|\bGameFramework\b'
        foreach ($file in Get-AppUIProductionFiles -PackageRoot $packageRoot) {
            $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
            if ($content -match $tokenPattern) {
                $productionMatches.Add($file.FullName.Substring($packageRoot.Length + 1).Replace('\', '/'))
            }
        }
        $checks.Add((New-AppUIPolicyCheck -Name 'ForbiddenProductionTokens' -Success ($productionMatches.Count -eq 0) -Details $(
            if ($productionMatches.Count -eq 0) { 'No forbidden async, inspector, Resources or host tokens found.' }
            else { 'Forbidden tokens: ' + ($productionMatches -join ', ') }
        )))

        $macroMatches = New-Object System.Collections.Generic.List[string]
        $macroRoots = @('Runtime', 'Editor/Binding')
        foreach ($relativeRoot in $macroRoots) {
            $root = Join-Path $packageRoot $relativeRoot
            if (-not (Test-Path -LiteralPath $root)) {
                continue
            }
            Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' | ForEach-Object {
                $content = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
                if ($content -match '\bUNITY_(?:2021|2022|6000)(?:_|\b)') {
                    $macroMatches.Add($_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/'))
                }
            }
        }
        $checks.Add((New-AppUIPolicyCheck -Name 'VersionMacroBoundary' -Success ($macroMatches.Count -eq 0) -Details $(
            if ($macroMatches.Count -eq 0) { 'No scattered Unity-version macros found.' }
            else { 'Version macros: ' + ($macroMatches -join ', ') }
        )))

        $multiVersionFiles = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -Force -File | Where-Object {
            $relativePath = $_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
            $relativePath -notmatch '^Documentation~/' -and
            $_.Name -match '(?i)(?:unity[-_.]?(?:2021|2022|6000|6(?:\.0)?).*(?:profile|package|release|tag)|(?:profile|package|release|tag).*unity[-_.]?(?:2021|2022|6000|6(?:\.0)?))'
        } | ForEach-Object {
            $_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
        })
        $checks.Add((New-AppUIPolicyCheck -Name 'SingleOfficialUnityLine' -Success ($multiVersionFiles.Count -eq 0) -Details $(
            if ($multiVersionFiles.Count -eq 0) { 'No official per-Unity profile, package, release or tag configuration found.' }
            else { 'Official multi-version files: ' + ($multiVersionFiles -join ', ') }
        )))

        $emptyCompatibilityDirectories = New-Object System.Collections.Generic.List[string]
        foreach ($relativeCompatibilityPath in @('Runtime/Compatibility', 'Editor/Compatibility')) {
            $compatibilityPath = Join-Path $packageRoot $relativeCompatibilityPath
            if (-not (Test-Path -LiteralPath $compatibilityPath -PathType Container)) {
                continue
            }

            $meaningfulFiles = @(Get-ChildItem -LiteralPath $compatibilityPath -Recurse -Force -File | Where-Object {
                $_.Name -ne '.gitkeep'
            })
            if ($meaningfulFiles.Count -eq 0) {
                $emptyCompatibilityDirectories.Add($relativeCompatibilityPath)
            }
        }
        $checks.Add((New-AppUIPolicyCheck -Name 'CompatibilityYagni' -Success ($emptyCompatibilityDirectories.Count -eq 0) -Details $(
            if ($emptyCompatibilityDirectories.Count -eq 0) { 'No empty Compatibility shell exists.' }
            else { 'Empty Compatibility shells: ' + ($emptyCompatibilityDirectories -join ', ') }
        )))

        $metaIssues = @(Test-AppUIUnityMetaIntegrity -PackageRoot $packageRoot)
        $checks.Add((New-AppUIPolicyCheck -Name 'UnityMetaIntegrity' -Success ($metaIssues.Count -eq 0) -Details $(
            if ($metaIssues.Count -eq 0) { 'Unity meta files and GUIDs are consistent.' }
            else { $metaIssues -join '; ' }
        )))

        $documentationIssues = @(Test-AppUIMarkdownLinks -PackageRoot $packageRoot)
        $checks.Add((New-AppUIPolicyCheck -Name 'DocumentationLinks' -Success ($documentationIssues.Count -eq 0) -Details $(
            if ($documentationIssues.Count -eq 0) { 'Public Markdown links and code fences are valid.' }
            else { $documentationIssues -join '; ' }
        )))

        $diffCheck = Invoke-AppUIGitText -RepositoryPath $RepositoryPath -Arguments @('diff-tree', '--check', '--root', '-r', $snapshot.SourceCommit) -AllowFailure
        $checks.Add((New-AppUIPolicyCheck -Name 'GitWhitespace' -Success ($diffCheck.ExitCode -eq 0) -Details $(
            if ($diffCheck.ExitCode -eq 0) { 'Commit tree has no Git whitespace errors.' }
            else { $diffCheck.Text }
        )))

        $checkArray = $checks.ToArray()
        $errorCount = @($checkArray | Where-Object { $_.Status -eq 'Error' }).Count
        return [PSCustomObject]@{
            Success = $errorCount -eq 0
            ErrorCount = $errorCount
            SourceCommit = $snapshot.SourceCommit
            SourceTree = $snapshot.SourceTree
            PackageVersion = $snapshot.PackageVersion
            PackageManifestSha256 = $snapshot.PackageManifestSha256
            Checks = $checkArray
        }
    }
    finally {
        if ((Test-Path -LiteralPath $auditRoot) -and
            $auditRoot.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $auditRoot).StartsWith('joih-appui-policy-', [System.StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $auditRoot -Recurse -Force
        }
    }
}

Export-ModuleMember -Function @(
    'Resolve-AppUIGitIdentity',
    'Export-AppUICandidateSnapshot',
    'New-AppUIConsumerWorkspace',
    'Test-AppUIPackagePolicy'
)
