[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script:Passed = 0
$script:Failed = 0
$script:JunctionPaths = New-Object 'System.Collections.Generic.List[string]'

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param(
        [AllowNull()]$Expected,
        [AllowNull()]$Actual,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Expected -ne $Actual) {
        throw ("{0} Expected <{1}> but found <{2}>." -f $Message, $Expected, $Actual)
    }
}

function Invoke-Test {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Body
    )

    try {
        & $Body
        $script:Passed++
        Write-Host ("PASS {0}" -f $Name)
    }
    catch {
        $script:Failed++
        Write-Host ("FAIL {0}: {1}" -f $Name, $_.Exception.Message)
    }
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function New-OrdinaryFolder {
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $path = Join-Path $RunRoot $Name
    [System.IO.Directory]::CreateDirectory($path) | Out-Null
    return $path
}

function New-UnityFixture {
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$AppUIReference = '',
        [bool]$IncludeUnityVersion = $true,
        [hashtable]$ExtraDependencies = @{}
    )

    $root = Join-Path $RunRoot $Name
    [System.IO.Directory]::CreateDirectory((Join-Path $root 'Assets')) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $root 'Packages')) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $root 'ProjectSettings')) | Out-Null

    if ($IncludeUnityVersion) {
        Write-Utf8File -Path (Join-Path $root 'ProjectSettings\ProjectVersion.txt') `
            -Content "m_EditorVersion: 6000.0.25f1`nm_EditorVersionWithRevision: 6000.0.25f1 (fixture)"
    }

    $dependencies = [ordered]@{
        'com.unity.ugui' = '2.0.0'
    }
    if (-not [string]::IsNullOrWhiteSpace($AppUIReference)) {
        $dependencies['com.joih.appui'] = $AppUIReference
    }
    foreach ($name in $ExtraDependencies.Keys) {
        $dependencies[$name] = [string]$ExtraDependencies[$name]
    }

    $manifest = [ordered]@{
        dependencies = $dependencies
    } | ConvertTo-Json -Depth 10
    Write-Utf8File -Path (Join-Path $root 'Packages\manifest.json') -Content $manifest

    $lockDependencies = [ordered]@{
        'com.unity.ugui' = [ordered]@{
            version = '2.0.0'
            depth = 0
            source = 'registry'
            dependencies = [ordered]@{}
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($AppUIReference)) {
        $source = if ($AppUIReference -match '^(?:https?|ssh|git\+|git@)' -or $AppUIReference -match '\.git(?:[?#]|$)') { 'git' } else { 'registry' }
        $lockDependencies['com.joih.appui'] = [ordered]@{
            version = $AppUIReference
            depth = 0
            source = $source
            dependencies = [ordered]@{
                'com.unity.ugui' = '2.0.0'
            }
        }
    }
    foreach ($name in $ExtraDependencies.Keys) {
        $value = [string]$ExtraDependencies[$name]
        $source = if ($value -match '^(?:https?|ssh|git\+|git@)' -or $value -match '\.git(?:[?#]|$)') { 'git' } else { 'registry' }
        $lockDependencies[$name] = [ordered]@{
            version = $value
            depth = 0
            source = $source
            dependencies = [ordered]@{}
        }
    }

    $lock = [ordered]@{
        dependencies = $lockDependencies
    } | ConvertTo-Json -Depth 12
    Write-Utf8File -Path (Join-Path $root 'Packages\packages-lock.json') -Content $lock
    return $root
}

function Add-SourceFile {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    Write-Utf8File -Path (Join-Path $Root $RelativePath) -Content $Content
}

function Add-AppUIHostAndPorts {
    param([Parameter(Mandatory = $true)][string]$Root)

    Add-SourceFile -Root $Root -RelativePath 'Assets\AppUI\Runtime\ProjectAppUIHost.cs' -Content @'
using Joi.H.AppUI;
public sealed class ProjectOperationFactory : IUIOperationFactory { }
public sealed class ProjectAssetProvider : IUIAssetProvider { }
public sealed class ProjectExecutionContext : IAppUIExecutionContext { }
public sealed class ProjectAppUIInstaller {
    private AppUIManager manager;
    private AppUIRuntimeHost runtimeHost;
}
'@
}

function Add-RuntimeRoot {
    param([Parameter(Mandatory = $true)][string]$Root)

    Add-SourceFile -Root $Root -RelativePath 'Assets\AppUI\Runtime\UILayerComposition.cs' -Content @'
using Joi.H.AppUI;
public sealed class UILayerComposition {
    private UILayerRoot layerRoot;
}
'@
    Write-Utf8File -Path (Join-Path $Root 'Assets\AppUI\Settings\MainAppUIRuntimeProfile.asset') -Content @'
--- !u!114 &11400000
MonoBehaviour:
  m_Name: MainAppUIRuntimeProfile
  m_ScriptType: AppUIRuntimeProfile
'@
    Write-Utf8File -Path (Join-Path $Root 'Assets\AppUI\Settings\MainUIPageDefinitionRegistry.asset') -Content @'
--- !u!114 &11400000
MonoBehaviour:
  m_Name: MainUIPageDefinitionRegistry
  m_ScriptType: UIPageDefinitionRegistry
'@
}

function Add-PageContract {
    param([Parameter(Mandatory = $true)][string]$Root)

    Add-SourceFile -Root $Root -RelativePath 'Assets\AppUI\Pages\SettingsPanelController.cs' -Content @'
using Joi.H.AppUI;
public partial class SettingsPanelController : PanelBaseController { }
'@
    Write-Utf8File -Path (Join-Path $Root 'Assets\AppUI\Pages\SettingsPageDefinition.asset') -Content @'
--- !u!114 &11400000
MonoBehaviour:
  m_Name: SettingsPageDefinition
  m_ScriptType: UIPageDefinition
  pageId: settings
'@
    Write-Utf8File -Path (Join-Path $Root 'Assets\AppUI\Settings\MainUIBindingSettings.asset') -Content @'
--- !u!114 &11400000
MonoBehaviour:
  m_Name: MainUIBindingSettings
  m_ScriptType: UIBindingSettings
'@
}

