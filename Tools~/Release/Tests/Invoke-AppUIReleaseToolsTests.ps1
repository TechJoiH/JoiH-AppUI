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
    Set-Utf8NoBomContent (Join-Path $Path 'commit-git-install-smoke.json') '{"initialized":true,"openPassed":true,"closePassed":true}'
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

    if (Test-GroupRequested 'Orchestration') {
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
            Remove-Item -LiteralPath (Join-Path $formalEvidence 'commit-git-install-smoke.json') -Force
            $formalWithoutSmoke = New-AppUIReleaseReport `
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
            Assert-Equal 'Blocked' $formalWithoutSmoke.status 'Formal report passed without Commit/Tag smoke.'
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
            Assert-Throws {
                Test-AppUIArtifactSecrets -Path $artifactRoot -ThrowOnSecret
            } 'secret|Authorization|github_pat' 'Secret-bearing artifact was accepted.'

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
        }

        Invoke-Test 'Release entry points expose the documented orchestration contract' {
            foreach ($relativePath in @(
                '..\Invoke-AppUIPreTagValidation.ps1',
                '..\Invoke-AppUIGitInstallSmoke.ps1',
                '..\New-AppUIReleaseReport.ps1'
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
