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

function Write-AppUIJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object]$Value,

        [ValidateRange(1, 100)]
        [int]$Depth = 10
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $parent = Split-Path -Parent $resolvedPath
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }

    Write-AppUIUtf8NoBom `
        -Path $resolvedPath `
        -Value (($Value | ConvertTo-Json -Depth $Depth) + "`n")
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

function Test-AppUISemVerTag {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Tag
    )

    $numericIdentifier = '(?:0|[1-9][0-9]*)'
    $preReleaseIdentifier = "(?:$numericIdentifier|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)"
    $buildIdentifier = '[0-9A-Za-z-]+'
    $pattern = "^v$numericIdentifier\.$numericIdentifier\.$numericIdentifier" +
        "(?:-$preReleaseIdentifier(?:\.$preReleaseIdentifier)*)?" +
        "(?:\+$buildIdentifier(?:\.$buildIdentifier)*)?$"
    return $Tag -match $pattern
}

function Invoke-AppUIGitRemoteText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [ValidateRange(1, 600)]
        [int]$TimeoutSeconds = 30,

        [string]$GitPath = 'git'
    )

    $resolvedRepository = [System.IO.Path]::GetFullPath($RepositoryPath)
    if (-not (Test-Path -LiteralPath $resolvedRepository -PathType Container)) {
        throw "Repository path does not exist: $resolvedRepository"
    }

    $resolvedGit = (Get-Command $GitPath -ErrorAction Stop).Source
    $processArguments = @('-C', $resolvedRepository) + $Arguments
    $escapedArguments = @(
        foreach ($argument in $processArguments) {
            $value = if ($null -eq $argument) { '' } else { [string]$argument }
            if ($value -notmatch '[\s"]') {
                $value
                continue
            }

            $builder = New-Object System.Text.StringBuilder
            [void]$builder.Append('"')
            $backslashCount = 0
            for ($index = 0; $index -lt $value.Length; $index++) {
                $character = $value[$index]
                if ($character -eq '\') {
                    $backslashCount++
                    continue
                }

                if ($character -eq '"') {
                    [void]$builder.Append(('\' * ($backslashCount * 2 + 1)))
                    [void]$builder.Append('"')
                    $backslashCount = 0
                    continue
                }

                if ($backslashCount -gt 0) {
                    [void]$builder.Append(('\' * $backslashCount))
                    $backslashCount = 0
                }

                [void]$builder.Append($character)
            }

            if ($backslashCount -gt 0) {
                [void]$builder.Append(('\' * ($backslashCount * 2)))
            }

            [void]$builder.Append('"')
            $builder.ToString()
        }
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = $null
    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $resolvedGit
        $startInfo.Arguments = $escapedArguments -join ' '
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.WorkingDirectory = $resolvedRepository
        $startInfo.EnvironmentVariables['GIT_TERMINAL_PROMPT'] = '0'
        $startInfo.EnvironmentVariables['GCM_INTERACTIVE'] = 'Never'
        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        if (-not $process.Start()) {
            throw "Failed to start Git process: $resolvedGit"
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            try {
                $process.Kill()
                $process.WaitForExit()
            }
            catch {
                throw "Timed out Git process could not be terminated. Id=$($process.Id). $($_.Exception.Message)"
            }
        }
        else {
            $process.WaitForExit()
        }

        $exitCode = if ($timedOut) { $null } else { $process.ExitCode }
        $processId = $process.Id
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $text = (@($stdout, $stderr) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join "`n"
        return [PSCustomObject][ordered]@{
            Status = if ($timedOut -or $exitCode -ne 0) { 'Blocked' } else { 'Passed' }
            Reason = if ($timedOut) { 'Timeout' } elseif ($exitCode -ne 0) { 'RemoteUnavailable' } else { '' }
            ExitCode = $exitCode
            TimedOut = $timedOut
            DurationMs = $stopwatch.ElapsedMilliseconds
            ProcessId = $processId
            Text = $text.Trim()
        }
    }
    finally {
        $stopwatch.Stop()
        if ($null -ne $process) {
            $process.Dispose()
        }
    }
}

function Resolve-AppUIRemoteTagIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,

        [Parameter(Mandatory = $true)]
        [string]$Tag
    )

    if (-not (Test-AppUISemVerTag -Tag $Tag)) {
        throw "Remote tag is not a valid AppUI SemVer tag: $Tag"
    }

    $result = Invoke-AppUIGitRemoteText `
        -RepositoryPath $RepositoryPath `
        -Arguments @(
            'ls-remote',
            'origin',
            "refs/tags/$Tag",
            "refs/tags/$Tag^{}")
    if ($result.Status -ne 'Passed') {
        throw "Remote tag lookup blocked. Reason=$($result.Reason) ExitCode=$($result.ExitCode). $($result.Text)"
    }
    $lines = @($result.Text -split "`r?`n" | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    })
    if ($lines.Count -eq 0) {
        throw "Remote tag does not exist: $Tag"
    }

    $peeled = $lines | Where-Object { $_ -match "refs/tags/$([regex]::Escape($Tag))\^\{\}$" } | Select-Object -First 1
    $selected = if ($null -ne $peeled) { $peeled } else {
        $lines | Where-Object { $_ -match "refs/tags/$([regex]::Escape($Tag))$" } | Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace([string]$selected)) {
        throw "Remote tag could not be resolved: $Tag"
    }

    $commit = ([string]$selected -split "\s+")[0]
    if ($commit -notmatch '^[0-9a-f]{40}$') {
        throw "Remote tag did not resolve to a commit SHA: $Tag"
    }

    $treeResult = Invoke-AppUIGitText `
        -RepositoryPath $RepositoryPath `
        -Arguments @('rev-parse', "$commit^{tree}")
    if ($treeResult.Text -notmatch '^[0-9a-f]{40}$') {
        throw "Remote tag commit did not resolve to a tree: $Tag"
    }

    return [PSCustomObject][ordered]@{
        Tag = $Tag
        SourceCommit = $commit
        SourceTree = $treeResult.Text
    }
}

function Test-AppUIReleaseReadiness {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,

        [Parameter(Mandatory = $true)]
        [string]$CandidateCommit,

        [Parameter(Mandatory = $true)]
        [string]$PlannedTag
    )

    $identity = Resolve-AppUIGitIdentity `
        -RepositoryPath $RepositoryPath `
        -SourceRef $CandidateCommit
    if ($identity.SourceCommit -ne $CandidateCommit) {
        throw "Release readiness requires an exact 40-character CandidateCommit."
    }

    if ($PlannedTag -ne ('v' + $identity.PackageVersion)) {
        throw "Release readiness planned tag mismatch. Expected=v$($identity.PackageVersion) Actual=$PlannedTag"
    }

    $remoteResult = Invoke-AppUIGitRemoteText `
        -RepositoryPath $RepositoryPath `
        -Arguments @(
            'ls-remote',
            'origin',
            'refs/heads/main',
            "refs/tags/$PlannedTag",
            "refs/tags/$PlannedTag^{}")
    if ($remoteResult.Status -ne 'Passed') {
        return [PSCustomObject][ordered]@{
            Status = 'Blocked'
            Reason = $remoteResult.Reason
            Repository = $identity.Repository
            CandidateCommit = $identity.SourceCommit
            CandidateTree = $identity.SourceTree
            PackageVersion = $identity.PackageVersion
            PlannedTag = $PlannedTag
            RemoteMainCommit = ''
            CandidateIsRemoteMain = $false
            TagExists = $false
            LocalTagExists = $false
            TagCommit = ''
            TagTree = ''
            RemoteExitCode = $remoteResult.ExitCode
            RemoteDurationMs = $remoteResult.DurationMs
        }
    }

    $remoteLines = @($remoteResult.Text -split "`r?`n" | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    })
    $remoteMainCommit = ''
    $mainLine = $remoteLines | Where-Object { $_ -match '\s+refs/heads/main$' } | Select-Object -First 1
    if ([string]$mainLine -match '^(?<commit>[0-9a-f]{40})\s+refs/heads/main$') {
        $remoteMainCommit = $Matches['commit']
    }

    $tagLines = @($remoteLines | Where-Object {
        $_ -match "\s+refs/tags/$([regex]::Escape($PlannedTag))(?:\^\{\})?$"
    })
    $tagExists = $tagLines.Count -gt 0
    $localTagResult = Invoke-AppUIGitText `
        -RepositoryPath $RepositoryPath `
        -Arguments @('tag', '--list', $PlannedTag) `
        -AllowFailure
    $localTagExists = $localTagResult.ExitCode -eq 0 -and
        -not [string]::IsNullOrWhiteSpace($localTagResult.Text)
    $tagCommit = ''
    $tagTree = ''
    if ($tagExists) {
        $peeledLine = $tagLines | Where-Object { $_ -match '\^\{\}$' } | Select-Object -First 1
        $selectedTagLine = if ($null -ne $peeledLine) { $peeledLine } else { $tagLines | Select-Object -First 1 }
        $tagCommit = ([string]$selectedTagLine -split '\s+')[0]
        if ($tagCommit -eq $identity.SourceCommit) {
            $tagTree = $identity.SourceTree
        }
        else {
            $tagTreeResult = Invoke-AppUIGitText `
                -RepositoryPath $RepositoryPath `
                -Arguments @('rev-parse', "$tagCommit^{tree}") `
                -AllowFailure
            if ($tagTreeResult.ExitCode -eq 0 -and $tagTreeResult.Text -match '^[0-9a-f]{40}$') {
                $tagTree = $tagTreeResult.Text
            }
        }
    }

    $status = if ($tagExists -and $tagCommit -ne $identity.SourceCommit) {
        'TagConflict'
    } elseif ($tagExists) {
        'TagExists'
    } elseif ($localTagExists) {
        'LocalTagExists'
    } elseif ($remoteMainCommit -eq $identity.SourceCommit) {
        'ReadyForTag'
    } else {
        'NotPushed'
    }
    return [PSCustomObject][ordered]@{
        Status = $status
        Reason = ''
        Repository = $identity.Repository
        CandidateCommit = $identity.SourceCommit
        CandidateTree = $identity.SourceTree
        PackageVersion = $identity.PackageVersion
        PlannedTag = $PlannedTag
        RemoteMainCommit = $remoteMainCommit
        CandidateIsRemoteMain = $remoteMainCommit -eq $identity.SourceCommit
        TagExists = $tagExists
        LocalTagExists = $localTagExists
        TagCommit = $tagCommit
        TagTree = $tagTree
        RemoteExitCode = $remoteResult.ExitCode
        RemoteDurationMs = $remoteResult.DurationMs
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

function Test-AppUICandidateSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageRoot,

        [Parameter(Mandatory = $true)]
        [string]$IdentityPath,

        [Parameter(Mandatory = $true)]
        [string]$ManifestPath
    )

    $resolvedPackageRoot = [System.IO.Path]::GetFullPath($PackageRoot)
    $resolvedIdentityPath = [System.IO.Path]::GetFullPath($IdentityPath)
    $resolvedManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
    foreach ($requiredPath in @($resolvedPackageRoot, $resolvedIdentityPath, $resolvedManifestPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Candidate snapshot identity audit input does not exist: $requiredPath"
        }
    }

    $identity = Get-Content -LiteralPath $resolvedIdentityPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$identity.packageManifestSha256 -ne [string]$manifest.packageManifestSha256) {
        throw "Candidate snapshot identity audit manifest hash mismatch."
    }

    $expected = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([System.StringComparer]::Ordinal)
    foreach ($entry in @($manifest.files)) {
        $expected.Add([string]$entry.path, $entry)
    }

    $actualFiles = @(Get-ChildItem -LiteralPath $resolvedPackageRoot -Recurse -Force -File)
    $actualPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    foreach ($file in $actualFiles) {
        $relativePath = $file.FullName.Substring($resolvedPackageRoot.Length).TrimStart('\', '/').Replace('\', '/')
        [void]$actualPaths.Add($relativePath)
        if (-not $expected.ContainsKey($relativePath)) {
            throw "Candidate snapshot identity audit found unexpected file: $relativePath"
        }

        $actualHash = Get-AppUISha256 -Path $file.FullName
        if ($actualHash -ne [string]$expected[$relativePath].sha256) {
            throw "Candidate snapshot identity audit file hash mismatch: $relativePath"
        }
    }

    foreach ($path in $expected.Keys) {
        if (-not $actualPaths.Contains($path)) {
            throw "Candidate snapshot identity audit found missing file: $path"
        }
    }

    $orderedPaths = [string[]]@($expected.Keys)
    [System.Array]::Sort($orderedPaths, [System.StringComparer]::Ordinal)
    $canonicalBuilder = New-Object System.Text.StringBuilder
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    foreach ($path in $orderedPaths) {
        $entry = $expected[$path]
        [void]$canonicalBuilder.Append($utf8.GetByteCount($path))
        [void]$canonicalBuilder.Append(':')
        [void]$canonicalBuilder.Append($path)
        [void]$canonicalBuilder.Append("`t")
        [void]$canonicalBuilder.Append([string]$entry.gitMode)
        [void]$canonicalBuilder.Append("`t")
        [void]$canonicalBuilder.Append([string]$entry.sha256)
        [void]$canonicalBuilder.Append("`n")
    }

    $temporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) (
        'joih-appui-candidate-audit-' + [Guid]::NewGuid().ToString('N') + '.txt')
    try {
        Write-AppUIUtf8NoBom -Path $temporaryPath -Value $canonicalBuilder.ToString()
        $manifestHash = Get-AppUISha256 -Path $temporaryPath
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }

    if ($manifestHash -ne [string]$identity.packageManifestSha256) {
        throw "Candidate snapshot identity audit package hash mismatch."
    }

    return [PSCustomObject][ordered]@{
        Success = $true
        FileCount = $actualFiles.Count
        PackageManifestSha256 = $manifestHash
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

    $gitReferenceMatch = [regex]::Match(
        $PackageReference,
        '^https://github\.com/TechJoiH/JoiH-AppUI\.git#(?<fragment>.+)$')
    if ($gitReferenceMatch.Success) {
        $fragment = $gitReferenceMatch.Groups['fragment'].Value
        $validSemVerTag = Test-AppUISemVerTag -Tag $fragment
        if ($fragment -match '^[0-9a-f]{40}$' -or $validSemVerTag) {
            return $PackageReference
        }

        throw "Git package reference must use an exact 40-character commit or SemVer tag: $PackageReference"
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
        $packageVersion = [string]$package.version
        $manifestSuccess = [string]$package.name -eq 'com.joih.appui' -and
            (Test-AppUISemVerTag -Tag ('v' + $packageVersion)) -and
            [string]$package.unity -eq '6000.0' -and
            $dependencyProperties.Count -eq 1 -and
            $dependencyProperties[0].Name -eq 'com.unity.ugui' -and
            [string]$dependencyProperties[0].Value -eq '2.0.0'
        $checks.Add((New-AppUIPolicyCheck -Name 'PackageManifest' -Success $manifestSuccess -Details $(
            if ($manifestSuccess) { 'Package ID, strict SemVer version, Unity and UGUI dependency match the official line.' }
            else { 'Expected com.joih.appui, a strict SemVer version, Unity 6000.0 and only com.unity.ugui 2.0.0.' }
        )))

        $packageManifests = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -Force -File -Filter 'package.json' | ForEach-Object {
            $_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
        })
        $singlePackageManifest = $packageManifests.Count -eq 1 -and $packageManifests[0] -ceq 'package.json'
        $checks.Add((New-AppUIPolicyCheck -Name 'SinglePackageManifest' -Success $singlePackageManifest -Details $(
            if ($singlePackageManifest) { 'The release tree contains only the root package.json.' }
            else { 'Package manifests: ' + ($packageManifests -join ', ') }
        )))

        $validationRoot = Join-Path $packageRoot 'Validation~'
        $consumerDirectories = @(if (Test-Path -LiteralPath $validationRoot -PathType Container) {
            Get-ChildItem -LiteralPath $validationRoot -Force -Directory | ForEach-Object { $_.Name }
        })
        $officialConsumerRoot = Join-Path $validationRoot 'Unity6000.0Consumer'
        $consumerContractFiles = @(
            'Assets',
            'Packages/manifest.template.json',
            'ProjectSettings/ProjectVersion.txt'
        )
        $consumerContractSuccess = $consumerDirectories.Count -eq 1 -and
            $consumerDirectories[0] -ceq 'Unity6000.0Consumer'
        if ($consumerContractSuccess) {
            foreach ($relativePath in $consumerContractFiles) {
                if (-not (Test-Path -LiteralPath (Join-Path $officialConsumerRoot $relativePath))) {
                    $consumerContractSuccess = $false
                    break
                }
            }
        }
        $checks.Add((New-AppUIPolicyCheck -Name 'SingleOfficialConsumer' -Success $consumerContractSuccess -Details $(
            if ($consumerContractSuccess) { 'Validation~ contains only the complete Unity6000.0Consumer project.' }
            else { 'Consumer directories: ' + ($consumerDirectories -join ', ') }
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

function Read-AppUINUnit3Result {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [switch]$RequirePassed
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "NUnit result does not exist: $resolvedPath"
    }

    try {
        [xml]$document = Get-Content -LiteralPath $resolvedPath -Raw -Encoding UTF8
    }
    catch {
        throw "NUnit result is malformed: $resolvedPath. $($_.Exception.Message)"
    }

    $run = $document.'test-run'
    if ($null -eq $run) {
        throw "NUnit result does not contain test-run: $resolvedPath"
    }

    try {
        $total = [int]$run.total
        $passed = [int]$run.passed
        $failed = [int]$run.failed
        $skipped = [int]$run.skipped
        $durationMs = [long]([double]$run.duration * 1000.0)
    }
    catch {
        throw "NUnit result contains invalid counters: $resolvedPath"
    }

    $failedNames = @(
        $document.SelectNodes('//test-case[@result="Failed"]') |
            ForEach-Object { [string]$_.fullname }
    )
    $status = if ($failed -eq 0 -and
        [string]$run.result -notmatch '^Failed') { 'Passed' } else { 'Failed' }
    if ($RequirePassed -and $status -ne 'Passed') {
        throw "NUnit result failed: $resolvedPath. Failed=$failed. Tests=$($failedNames -join ', ')"
    }

    return [PSCustomObject][ordered]@{
        Status = $status
        Total = $total
        Passed = $passed
        Failed = $failed
        Skipped = $skipped
        DurationMs = $durationMs
        FailedTests = $failedNames
        EvidenceFile = [System.IO.Path]::GetFileName($resolvedPath)
    }
}

function Invoke-AppUIProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [string[]]$ArgumentList = @(),

        [ValidateRange(1, 86400)]
        [int]$TimeoutSeconds = 120
    )

    $resolvedFile = (Get-Command $FilePath -ErrorAction Stop).Source
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $escapedArguments = @(
        foreach ($argument in $ArgumentList) {
            $value = if ($null -eq $argument) { '' } else { [string]$argument }
            if ($value -notmatch '[\s"]') {
                $value
                continue
            }

            $builder = New-Object System.Text.StringBuilder
            [void]$builder.Append('"')
            $backslashCount = 0
            for ($index = 0; $index -lt $value.Length; $index++) {
                $character = $value[$index]
                if ($character -eq '\') {
                    $backslashCount++
                    continue
                }

                if ($character -eq '"') {
                    [void]$builder.Append(('\' * ($backslashCount * 2 + 1)))
                    [void]$builder.Append('"')
                    $backslashCount = 0
                    continue
                }

                if ($backslashCount -gt 0) {
                    [void]$builder.Append(('\' * $backslashCount))
                    $backslashCount = 0
                }

                [void]$builder.Append($character)
            }

            if ($backslashCount -gt 0) {
                [void]$builder.Append(('\' * ($backslashCount * 2)))
            }

            [void]$builder.Append('"')
            $builder.ToString()
        }
    )
    $process = Start-Process `
        -FilePath $resolvedFile `
        -ArgumentList $escapedArguments `
        -PassThru `
        -WindowStyle Hidden
    $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) {
        try {
            $process.Kill()
            $process.WaitForExit()
        }
        catch {
            throw "Timed out process could not be terminated. Id=$($process.Id). $($_.Exception.Message)"
        }
    }

    $stopwatch.Stop()
    $exitCode = if ($timedOut) { $null } else { $process.ExitCode }
    $processId = $process.Id
    $process.Dispose()
    return [PSCustomObject][ordered]@{
        Status = if ($timedOut) { 'Blocked' } elseif ($exitCode -eq 0) { 'Passed' } else { 'Failed' }
        ExitCode = $exitCode
        TimedOut = $timedOut
        DurationMs = $stopwatch.ElapsedMilliseconds
        ProcessId = $processId
    }
}

function Invoke-AppUIUnityProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$UnityPath,

        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$LogFile,

        [string[]]$Arguments = @(),

        [ValidateRange(1, 86400)]
        [int]$TimeoutSeconds = 120
    )

    $resolvedUnity = [System.IO.Path]::GetFullPath($UnityPath)
    $resolvedProject = [System.IO.Path]::GetFullPath($ProjectPath)
    $resolvedLog = [System.IO.Path]::GetFullPath($LogFile)
    if (-not (Test-Path -LiteralPath $resolvedUnity -PathType Leaf)) {
        throw "Unity executable does not exist: $resolvedUnity"
    }

    if (-not (Test-Path -LiteralPath $resolvedProject -PathType Container)) {
        throw "Unity project does not exist: $resolvedProject"
    }

    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedLog)) | Out-Null
    $unityArguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath',
        $resolvedProject
    ) + $Arguments + @('-logFile', $resolvedLog)
    $result = Invoke-AppUIProcess `
        -FilePath $resolvedUnity `
        -ArgumentList $unityArguments `
        -TimeoutSeconds $TimeoutSeconds
    return [PSCustomObject][ordered]@{
        Status = $result.Status
        ExitCode = $result.ExitCode
        TimedOut = $result.TimedOut
        DurationMs = $result.DurationMs
        ProcessId = $result.ProcessId
        LogFile = $resolvedLog
    }
}

function Test-AppUIBuildEnvironment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$UnityPath,

        [string]$ExpectedUnityVersion = '6000.0.25f1',

        [string]$VsWherePath = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe',

        [AllowEmptyString()]
        [string]$UnityVersionOverride = '',

        [AllowEmptyString()]
        [string]$VsInstallationPathOverride = '',

        [switch]$DisableVsWhereDiscovery
    )

    $resolvedUnity = [System.IO.Path]::GetFullPath($UnityPath)
    $unityVersion = $UnityVersionOverride
    if (-not (Test-Path -LiteralPath $resolvedUnity -PathType Leaf)) {
        return [PSCustomObject][ordered]@{
            schemaVersion = 'appui-build-environment.v1'
            gate = 'IL2CPP'
            Status = 'Blocked'
            Reason = 'UnityNotFound'
            UnityVersion = ''
            ExpectedUnityVersion = $ExpectedUnityVersion
            VsInstallationPath = ''
            VcVarsPath = ''
            Details = "Unity executable does not exist: $resolvedUnity"
            checkedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
    }

    if ([string]::IsNullOrWhiteSpace($unityVersion)) {
        $productVersion = [string](Get-Item -LiteralPath $resolvedUnity).VersionInfo.ProductVersion
        $versionMatch = [regex]::Match($productVersion, '^(?<version>\d+\.\d+\.\d+[fp]\d+)')
        if ($versionMatch.Success) {
            $unityVersion = $versionMatch.Groups['version'].Value
        }
    }

    if ($unityVersion -ne $ExpectedUnityVersion) {
        return [PSCustomObject][ordered]@{
            schemaVersion = 'appui-build-environment.v1'
            gate = 'IL2CPP'
            Status = 'Blocked'
            Reason = 'UnityVersionMismatch'
            UnityVersion = $unityVersion
            ExpectedUnityVersion = $ExpectedUnityVersion
            VsInstallationPath = ''
            VcVarsPath = ''
            Details = "Unity version mismatch. Expected=$ExpectedUnityVersion Actual=$unityVersion"
            checkedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
    }

    $vsInstallationPath = $VsInstallationPathOverride
    $usingOverride = -not [string]::IsNullOrWhiteSpace($VsInstallationPathOverride)
    if (-not $usingOverride -and -not $DisableVsWhereDiscovery) {
        $resolvedVsWhere = [System.IO.Path]::GetFullPath($VsWherePath)
        if (Test-Path -LiteralPath $resolvedVsWhere -PathType Leaf) {
            $vsOutput = & $resolvedVsWhere `
                -version '[17.0,18.0)' `
                -products * `
                -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
                -property installationPath 2>$null
            if ($LASTEXITCODE -eq 0) {
                $vsInstallationPath = @($vsOutput | Where-Object {
                    -not [string]::IsNullOrWhiteSpace([string]$_)
                } | Select-Object -First 1)
                if ($vsInstallationPath.Count -gt 0) {
                    $vsInstallationPath = [string]$vsInstallationPath[0]
                }
                else {
                    $vsInstallationPath = ''
                }
            }
        }
    }

    $vcVarsPath = if ([string]::IsNullOrWhiteSpace($vsInstallationPath)) {
        ''
    } else {
        Join-Path $vsInstallationPath 'VC\Auxiliary\Build\vcvars64.bat'
    }
    if ([string]::IsNullOrWhiteSpace($vsInstallationPath) -or
        -not (Test-Path -LiteralPath $vcVarsPath -PathType Leaf)) {
        return [PSCustomObject][ordered]@{
            schemaVersion = 'appui-build-environment.v1'
            gate = 'IL2CPP'
            Status = 'Blocked'
            Reason = 'MissingToolchain'
            UnityVersion = $unityVersion
            ExpectedUnityVersion = $ExpectedUnityVersion
            VsInstallationPath = $vsInstallationPath
            VcVarsPath = $vcVarsPath
            Details = 'Visual Studio 2022 C++ Build Tools with vcvars64.bat were not found.'
            checkedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
    }

    $tempBase = [System.IO.Path]::GetTempPath()
    $probeRoot = Join-Path $tempBase ('joih-appui-toolchain-' + [Guid]::NewGuid().ToString('N'))
    $probeScript = Join-Path $probeRoot 'probe.cmd'
    try {
        [System.IO.Directory]::CreateDirectory($probeRoot) | Out-Null
        Write-AppUIUtf8NoBom -Path $probeScript -Value (@"
@echo off
call "$vcVarsPath" >nul
if errorlevel 1 exit /b 2
if not defined WindowsSdkDir exit /b 3
where cl.exe >nul || exit /b 4
where link.exe >nul || exit /b 5
where rc.exe >nul || exit /b 6
exit /b 0
"@ -replace "`n", "`r`n")
        $probe = Invoke-AppUIProcess `
            -FilePath 'cmd.exe' `
            -ArgumentList @('/d', '/c', $probeScript) `
            -TimeoutSeconds 30
    }
    finally {
        if ((Test-Path -LiteralPath $probeRoot) -and
            $probeRoot.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $probeRoot).StartsWith('joih-appui-toolchain-', [System.StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $probeRoot -Recurse -Force
        }
    }
    if ($probe.Status -ne 'Passed') {
        return [PSCustomObject][ordered]@{
            schemaVersion = 'appui-build-environment.v1'
            gate = 'IL2CPP'
            Status = 'Blocked'
            Reason = 'ToolchainProbeFailed'
            UnityVersion = $unityVersion
            ExpectedUnityVersion = $ExpectedUnityVersion
            VsInstallationPath = $vsInstallationPath
            VcVarsPath = $vcVarsPath
            Details = "vcvars64.bat did not expose cl.exe, link.exe, rc.exe and WindowsSdkDir. ExitCode=$($probe.ExitCode)"
            checkedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
    }

    return [PSCustomObject][ordered]@{
        schemaVersion = 'appui-build-environment.v1'
        gate = 'IL2CPP'
        Status = 'Passed'
        Reason = 'None'
        UnityVersion = $unityVersion
        ExpectedUnityVersion = $ExpectedUnityVersion
        VsInstallationPath = $vsInstallationPath
        VcVarsPath = $vcVarsPath
        Details = 'Unity and Visual Studio 2022 C++ toolchain preflight passed.'
        checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
}

function Get-AppUIJsonEvidenceGate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Binding', 'Build', 'Smoke')]
        [string]$Kind
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        return [PSCustomObject][ordered]@{
            status = 'NotRun'
            evidenceFile = [System.IO.Path]::GetFileName($resolvedPath)
            durationMs = 0
        }
    }

    try {
        $document = Get-Content -LiteralPath $resolvedPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "Evidence JSON is malformed: $resolvedPath. $($_.Exception.Message)"
    }

    if ($Kind -eq 'Binding') {
        $passed = [bool]$document.success -and [int]$document.errorCount -eq 0
        $duration = if ($document.PSObject.Properties['durationMs']) { [long]$document.durationMs } else { 0 }
    }
    elseif ($Kind -eq 'Build') {
        $passed = [string]$document.result -eq 'Succeeded'
        $duration = if ($document.PSObject.Properties['totalTimeMs']) { [long]$document.totalTimeMs } else { 0 }
    }
    else {
        $passed = [bool]$document.initialized -and
            [bool]$document.openPassed -and [bool]$document.closePassed
        $duration = if ($document.PSObject.Properties['durationMs']) { [long]$document.durationMs } else { 0 }
    }

    return [PSCustomObject][ordered]@{
        status = if ($passed) { 'Passed' } else { 'Failed' }
        evidenceFile = [System.IO.Path]::GetFileName($resolvedPath)
        durationMs = $duration
    }
}

function Test-AppUISmokeIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$GateName,

        [Parameter(Mandatory = $true)]
        [object]$Identity,

        [switch]$Required
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        if ($Required) {
            throw "$GateName evidence is missing: $resolvedPath"
        }

        return
    }

    $smoke = Get-Content -LiteralPath $resolvedPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($comparison in @(
        @('repository', [string]$smoke.repository, [string]$Identity.repository),
        @('sourceCommit', [string]$smoke.sourceCommit, [string]$Identity.sourceCommit),
        @('sourceTree', [string]$smoke.sourceTree, [string]$Identity.sourceTree),
        @('packageVersion', [string]$smoke.packageVersion, [string]$Identity.packageVersion),
        @('packageManifestSha256', [string]$smoke.packageManifestSha256, [string]$Identity.packageManifestSha256)
    )) {
        if ($comparison[1] -ne $comparison[2]) {
            throw "$GateName $($comparison[0]) mismatch. Expected=$($comparison[2]) Actual=$($comparison[1])"
        }
    }

    if ([string]::IsNullOrWhiteSpace([string]$smoke.packageReference) -or
        [string]$smoke.packageReference -notmatch '^https://github\.com/TechJoiH/JoiH-AppUI\.git#') {
        throw "$GateName packageReference is missing or invalid."
    }

    $expectedFragment = if ($GateName -eq 'commitGitInstallSmoke') {
        [string]$Identity.sourceCommit
    } elseif ($GateName -eq 'tagGitInstallSmoke') {
        'v' + [string]$Identity.packageVersion
    } else {
        ''
    }
    if (-not [string]::IsNullOrWhiteSpace($expectedFragment) -and
        [string]$smoke.packageReference -ne (
            'https://github.com/TechJoiH/JoiH-AppUI.git#' + $expectedFragment)) {
        throw "$GateName packageReference mismatch. Expected fragment=$expectedFragment Actual=$($smoke.packageReference)"
    }
}

