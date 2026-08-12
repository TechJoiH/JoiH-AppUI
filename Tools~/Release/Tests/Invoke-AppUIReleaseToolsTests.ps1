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
        if ($null -ne $_.InvocationInfo) {
            Write-Host $_.InvocationInfo.PositionMessage
        }
        if (-not [string]::IsNullOrWhiteSpace($_.ScriptStackTrace)) {
            Write-Host $_.ScriptStackTrace
        }
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

function New-ConsumerTestTemplate {
    param([string]$Path)

    [System.IO.Directory]::CreateDirectory((Join-Path $Path 'Assets')) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $Path 'Packages')) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $Path 'ProjectSettings')) | Out-Null
    Set-Utf8NoBomContent (Join-Path $Path 'Assets\Marker.txt') "template marker`n"
    Set-Utf8NoBomContent (Join-Path $Path 'Assets\Marker.txt.meta') @'
fileFormatVersion: 2
guid: 11111111111111111111111111111111
'@
    Set-Utf8NoBomContent (Join-Path $Path 'Packages\manifest.template.json') @'
{
  "dependencies": {
    "com.joih.appui": "__APPUI_PACKAGE_REFERENCE__",
    "com.unity.test-framework": "1.4.5",
    "com.unity.ugui": "2.0.0"
  },
  "testables": [
    "com.joih.appui"
  ]
}
'@
    Set-Utf8NoBomContent (Join-Path $Path 'ProjectSettings\ProjectVersion.txt') "m_EditorVersion: 6000.0.25f1`n"
}

