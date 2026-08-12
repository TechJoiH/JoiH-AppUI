[CmdletBinding()]
param(
    [ValidateSet('All', 'Snapshot', 'Consumer', 'Policy', 'Docs')]
    [string[]]$TestGroup = @('All')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$script:Passed = 0
$script:Failed = 0
$script:ModulePath = Join-Path $PSScriptRoot '..\AppUI.ReleaseTools.psm1'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param(
        $Expected,
        $Actual,
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Assert-Throws {
    param(
        [scriptblock]$Action,
        [string]$MessagePattern,
        [string]$Message
    )

    try {
        & $Action
    }
    catch {
        if ([string]::IsNullOrEmpty($MessagePattern) -or
            $_.Exception.Message -match $MessagePattern) {
            return
        }

        throw "$Message Unexpected error: $($_.Exception.Message)"
    }

    throw "$Message Expected an exception."
}

function Invoke-Test {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    try {
        & $Action
        $script:Passed++
        Write-Host "PASS $Name"
    }
    catch {
        $script:Failed++
        Write-Host "FAIL $Name"
        Write-Host $_.Exception.ToString()
    }
}

function Invoke-TestGit {
    param(
        [string]$RepositoryPath,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    $output = & git -C $RepositoryPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git -C $RepositoryPath $($Arguments -join ' ')`n$($output -join "`n")"
    }

    return ($output -join "`n").Trim()
}

function Set-Utf8NoBomContent {
    param(
        [string]$Path,
        [string]$Value
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrEmpty($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }

    [System.IO.File]::WriteAllText(
        $Path,
        $Value,
        (New-Object System.Text.UTF8Encoding($false)))
}

function New-SnapshotTestRepository {
    param([string]$Path)

    [System.IO.Directory]::CreateDirectory($Path) | Out-Null
    Invoke-TestGit $Path init | Out-Null
    Invoke-TestGit $Path config user.name 'AppUI Release Test' | Out-Null
    Invoke-TestGit $Path config user.email 'appui-release-test@example.invalid' | Out-Null
    Invoke-TestGit $Path config core.autocrlf false | Out-Null

    Set-Utf8NoBomContent (Join-Path $Path 'package.json') @'
{
  "name": "com.joih.appui",
  "version": "9.8.7-test.1",
  "unity": "6000.0",
  "dependencies": {
    "com.unity.ugui": "2.0.0"
  }
}
'@
    Set-Utf8NoBomContent (Join-Path $Path 'Runtime\A.cs') "// committed`npublic sealed class A { }`n"
    Set-Utf8NoBomContent (Join-Path $Path 'Z.txt') "uppercase sorts first`n"
    Set-Utf8NoBomContent (Join-Path $Path 'a.txt') "lowercase sorts second`n"
    $unicodeName = ([string][char]0x4E2D) + ([string][char]0x6587)
    $unicodePath = 'Documentation~/space name ' + $unicodeName + '.md'
    Set-Utf8NoBomContent (Join-Path $Path $unicodePath) "# committed unicode path`n"
    Invoke-TestGit $Path add -- package.json Runtime/A.cs Z.txt a.txt $unicodePath | Out-Null
    Invoke-TestGit $Path commit -m 'Initial candidate' | Out-Null
    return Invoke-TestGit $Path rev-parse HEAD
}

function Test-GroupRequested {
    param([string]$Name)

    return $TestGroup -contains 'All' -or $TestGroup -contains $Name
}

if (-not (Test-Path -LiteralPath $script:ModulePath -PathType Leaf)) {
    throw "Release tools module does not exist: $script:ModulePath"
}

Import-Module $script:ModulePath -Force

$tempBase = [System.IO.Path]::GetTempPath()
$testRoot = Join-Path $tempBase ('joih-appui-release-tests-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($testRoot) | Out-Null

try {
    if (Test-GroupRequested 'Snapshot') {
        Invoke-Test 'Snapshot exports the committed tree and ignores worktree changes' {
            $repository = Join-Path $testRoot 'snapshot-source'
            $firstCommit = New-SnapshotTestRepository $repository

            Set-Utf8NoBomContent (Join-Path $repository 'Runtime\A.cs') "// dirty worktree`npublic sealed class A { public int Dirty; }`n"
            Set-Utf8NoBomContent (Join-Path $repository 'secret.txt') "github_pat_not_committed`n"

            $firstOutput = Join-Path $testRoot 'candidate-one'
            $secondOutput = Join-Path $testRoot 'candidate-two'
            $first = Export-AppUICandidateSnapshot -RepositoryPath $repository -SourceRef $firstCommit -DestinationPath $firstOutput
            $second = Export-AppUICandidateSnapshot -RepositoryPath $repository -SourceRef $firstCommit -DestinationPath $secondOutput

            Assert-Equal $firstCommit $first.SourceCommit 'Snapshot source commit drifted.'
            Assert-Equal '9.8.7-test.1' $first.PackageVersion 'Package version was not read from the commit.'
            Assert-Equal $first.PackageManifestSha256 $second.PackageManifestSha256 'Same commit produced different manifest hashes.'
            $unicodeName = ([string][char]0x4E2D) + ([string][char]0x6587)
            $unicodeCandidatePath = 'candidate\package\Documentation~\space name ' + $unicodeName + '.md'
            Assert-True (Test-Path -LiteralPath (Join-Path $firstOutput $unicodeCandidatePath)) 'Unicode/space path was not exported.'
            Assert-True (-not (Test-Path -LiteralPath (Join-Path $firstOutput 'candidate\package\secret.txt'))) 'Untracked secret leaked into snapshot.'

            $candidateSource = Get-Content -LiteralPath (Join-Path $firstOutput 'candidate\package\Runtime\A.cs') -Raw -Encoding UTF8
            Assert-True ($candidateSource -match '// committed') 'Snapshot used dirty worktree content.'
            Assert-True ($candidateSource -notmatch 'Dirty') 'Dirty field leaked into snapshot.'

            $manifest = Get-Content -LiteralPath $first.ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $manifestPaths = @($manifest.files | ForEach-Object { $_.path })
            $upperIndex = [Array]::IndexOf($manifestPaths, 'Z.txt')
            $lowerIndex = [Array]::IndexOf($manifestPaths, 'a.txt')
            Assert-True ($upperIndex -ge 0 -and $lowerIndex -ge 0 -and $upperIndex -lt $lowerIndex) 'Manifest paths were not sorted with ordinal semantics.'
        }

        Invoke-Test 'Snapshot content hash changes when the committed tree changes' {
            $repository = Join-Path $testRoot 'hash-source'
            $firstCommit = New-SnapshotTestRepository $repository
            $firstOutput = Join-Path $testRoot 'hash-one'
            $first = Export-AppUICandidateSnapshot -RepositoryPath $repository -SourceRef $firstCommit -DestinationPath $firstOutput

            Set-Utf8NoBomContent (Join-Path $repository 'Runtime\A.cs') "// second commit`npublic sealed class A { public int Value; }`n"
            Invoke-TestGit $repository add -- Runtime/A.cs | Out-Null
            Invoke-TestGit $repository commit -m 'Change candidate content' | Out-Null
            $secondCommit = Invoke-TestGit $repository rev-parse HEAD
            $secondOutput = Join-Path $testRoot 'hash-two'
            $second = Export-AppUICandidateSnapshot -RepositoryPath $repository -SourceRef $secondCommit -DestinationPath $secondOutput

            Assert-True ($first.PackageManifestSha256 -ne $second.PackageManifestSha256) 'Different trees produced the same manifest hash.'
            Assert-True ($first.SourceTree -ne $second.SourceTree) 'Different commits reported the same tree.'
        }

        Invoke-Test 'Snapshot refuses to overwrite an existing destination' {
            $repository = Join-Path $testRoot 'overwrite-source'
            $commit = New-SnapshotTestRepository $repository
            $existing = Join-Path $testRoot 'already-exists'
            [System.IO.Directory]::CreateDirectory($existing) | Out-Null

            Assert-Throws {
                Export-AppUICandidateSnapshot -RepositoryPath $repository -SourceRef $commit -DestinationPath $existing
            } 'already exists' 'Snapshot overwrote an existing destination.'
        }
    }
}
finally {
    if ((Test-Path -LiteralPath $testRoot) -and
        $testRoot.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $testRoot).StartsWith('joih-appui-release-tests-', [System.StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

Write-Host "RESULT Passed=$script:Passed Failed=$script:Failed"
if ($script:Failed -gt 0) {
    exit 1
}