function New-AppUIReleaseReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$IdentityPath,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceRoot,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedSourceCommit,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedSourceTree,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedPackageVersion,

        [Parameter(Mandatory = $true)]
        [string]$PlannedTag,

        [ValidateSet('PreTag', 'Formal')]
        [string]$Mode = 'PreTag',

        [AllowEmptyString()]
        [string]$ResolvedTag = '',

        [AllowEmptyString()]
        [string]$RepositoryPath = '',

        [AllowEmptyString()]
        [string]$CommitSmokePath = '',

        [AllowEmptyString()]
        [string]$TagSmokePath = ''
    )

    $resolvedIdentityPath = [System.IO.Path]::GetFullPath($IdentityPath)
    $resolvedEvidenceRoot = [System.IO.Path]::GetFullPath($EvidenceRoot)
    if (-not (Test-Path -LiteralPath $resolvedIdentityPath -PathType Leaf)) {
        throw "Candidate identity does not exist: $resolvedIdentityPath"
    }

    $identity = Get-Content -LiteralPath $resolvedIdentityPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $packageManifestPath = Join-Path $resolvedEvidenceRoot 'package-manifest.json'
    if (-not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) {
        throw "Release report package manifest is missing: $packageManifestPath"
    }

    $packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$packageManifest.packageManifestSha256 -ne
        [string]$identity.packageManifestSha256) {
        throw "Release report package manifest packageManifestSha256 mismatch."
    }
    foreach ($comparison in @(
        @('sourceCommit', [string]$identity.sourceCommit, $ExpectedSourceCommit),
        @('sourceTree', [string]$identity.sourceTree, $ExpectedSourceTree),
        @('packageVersion', [string]$identity.packageVersion, $ExpectedPackageVersion)
    )) {
        if ($comparison[1] -ne $comparison[2]) {
            throw "Release report $($comparison[0]) mismatch. Expected=$($comparison[2]) Actual=$($comparison[1])"
        }
    }

    $expectedTag = 'v' + $ExpectedPackageVersion
    if ($PlannedTag -ne $expectedTag) {
        throw "Release report plannedTag mismatch. Expected=$expectedTag Actual=$PlannedTag"
    }

    if ($Mode -eq 'PreTag' -and
        -not [string]::IsNullOrWhiteSpace($ResolvedTag)) {
        throw "PreTag release report must not resolve a tag."
    }

    if ($Mode -eq 'Formal' -and
        [string]::IsNullOrWhiteSpace($ResolvedTag)) {
        throw "Formal release report requires ResolvedTag."
    }

    if (-not [string]::IsNullOrWhiteSpace($ResolvedTag) -and
        $ResolvedTag -ne $PlannedTag) {
        throw "Release report resolvedTag mismatch. Expected=$PlannedTag Actual=$ResolvedTag"
    }

    if (-not [string]::IsNullOrWhiteSpace($ResolvedTag)) {
        if ([string]::IsNullOrWhiteSpace($RepositoryPath)) {
            throw "Formal release report requires RepositoryPath to resolve the remote tag."
        }

        $remoteTag = Resolve-AppUIRemoteTagIdentity `
            -RepositoryPath $RepositoryPath `
            -Tag $ResolvedTag
        if ($remoteTag.SourceCommit -ne $ExpectedSourceCommit) {
            throw "Remote tag sourceCommit mismatch. Expected=$ExpectedSourceCommit Actual=$($remoteTag.SourceCommit)"
        }

        if ($remoteTag.SourceTree -ne $ExpectedSourceTree) {
            throw "Remote tag sourceTree mismatch. Expected=$ExpectedSourceTree Actual=$($remoteTag.SourceTree)"
        }
    }

    $editMode = Read-AppUINUnit3Result -Path (Join-Path $resolvedEvidenceRoot 'editmode.xml')
    $playMode = Read-AppUINUnit3Result -Path (Join-Path $resolvedEvidenceRoot 'playmode.xml')
    $binding = Get-AppUIJsonEvidenceGate -Path (Join-Path $resolvedEvidenceRoot 'binding-validation.json') -Kind Binding
    $mono = Get-AppUIJsonEvidenceGate -Path (Join-Path $resolvedEvidenceRoot 'build-windowsmono.json') -Kind Build
    $il2cpp = Get-AppUIJsonEvidenceGate -Path (Join-Path $resolvedEvidenceRoot 'build-windowsil2cpp.json') -Kind Build
    $resolvedCommitSmokePath = if ([string]::IsNullOrWhiteSpace($CommitSmokePath)) {
        Join-Path $resolvedEvidenceRoot 'commit-git-install-smoke.json'
    } else {
        [System.IO.Path]::GetFullPath($CommitSmokePath)
    }
    $resolvedTagSmokePath = if ([string]::IsNullOrWhiteSpace($TagSmokePath)) {
        Join-Path $resolvedEvidenceRoot 'tag-git-install-smoke.json'
    } else {
        [System.IO.Path]::GetFullPath($TagSmokePath)
    }
    $commitSmoke = Get-AppUIJsonEvidenceGate -Path $resolvedCommitSmokePath -Kind Smoke
    $tagSmoke = Get-AppUIJsonEvidenceGate -Path $resolvedTagSmokePath -Kind Smoke
    Test-AppUISmokeIdentity `
        -Path $resolvedCommitSmokePath `
        -GateName 'commitGitInstallSmoke' `
        -Identity $identity `
        -Required:($Mode -eq 'Formal')
    Test-AppUISmokeIdentity `
        -Path $resolvedTagSmokePath `
        -GateName 'tagGitInstallSmoke' `
        -Identity $identity `
        -Required:($Mode -eq 'Formal')
    $unityVersion = ''
    $bindingDocument = Get-Content -LiteralPath (Join-Path $resolvedEvidenceRoot 'binding-validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($bindingDocument.PSObject.Properties['unityVersion']) {
        $unityVersion = [string]$bindingDocument.unityVersion
    }

    $requiredStatuses = @(
        $editMode.Status,
        $playMode.Status,
        $binding.status,
        $mono.status,
        $il2cpp.status
    )
    if ($Mode -eq 'Formal') {
        $requiredStatuses += @(
            $commitSmoke.status,
            $tagSmoke.status
        )
    } else {
        if ($commitSmoke.status -ne 'NotRun') {
            $requiredStatuses += $commitSmoke.status
        }

        if ($tagSmoke.status -ne 'NotRun') {
            $requiredStatuses += $tagSmoke.status
        }
    }
    $overallStatus = if ($requiredStatuses -contains 'Failed') {
        'Failed'
    } elseif ($requiredStatuses -contains 'Blocked' -or
              $requiredStatuses -contains 'NotRun') {
        'Blocked'
    } else {
        'Passed'
    }

    $report = [ordered]@{
        schemaVersion = 'appui-release-report.v1'
        mode = $Mode
        status = $overallStatus
        repository = [string]$identity.repository
        sourceCommit = [string]$identity.sourceCommit
        sourceTree = [string]$identity.sourceTree
        plannedTag = $PlannedTag
        resolvedTag = if ([string]::IsNullOrWhiteSpace($ResolvedTag)) { $null } else { $ResolvedTag }
        packageVersion = [string]$identity.packageVersion
        packageManifestSha256 = [string]$identity.packageManifestSha256
        unityVersion = $unityVersion
        operatingSystem = 'Windows'
        uguiVersion = '2.0.0'
        editMode = [ordered]@{
            status = $editMode.Status
            passed = $editMode.Passed
            failed = $editMode.Failed
            skipped = $editMode.Skipped
            evidenceFile = $editMode.EvidenceFile
            durationMs = $editMode.DurationMs
        }
        playMode = [ordered]@{
            status = $playMode.Status
            passed = $playMode.Passed
            failed = $playMode.Failed
            skipped = $playMode.Skipped
            evidenceFile = $playMode.EvidenceFile
            durationMs = $playMode.DurationMs
        }
        bindingValidation = $binding
        monoBuild = $mono
        il2cppBuild = $il2cpp
        commitGitInstallSmoke = $commitSmoke
        tagGitInstallSmoke = $tagSmoke
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }

    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedOutput)) | Out-Null
    Write-AppUIUtf8NoBom -Path $resolvedOutput -Value (($report | ConvertTo-Json -Depth 10) + "`n")
    return [PSCustomObject]$report
}

