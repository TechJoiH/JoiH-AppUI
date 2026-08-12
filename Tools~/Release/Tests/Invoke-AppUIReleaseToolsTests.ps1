[CmdletBinding()]
param(
    [ValidateSet('All', 'Snapshot', 'Consumer', 'Policy', 'Orchestration', 'Docs')]
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
        [string]$PackageVersion = '1.2.3-test.1',
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
  "version": "$PackageVersion",
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
    [System.IO.Directory]::CreateDirectory((Join-Path $Path 'Validation~\Unity6000.0Consumer\Assets')) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $Path 'Validation~\Unity6000.0Consumer\Packages')) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $Path 'Validation~\Unity6000.0Consumer\ProjectSettings')) | Out-Null
    Set-Utf8NoBomContent (Join-Path $Path 'Validation~\Unity6000.0Consumer\Assets\Marker.txt') "consumer marker`n"
    Set-Utf8NoBomContent (Join-Path $Path 'Validation~\Unity6000.0Consumer\Assets\Marker.txt.meta') @'
fileFormatVersion: 2
guid: cccccccccccccccccccccccccccccccc
'@
    Set-Utf8NoBomContent (Join-Path $Path 'Validation~\Unity6000.0Consumer\Packages\manifest.template.json') '{}'
    Set-Utf8NoBomContent (Join-Path $Path 'Validation~\Unity6000.0Consumer\ProjectSettings\ProjectVersion.txt') "m_EditorVersion: 6000.0.25f1`n"
    Invoke-TestGit $Path add -- package.json Runtime.meta Runtime/SafeType.cs Runtime/SafeType.cs.meta 'Validation~/Unity6000.0Consumer' | Out-Null
    Invoke-TestGit $Path commit -m 'Policy fixture' | Out-Null
    return Invoke-TestGit $Path rev-parse HEAD
}