function New-PolicyTestRepository {
    param(
        [string]$Path,
        [string]$RuntimeSource = "namespace Joi.H.AppUI { public sealed class SafeType { } }`n",
        [string]$UnityVersion = '6000.0',
        [string]$DependenciesJson = '"com.unity.ugui": "2.0.0"'
    )

    [System.IO.Directory]::CreateDirectory($Path) | Out-Null
    Invoke-TestGit $Path init | Out-Null
    Invoke-TestGit $Path config user.name 'AppUI Release Test' | Out-Null
    Invoke-TestGit $Path config user.email 'appui-release-test@example.invalid' | Out-Null
    Invoke-TestGit $Path config core.autocrlf false | Out-Null
    Set-Utf8NoBomContent (Join-Path $Path 'package.json') @"
{
  "name": "com.joih.appui",
  "version": "1.2.3-test.1",
  "unity": "$UnityVersion",
  "dependencies": {
    $DependenciesJson
  }
}
"@
    Set-Utf8NoBomContent (Join-Path $Path 'Runtime\SafeType.cs') $RuntimeSource
    Set-Utf8NoBomContent (Join-Path $Path 'Runtime\SafeType.cs.meta') @'
fileFormatVersion: 2
guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
'@
    Set-Utf8NoBomContent (Join-Path $Path 'Runtime.meta') @'
fileFormatVersion: 2
guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
folderAsset: yes
'@
    Invoke-TestGit $Path add -- package.json Runtime.meta Runtime/SafeType.cs Runtime/SafeType.cs.meta | Out-Null
    Invoke-TestGit $Path commit -m 'Policy fixture' | Out-Null
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

    if (Test-GroupRequested 'Consumer') {
        Invoke-Test 'Consumer workspace materializes a file package without mutating the template' {
            $template = Join-Path $testRoot 'consumer-template'
            New-ConsumerTestTemplate $template
            $templateBefore = Get-Content -LiteralPath (Join-Path $template 'Packages\manifest.template.json') -Raw -Encoding UTF8
            $destination = Join-Path $testRoot 'consumer-materialized'
            $packagePath = Join-Path $testRoot 'candidate package'
            [System.IO.Directory]::CreateDirectory($packagePath) | Out-Null

            $result = New-AppUIConsumerWorkspace -TemplatePath $template -DestinationPath $destination -PackageReference $packagePath

            $manifestPath = Join-Path $destination 'Packages\manifest.json'
            Assert-True (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'Materialized manifest is missing.'
            $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $expectedReference = 'file:' + ([System.IO.Path]::GetFullPath($packagePath).Replace('\', '/'))
            Assert-Equal $expectedReference $manifest.dependencies.'com.joih.appui' 'File package reference was not normalized.'
            Assert-Equal '1.4.5' $manifest.dependencies.'com.unity.test-framework' 'Test Framework version drifted.'
            Assert-Equal '2.0.0' $manifest.dependencies.'com.unity.ugui' 'UGUI version drifted.'
            Assert-Equal $templateBefore (Get-Content -LiteralPath (Join-Path $template 'Packages\manifest.template.json') -Raw -Encoding UTF8) 'Source template was mutated.'
            Assert-True (-not (Test-Path -LiteralPath (Join-Path $template 'Packages\manifest.json'))) 'Source template received a materialized manifest.'
            Assert-Equal $manifestPath $result.ManifestPath 'Workspace result returned the wrong manifest path.'
        }

        Invoke-Test 'Consumer workspace preserves an approved Git package URL' {
            $template = Join-Path $testRoot 'consumer-git-template'
            New-ConsumerTestTemplate $template
            $destination = Join-Path $testRoot 'consumer-git-materialized'
            $gitReference = 'https://github.com/TechJoiH/JoiH-AppUI.git#0123456789abcdef0123456789abcdef01234567'

            New-AppUIConsumerWorkspace -TemplatePath $template -DestinationPath $destination -PackageReference $gitReference | Out-Null
            $manifest = Get-Content -LiteralPath (Join-Path $destination 'Packages\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            Assert-Equal $gitReference $manifest.dependencies.'com.joih.appui' 'Git package reference changed during materialization.'
        }

        Invoke-Test 'Consumer workspace rejects unsafe templates and existing destinations' {
            $template = Join-Path $testRoot 'unsafe-template'
            New-ConsumerTestTemplate $template
            [System.IO.Directory]::CreateDirectory((Join-Path $template 'Library')) | Out-Null
            $destination = Join-Path $testRoot 'unsafe-materialized'

            Assert-Throws {
                New-AppUIConsumerWorkspace -TemplatePath $template -DestinationPath $destination -PackageReference 'https://github.com/TechJoiH/JoiH-AppUI.git#v1.0.0'
            } 'Library|forbidden' 'Unsafe template was accepted.'

            Remove-Item -LiteralPath (Join-Path $template 'Library') -Recurse -Force
            [System.IO.Directory]::CreateDirectory($destination) | Out-Null
            Assert-Throws {
                New-AppUIConsumerWorkspace -TemplatePath $template -DestinationPath $destination -PackageReference 'https://github.com/TechJoiH/JoiH-AppUI.git#v1.0.0'
            } 'already exists' 'Existing consumer destination was overwritten.'
        }
    }

    if (Test-GroupRequested 'Policy') {
        Invoke-Test 'Package policy accepts a clean exact commit' {
            $repository = Join-Path $testRoot 'policy-clean'
            $commit = New-PolicyTestRepository $repository
            $result = Test-AppUIPackagePolicy -RepositoryPath $repository -SourceRef $commit

            Assert-True $result.Success 'Clean policy fixture was rejected.'
            Assert-Equal 0 $result.ErrorCount 'Clean policy fixture reported errors.'
        }

        Invoke-Test 'Package policy audits the requested commit instead of dirty worktree content' {
            $repository = Join-Path $testRoot 'policy-dirty'
            $commit = New-PolicyTestRepository $repository
            Set-Utf8NoBomContent (Join-Path $repository 'Runtime\SafeType.cs') "using Cysharp.Threading.Tasks; namespace Annals { public sealed class Dirty { } }`n"

            $result = Test-AppUIPackagePolicy -RepositoryPath $repository -SourceRef $commit
            Assert-True $result.Success 'Dirty worktree content affected commit policy.'
        }

        Invoke-Test 'Package policy rejects wrong manifest and forbidden production tokens' {
            $repository = Join-Path $testRoot 'policy-forbidden'
            $source = "using Cysharp.Threading.Tasks; namespace Annals { public sealed class GameFrameworkAdapter { } }`n"
            $commit = New-PolicyTestRepository -Path $repository -RuntimeSource $source -UnityVersion '2022.3' -DependenciesJson '"com.unity.ugui": "1.0.0", "com.example.extra": "1.0.0"'
            $result = Test-AppUIPackagePolicy -RepositoryPath $repository -SourceRef $commit

            Assert-True (-not $result.Success) 'Forbidden policy fixture was accepted.'
            Assert-True (@($result.Checks | Where-Object { $_.Status -eq 'Error' -and $_.Name -eq 'PackageManifest' }).Count -eq 1) 'Manifest error was not reported.'
            Assert-True (@($result.Checks | Where-Object { $_.Status -eq 'Error' -and $_.Name -eq 'ForbiddenProductionTokens' }).Count -eq 1) 'Forbidden token error was not reported.'
        }

        Invoke-Test 'Package policy rejects missing meta, duplicate GUID and scattered version macros' {
            $repository = Join-Path $testRoot 'policy-meta-macro'
            $commit = New-PolicyTestRepository $repository
            Set-Utf8NoBomContent (Join-Path $repository 'Runtime\MissingMeta.cs') "public sealed class MissingMeta { }`n"
            Set-Utf8NoBomContent (Join-Path $repository 'Runtime\Duplicate.cs') "#if UNITY_2022`npublic sealed class Duplicate { }`n#endif`n"
            Set-Utf8NoBomContent (Join-Path $repository 'Runtime\Duplicate.cs.meta') @'
fileFormatVersion: 2
guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
'@
            Invoke-TestGit $repository add -- Runtime/MissingMeta.cs Runtime/Duplicate.cs Runtime/Duplicate.cs.meta | Out-Null
            Invoke-TestGit $repository commit -m 'Break meta and macro policy' | Out-Null
            $brokenCommit = Invoke-TestGit $repository rev-parse HEAD
            $result = Test-AppUIPackagePolicy -RepositoryPath $repository -SourceRef $brokenCommit

            Assert-True (-not $result.Success) 'Meta/macro policy fixture was accepted.'
            Assert-True (@($result.Checks | Where-Object { $_.Status -eq 'Error' -and $_.Name -eq 'UnityMetaIntegrity' }).Count -eq 1) 'Meta integrity error was not reported.'
            Assert-True (@($result.Checks | Where-Object { $_.Status -eq 'Error' -and $_.Name -eq 'VersionMacroBoundary' }).Count -eq 1) 'Version macro error was not reported.'
        }

        Invoke-Test 'Package policy rejects official multi-version profiles and empty compatibility shells' {
            $repository = Join-Path $testRoot 'policy-multi-version'
            $commit = New-PolicyTestRepository $repository
            Set-Utf8NoBomContent (Join-Path $repository 'Tools~\Release\unity2022.3-profile.json') "{}`n"
            Set-Utf8NoBomContent (Join-Path $repository 'Runtime\Compatibility\.gitkeep') ""
            Invoke-TestGit $repository add -- 'Tools~/Release/unity2022.3-profile.json' 'Runtime/Compatibility/.gitkeep' | Out-Null
            Invoke-TestGit $repository commit -m 'Add forbidden official compatibility shells' | Out-Null
            $brokenCommit = Invoke-TestGit $repository rev-parse HEAD

            $result = Test-AppUIPackagePolicy -RepositoryPath $repository -SourceRef $brokenCommit
            Assert-True (-not $result.Success) 'Multi-version profile/empty compatibility fixture was accepted.'
            Assert-True (@($result.Checks | Where-Object { $_.Status -eq 'Error' -and $_.Name -eq 'SingleOfficialUnityLine' }).Count -eq 1) 'Single official line error was not reported.'
            Assert-True (@($result.Checks | Where-Object { $_.Status -eq 'Error' -and $_.Name -eq 'CompatibilityYagni' }).Count -eq 1) 'Empty compatibility shell error was not reported.'
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