function Protect-AppUILog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputPath,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [string]$RepositoryPath = '',
        [string]$ConsumerPath = '',
        [string]$UserProfilePath = '',

        [switch]$RedactAllLocalPathRoots
    )

    $text = Get-Content -LiteralPath $InputPath -Raw -Encoding UTF8
    foreach ($replacement in @(
        @($RepositoryPath, '<REPOSITORY>'),
        @($ConsumerPath, '<CONSUMER>'),
        @($UserProfilePath, '<USER_PROFILE>')
    )) {
        if ([string]::IsNullOrWhiteSpace($replacement[0])) {
            continue
        }

        foreach ($variant in @(
            $replacement[0],
            $replacement[0].Replace('\', '/'),
            $replacement[0].Replace('/', '\'),
            $replacement[0].Replace('\', '\\'),
            $replacement[0].Replace('/', '\/'),
            $replacement[0].Replace('\', '/').Replace('/', '\/'),
            $replacement[0].Replace('/', '\').Replace('\', '\\')
        ) | Select-Object -Unique) {
            $text = [regex]::Replace(
                $text,
                [regex]::Escape($variant),
                $replacement[1],
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }
    }

    if ($RedactAllLocalPathRoots) {
        # Unity and native build logs contain additional machine-owned paths such as
        # the Editor, SDK and toolchain locations. Preserve the useful suffix while
        # removing the local drive or file URI root from every remaining path.
        $text = [regex]::Replace(
            $text,
            '(?i)(?<![a-z])(?:[a-z]:[\\/]|file:[\\/]{1,2})',
            '<LOCAL_PATH_ROOT>/')
    }

    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedOutput)) | Out-Null
    Write-AppUIUtf8NoBom -Path $resolvedOutput -Value $text
}

function Test-AppUIArtifactSecrets {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [switch]$ThrowOnSecret
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        throw "Artifact path does not exist: $resolvedPath"
    }

    $files = if (Test-Path -LiteralPath $resolvedPath -PathType Container) {
        @(Get-ChildItem -LiteralPath $resolvedPath -Recurse -Force -File)
    } else {
        @(Get-Item -LiteralPath $resolvedPath)
    }
    $patterns = @(
        '(?i)github_pat_[a-z0-9_]+',
        '(?i)ghp_[a-z0-9]+',
        '(?i)Authorization\s*:\s*(?:Bearer|Basic)\s+\S+',
        '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
    )
    $findings = New-Object System.Collections.Generic.List[string]
    $temporaryArchives = New-Object System.Collections.Generic.List[string]
    $expandedFiles = New-Object System.Collections.Generic.List[object]
    try {
        foreach ($file in $files) {
            if ($file.Extension -ieq '.zip') {
                $archiveRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
                    'joih-appui-artifact-audit-' + [Guid]::NewGuid().ToString('N'))
                [System.IO.Directory]::CreateDirectory($archiveRoot) | Out-Null
                $temporaryArchives.Add($archiveRoot)
                try {
                    Expand-Archive -LiteralPath $file.FullName -DestinationPath $archiveRoot
                }
                catch {
                    $findings.Add($file.FullName)
                    continue
                }

                foreach ($expanded in Get-ChildItem -LiteralPath $archiveRoot -Recurse -Force -File) {
                    $expandedFiles.Add($expanded)
                }
                continue
            }

            $expandedFiles.Add($file)
        }

        foreach ($file in $expandedFiles) {
        if ($file.Length -gt 20MB) {
            continue
        }

        $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        foreach ($pattern in $patterns) {
            if ($content -match $pattern) {
                $findings.Add($file.FullName)
            }
        }
        }
    }
    finally {
        foreach ($archiveRoot in $temporaryArchives) {
            if ((Test-Path -LiteralPath $archiveRoot) -and
                $archiveRoot.StartsWith([System.IO.Path]::GetTempPath(), [System.StringComparison]::OrdinalIgnoreCase) -and
                (Split-Path -Leaf $archiveRoot).StartsWith('joih-appui-artifact-audit-', [System.StringComparison]::Ordinal)) {
                Remove-Item -LiteralPath $archiveRoot -Recurse -Force
            }
        }
    }

    if ($findings.Count -gt 0 -and $ThrowOnSecret) {
        $uniqueFiles = @($findings | Select-Object -Unique)
        throw "Artifact secret audit failed. Files=$($uniqueFiles -join ' | ')"
    }

    return $findings.Count -eq 0
}

function Test-AppUIArtifactLocalPaths {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [switch]$ThrowOnPath
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        throw "Artifact path does not exist: $resolvedPath"
    }

    $files = if (Test-Path -LiteralPath $resolvedPath -PathType Container) {
        @(Get-ChildItem -LiteralPath $resolvedPath -Recurse -Force -File)
    } else {
        @(Get-Item -LiteralPath $resolvedPath)
    }
    $temporaryArchives = New-Object System.Collections.Generic.List[string]
    $expandedFiles = New-Object System.Collections.Generic.List[object]
    $findings = New-Object System.Collections.Generic.List[string]
    try {
        foreach ($file in $files) {
            if ($file.Extension -ieq '.zip') {
                $archiveRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
                    'joih-appui-path-audit-' + [Guid]::NewGuid().ToString('N'))
                [System.IO.Directory]::CreateDirectory($archiveRoot) | Out-Null
                $temporaryArchives.Add($archiveRoot)
                try {
                    Expand-Archive -LiteralPath $file.FullName -DestinationPath $archiveRoot
                }
                catch {
                    $findings.Add($file.FullName)
                    continue
                }

                foreach ($expanded in Get-ChildItem -LiteralPath $archiveRoot -Recurse -Force -File) {
                    $expandedFiles.Add($expanded)
                }
                continue
            }

            $expandedFiles.Add($file)
        }

        foreach ($file in $expandedFiles) {
            if ($file.Length -gt 20MB) {
                continue
            }

            $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
            if ($content -match '(?i)(?:(?<![a-z])[a-z]:[\\/]|file:[\\/]{1,2})') {
                $findings.Add($file.FullName)
            }
        }
    }
    finally {
        foreach ($archiveRoot in $temporaryArchives) {
            if ((Test-Path -LiteralPath $archiveRoot) -and
                $archiveRoot.StartsWith([System.IO.Path]::GetTempPath(), [System.StringComparison]::OrdinalIgnoreCase) -and
                (Split-Path -Leaf $archiveRoot).StartsWith('joih-appui-path-audit-', [System.StringComparison]::Ordinal)) {
                Remove-Item -LiteralPath $archiveRoot -Recurse -Force
            }
        }
    }

    if ($findings.Count -gt 0 -and $ThrowOnPath) {
        $uniqueFiles = @($findings | Select-Object -Unique)
        throw "Artifact local path audit failed. Files=$($uniqueFiles -join ' | ')"
    }

    return $findings.Count -eq 0
}