function New-ReleaseEvidenceFixture {
    param(
        [string]$Path,
        [string]$SourceCommit = '0123456789abcdef0123456789abcdef01234567',
        [string]$SourceTree = '89abcdef0123456789abcdef0123456789abcdef',
        [string]$Version = '1.2.3-test.1'
    )

    [System.IO.Directory]::CreateDirectory($Path) | Out-Null
    Set-Utf8NoBomContent (Join-Path $Path 'candidate-identity.json') (@{
        repository = 'TechJoiH/JoiH-AppUI'
        sourceCommit = $SourceCommit
        sourceTree = $SourceTree
        packageVersion = $Version
        packageManifestSha256 = ('a' * 64)
    } | ConvertTo-Json)
    Set-Utf8NoBomContent (Join-Path $Path 'package-manifest.json') (@{
        packageManifestSha256 = ('a' * 64)
        files = @()
    } | ConvertTo-Json)
    foreach ($name in @('editmode.xml', 'playmode.xml')) {
        Set-Utf8NoBomContent (Join-Path $Path $name) @'
<?xml version="1.0" encoding="utf-8"?>
<test-run total="3" passed="3" failed="0" skipped="0" duration="1.25" result="Passed">
  <test-suite result="Passed">
    <test-case fullname="Fixture.Passes" result="Passed" />
  </test-suite>
</test-run>
'@
    }

    Set-Utf8NoBomContent (Join-Path $Path 'binding-validation.json') '{"success":true,"errorCount":0,"durationMs":25,"unityVersion":"6000.0.25f1"}'
    Set-Utf8NoBomContent (Join-Path $Path 'build-windowsmono.json') '{"result":"Succeeded","totalTimeMs":100}'
    Set-Utf8NoBomContent (Join-Path $Path 'build-windowsil2cpp.json') '{"result":"Succeeded","totalTimeMs":200}'
    Set-Utf8NoBomContent (Join-Path $Path 'commit-git-install-smoke.json') (@{
        repository = 'TechJoiH/JoiH-AppUI'
        sourceCommit = $SourceCommit
        sourceTree = $SourceTree
        packageVersion = $Version
        packageManifestSha256 = ('a' * 64)
        packageReference = 'https://github.com/TechJoiH/JoiH-AppUI.git#' + $SourceCommit
        initialized = $true
        openPassed = $true
        closePassed = $true
    } | ConvertTo-Json)
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

        Invoke-Test 'Snapshot identity audit detects mutation and extra files' {
            $repository = Join-Path $testRoot 'snapshot-audit-source'
            $commit = New-SnapshotTestRepository $repository
            $output = Join-Path $testRoot 'snapshot-audit-output'
            $snapshot = Export-AppUICandidateSnapshot -RepositoryPath $repository -SourceRef $commit -DestinationPath $output

            $audit = Test-AppUICandidateSnapshot `
                -PackageRoot $snapshot.PackageRoot `
                -IdentityPath $snapshot.IdentityPath `
                -ManifestPath $snapshot.ManifestPath
            Assert-True $audit.Success 'Fresh candidate snapshot failed identity audit.'
            Assert-Equal $snapshot.PackageManifestSha256 $audit.PackageManifestSha256 'Snapshot audit hash drifted.'

            Set-Utf8NoBomContent (Join-Path $snapshot.PackageRoot 'Runtime\A.cs') 'mutated'
            Assert-Throws {
                Test-AppUICandidateSnapshot `
                    -PackageRoot $snapshot.PackageRoot `
                    -IdentityPath $snapshot.IdentityPath `
                    -ManifestPath $snapshot.ManifestPath
            } 'hash mismatch|identity audit' 'Mutated candidate snapshot was accepted.'

            $outputTwo = Join-Path $testRoot 'snapshot-audit-extra-output'
            $snapshotTwo = Export-AppUICandidateSnapshot -RepositoryPath $repository -SourceRef $commit -DestinationPath $outputTwo
            Set-Utf8NoBomContent (Join-Path $snapshotTwo.PackageRoot 'extra.txt') 'untracked export mutation'
            Assert-Throws {
                Test-AppUICandidateSnapshot `
                    -PackageRoot $snapshotTwo.PackageRoot `
                    -IdentityPath $snapshotTwo.IdentityPath `
                    -ManifestPath $snapshotTwo.ManifestPath
            } 'unexpected file|identity audit' 'Snapshot with an extra file was accepted.'
        }
    }

    if (Test-GroupRequested 'Consumer') {
        Invoke-Test 'Official Unity 6000 consumer template materializes with pinned versions and clean assets' {
            $repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
            $template = Join-Path $repositoryRoot 'Validation~\Unity6000.0Consumer'
            $destination = Join-Path $testRoot 'official-consumer-materialized'
            $packagePath = Join-Path $testRoot 'official-package'
            [System.IO.Directory]::CreateDirectory($packagePath) | Out-Null

            New-AppUIConsumerWorkspace -TemplatePath $template -DestinationPath $destination -PackageReference $packagePath | Out-Null

            $projectVersion = Get-Content -LiteralPath (Join-Path $destination 'ProjectSettings\ProjectVersion.txt') -Raw -Encoding UTF8
            Assert-True ($projectVersion -match 'm_EditorVersion:\s*6000\.0\.25f1') 'Official consumer Unity patch version drifted.'
            Assert-True ($projectVersion -match '4859ab7b5a49') 'Official consumer Unity revision drifted.'

            $manifest = Get-Content -LiteralPath (Join-Path $destination 'Packages\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            Assert-Equal '1.4.5' $manifest.dependencies.'com.unity.test-framework' 'Official consumer Test Framework version drifted.'
            Assert-Equal '2.0.0' $manifest.dependencies.'com.unity.ugui' 'Official consumer UGUI version drifted.'
            Assert-True (-not (Test-Path -LiteralPath (Join-Path $template 'Packages\manifest.json'))) 'Official template contains a materialized manifest.'

            foreach ($requiredPath in @(
                'README.md',
                '.gitignore',
                'Assets\AppUIConsumer\Runtime\Joi.H.AppUI.Validation.Consumer.asmdef',
                'Assets\AppUIConsumer\Runtime\Adapters\ConsumerOperationFactory.cs',
                'Assets\AppUIConsumer\Runtime\Adapters\ConsumerExecutionContext.cs',
                'Assets\AppUIConsumer\Runtime\Adapters\ConsumerAssetProvider.cs',
                'ProjectSettings\ProjectSettings.asset',
                'ProjectSettings\EditorSettings.asset',
                'ProjectSettings\EditorBuildSettings.asset',
                'ProjectSettings\GraphicsSettings.asset',
                'ProjectSettings\QualitySettings.asset',
                'ProjectSettings\InputManager.asset',
                'ProjectSettings\TagManager.asset',
                'ProjectSettings\TimeManager.asset'
            )) {
                Assert-True (Test-Path -LiteralPath (Join-Path $destination $requiredPath) -PathType Leaf) "Official consumer file is missing: $requiredPath"
            }

            $assetRoot = Join-Path $destination 'Assets'
            $assetTargets = @(Get-ChildItem -LiteralPath $assetRoot -Recurse -Force | Where-Object {
                -not $_.Name.EndsWith('.meta', [System.StringComparison]::OrdinalIgnoreCase)
            })
            foreach ($assetTarget in $assetTargets) {
                Assert-True (Test-Path -LiteralPath ($assetTarget.FullName + '.meta') -PathType Leaf) "Official consumer asset is missing meta: $($assetTarget.FullName)"
            }
        }

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

            foreach ($invalidReference in @(
                'https://github.com/TechJoiH/JoiH-AppUI.git#vabc',
                'https://github.com/TechJoiH/JoiH-AppUI.git#v1',
                'https://github.com/TechJoiH/JoiH-AppUI.git#v1.2',
                'https://github.com/TechJoiH/JoiH-AppUI.git#main'
            )) {
                Assert-Throws {
                    Resolve-AppUIPackageReference -PackageReference $invalidReference
                } 'does not exist|reference|invalid|path' "Non-SemVer Git reference was accepted: $invalidReference"
            }
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

        Invoke-Test 'Package policy requires strict SemVer and one official package/Consumer line' {
            $repository = Join-Path $testRoot 'policy-single-line'
            $commit = New-PolicyTestRepository -Path $repository -PackageVersion '01.2.3'
            Set-Utf8NoBomContent (Join-Path $repository 'Samples~\Nested\package.json') '{}'
            [System.IO.Directory]::CreateDirectory((Join-Path $repository 'Validation~\Unity2022.3Consumer')) | Out-Null
            Set-Utf8NoBomContent (Join-Path $repository 'Validation~\Unity2022.3Consumer\.gitkeep') ''
            Invoke-TestGit $repository add -- 'Samples~/Nested/package.json' 'Validation~/Unity2022.3Consumer/.gitkeep' | Out-Null
            Invoke-TestGit $repository commit -m 'Break package and Consumer identity' | Out-Null
            $brokenCommit = Invoke-TestGit $repository rev-parse HEAD

            $result = Test-AppUIPackagePolicy -RepositoryPath $repository -SourceRef $brokenCommit
            Assert-True (-not $result.Success) 'Invalid SemVer or duplicate package/Consumer line was accepted.'
            foreach ($checkName in @('PackageManifest', 'SinglePackageManifest', 'SingleOfficialConsumer')) {
                Assert-True (@($result.Checks | Where-Object { $_.Status -eq 'Error' -and $_.Name -eq $checkName }).Count -eq 1) "Missing policy error: $checkName"
            }
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

    if (Test-GroupRequested 'Orchestration') {
        Invoke-Test 'SemVer tags enforce the immutable release grammar' {
            foreach ($tag in @('v0.2.0-pre.2', 'v1.0.0', 'v1.2.3-rc.1+build.5')) {
                Assert-True (Test-AppUISemVerTag -Tag $tag) "Valid SemVer Tag was rejected: $tag"
            }
            foreach ($tag in @('v01.2.3', 'v1.02.3', 'v1.2.03', 'v1.2.3-01', 'v1', 'main', 'v1.2.3-')) {
                Assert-True (-not (Test-AppUISemVerTag -Tag $tag)) "Invalid SemVer Tag was accepted: $tag"
            }
        }

        Invoke-Test 'NUnit3 parser reports exact counts and rejects bad evidence' {
            $evidence = Join-Path $testRoot 'nunit-evidence'
            New-ReleaseEvidenceFixture $evidence
            $result = Read-AppUINUnit3Result -Path (Join-Path $evidence 'editmode.xml')
            Assert-Equal 3 $result.Total 'NUnit total was parsed incorrectly.'
            Assert-Equal 3 $result.Passed 'NUnit passed was parsed incorrectly.'
            Assert-Equal 0 $result.Failed 'NUnit failed was parsed incorrectly.'
            Assert-Equal 'Passed' $result.Status 'NUnit status was parsed incorrectly.'

            Set-Utf8NoBomContent (Join-Path $evidence 'failed.xml') @'
<test-run total="1" passed="0" failed="1" skipped="0" result="Failed">
  <test-case fullname="Fixture.Fails" result="Failed" />
</test-run>
'@
            Assert-Throws {
                Read-AppUINUnit3Result -Path (Join-Path $evidence 'failed.xml') -RequirePassed
            } 'failed|Fixture.Fails' 'Failed NUnit evidence was accepted.'
            Assert-Throws {
                Read-AppUINUnit3Result -Path (Join-Path $evidence 'missing.xml')
            } 'does not exist' 'Missing NUnit evidence was accepted.'
        }

        Invoke-Test 'Bounded process runner returns Blocked and terminates a timeout' {
            $slowScript = Join-Path $testRoot 'slow-process.ps1'
            Set-Utf8NoBomContent $slowScript 'Start-Sleep -Seconds 10'
            $powershellPath = (Get-Command powershell.exe).Source
            $result = Invoke-AppUIProcess `
                -FilePath $powershellPath `
                -ArgumentList @('-NoProfile', '-File', $slowScript) `
                -TimeoutSeconds 1
            Assert-Equal 'Blocked' $result.Status 'Timed out process was not blocked.'
            Assert-True $result.TimedOut 'Timed out process did not report TimedOut.'

            $spaceRoot = Join-Path $testRoot 'process path with spaces'
            $argumentScript = Join-Path $spaceRoot 'write marker.ps1'
            $markerPath = Join-Path $spaceRoot 'marker with spaces.txt'
            Set-Utf8NoBomContent $argumentScript @'
param([string]$MarkerPath)
[System.IO.File]::WriteAllText($MarkerPath, 'passed')
'@
            $spaceResult = Invoke-AppUIProcess `
                -FilePath $powershellPath `
                -ArgumentList @('-NoProfile', '-File', $argumentScript, '-MarkerPath', $markerPath) `
                -TimeoutSeconds 10
            Assert-Equal 'Passed' $spaceResult.Status 'Process runner did not preserve arguments with spaces.'
            Assert-True (Test-Path -LiteralPath $markerPath -PathType Leaf) 'Process argument with spaces did not reach the child.'
        }

        Invoke-Test 'Bounded remote runner distinguishes timeout and remote failure' {
            $fakeGit = Join-Path $testRoot 'fake-git.exe'
            Add-Type -TypeDefinition @'
using System;
using System.Threading;
public static class FakeGitProgram
{
    public static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("APPUI_REMOTE_FIXTURE_MODE") == "Sleep")
        {
            Thread.Sleep(10000);
            return 0;
        }

        Console.Error.WriteLine("network unavailable");
        return 23;
    }
}
'@ -OutputAssembly $fakeGit -OutputType ConsoleApplication

            $previousFixtureMode = $env:APPUI_REMOTE_FIXTURE_MODE
            try {
                $env:APPUI_REMOTE_FIXTURE_MODE = 'Sleep'
                $timeout = Invoke-AppUIGitRemoteText `
                    -RepositoryPath $testRoot `
                    -Arguments @('ignored') `
                    -TimeoutSeconds 1 `
                    -GitPath $fakeGit
                Assert-Equal 'Blocked' $timeout.Status 'Timed out remote command was not blocked.'
                Assert-Equal 'Timeout' $timeout.Reason 'Timed out remote command reason was wrong.'

                $env:APPUI_REMOTE_FIXTURE_MODE = 'Fail'
                $unavailable = Invoke-AppUIGitRemoteText `
                    -RepositoryPath $testRoot `
                    -Arguments @('ignored') `
                    -TimeoutSeconds 10 `
                    -GitPath $fakeGit
            }
            finally {
                $env:APPUI_REMOTE_FIXTURE_MODE = $previousFixtureMode
            }
            Assert-Equal 'Blocked' $unavailable.Status 'Failed remote command was not blocked.'
            Assert-Equal 'RemoteUnavailable' $unavailable.Reason 'Failed remote command reason was wrong.'
            Assert-Equal 23 $unavailable.ExitCode 'Failed remote command exit code was lost.'
        }

        Invoke-Test 'Unity and VS2022 C++ preflight reports supported and blocked environments' {
            $preflightRoot = Join-Path $testRoot 'preflight'
            $unity = Join-Path $preflightRoot 'Unity.exe'
            $vswhere = Join-Path $preflightRoot 'vswhere.exe'
            $vsInstall = Join-Path $preflightRoot 'VS2022'
            Set-Utf8NoBomContent $unity 'fixture'
            Set-Utf8NoBomContent $vswhere 'fixture'
            Set-Utf8NoBomContent (Join-Path $vsInstall 'VC\Auxiliary\Build\vcvars64.bat') '@echo off'

            $blocked = Test-AppUIBuildEnvironment `
                -UnityPath $unity `
                -ExpectedUnityVersion '6000.0.25f1' `
                -UnityVersionOverride '6000.0.25f1' `
                -VsWherePath $vswhere `
                -DisableVsWhereDiscovery
            Assert-Equal 'Blocked' $blocked.Status 'Missing VS2022 toolchain was not blocked.'
            Assert-Equal 'MissingToolchain' $blocked.Reason 'Missing VS2022 toolchain reason was wrong.'

            $passed = Test-AppUIBuildEnvironment `
                -UnityPath $unity `
                -ExpectedUnityVersion '6000.0.25f1' `
                -UnityVersionOverride '6000.0.25f1' `
                -VsWherePath $vswhere `
                -VsInstallationPathOverride $vsInstall
            Assert-Equal 'Passed' $passed.Status 'Valid VS2022 fixture was rejected.'
            Assert-Equal '6000.0.25f1' $passed.UnityVersion 'Unity version was not preserved.'
            Assert-Equal $vsInstall $passed.VsInstallationPath 'VS2022 path was not preserved.'

            $wrongUnity = Test-AppUIBuildEnvironment `
                -UnityPath $unity `
                -ExpectedUnityVersion '6000.0.25f1' `
                -UnityVersionOverride '6000.0.26f1' `
                -VsWherePath $vswhere `
                -VsInstallationPathOverride $vsInstall
            Assert-Equal 'Blocked' $wrongUnity.Status 'Wrong Unity version was accepted.'
            Assert-Equal 'UnityVersionMismatch' $wrongUnity.Reason 'Wrong Unity block reason was wrong.'
        }

        Invoke-Test 'Release report enforces candidate identity and tag contract' {
            $evidence = Join-Path $testRoot 'report-evidence'
            New-ReleaseEvidenceFixture $evidence
            $output = Join-Path $evidence 'pretag-report.json'
            $report = New-AppUIReleaseReport `
                -IdentityPath (Join-Path $evidence 'candidate-identity.json') `
                -EvidenceRoot $evidence `
                -OutputPath $output `
                -ExpectedSourceCommit '0123456789abcdef0123456789abcdef01234567' `
                -ExpectedSourceTree '89abcdef0123456789abcdef0123456789abcdef' `
                -ExpectedPackageVersion '1.2.3-test.1' `
                -PlannedTag 'v1.2.3-test.1'
            Assert-Equal $null $report.resolvedTag 'Pre-tag report resolved a tag.'
            Assert-Equal 'Passed' $report.editMode.status 'EditMode report status was wrong.'
            Assert-True (Test-Path -LiteralPath $output) 'Release report was not written.'

            $badManifest = Get-Content -LiteralPath (Join-Path $evidence 'package-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $badManifest.packageManifestSha256 = ('b' * 64)
            Set-Utf8NoBomContent (Join-Path $evidence 'package-manifest.json') ($badManifest | ConvertTo-Json)
            Assert-Throws {
                New-AppUIReleaseReport `
                    -IdentityPath (Join-Path $evidence 'candidate-identity.json') `
                    -EvidenceRoot $evidence `
                    -OutputPath (Join-Path $evidence 'bad-manifest-report.json') `
                    -ExpectedSourceCommit '0123456789abcdef0123456789abcdef01234567' `
                    -ExpectedSourceTree '89abcdef0123456789abcdef0123456789abcdef' `
                    -ExpectedPackageVersion '1.2.3-test.1' `
                    -PlannedTag 'v1.2.3-test.1'
            } 'package manifest.*mismatch|packageManifestSha256' 'Mismatched package manifest was accepted.'
            New-ReleaseEvidenceFixture $evidence

            $externalSmokeRoot = Join-Path $testRoot 'external-smoke-evidence'
            [System.IO.Directory]::CreateDirectory($externalSmokeRoot) | Out-Null
            Move-Item -LiteralPath (Join-Path $evidence 'commit-git-install-smoke.json') -Destination (Join-Path $externalSmokeRoot 'commit.json')
            $externalSmokeReport = New-AppUIReleaseReport `
                -IdentityPath (Join-Path $evidence 'candidate-identity.json') `
                -EvidenceRoot $evidence `
                -OutputPath (Join-Path $evidence 'external-smoke-report.json') `
                -ExpectedSourceCommit '0123456789abcdef0123456789abcdef01234567' `
                -ExpectedSourceTree '89abcdef0123456789abcdef0123456789abcdef' `
                -ExpectedPackageVersion '1.2.3-test.1' `
                -PlannedTag 'v1.2.3-test.1' `
                -CommitSmokePath (Join-Path $externalSmokeRoot 'commit.json')
            Assert-Equal 'Passed' $externalSmokeReport.commitGitInstallSmoke.status 'External Commit smoke was not merged.'
            Assert-Equal 'commit.json' $externalSmokeReport.commitGitInstallSmoke.evidenceFile 'External Commit smoke filename was not preserved.'
            Move-Item -LiteralPath (Join-Path $externalSmokeRoot 'commit.json') -Destination (Join-Path $evidence 'commit-git-install-smoke.json')

            $failedSmoke = Get-Content -LiteralPath (Join-Path $evidence 'commit-git-install-smoke.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $failedSmoke.openPassed = $false
            Set-Utf8NoBomContent (Join-Path $evidence 'commit-git-install-smoke.json') ($failedSmoke | ConvertTo-Json)
            $failedSmokeReport = New-AppUIReleaseReport `
                -IdentityPath (Join-Path $evidence 'candidate-identity.json') `
                -EvidenceRoot $evidence `
                -OutputPath (Join-Path $evidence 'failed-smoke-report.json') `
                -ExpectedSourceCommit '0123456789abcdef0123456789abcdef01234567' `
                -ExpectedSourceTree '89abcdef0123456789abcdef0123456789abcdef' `
                -ExpectedPackageVersion '1.2.3-test.1' `
                -PlannedTag 'v1.2.3-test.1'
            Assert-Equal 'Failed' $failedSmokeReport.status 'Present failed Commit smoke was not included in PreTag status.'
            New-ReleaseEvidenceFixture $evidence

            Assert-Throws {
                New-AppUIReleaseReport `
                    -IdentityPath (Join-Path $evidence 'candidate-identity.json') `
                    -EvidenceRoot $evidence `
                    -OutputPath (Join-Path $evidence 'bad-report.json') `
                    -ExpectedSourceCommit ('f' * 40) `
                    -ExpectedSourceTree '89abcdef0123456789abcdef0123456789abcdef' `
                    -ExpectedPackageVersion '1.2.3-test.1' `
                    -PlannedTag 'v1.2.3-test.1'
            } 'sourceCommit' 'Mismatched report identity was accepted.'
            Assert-Throws {
                New-AppUIReleaseReport `
                    -IdentityPath (Join-Path $evidence 'candidate-identity.json') `
                    -EvidenceRoot $evidence `
                    -OutputPath (Join-Path $evidence 'bad-tag-report.json') `
                    -ExpectedSourceCommit '0123456789abcdef0123456789abcdef01234567' `
                    -ExpectedSourceTree '89abcdef0123456789abcdef0123456789abcdef' `
                    -ExpectedPackageVersion '1.2.3-test.1' `
                    -PlannedTag 'v1.2.4'
            } 'plannedTag' 'Mismatched planned tag was accepted.'

            Set-Utf8NoBomContent (Join-Path $evidence 'build-windowsil2cpp.json') '{"result":"Failed","totalTimeMs":200}'
            $failedReport = New-AppUIReleaseReport `
                -IdentityPath (Join-Path $evidence 'candidate-identity.json') `
                -EvidenceRoot $evidence `
                -OutputPath (Join-Path $evidence 'failed-report.json') `
                -ExpectedSourceCommit '0123456789abcdef0123456789abcdef01234567' `
                -ExpectedSourceTree '89abcdef0123456789abcdef0123456789abcdef' `
                -ExpectedPackageVersion '1.2.3-test.1' `
                -PlannedTag 'v1.2.3-test.1'
            Assert-Equal 'Failed' $failedReport.status 'A failed build was mislabeled as Blocked.'

            Set-Utf8NoBomContent (Join-Path $evidence 'build-windowsil2cpp.json') '{"result":"Succeeded","totalTimeMs":200}'
            $mismatchedSmoke = Get-Content -LiteralPath (Join-Path $evidence 'commit-git-install-smoke.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $mismatchedSmoke.sourceCommit = ('f' * 40)
            Set-Utf8NoBomContent (Join-Path $evidence 'commit-git-install-smoke.json') ($mismatchedSmoke | ConvertTo-Json)
            Assert-Throws {
                New-AppUIReleaseReport `
                    -IdentityPath (Join-Path $evidence 'candidate-identity.json') `
                    -EvidenceRoot $evidence `
                    -OutputPath (Join-Path $evidence 'mismatched-smoke-report.json') `
                    -ExpectedSourceCommit '0123456789abcdef0123456789abcdef01234567' `
                    -ExpectedSourceTree '89abcdef0123456789abcdef0123456789abcdef' `
                    -ExpectedPackageVersion '1.2.3-test.1' `
                    -PlannedTag 'v1.2.3-test.1'
            } 'commitGitInstallSmoke.*sourceCommit|sourceCommit.*commitGitInstallSmoke' 'Mismatched Commit smoke identity was accepted.'

            New-ReleaseEvidenceFixture $evidence

            $formalRepository = Join-Path $testRoot 'formal-report-repository'
            $formalCommit = New-SnapshotTestRepository $formalRepository
            $formalTree = Invoke-TestGit $formalRepository rev-parse "$formalCommit^{tree}"
            $formalRemote = Join-Path $testRoot 'formal-report-remote.git'
            Invoke-TestGit $testRoot init --bare $formalRemote | Out-Null
            Invoke-TestGit $formalRepository remote add origin $formalRemote | Out-Null
            Invoke-TestGit $formalRepository tag v9.8.7-test.1 $formalCommit | Out-Null
            Invoke-TestGit $formalRepository push --quiet origin refs/tags/v9.8.7-test.1 | Out-Null
            $formalEvidence = Join-Path $testRoot 'formal-report-evidence'
            New-ReleaseEvidenceFixture `
                -Path $formalEvidence `
                -SourceCommit $formalCommit `
                -SourceTree $formalTree `
                -Version '9.8.7-test.1'
            $tagSmoke = Get-Content -LiteralPath (Join-Path $formalEvidence 'commit-git-install-smoke.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $tagSmoke.packageReference = 'https://github.com/TechJoiH/JoiH-AppUI.git#v9.8.7-test.1'
            Set-Utf8NoBomContent (Join-Path $formalEvidence 'tag-git-install-smoke.json') ($tagSmoke | ConvertTo-Json)
            Remove-Item -LiteralPath (Join-Path $formalEvidence 'commit-git-install-smoke.json') -Force
            Assert-Throws {
                New-AppUIReleaseReport `
                    -IdentityPath (Join-Path $formalEvidence 'candidate-identity.json') `
                    -EvidenceRoot $formalEvidence `
                    -OutputPath (Join-Path $formalEvidence 'formal-without-smoke.json') `
                    -ExpectedSourceCommit $formalCommit `
                    -ExpectedSourceTree $formalTree `
                    -ExpectedPackageVersion '9.8.7-test.1' `
                    -PlannedTag 'v9.8.7-test.1' `
                    -ResolvedTag 'v9.8.7-test.1' `
                    -RepositoryPath $formalRepository `
                    -Mode Formal
            } 'commitGitInstallSmoke evidence is missing' 'Formal report accepted missing Commit smoke.'
        }

        Invoke-Test 'Artifact sanitization redacts paths and rejects secrets' {
            $artifactRoot = Join-Path $testRoot 'artifact-audit'
            [System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
            $raw = Join-Path $artifactRoot 'raw.log'
            $safe = Join-Path $artifactRoot 'safe.log'
            Set-Utf8NoBomContent $raw 'repo=C:\work\repo consumer=C:\work\consumer user=C:\Users\Tester'
            Protect-AppUILog `
                -InputPath $raw `
                -OutputPath $safe `
                -RepositoryPath 'C:\work\repo' `
                -ConsumerPath 'C:\work\consumer' `
                -UserProfilePath 'C:\Users\Tester'
            $safeText = Get-Content -LiteralPath $safe -Raw -Encoding UTF8
            Assert-True ($safeText -match '<REPOSITORY>') 'Repository path was not redacted.'
            Assert-True ($safeText -match '<CONSUMER>') 'Consumer path was not redacted.'
            Assert-True ($safeText -match '<USER_PROFILE>') 'User profile path was not redacted.'
            Assert-True (Test-AppUIArtifactSecrets -Path $safe) 'Sanitized log failed the secret audit.'

            Set-Utf8NoBomContent (Join-Path $artifactRoot 'secret.log') 'Authorization: Bearer github_pat_example'
            try {
                Test-AppUIArtifactSecrets -Path $artifactRoot -ThrowOnSecret
                throw 'Secret-bearing artifact was accepted.'
            }
            catch {
                Assert-True ($_.Exception.Message -match 'secret audit failed') 'Secret audit failure was not reported.'
                Assert-True ($_.Exception.Message -notmatch 'github_pat_example') 'Secret audit leaked the matched credential.'
            }

            Remove-Item -LiteralPath (Join-Path $artifactRoot 'secret.log') -Force
            $archive = Join-Path $testRoot 'sanitized-logs.zip'
            $archiveResult = New-AppUISanitizedLogArchive `
                -InputDirectory $artifactRoot `
                -OutputArchive $archive `
                -RepositoryPath 'C:\work\repo' `
                -ConsumerPath 'C:\work\consumer' `
                -UserProfilePath 'C:\Users\Tester'
            Assert-True (Test-Path -LiteralPath $archive -PathType Leaf) 'Sanitized log archive was not created.'
            Assert-True ($archiveResult.Sha256 -match '^[0-9a-f]{64}$') 'Sanitized log archive hash was invalid.'

            $secretZipSource = Join-Path $testRoot 'secret-zip-source'
            [System.IO.Directory]::CreateDirectory($secretZipSource) | Out-Null
            Set-Utf8NoBomContent (Join-Path $secretZipSource 'secret.log') 'ghp_secret_inside_archive'
            $secretZip = Join-Path $testRoot 'secret-logs.zip'
            Compress-Archive -Path (Join-Path $secretZipSource '*') -DestinationPath $secretZip
            Assert-Throws {
                Test-AppUIArtifactSecrets -Path $secretZip -ThrowOnSecret
            } 'secret audit failed' 'Secret inside a ZIP artifact was accepted.'

            $pathZipSource = Join-Path $testRoot 'path-zip-source'
            [System.IO.Directory]::CreateDirectory($pathZipSource) | Out-Null
            Set-Utf8NoBomContent (Join-Path $pathZipSource 'machine.log') 'tool=D:\PrivateTools\sdk.exe'
            $pathZip = Join-Path $testRoot 'machine-path-logs.zip'
            Compress-Archive -Path (Join-Path $pathZipSource '*') -DestinationPath $pathZip
            Assert-Throws {
                Test-AppUIArtifactLocalPaths -Path $pathZip -ThrowOnPath
            } 'local path audit failed' 'Local path inside a ZIP artifact was accepted.'
        }

        Invoke-Test 'Release artifact staging emits the exact sanitized ten-file set' {
            $source = Join-Path $testRoot 'release-artifact-source'
            $output = Join-Path $testRoot 'release-artifact-output'
            [System.IO.Directory]::CreateDirectory($source) | Out-Null
            $repoPath = 'C:\work\repo'
            $consumerPath = 'C:\work\consumer'
            $profilePath = 'C:\Users\Tester'
            $sourceNames = @(
                'release-report.json',
                'package-manifest.json',
                'editmode.xml',
                'playmode.xml',
                'binding-validation.json',
                'build-windowsmono.json',
                'build-windowsil2cpp.json',
                'commit-git-install-smoke.json',
                'tag-git-install-smoke.json'
            )
            foreach ($name in $sourceNames) {
                $value = if ($name.EndsWith('.json')) {
                    '{"path":"C:\\work\\repo","consumer":"C:/work/consumer","profile":"C:\\Users\\Tester"}'
                } else {
                    '<test-run path="C:\work\repo" />'
                }
                Set-Utf8NoBomContent (Join-Path $source $name) $value
            }
            $logsSource = Join-Path $testRoot 'release-artifact-logs-source'
            [System.IO.Directory]::CreateDirectory($logsSource) | Out-Null
            Set-Utf8NoBomContent (Join-Path $logsSource 'safe.log') 'https://github.com/TechJoiH/JoiH-AppUI'
            Compress-Archive -Path (Join-Path $logsSource '*') -DestinationPath (Join-Path $source 'logs.zip')

            $bundle = New-AppUIReleaseArtifacts `
                -SourceDirectory $source `
                -OutputDirectory $output `
                -Version '0.2.0-pre.2' `
                -RepositoryPath $repoPath `
                -ConsumerPath $consumerPath `
                -UserProfilePath $profilePath
            Assert-Equal 10 $bundle.ArtifactCount 'Release artifact count was wrong.'
            Assert-Equal 10 @(Get-ChildItem -LiteralPath $output -File).Count 'Release artifact directory contained the wrong count.'
            foreach ($file in Get-ChildItem -LiteralPath $output -File | Where-Object Extension -ne '.zip') {
                $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
                Assert-True ($content -notmatch '(?i)(?<![a-z])[a-z]:[\\/]') "Release artifact retained a local path: $($file.Name)"
            }

            $bindingArtifact = Get-Content -LiteralPath (Join-Path $output 'appui-v0.2.0-pre.2-binding-validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            Assert-Equal '<REPOSITORY>' $bindingArtifact.path 'Escaped JSON repository path was not redacted safely.'

            $unsafeSource = Join-Path $testRoot 'release-artifact-unsafe-source'
            Copy-Item -LiteralPath $source -Destination $unsafeSource -Recurse
            Set-Utf8NoBomContent (Join-Path $unsafeSource 'release-report.json') '{"unlistedMachinePath":"D:\\PrivateTools\\sdk.exe"}'
            Assert-Throws {
                New-AppUIReleaseArtifacts `
                    -SourceDirectory $unsafeSource `
                    -OutputDirectory (Join-Path $testRoot 'release-artifact-unsafe-output') `
                    -Version '0.2.0-pre.2' `
                    -RepositoryPath $repoPath `
                    -ConsumerPath $consumerPath `
                    -UserProfilePath $profilePath
            } 'local path audit failed|absolute path' 'Unlisted local path was accepted in release artifacts.'
        }

        Invoke-Test 'Release entry points expose the documented orchestration contract' {
            foreach ($relativePath in @(
                '..\Invoke-AppUIPreTagValidation.ps1',
                '..\Invoke-AppUIGitInstallSmoke.ps1',
                '..\New-AppUIReleaseReport.ps1',
                '..\New-AppUIReleaseArtifacts.ps1',
                '..\Test-AppUIReleaseReadiness.ps1'
            )) {
                Assert-True (Test-Path -LiteralPath (Join-Path $PSScriptRoot $relativePath) -PathType Leaf) "Release entry point is missing: $relativePath"
            }

            $preTagText = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Invoke-AppUIPreTagValidation.ps1') -Raw -Encoding UTF8
            foreach ($token in @(
                'StaticPolicy',
                'Snapshot',
                'ImportBasicIntegration',
                'CreateFixturesAndGenerateBindings',
                'BindAndValidate',
                'BuildMono',
                'BuildIl2Cpp',
                'New-AppUIReleaseReport',
                'Test-AppUIBuildEnvironment',
                'build-environment.json',
                'New-AppUISanitizedLogArchive'
            )) {
                Assert-True ($preTagText.Contains($token)) "Pre-tag entry point is missing contract token: $token"
            }

            $gitSmokeText = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Invoke-AppUIGitInstallSmoke.ps1') -Raw -Encoding UTF8
            Assert-True ($gitSmokeText -match '40-character|SemVer') 'Git smoke does not document immutable refs.'
            Assert-True ($gitSmokeText.Contains('ExpectedPackageVersion')) 'Git smoke public ExpectedPackageVersion parameter is missing.'
            Assert-True ($gitSmokeText.Contains('commit-git-install-smoke.json')) 'Commit smoke output is missing.'
            Assert-True ($gitSmokeText.Contains('tag-git-install-smoke.json')) 'Tag smoke output is missing.'
            Assert-True ($gitSmokeText.Contains('Resolve-AppUIRemoteTagIdentity')) 'Tag smoke is not bound to the remote Tag identity.'
        }

        Invoke-Test 'Release readiness distinguishes pushed candidate and occupied tag' {
            $repository = Join-Path $testRoot 'readiness-repository'
            $commit = New-SnapshotTestRepository $repository
            $tree = Invoke-TestGit $repository rev-parse "$commit^{tree}"
            $remote = Join-Path $testRoot 'readiness-remote.git'
            Invoke-TestGit $testRoot init --bare $remote | Out-Null
            Invoke-TestGit $repository remote add origin $remote | Out-Null

            $notPushed = Test-AppUIReleaseReadiness `
                -RepositoryPath $repository `
                -CandidateCommit $commit `
                -PlannedTag 'v9.8.7-test.1'
            Assert-Equal 'NotPushed' $notPushed.Status 'Unpushed candidate readiness status was wrong.'
            Assert-Equal $tree $notPushed.CandidateTree 'Candidate tree was wrong.'

            Invoke-TestGit $repository push --quiet origin "$commit`:refs/heads/main" | Out-Null
            $ready = Test-AppUIReleaseReadiness `
                -RepositoryPath $repository `
                -CandidateCommit $commit `
                -PlannedTag 'v9.8.7-test.1'
            Assert-Equal 'ReadyForTag' $ready.Status 'Pushed candidate was not ready for Tag.'
            Assert-True (-not $ready.TagExists) 'Unused Tag was reported as occupied.'

            Invoke-TestGit $repository tag v9.8.7-test.1 $commit | Out-Null
            $localTag = Test-AppUIReleaseReadiness `
                -RepositoryPath $repository `
                -CandidateCommit $commit `
                -PlannedTag 'v9.8.7-test.1'
            Assert-Equal 'LocalTagExists' $localTag.Status 'Local-only Tag readiness status was wrong.'

            Invoke-TestGit $repository push --quiet origin refs/tags/v9.8.7-test.1 | Out-Null
            $occupied = Test-AppUIReleaseReadiness `
                -RepositoryPath $repository `
                -CandidateCommit $commit `
                -PlannedTag 'v9.8.7-test.1'
            Assert-Equal 'TagExists' $occupied.Status 'Occupied Tag readiness status was wrong.'
            Assert-Equal $commit $occupied.TagCommit 'Occupied Tag commit was wrong.'

            Set-Utf8NoBomContent (Join-Path $repository 'different.txt') 'different commit'
            Invoke-TestGit $repository add -- different.txt | Out-Null
            Invoke-TestGit $repository commit -m 'Different tag target' | Out-Null
            $differentCommit = Invoke-TestGit $repository rev-parse HEAD
            Invoke-TestGit $repository tag -f v9.8.7-test.1 $differentCommit | Out-Null
            Invoke-TestGit $repository push --quiet --force origin refs/tags/v9.8.7-test.1 | Out-Null
            $conflict = Test-AppUIReleaseReadiness `
                -RepositoryPath $repository `
                -CandidateCommit $commit `
                -PlannedTag 'v9.8.7-test.1'
            Assert-Equal 'TagConflict' $conflict.Status 'Conflicting remote Tag was not detected.'
            Assert-Equal $differentCommit $conflict.TagCommit 'Conflicting Tag commit was wrong.'

            Invoke-TestGit $repository tag v9.8.7-test.2 $differentCommit | Out-Null
            Invoke-TestGit $repository push --quiet origin refs/tags/v9.8.7-test.2 | Out-Null
            Assert-Throws {
                Test-AppUIReleaseReadiness `
                    -RepositoryPath $repository `
                    -CandidateCommit $commit `
                    -PlannedTag 'v9.8.7-test.2'
            } 'planned tag mismatch' 'Readiness accepted a Tag whose version did not match package.json.'

            $unavailableRepository = Join-Path $testRoot 'readiness-unavailable'
            $unavailableCommit = New-SnapshotTestRepository $unavailableRepository
            Invoke-TestGit $unavailableRepository remote add origin (Join-Path $testRoot 'missing-remote.git') | Out-Null
            $unavailable = Test-AppUIReleaseReadiness `
                -RepositoryPath $unavailableRepository `
                -CandidateCommit $unavailableCommit `
                -PlannedTag 'v9.8.7-test.1'
            Assert-Equal 'Blocked' $unavailable.Status 'Unavailable remote was reported as a release state.'
            Assert-Equal 'RemoteUnavailable' $unavailable.Reason 'Unavailable remote block reason was wrong.'
        }
    }

    if (Test-GroupRequested 'Docs') {
        Invoke-Test 'Public docs match release tools and planned package version' {
            $repositoryRoot = [System.IO.Path]::GetFullPath(
                (Join-Path $PSScriptRoot '..\..\..'))
            $package = Get-Content -LiteralPath (Join-Path $repositoryRoot 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            Assert-Equal '0.2.0-pre.2' $package.version 'Planned package version drifted.'
            Assert-Equal '6000.0' $package.unity 'Official Unity target drifted.'
            Assert-Equal 1 @($package.dependencies.PSObject.Properties).Count 'Package gained an undeclared dependency.'
            Assert-Equal '2.0.0' $package.dependencies.'com.unity.ugui' 'UGUI dependency drifted.'

            foreach ($relativePath in @(
                'Tools~\Release\New-AppUICandidateSnapshot.ps1',
                'Tools~\Release\New-AppUIConsumerWorkspace.ps1',
                'Tools~\Release\Invoke-AppUIPreTagValidation.ps1',
                'Tools~\Release\Invoke-AppUIGitInstallSmoke.ps1',
                'Tools~\Release\New-AppUIReleaseReport.ps1',
                'Tools~\Release\New-AppUIReleaseArtifacts.ps1',
                'Tools~\Release\Test-AppUIReleaseReadiness.ps1',
                'Validation~\Unity6000.0Consumer\README.md'
            )) {
                Assert-True (Test-Path -LiteralPath (Join-Path $repositoryRoot $relativePath) -PathType Leaf) "Documented release file is missing: $relativePath"
            }

            $editorSources = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'Validation~\Unity6000.0Consumer\Assets\AppUIConsumer\Editor') -Filter '*.cs' -File | ForEach-Object {
                Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
            }) -join "`n"
            foreach ($method in @(
                'AppUIConsumerFixtureCommand',
                'ImportBasicIntegration',
                'CreateFixturesAndGenerateBindings',
                'AppUIConsumerBindingCommand',
                'BindAndValidate',
                'AppUIConsumerBuildCommand',
                'BuildMono',
                'BuildIl2Cpp',
                'AppUIConsumerSmokeCommand'
            )) {
                Assert-True ($editorSources.Contains($method)) "Documented executeMethod target is missing: $method"
            }

            $readme = Get-Content -LiteralPath (Join-Path $repositoryRoot 'README.md') -Raw -Encoding UTF8
            Assert-True ($readme.Contains('https://github.com/TechJoiH/JoiH-AppUI.git#v0.2.0-pre.2')) 'README does not show the planned immutable tag URL.'
            Assert-True ($readme.Contains('Planned tag; install only after it appears on the GitHub Release page.')) 'README does not warn that the planned tag is unavailable before release.'
            Assert-True ($readme -notmatch 'git#main|\.git#main') 'README recommends main as a production install.'
            Assert-True ($readme.Contains('Historical Development Evidence')) 'README does not separate historical candidate evidence.'

            $validation = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Documentation~\validation.md') -Raw -Encoding UTF8
            foreach ($evidenceBoundary in @(
                'Historical Development Evidence',
                'Current Candidate Evidence',
                'Blocked/MissingToolchain'
            )) {
                Assert-True ($validation.Contains($evidenceBoundary)) "Validation docs blur current and historical evidence: $evidenceBoundary"
            }
            Assert-True ($validation -match '(?s)Current Candidate Evidence.*Package resolve.*IL2CPP.*NotRun') 'Validation docs do not mark current candidate Consumer gates NotRun.'

            $implementationPlan = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Documentation~\superpowers\plans\2026-08-12-single-official-unity-line-implementation.md') -Raw -Encoding UTF8
            foreach ($obsoleteName in @('Read-AppUINUnitResult', 'Protect-AppUIReleaseArtifact')) {
                Assert-True (-not $implementationPlan.Contains($obsoleteName)) "Implementation plan retains an obsolete release API: $obsoleteName"
            }
            foreach ($currentName in @(
                'Invoke-AppUIGitRemoteText',
                'Read-AppUINUnit3Result',
                'Protect-AppUILog',
                'New-AppUIReleaseArtifacts.ps1',
                'Test-AppUIReleaseReadiness.ps1',
                'Blocked/MissingToolchain'
            )) {
                Assert-True ($implementationPlan.Contains($currentName)) "Implementation plan is missing current release contract: $currentName"
            }
            Assert-True ($validation.Contains('Blocked/RemoteUnavailable')) 'Validation docs do not distinguish unavailable remote state.'
            Assert-True ($validation.Contains('Blocked/Timeout')) 'Validation docs do not distinguish remote timeout state.'
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
