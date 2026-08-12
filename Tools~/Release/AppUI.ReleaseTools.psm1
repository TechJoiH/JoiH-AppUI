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

Export-ModuleMember -Function @(
    'Resolve-AppUIGitIdentity',
    'Export-AppUICandidateSnapshot'
)