function Add-GeneratedBinding {
    param([Parameter(Mandatory = $true)][string]$Root)

    Add-SourceFile -Root $Root -RelativePath 'Assets\AppUI\Pages\SettingsPanelController.Bindings.cs' -Content @'
public partial class SettingsPanelController {
    private UnityEngine.UI.Button B_Close;
}
'@
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Add-ValidationEvidence {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][ValidateSet('Binding', 'Runtime')][string]$Kind,
        [Parameter(Mandatory = $true)][ValidateSet('Passed', 'Failed', 'Pending')][string]$Status,
        [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$HostBoundariesStatus = 'Passed',
        [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$RuntimeRootStatus = 'Passed',
        [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$PageContractStatus = 'Passed',
        [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$BindingStatus = 'Passed',
        [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$RuntimeLifecycleStatus = 'Passed',
        [string]$SchemaVersion = 'joih-appui-project-validation-evidence.v1',
        [string]$Producer = 'Joi.H.AppUI.Editor.ProjectValidation'
    )

    $name = if ($Kind -eq 'Binding') { 'AppUIBindingValidationEvidence.asset' } else { 'AppUIRuntimeValidationEvidence.asset' }
    $manifestHash = Get-FileSha256 -Path (Join-Path $Root 'Packages\manifest.json')
    $lockHash = Get-FileSha256 -Path (Join-Path $Root 'Packages\packages-lock.json')
    $projectVersionHash = Get-FileSha256 -Path (Join-Path $Root 'ProjectSettings\ProjectVersion.txt')
    Write-Utf8File -Path (Join-Path $Root (Join-Path 'Assets\AppUI\Validation' $name)) -Content @"
--- !u!114 &11400000
MonoBehaviour:
  m_Name: $Kind Validation
  schemaVersion: $SchemaVersion
  producer: $Producer
  validationKind: $Kind
  status: $Status
  unityVersion: 6000.0.25f1
  manifestSha256: $manifestHash
  packagesLockSha256: $lockHash
  projectVersionSha256: $projectVersionHash
  hostBoundariesStatus: $HostBoundariesStatus
  runtimeRootStatus: $RuntimeRootStatus
  pageContractStatus: $PageContractStatus
  bindingStatus: $BindingStatus
  runtimeLifecycleStatus: $RuntimeLifecycleStatus
"@
}

function Invoke-Inspection {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [string]$OutputPath = '',
        [int]$MaxSourceFiles = 2000
    )

    $arguments = @{
        ProjectPath = $ProjectPath
        MaxSourceFiles = $MaxSourceFiles
    }
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $arguments.OutputPath = $OutputPath
    }

    $json = & $script:InspectorPath @arguments
    Assert-True -Condition (-not [string]::IsNullOrWhiteSpace(($json -join ''))) `
        -Message 'Inspector emitted no JSON.'
    return (($json -join [Environment]::NewLine) | ConvertFrom-Json)
}

function Assert-Status {
    param(
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)][string]$ProjectPath
    )

    $result = Invoke-Inspection -ProjectPath $ProjectPath
    Assert-Equal -Expected 'joih-appui-project-inspection.v1' -Actual $result.schemaVersion `
        -Message 'Schema version changed.'
    Assert-Equal -Expected $Expected -Actual $result.status -Message 'Unexpected integration status.'
    Assert-True -Condition ($null -ne $result.project) -Message 'Project facts are missing.'
    Assert-True -Condition ($null -ne $result.packages) -Message 'Package facts are missing.'
    Assert-True -Condition ($null -ne $result.integration) -Message 'Integration facts are missing.'
    Assert-True -Condition ($null -ne $result.samples) -Message 'Sample facts are missing.'
    Assert-True -Condition ($null -ne $result.issues) -Message 'Issues collection is missing.'
}

$skillRoot = Split-Path -Parent $PSScriptRoot
$script:InspectorPath = Join-Path $skillRoot 'scripts\inspect-appui-project.ps1'

if (-not (Test-Path -LiteralPath $script:InspectorPath -PathType Leaf)) {
    Write-Host ("FAIL Inspector script exists: missing {0}" -f $script:InspectorPath)
    Write-Host 'RESULT Passed=0 Failed=1'
    exit 1
}

$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
$runRoot = Join-Path $tempRoot ("integrating-appui-inspector-tests-{0}" -f [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null

try {
    $officialTag = 'https://github.com/TechJoiH/JoiH-AppUI.git#v0.4.0-pre.1'
    $ordinaryFolder = New-OrdinaryFolder -RunRoot $runRoot -Name 'ordinary'
    $unityVersionUnknown = New-UnityFixture -RunRoot $runRoot -Name 'unity-version-unknown' -IncludeUnityVersion $false
    $unityWithoutAppUI = New-UnityFixture -RunRoot $runRoot -Name 'unity-without-appui'
    $appUIManifestOnly = New-UnityFixture -RunRoot $runRoot -Name 'appui-manifest-only' -AppUIReference $officialTag

    $runtimeHostWithoutPorts = New-UnityFixture -RunRoot $runRoot -Name 'host-without-ports' -AppUIReference $officialTag
    Add-SourceFile -Root $runtimeHostWithoutPorts -RelativePath 'Assets\AppUI\Runtime\Host.cs' -Content @'
using Joi.H.AppUI;
public sealed class HostBootstrap {
    private AppUIRuntimeHost runtimeHost;
}
'@

    $portsWithoutRuntimeRoot = New-UnityFixture -RunRoot $runRoot -Name 'ports-without-root' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $portsWithoutRuntimeRoot
    Add-ValidationEvidence -Root $portsWithoutRuntimeRoot -Kind Binding -Status Pending `
        -HostBoundariesStatus Passed -RuntimeRootStatus Pending -PageContractStatus NotRun `
        -BindingStatus NotRun -RuntimeLifecycleStatus NotRun

    $runtimeWithoutPageContract = New-UnityFixture -RunRoot $runRoot -Name 'runtime-without-page' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $runtimeWithoutPageContract
    Add-RuntimeRoot -Root $runtimeWithoutPageContract
    Add-ValidationEvidence -Root $runtimeWithoutPageContract -Kind Binding -Status Pending `
        -HostBoundariesStatus Passed -RuntimeRootStatus Passed -PageContractStatus Pending `
        -BindingStatus NotRun -RuntimeLifecycleStatus NotRun

    $pageWithoutBindings = New-UnityFixture -RunRoot $runRoot -Name 'page-without-bindings' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $pageWithoutBindings
    Add-RuntimeRoot -Root $pageWithoutBindings
    Add-PageContract -Root $pageWithoutBindings
    Add-ValidationEvidence -Root $pageWithoutBindings -Kind Binding -Status Pending `
        -HostBoundariesStatus Passed -RuntimeRootStatus Passed -PageContractStatus Passed `
        -BindingStatus Pending -RuntimeLifecycleStatus NotRun

    $bindingInvalid = New-UnityFixture -RunRoot $runRoot -Name 'binding-invalid' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $bindingInvalid
    Add-RuntimeRoot -Root $bindingInvalid
    Add-PageContract -Root $bindingInvalid
    Add-GeneratedBinding -Root $bindingInvalid
    Add-ValidationEvidence -Root $bindingInvalid -Kind Binding -Status Failed -BindingStatus Failed `
        -RuntimeLifecycleStatus NotRun

    $runtimeValidationPending = New-UnityFixture -RunRoot $runRoot -Name 'runtime-validation-pending' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $runtimeValidationPending
    Add-RuntimeRoot -Root $runtimeValidationPending
    Add-PageContract -Root $runtimeValidationPending
    Add-GeneratedBinding -Root $runtimeValidationPending
    Add-ValidationEvidence -Root $runtimeValidationPending -Kind Binding -Status Passed `
        -RuntimeLifecycleStatus NotRun
    Add-SourceFile -Root $runtimeValidationPending -RelativePath 'Assets\AppUI\Pages\StatusWords.cs' -Content @'
// These words are not validation evidence: BindingInvalid RuntimeValidationPending Ready Passed.
public static class StatusWords { }
'@

    $completeFixture = New-UnityFixture -RunRoot $runRoot -Name 'complete' -AppUIReference $officialTag `
        -ExtraDependencies @{
            'com.unity.textmeshpro' = '3.0.6'
            'com.unity.addressables' = '2.3.16'
            'com.cysharp.unitask' = 'https://github.com/Cysharp/UniTask.git#2.5.10'
        }
    Add-AppUIHostAndPorts -Root $completeFixture
    Add-RuntimeRoot -Root $completeFixture
    Add-PageContract -Root $completeFixture
    Add-GeneratedBinding -Root $completeFixture
    Add-ValidationEvidence -Root $completeFixture -Kind Binding -Status Passed
    Add-ValidationEvidence -Root $completeFixture -Kind Runtime -Status Passed
    Write-Utf8File -Path (Join-Path $completeFixture 'ProjectSettings\ProjectSettings.asset') -Content @'
PlayerSettings:
  scriptingDefineSymbols:
    Standalone: EXISTING_DEFINE;JOIH_APPUI_TMP
'@
    Add-SourceFile -Root $completeFixture `
        -RelativePath 'Assets\Samples\Joi.H AppUI\0.4.0-pre.1\Basic Integration\BasicSample.cs' `
        -Content 'public sealed class BasicSample { }'
    Add-SourceFile -Root $completeFixture `
        -RelativePath 'Assets\Samples\Joi.H AppUI\0.4.0-pre.1\Custom Host Integration\CustomSample.cs' `
        -Content 'public sealed class CustomSample { }'
    Add-SourceFile -Root $completeFixture `
        -RelativePath 'Assets\Samples\Joi.H AppUI\0.4.0-pre.1\TextMeshPro Integration\TMPSample.cs' `
        -Content 'public sealed class TMPSample { }'

    $envSentinel = 'APPUI_ENV_SENTINEL_8B7E55A9'
    Write-Utf8File -Path (Join-Path $completeFixture 'Assets\Config\.env') -Content ("TOKEN={0}" -f $envSentinel)

    $outsideRoot = Join-Path $runRoot 'outside-project-tree'
    [System.IO.Directory]::CreateDirectory($outsideRoot) | Out-Null
    $reparseSentinel = 'APPUI_REPARSE_SENTINEL_0C72E14D'
    Write-Utf8File -Path (Join-Path $outsideRoot 'EscapedAppUIRuntimeHost.cs') `
        -Content ("// {0}`npublic sealed class Escaped {{ AppUIRuntimeHost host; }}" -f $reparseSentinel)
    $script:JunctionPath = Join-Path $completeFixture 'Assets\LinkedOutside'
    New-Item -ItemType Junction -Path $script:JunctionPath -Target $outsideRoot | Out-Null
    $script:JunctionPaths.Add($script:JunctionPath) | Out-Null

    Invoke-Test -Name 'Status precedence covers every integration state' -Body {
        Assert-Status -Expected 'NotAUnityProject' -ProjectPath $ordinaryFolder
        Assert-Status -Expected 'UnityVersionUnverified' -ProjectPath $unityVersionUnknown
        Assert-Status -Expected 'AppUINotInstalled' -ProjectPath $unityWithoutAppUI
        Assert-Status -Expected 'InstalledNotInitialized' -ProjectPath $appUIManifestOnly
        Assert-Status -Expected 'HostBoundariesMissing' -ProjectPath $runtimeHostWithoutPorts
        Assert-Status -Expected 'RuntimeRootIncomplete' -ProjectPath $portsWithoutRuntimeRoot
        Assert-Status -Expected 'PageContractIncomplete' -ProjectPath $runtimeWithoutPageContract
        Assert-Status -Expected 'BindingGenerationPending' -ProjectPath $pageWithoutBindings
        Assert-Status -Expected 'BindingInvalid' -ProjectPath $bindingInvalid
        Assert-Status -Expected 'RuntimeValidationPending' -ProjectPath $runtimeValidationPending
        Assert-Status -Expected 'Ready' -ProjectPath $completeFixture
    }

    Invoke-Test -Name 'Unity root and package facts are exact' -Body {
        $nestedPath = Join-Path $completeFixture 'Assets\AppUI\Pages'
        $result = Invoke-Inspection -ProjectPath $nestedPath
        Assert-Equal -Expected ([System.IO.Path]::GetFullPath($completeFixture)) -Actual $result.project.root `
            -Message 'Unity root discovery failed.'
        Assert-Equal -Expected '6000.0.25f1' -Actual $result.project.unityVersion `
            -Message 'Unity version parsing failed.'
        Assert-True -Condition ([bool]$result.packages.ugui.installed) -Message 'UGUI package was not detected.'
        Assert-Equal -Expected '2.0.0' -Actual $result.packages.ugui.version -Message 'UGUI version is wrong.'
        Assert-True -Condition ([bool]$result.packages.textMeshPro.installed) -Message 'TMP package was not detected.'
        Assert-Equal -Expected '3.0.6' -Actual $result.packages.textMeshPro.version -Message 'TMP version is wrong.'
        Assert-True -Condition ($result.packages.asyncCandidates.name -contains 'com.cysharp.unitask') `
            -Message 'Async package candidate was not reported.'
        Assert-True -Condition ($result.packages.assetCandidates.name -contains 'com.unity.addressables') `
            -Message 'Asset package candidate was not reported.'
    }

    Invoke-Test -Name 'SemVer Git fragment is an unverified offline Tag candidate' -Body {
        $result = Invoke-Inspection -ProjectPath $appUIManifestOnly
        Assert-Equal -Expected $officialTag -Actual $result.packages.appUI.manifestReference `
            -Message 'Manifest AppUI reference changed.'
        Assert-Equal -Expected 'Git' -Actual $result.packages.appUI.installSource `
            -Message 'Git source was not classified.'
        Assert-Equal -Expected 'v0.4.0-pre.1' -Actual $result.packages.appUI.gitRef `
            -Message 'Git fragment was not parsed exactly.'
        Assert-Equal -Expected 'TagCandidate' -Actual $result.packages.appUI.gitRefKind `
            -Message 'SemVer fragment was not classified as an offline Tag candidate.'
        Assert-Equal -Expected '0.4.0-pre.1' -Actual $result.packages.appUI.version `
            -Message 'AppUI version was not derived from the exact Tag.'
        Assert-Equal -Expected $null -Actual $result.packages.appUI.mutable `
            -Message 'Offline Tag candidate claimed a definitive mutable/immutable value.'
        Assert-Equal -Expected 'UnverifiedOffline' -Actual $result.packages.appUI.immutability `
            -Message 'Offline Tag identity uncertainty was not explicit.'
        Assert-Equal -Expected $false -Actual ([bool]$result.packages.appUI.tagIdentityVerified) `
            -Message 'Offline parsing claimed remote Tag identity verification.'
        Assert-True -Condition ($result.issues.code -contains 'APPUI_TAG_IDENTITY_UNVERIFIED') `
            -Message 'Offline Tag identity issue is missing.'
    }

    Invoke-Test -Name 'Mutable and commit Git references are distinguished' -Body {
        $branchFixture = New-UnityFixture -RunRoot $runRoot -Name 'git-branch' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#main'
        $unversionedFixture = New-UnityFixture -RunRoot $runRoot -Name 'git-unversioned' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git'
        $commitFixture = New-UnityFixture -RunRoot $runRoot -Name 'git-commit' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#0123456789abcdef0123456789abcdef01234567'

        $branch = Invoke-Inspection -ProjectPath $branchFixture
        Assert-Equal -Expected 'main' -Actual $branch.packages.appUI.gitRef -Message 'Branch ref changed.'
        Assert-Equal -Expected 'Branch' -Actual $branch.packages.appUI.gitRefKind -Message 'main was not a branch.'
        Assert-Equal -Expected $true -Actual ([bool]$branch.packages.appUI.mutable) -Message 'Branch was not mutable.'
        Assert-Equal -Expected 'Mutable' -Actual $branch.packages.appUI.immutability `
            -Message 'Branch mutability was not explicit.'

        $unversioned = Invoke-Inspection -ProjectPath $unversionedFixture
        Assert-Equal -Expected $null -Actual $unversioned.packages.appUI.gitRef -Message 'Unversioned Git gained a ref.'
        Assert-Equal -Expected 'Unversioned' -Actual $unversioned.packages.appUI.gitRefKind `
            -Message 'Unversioned Git was misclassified.'
        Assert-Equal -Expected $true -Actual ([bool]$unversioned.packages.appUI.mutable) `
            -Message 'Unversioned Git was not mutable.'
        Assert-Equal -Expected 'Mutable' -Actual $unversioned.packages.appUI.immutability `
            -Message 'Unversioned Git mutability was not explicit.'

        $commit = Invoke-Inspection -ProjectPath $commitFixture
        Assert-Equal -Expected 'Commit' -Actual $commit.packages.appUI.gitRefKind `
            -Message 'Full Git SHA was not immutable.'
        Assert-Equal -Expected $false -Actual ([bool]$commit.packages.appUI.mutable) `
            -Message 'Full Git SHA was marked mutable.'
        Assert-Equal -Expected 'PinnedCommit' -Actual $commit.packages.appUI.immutability `
            -Message 'Full Git SHA pin was not explicit.'
    }

    Invoke-Test -Name 'Only exact SemVer 2.0 Git fragments are Tag candidates' -Body {
        $invalidNumeric = New-UnityFixture -RunRoot $runRoot -Name 'invalid-semver-leading-zero' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#v1.2.3-01'
        $invalidEmpty = New-UnityFixture -RunRoot $runRoot -Name 'invalid-semver-empty-identifier' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#v1.2.3-alpha..1'
        $validComplex = New-UnityFixture -RunRoot $runRoot -Name 'valid-semver-complex' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#v1.2.3-0A.0+build.01'

        $invalidNumericResult = Invoke-Inspection -ProjectPath $invalidNumeric
        Assert-Equal -Expected 'Branch' -Actual $invalidNumericResult.packages.appUI.gitRefKind `
            -Message 'SemVer numeric identifier with a leading zero was accepted.'
        $invalidEmptyResult = Invoke-Inspection -ProjectPath $invalidEmpty
        Assert-Equal -Expected 'Branch' -Actual $invalidEmptyResult.packages.appUI.gitRefKind `
            -Message 'SemVer with an empty prerelease identifier was accepted.'
        $validComplexResult = Invoke-Inspection -ProjectPath $validComplex
        Assert-Equal -Expected 'TagCandidate' -Actual $validComplexResult.packages.appUI.gitRefKind `
            -Message 'Valid SemVer 2.0 fragment was rejected.'
        Assert-Equal -Expected '1.2.3-0A.0+build.01' -Actual $validComplexResult.packages.appUI.version `
            -Message 'Valid complex SemVer version changed.'
    }

    Invoke-Test -Name 'Package URI facts redact userinfo and sensitive query values' -Body {
        $credentialReference = 'https://alice:USERINFO_SECRET@github.com/TechJoiH/JoiH-AppUI.git?path=/package&token=QUERY_SECRET&api_key=KEY_SECRET#v0.4.0-pre.1'
        $credentialFixture = New-UnityFixture -RunRoot $runRoot -Name 'credential-uri' `
            -AppUIReference $credentialReference

        $raw = & $script:InspectorPath -ProjectPath $credentialFixture
        $json = $raw -join [Environment]::NewLine
        Assert-True -Condition (-not $json.Contains('alice')) -Message 'URI username leaked into raw JSON.'
        Assert-True -Condition (-not $json.Contains('USERINFO_SECRET')) -Message 'URI password leaked into raw JSON.'
        Assert-True -Condition (-not $json.Contains('QUERY_SECRET')) -Message 'Sensitive token query value leaked.'
        Assert-True -Condition (-not $json.Contains('KEY_SECRET')) -Message 'Sensitive API key query value leaked.'

        $result = $json | ConvertFrom-Json
        $sanitized = 'https://github.com/TechJoiH/JoiH-AppUI.git?path=/package&token=<redacted>&api_key=<redacted>#v0.4.0-pre.1'
        Assert-Equal -Expected $sanitized -Actual $result.packages.appUI.manifestReference `
            -Message 'Manifest reference was not safely redacted.'
        Assert-Equal -Expected $sanitized -Actual $result.packages.appUI.lockReference `
            -Message 'Lock reference was not safely redacted.'
    }

    Invoke-Test -Name 'Defines, Samples and likely integration candidates are reported' -Body {
        $result = Invoke-Inspection -ProjectPath $completeFixture
        Assert-True -Condition ($result.integration.defines -contains 'JOIH_APPUI_TMP') `
            -Message 'JOIH_APPUI_TMP define was not reported.'
        Assert-True -Condition ([bool]$result.integration.textMeshProDefineEnabled) `
            -Message 'TMP opt-in define was not enabled.'
        Assert-True -Condition ([bool]$result.integration.hostBoundaries.operationFactory.present) `
            -Message 'IUIOperationFactory candidate was not found.'
        Assert-True -Condition ([bool]$result.integration.hostBoundaries.assetProvider.present) `
            -Message 'IUIAssetProvider candidate was not found.'
        Assert-True -Condition ([bool]$result.integration.hostBoundaries.executionContext.present) `
            -Message 'IAppUIExecutionContext candidate was not found.'
        Assert-True -Condition ([bool]$result.samples.basicIntegration.imported) `
            -Message 'Basic Integration Sample was not detected.'
        Assert-True -Condition ([bool]$result.samples.customHostIntegration.imported) `
            -Message 'Custom Host Integration Sample was not detected.'
        Assert-True -Condition ([bool]$result.samples.textMeshProIntegration.imported) `
            -Message 'TextMeshPro Integration Sample was not detected.'
        Assert-True -Condition ($result.samples.basicIntegration.paths -contains `
            'Assets/Samples/Joi.H AppUI/0.4.0-pre.1/Basic Integration') `
            -Message 'Imported Sample path was not project-relative.'
    }

    Invoke-Test -Name 'An asmdef constraint does not enable the project TMP define' -Body {
        $constraintOnly = New-UnityFixture -RunRoot $runRoot -Name 'asmdef-constraint-only' `
            -AppUIReference $officialTag
        Add-SourceFile -Root $constraintOnly -RelativePath 'Assets\OptionalTMP.asmdef' -Content @'
{
  "name": "OptionalTMP",
  "defineConstraints": [ "JOIH_APPUI_TMP" ]
}
'@

        $result = Invoke-Inspection -ProjectPath $constraintOnly
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.textMeshProDefineEnabled) `
            -Message 'An asmdef consumer constraint was mistaken for an enabled scripting define.'
        Assert-True -Condition ($result.integration.asmdefDefineConstraints -contains 'JOIH_APPUI_TMP') `
            -Message 'Asmdef define constraints were not reported separately.'
    }

    Invoke-Test -Name 'Imported Sample code does not prove project initialization' -Body {
        $sampleOnly = New-UnityFixture -RunRoot $runRoot -Name 'imported-sample-only' `
            -AppUIReference $officialTag
        $sampleRoot = 'Assets\Samples\Joi.H AppUI\0.4.0-pre.1\Basic Integration'
        Add-SourceFile -Root $sampleOnly -RelativePath (Join-Path $sampleRoot 'SampleAppUIInstaller.cs') -Content @'
public sealed class SampleOperationFactory : IUIOperationFactory { }
public sealed class SampleAssetProvider : IUIAssetProvider { }
public sealed class SampleExecutionContext : IAppUIExecutionContext { }
public sealed class SamplePanelController : PanelBaseController {
    private AppUIRuntimeHost runtimeHost;
    private AppUIManager manager;
    private UILayerRoot layerRoot;
}
'@
        Add-SourceFile -Root $sampleOnly -RelativePath (Join-Path $sampleRoot 'SamplePanelController.Bindings.cs') `
            -Content 'public partial class SamplePanelController { }'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'SampleAppUIRuntimeProfile.asset')) `
            -Content 'm_ScriptType: AppUIRuntimeProfile'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'SampleUIPageDefinitionRegistry.asset')) `
            -Content 'm_ScriptType: UIPageDefinitionRegistry'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'SampleUIPageDefinition.asset')) `
            -Content 'm_ScriptType: UIPageDefinition'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'SampleUIBindingSettings.asset')) `
            -Content 'm_ScriptType: UIBindingSettings'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'AppUIBindingValidationReport.asset')) `
            -Content 'status: Passed'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'AppUIRuntimeValidationReport.asset')) `
            -Content 'status: Passed'

        $result = Invoke-Inspection -ProjectPath $sampleOnly
        Assert-Equal -Expected 'InstalledNotInitialized' -Actual $result.status `
            -Message 'Merely imported Sample files were treated as a project-owned Runtime.'
        Assert-Equal -Expected $true -Actual ([bool]$result.samples.basicIntegration.imported) `
            -Message 'The imported Sample itself was not reported.'
    }

    Invoke-Test -Name 'Heuristic filenames regexes and comments never prove completion' -Body {
        $heuristicOnly = New-UnityFixture -RunRoot $runRoot -Name 'heuristic-only' -AppUIReference $officialTag
        Add-SourceFile -Root $heuristicOnly -RelativePath 'Assets\AppUI\HeuristicOnly.cs' -Content @'
// public sealed class CommentOperationFactory : IUIOperationFactory { }
// public sealed class CommentAssetProvider : IUIAssetProvider { }
// public sealed class CommentExecutionContext : IAppUIExecutionContext { }
// public sealed class CommentPanelController : PanelBaseController { }
// AppUIRuntimeHost AppUIManager UILayerRoot
'@
        Write-Utf8File -Path (Join-Path $heuristicOnly 'Assets\AppUI\MainAppUIRuntimeProfile.asset') `
            -Content 'm_Name: filename-only'
        Write-Utf8File -Path (Join-Path $heuristicOnly 'Assets\AppUI\MainUIPageDefinitionRegistry.asset') `
            -Content 'm_Name: filename-only'
        Write-Utf8File -Path (Join-Path $heuristicOnly 'Assets\AppUI\SettingsPageDefinition.asset') `
            -Content 'm_Name: filename-only'
        Write-Utf8File -Path (Join-Path $heuristicOnly 'Assets\AppUI\MainUIBindingSettings.asset') `
            -Content 'm_Name: filename-only'
        Add-SourceFile -Root $heuristicOnly -RelativePath 'Assets\AppUI\CommentPanelController.Bindings.cs' `
            -Content '// filename-only generated-binding candidate'

        $result = Invoke-Inspection -ProjectPath $heuristicOnly
        Assert-True -Condition ([bool]$result.integration.hostBoundaries.candidateCoverageComplete) `
            -Message 'Heuristic host-boundary coverage was not reported as a candidate fact.'
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.hostBoundaries.complete) `
            -Message 'Heuristic host-boundary text was treated as verified completion.'
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.runtimeRoot.complete) `
            -Message 'Heuristic Runtime-root filenames were treated as verified completion.'
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.pageContract.complete) `
            -Message 'Heuristic page-contract names were treated as verified completion.'
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.binding.generationComplete) `
            -Message 'A .Bindings.cs filename was treated as verified generation.'
        Assert-True -Condition ($result.status -ne 'Ready') -Message 'Heuristic candidates produced Ready.'
        Assert-True -Condition (-not ($result.integration.hostBoundaries.operationFactory.candidates.confidence -contains 'Verified')) `
            -Message 'Text-search candidate confidence claimed verification.'
    }

    Invoke-Test -Name 'Validation states require explicit evidence' -Body {
        $pending = Invoke-Inspection -ProjectPath $runtimeValidationPending
        Assert-Equal -Expected 'Passed' -Actual $pending.integration.validation.binding.status `
            -Message 'Bound Binding validation evidence was ignored.'
        Assert-Equal -Expected 'Unknown' -Actual $pending.integration.validation.runtime.status `
            -Message 'Source text was treated as Runtime validation evidence.'

        $invalid = Invoke-Inspection -ProjectPath $bindingInvalid
        Assert-Equal -Expected 'Failed' -Actual $invalid.integration.validation.binding.status `
            -Message 'Explicit failed Binding evidence was ignored.'

        $ready = Invoke-Inspection -ProjectPath $completeFixture
        Assert-Equal -Expected 'Passed' -Actual $ready.integration.validation.binding.status `
            -Message 'Explicit Binding pass evidence was ignored.'
        Assert-Equal -Expected 'Passed' -Actual $ready.integration.validation.runtime.status `
            -Message 'Explicit Runtime pass evidence was ignored.'
    }

    Invoke-Test -Name 'Validation evidence is exact produced and bound to current project facts' -Body {
        $ready = Invoke-Inspection -ProjectPath $completeFixture
        Assert-Equal -Expected 'joih-appui-project-validation-evidence.v1' `
            -Actual $ready.integration.validation.contract.schemaVersion `
            -Message 'Validation evidence schema contract was not reported.'
        Assert-Equal -Expected 'Joi.H.AppUI.Editor.ProjectValidation' `
            -Actual $ready.integration.validation.contract.producer `
            -Message 'Validation evidence producer contract was not reported.'
        Assert-Equal -Expected 'Bound' -Actual $ready.integration.validation.binding.evidence[0].binding `
            -Message 'Valid Binding evidence was not bound to current facts.'

        $handAuthored = New-UnityFixture -RunRoot $runRoot -Name 'hand-authored-evidence' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $handAuthored
        Add-RuntimeRoot -Root $handAuthored
        Add-PageContract -Root $handAuthored
        Add-GeneratedBinding -Root $handAuthored
        Write-Utf8File -Path (Join-Path $handAuthored 'Assets\AppUI\Validation\AppUIBindingValidationEvidence.asset') `
            -Content 'status: Passed'
        Write-Utf8File -Path (Join-Path $handAuthored 'Assets\AppUI\Validation\AppUIRuntimeValidationEvidence.asset') `
            -Content 'status: Passed'
        $handResult = Invoke-Inspection -ProjectPath $handAuthored
        Assert-True -Condition ($handResult.status -ne 'Ready') `
            -Message 'Hand-authored status text produced Ready.'
        Assert-Equal -Expected 'Unknown' -Actual $handResult.integration.validation.binding.status `
            -Message 'Hand-authored Binding status was trusted.'
        Assert-True -Condition ($handResult.integration.validation.binding.rejectedEvidence.reason -contains 'SchemaMismatch') `
            -Message 'Schema-less status text was not rejected explicitly.'

        $wrongProducer = New-UnityFixture -RunRoot $runRoot -Name 'wrong-producer-evidence' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $wrongProducer
        Add-RuntimeRoot -Root $wrongProducer
        Add-PageContract -Root $wrongProducer
        Add-GeneratedBinding -Root $wrongProducer
        Add-ValidationEvidence -Root $wrongProducer -Kind Binding -Status Passed -Producer 'Hand.Authored.Status'
        Add-ValidationEvidence -Root $wrongProducer -Kind Runtime -Status Passed -Producer 'Hand.Authored.Status'
        $wrongProducerResult = Invoke-Inspection -ProjectPath $wrongProducer
        Assert-True -Condition ($wrongProducerResult.status -ne 'Ready') `
            -Message 'Unrecognized evidence producer produced Ready.'
        Assert-True -Condition ($wrongProducerResult.integration.validation.binding.rejectedEvidence.reason -contains 'ProducerMismatch') `
            -Message 'Unrecognized producer was not rejected explicitly.'

        $stale = New-UnityFixture -RunRoot $runRoot -Name 'stale-evidence' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $stale
        Add-RuntimeRoot -Root $stale
        Add-PageContract -Root $stale
        Add-GeneratedBinding -Root $stale
        Add-ValidationEvidence -Root $stale -Kind Binding -Status Passed
        Add-ValidationEvidence -Root $stale -Kind Runtime -Status Passed
        $manifestPath = Join-Path $stale 'Packages\manifest.json'
        $manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
        $manifest.dependencies | Add-Member -NotePropertyName 'com.example.changed' -NotePropertyValue '1.0.0'
        Write-Utf8File -Path $manifestPath -Content ($manifest | ConvertTo-Json -Depth 10)
        $staleResult = Invoke-Inspection -ProjectPath $stale
        Assert-True -Condition ($staleResult.status -ne 'Ready') -Message 'Stale evidence produced Ready.'
        Assert-Equal -Expected 'Unknown' -Actual $staleResult.integration.validation.binding.status `
            -Message 'Stale Binding evidence was trusted.'
        Assert-True -Condition ($staleResult.integration.validation.binding.rejectedEvidence.reason -contains 'ProjectFactsMismatch') `
            -Message 'Stale project binding was not rejected explicitly.'
    }

    Invoke-Test -Name 'Source scan is bounded and reports truncation' -Body {
        $bounded = New-UnityFixture -RunRoot $runRoot -Name 'bounded' -AppUIReference $officialTag
        Add-SourceFile -Root $bounded -RelativePath 'Assets\A.cs' -Content 'public sealed class A { }'
        Add-SourceFile -Root $bounded -RelativePath 'Assets\B.cs' -Content 'public sealed class B { }'
        Add-SourceFile -Root $bounded -RelativePath 'Assets\C.cs' -Content 'public sealed class C { AppUIRuntimeHost host; }'
        Add-SourceFile -Root $bounded -RelativePath 'Assets\D.asmdef' -Content '{ "name": "D" }'

        $result = Invoke-Inspection -ProjectPath $bounded -MaxSourceFiles 2
        Assert-Equal -Expected 2 -Actual ([int]$result.project.maxSourceFiles) `
            -Message 'Inspector did not report the applied MaxSourceFiles bound.'
        Assert-Equal -Expected 2 -Actual ([int]$result.project.scannedFileCount) `
            -Message 'Inspector exceeded MaxSourceFiles.'
        Assert-Equal -Expected $true -Actual ([bool]$result.project.sourceFileLimitReached) `
            -Message 'Selected-file truncation was not reported separately.'
        Assert-Equal -Expected $true -Actual ([bool]$result.project.scanLimitReached) `
            -Message 'Bounded scan did not report truncation.'
        Assert-True -Condition ($result.issues.code -contains 'SOURCE_SCAN_LIMIT_REACHED') `
            -Message 'Bounded scan issue code is missing.'
    }

    Invoke-Test -Name 'Any source truncation prevents Ready and definitive validation passes' -Body {
        $truncatedReady = New-UnityFixture -RunRoot $runRoot -Name 'truncated-ready' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $truncatedReady
        Add-RuntimeRoot -Root $truncatedReady
        Add-PageContract -Root $truncatedReady
        Add-GeneratedBinding -Root $truncatedReady
        Add-ValidationEvidence -Root $truncatedReady -Kind Binding -Status Passed
        Add-ValidationEvidence -Root $truncatedReady -Kind Runtime -Status Passed
        for ($index = 0; $index -lt 30; $index++) {
            Add-SourceFile -Root $truncatedReady `
                -RelativePath ("Assets\Overflow\Overflow{0:D2}.cs" -f $index) `
                -Content ("public sealed class Overflow{0:D2} {{ }}" -f $index)
        }

        $result = Invoke-Inspection -ProjectPath $truncatedReady -MaxSourceFiles 20
        Assert-Equal -Expected $true -Actual ([bool]$result.project.sourceFileLimitReached) `
            -Message 'Source-file truncation was not recorded.'
        Assert-Equal -Expected 'RuntimeValidationPending' -Actual $result.status `
            -Message 'Truncated inspection claimed a definitive integration state.'
        Assert-Equal -Expected 'Indeterminate' -Actual $result.integration.validation.binding.status `
            -Message 'Truncated inspection retained a definitive Binding pass.'
        Assert-Equal -Expected 'Indeterminate' -Actual $result.integration.validation.runtime.status `
            -Message 'Truncated inspection retained a definitive Runtime pass.'
    }

    Invoke-Test -Name 'Total file and directory entry enumeration has a hard budget' -Body {
        $entryBounded = New-UnityFixture -RunRoot $runRoot -Name 'entry-bounded' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $entryBounded
        Add-RuntimeRoot -Root $entryBounded
        Add-PageContract -Root $entryBounded
        Add-GeneratedBinding -Root $entryBounded
        Add-ValidationEvidence -Root $entryBounded -Kind Binding -Status Passed
        Add-ValidationEvidence -Root $entryBounded -Kind Runtime -Status Passed
        for ($index = 0; $index -lt 600; $index++) {
            Write-Utf8File -Path (Join-Path $entryBounded ("Assets\EntryFlood\Entry{0:D3}.txt" -f $index)) `
                -Content 'ignored but still an enumerated filesystem entry'
        }

        $result = Invoke-Inspection -ProjectPath $entryBounded -MaxSourceFiles 20
        Assert-Equal -Expected $true -Actual ([bool]$result.project.entryLimitReached) `
            -Message 'Total enumerated-entry budget was not enforced.'
        Assert-True -Condition ([int]$result.project.enumeratedEntryCount -le [int]$result.project.maxEnumeratedEntries) `
            -Message 'Inspector enumerated beyond its reported total-entry budget.'
        Assert-Equal -Expected ([int]$result.project.enumeratedEntryCount) `
            -Actual ([int]$result.project.enumeratedFileEntryCount + [int]$result.project.enumeratedDirectoryEntryCount) `
            -Message 'File and directory entry counts do not reconcile.'
        Assert-True -Condition ($result.status -ne 'Ready') -Message 'Entry-truncated inspection produced Ready.'
        Assert-True -Condition ($result.integration.validation.binding.status -ne 'Passed') `
            -Message 'Entry-truncated inspection retained a definitive Binding pass.'
    }

    Invoke-Test -Name 'Directory traversal budget is reported separately' -Body {
        $directoryBounded = New-UnityFixture -RunRoot $runRoot -Name 'directory-bounded' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $directoryBounded
        Add-RuntimeRoot -Root $directoryBounded
        Add-PageContract -Root $directoryBounded
        Add-GeneratedBinding -Root $directoryBounded
        Add-ValidationEvidence -Root $directoryBounded -Kind Binding -Status Passed
        Add-ValidationEvidence -Root $directoryBounded -Kind Runtime -Status Passed
        $deepRoot = Join-Path $directoryBounded 'Assets\ManyDirectories'
        for ($index = 0; $index -lt 150; $index++) {
            [System.IO.Directory]::CreateDirectory((Join-Path $deepRoot ("D{0:D3}" -f $index))) | Out-Null
        }

        $result = Invoke-Inspection -ProjectPath $directoryBounded -MaxSourceFiles 20
        Assert-Equal -Expected $true -Actual ([bool]$result.project.directoryLimitReached) `
            -Message 'Directory budget truncation was not reported separately.'
        Assert-True -Condition ([int]$result.project.visitedDirectoryCount -le [int]$result.project.maxDirectories) `
            -Message 'Inspector visited beyond its reported directory budget.'
        Assert-True -Condition ($result.status -ne 'Ready') -Message 'Directory-truncated inspection produced Ready.'
        Assert-True -Condition ($result.integration.validation.runtime.status -ne 'Passed') `
            -Message 'Directory-truncated inspection retained a definitive Runtime pass.'
    }

    Invoke-Test -Name 'Secret files and reparse escapes are never scanned' -Body {
        $json = & $script:InspectorPath -ProjectPath $completeFixture
        $text = $json -join [Environment]::NewLine
        Assert-True -Condition (-not $text.Contains($envSentinel)) -Message '.env sentinel leaked into JSON.'
        Assert-True -Condition (-not $text.Contains($reparseSentinel)) -Message 'Reparse target sentinel leaked into JSON.'
        Assert-True -Condition (-not $text.Contains('EscapedAppUIRuntimeHost.cs')) `
            -Message 'A file beyond the project root was reported.'
    }

    Invoke-Test -Name 'Known Unity inputs are not read through reparse directories' -Body {
        $reparsePackages = New-UnityFixture -RunRoot $runRoot -Name 'reparse-packages'
        $externalPackages = Join-Path $runRoot 'outside-packages-input'
        [System.IO.Directory]::CreateDirectory($externalPackages) | Out-Null
        $knownInputSentinel = 'APPUI_KNOWN_INPUT_REPARSE_SENTINEL_518DC06A'
        Write-Utf8File -Path (Join-Path $externalPackages 'manifest.json') -Content @"
{
  "sentinel": "$knownInputSentinel",
  "dependencies": {
    "com.joih.appui": "$officialTag",
    "com.unity.ugui": "2.0.0"
  }
}
"@
        Write-Utf8File -Path (Join-Path $externalPackages 'packages-lock.json') -Content '{ "dependencies": {} }'
        $packagesPath = Join-Path $reparsePackages 'Packages'
        [System.IO.Directory]::Delete($packagesPath, $true)
        New-Item -ItemType Junction -Path $packagesPath -Target $externalPackages | Out-Null
        $script:JunctionPaths.Add($packagesPath) | Out-Null

        $json = & $script:InspectorPath -ProjectPath $reparsePackages
        $text = $json -join [Environment]::NewLine
        $result = $text | ConvertFrom-Json
        Assert-Equal -Expected 'AppUINotInstalled' -Actual $result.status `
            -Message 'A reparse-point Packages directory was treated as trusted input.'
        Assert-True -Condition (-not $text.Contains($knownInputSentinel)) `
            -Message 'A known-input reparse sentinel leaked into JSON.'
    }

    Invoke-Test -Name 'Hidden and secret validation files cannot affect evidence' -Body {
        $isolated = New-UnityFixture -RunRoot $runRoot -Name 'hidden-secret-evidence' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $isolated
        Add-RuntimeRoot -Root $isolated
        Add-PageContract -Root $isolated
        Add-GeneratedBinding -Root $isolated
        Add-ValidationEvidence -Root $isolated -Kind Binding -Status Passed
        Add-ValidationEvidence -Root $isolated -Kind Runtime -Status Passed

        $hiddenPath = Join-Path $isolated 'Assets\AppUI\Validation\HiddenRuntimeValidationReport.asset'
        Write-Utf8File -Path $hiddenPath -Content 'status: Failed'
        [System.IO.File]::SetAttributes($hiddenPath, [System.IO.FileAttributes]::Hidden)
        Write-Utf8File -Path (Join-Path $isolated 'Assets\Secrets\AppUIRuntimeValidationReport.asset') `
            -Content 'status: Failed'

        $result = Invoke-Inspection -ProjectPath $isolated
        Assert-Equal -Expected 'Ready' -Actual $result.status `
            -Message 'Hidden or secret validation content changed the project status.'
        Assert-True -Condition (-not ($result.integration.validation.runtime.evidence.path -contains `
            'Assets/AppUI/Validation/HiddenRuntimeValidationReport.asset')) `
            -Message 'A hidden validation file was reported as evidence.'
    }

    Invoke-Test -Name 'OutputPath bytes equal stdout JSON and use UTF8 without BOM' -Body {
        $outputPath = Join-Path $runRoot 'reports\inspection.json'
        $stdout = & $script:InspectorPath -ProjectPath $completeFixture -OutputPath $outputPath
        $stdoutJson = $stdout -join [Environment]::NewLine
        Assert-True -Condition (Test-Path -LiteralPath $outputPath -PathType Leaf) `
            -Message 'OutputPath file was not created.'
        $fileJson = [System.IO.File]::ReadAllText($outputPath)
        Assert-Equal -Expected $stdoutJson -Actual $fileJson `
            -Message 'OutputPath raw JSON differs from stdout.'
        $bytes = [System.IO.File]::ReadAllBytes($outputPath)
        $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        Assert-Equal -Expected $false -Actual $hasUtf8Bom -Message 'OutputPath JSON has a UTF-8 BOM.'
        $fileResult = $fileJson | ConvertFrom-Json
        Assert-Equal -Expected ([System.IO.Path]::GetFullPath($outputPath)) -Actual $fileResult.outputPath `
            -Message 'Inspector did not report the requested output path.'
    }

    Invoke-Test -Name 'OutputPath cannot overwrite Unity project inputs' -Body {
        $manifestPath = Join-Path $completeFixture 'Packages\manifest.json'
        $before = [System.IO.File]::ReadAllText($manifestPath)
        $blocked = $false
        try {
            & $script:InspectorPath -ProjectPath $completeFixture -OutputPath $manifestPath | Out-Null
        }
        catch {
            $blocked = $true
        }

        Assert-True -Condition $blocked -Message 'Inspector allowed OutputPath to target Packages/manifest.json.'
        Assert-Equal -Expected $before -Actual ([System.IO.File]::ReadAllText($manifestPath)) `
            -Message 'Inspector modified Packages/manifest.json.'
    }

    Invoke-Test -Name 'OutputPath rejects reparse targets and protected aliases' -Body {
        $externalOutput = Join-Path $runRoot 'external-output-target'
        [System.IO.Directory]::CreateDirectory($externalOutput) | Out-Null
        $outputAlias = Join-Path $runRoot 'output-alias'
        New-Item -ItemType Junction -Path $outputAlias -Target $externalOutput | Out-Null
        $script:JunctionPaths.Add($outputAlias) | Out-Null
        $aliasBlocked = $false
        try {
            & $script:InspectorPath -ProjectPath $completeFixture `
                -OutputPath (Join-Path $outputAlias 'inspection.json') | Out-Null
        }
        catch {
            $aliasBlocked = $true
        }
        Assert-True -Condition $aliasBlocked -Message 'OutputPath traversed an existing reparse-point ancestor.'
        Assert-Equal -Expected $false -Actual (Test-Path -LiteralPath (Join-Path $externalOutput 'inspection.json')) `
            -Message 'OutputPath wrote through a reparse-point ancestor.'

        $protectedFixture = New-UnityFixture -RunRoot $runRoot -Name 'protected-output-alias' -AppUIReference $officialTag
        $protectedManifest = Join-Path $protectedFixture 'Packages\manifest.json'
        $protectedBefore = [System.IO.File]::ReadAllText($protectedManifest)
        $packagesAlias = Join-Path $runRoot 'packages-output-alias'
        New-Item -ItemType Junction -Path $packagesAlias -Target (Join-Path $protectedFixture 'Packages') | Out-Null
        $script:JunctionPaths.Add($packagesAlias) | Out-Null
        $protectedBlocked = $false
        try {
            & $script:InspectorPath -ProjectPath $protectedFixture `
                -OutputPath (Join-Path $packagesAlias 'manifest.json') | Out-Null
        }
        catch {
            $protectedBlocked = $true
        }
        Assert-True -Condition $protectedBlocked -Message 'Protected project file was reachable through a junction alias.'
        Assert-Equal -Expected $protectedBefore -Actual ([System.IO.File]::ReadAllText($protectedManifest)) `
            -Message 'Packages/manifest.json was overwritten through a junction alias.'
    }
}
finally {
    foreach ($junctionPath in $script:JunctionPaths) {
        if ([System.IO.Directory]::Exists($junctionPath)) {
            [System.IO.Directory]::Delete($junctionPath)
        }
    }

    $tempPrefix = $tempRoot + [System.IO.Path]::DirectorySeparatorChar
    $resolvedRunRoot = [System.IO.Path]::GetFullPath($runRoot)
    if (-not $resolvedRunRoot.StartsWith($tempPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw ("Refusing to clean test root outside the system temp directory: {0}" -f $resolvedRunRoot)
    }
    if (Test-Path -LiteralPath $resolvedRunRoot) {
        Remove-Item -LiteralPath $resolvedRunRoot -Recurse -Force
    }
}

if (Test-Path -LiteralPath $runRoot) {
    $script:Failed++
    Write-Host ("FAIL Disposable fixture cleanup: {0} still exists." -f $runRoot)
}
else {
    $script:Passed++
    Write-Host 'PASS Disposable fixture cleanup'
}

Write-Host ("RESULT Passed={0} Failed={1}" -f $script:Passed, $script:Failed)
if ($script:Failed -gt 0) {
    exit 1
}