function New-AppUISanitizedLogArchive {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputDirectory,

        [Parameter(Mandatory = $true)]
        [string]$OutputArchive,

        [string]$RepositoryPath = '',
        [string]$ConsumerPath = '',
        [string]$UserProfilePath = ''
    )

    $resolvedInput = [System.IO.Path]::GetFullPath($InputDirectory)
    $resolvedArchive = [System.IO.Path]::GetFullPath($OutputArchive)
    if (-not (Test-Path -LiteralPath $resolvedInput -PathType Container)) {
        throw "Log input directory does not exist: $resolvedInput"
    }

    if (Test-Path -LiteralPath $resolvedArchive) {
        throw "Log archive already exists: $resolvedArchive"
    }

    $archiveParent = Split-Path -Parent $resolvedArchive
    [System.IO.Directory]::CreateDirectory($archiveParent) | Out-Null
    $temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
        'joih-appui-sanitized-logs-' + [Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    try {
        $files = @(Get-ChildItem -LiteralPath $resolvedInput -Recurse -Force -File)
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($resolvedInput.Length).TrimStart('\', '/')
            $destination = Join-Path $temporaryRoot $relative
            [System.IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
            Protect-AppUILog `
                -InputPath $file.FullName `
                -OutputPath $destination `
                -RepositoryPath $RepositoryPath `
                -ConsumerPath $ConsumerPath `
                -UserProfilePath $UserProfilePath `
                -RedactAllLocalPathRoots
        }

        Test-AppUIArtifactSecrets `
            -Path $temporaryRoot `
            -ThrowOnSecret | Out-Null
        Test-AppUIArtifactLocalPaths `
            -Path $temporaryRoot `
            -ThrowOnPath | Out-Null
        Compress-Archive `
            -Path (Join-Path $temporaryRoot '*') `
            -DestinationPath $resolvedArchive `
            -CompressionLevel Optimal
        if (-not (Test-Path -LiteralPath $resolvedArchive -PathType Leaf)) {
            throw "Sanitized log archive was not created: $resolvedArchive"
        }
        Test-AppUIArtifactSecrets `
            -Path $resolvedArchive `
            -ThrowOnSecret | Out-Null
        Test-AppUIArtifactLocalPaths `
            -Path $resolvedArchive `
            -ThrowOnPath | Out-Null
    }
    finally {
        if ((Test-Path -LiteralPath $temporaryRoot) -and
            $temporaryRoot.StartsWith([System.IO.Path]::GetTempPath(), [System.StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $temporaryRoot).StartsWith('joih-appui-sanitized-logs-', [System.StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }

    return [PSCustomObject][ordered]@{
        ArchivePath = $resolvedArchive
        Sha256 = Get-AppUISha256 -Path $resolvedArchive
    }
}

function New-AppUIReleaseArtifacts {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,

        [Parameter(Mandatory = $true)]
        [string]$Version,

        [string]$RepositoryPath = '',
        [string]$ConsumerPath = '',
        [string]$UserProfilePath = ''
    )

    if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z.+-]*$') {
        throw "Release artifact version is invalid: $Version"
    }

    $resolvedSource = [System.IO.Path]::GetFullPath($SourceDirectory)
    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
    if (-not (Test-Path -LiteralPath $resolvedSource -PathType Container)) {
        throw "Release artifact source does not exist: $resolvedSource"
    }

    if (Test-Path -LiteralPath $resolvedOutput) {
        throw "Release artifact output already exists: $resolvedOutput"
    }

    $mapping = [ordered]@{
        'release-report.json' = "appui-v$Version-release-report.json"
        'package-manifest.json' = "appui-v$Version-package-manifest.json"
        'editmode.xml' = "appui-v$Version-editmode.xml"
        'playmode.xml' = "appui-v$Version-playmode.xml"
        'binding-validation.json' = "appui-v$Version-binding-validation.json"
        'build-windowsmono.json' = "appui-v$Version-mono-build.json"
        'build-windowsil2cpp.json' = "appui-v$Version-il2cpp-build.json"
        'commit-git-install-smoke.json' = "appui-v$Version-commit-smoke.json"
        'tag-git-install-smoke.json' = "appui-v$Version-tag-smoke.json"
        'logs.zip' = "appui-v$Version-logs.zip"
    }

    foreach ($sourceName in $mapping.Keys) {
        $sourcePath = Join-Path $resolvedSource $sourceName
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Required release artifact source is missing: $sourceName"
        }
    }

    [System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
    try {
        foreach ($sourceName in $mapping.Keys) {
            $sourcePath = Join-Path $resolvedSource $sourceName
            $destinationPath = Join-Path $resolvedOutput $mapping[$sourceName]
            if ($sourceName.EndsWith('.zip', [System.StringComparison]::OrdinalIgnoreCase)) {
                Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
                continue
            }

            Protect-AppUILog `
                -InputPath $sourcePath `
                -OutputPath $destinationPath `
                -RepositoryPath $RepositoryPath `
                -ConsumerPath $ConsumerPath `
                -UserProfilePath $UserProfilePath
        }

        Test-AppUIArtifactSecrets `
            -Path $resolvedOutput `
            -ThrowOnSecret | Out-Null
        Test-AppUIArtifactLocalPaths `
            -Path $resolvedOutput `
            -ThrowOnPath | Out-Null

        $files = @(Get-ChildItem -LiteralPath $resolvedOutput -Force -File)
        if ($files.Count -ne $mapping.Count) {
            throw "Release artifact count mismatch. Expected=$($mapping.Count) Actual=$($files.Count)"
        }

        $hashes = [ordered]@{}
        foreach ($file in $files | Sort-Object Name) {
            $hashes[$file.Name] = Get-AppUISha256 -Path $file.FullName
        }

        return [PSCustomObject][ordered]@{
            OutputDirectory = $resolvedOutput
            ArtifactCount = $files.Count
            Hashes = [PSCustomObject]$hashes
        }
    }
    catch {
        if ((Test-Path -LiteralPath $resolvedOutput) -and
            $resolvedOutput -ne [System.IO.Path]::GetPathRoot($resolvedOutput)) {
            Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
        }
        throw
    }
}

Export-ModuleMember -Function @(
    'Resolve-AppUIGitIdentity',
    'Resolve-AppUIRemoteTagIdentity',
    'Test-AppUISemVerTag',
    'Invoke-AppUIGitRemoteText',
    'Test-AppUIReleaseReadiness',
    'Write-AppUIJson',
    'Export-AppUICandidateSnapshot',
    'Test-AppUICandidateSnapshot',
    'New-AppUIConsumerWorkspace',
    'Test-AppUIPackagePolicy',
    'Read-AppUINUnit3Result',
    'Invoke-AppUIProcess',
    'Invoke-AppUIUnityProcess',
    'Test-AppUIBuildEnvironment',
    'New-AppUIReleaseReport',
    'Protect-AppUILog',
    'Test-AppUIArtifactSecrets',
    'Test-AppUIArtifactLocalPaths',
    'New-AppUISanitizedLogArchive',
    'New-AppUIReleaseArtifacts'
)
