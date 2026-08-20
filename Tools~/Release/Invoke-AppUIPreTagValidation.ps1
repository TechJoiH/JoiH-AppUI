[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryPath,

    [Parameter(Mandatory = $true)]
    [string]$SourceCommit,

    [Parameter(Mandatory = $true)]
    [string]$PlannedTag,

    [Parameter(Mandatory = $true)]
    [string]$UnityPath,

    [Parameter(Mandatory = $true)]
    [string]$RunRoot,

    [ValidateSet('Full', 'StaticPolicy', 'Snapshot')]
    [string]$StopAfter = 'Full',

    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$modulePath = Join-Path $PSScriptRoot 'AppUI.ReleaseTools.psm1'
Import-Module $modulePath -Force

$resolvedRepository = [System.IO.Path]::GetFullPath($RepositoryPath)
$resolvedRunRoot = [System.IO.Path]::GetFullPath($RunRoot)
if (Test-Path -LiteralPath $resolvedRunRoot) {
    throw "Pre-tag RunRoot already exists: $resolvedRunRoot"
}

$identity = Resolve-AppUIGitIdentity `
    -RepositoryPath $resolvedRepository `
    -SourceRef $SourceCommit
if ($identity.SourceCommit -ne $SourceCommit) {
    throw "SourceCommit must be an exact 40-character commit SHA. Resolved=$($identity.SourceCommit)"
}

if ($PlannedTag -ne ('v' + $identity.PackageVersion)) {
    throw "Planned tag must equal v + package version. Expected=v$($identity.PackageVersion) Actual=$PlannedTag"
}

$head = (git -C $resolvedRepository rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve repository HEAD."
}

if ($head -eq $identity.SourceCommit) {
    $dirty = (git -C $resolvedRepository status --porcelain) -join "`n"
    if (-not [string]::IsNullOrWhiteSpace($dirty)) {
        throw "Current candidate is HEAD but the worktree is not clean."
    }
}
else {
    Write-Warning "Validating a historical commit; current worktree state does not affect snapshot identity."
}

$policy = Test-AppUIPackagePolicy `
    -RepositoryPath $resolvedRepository `
    -SourceRef $identity.SourceCommit
if (-not $policy.Success) {
    $errors = @($policy.Checks | Where-Object { $_.Status -eq 'Error' } |
        ForEach-Object { $_.Name + ': ' + $_.Details })
    throw "Static package policy failed: $($errors -join ' | ')"
}

[System.IO.Directory]::CreateDirectory($resolvedRunRoot) | Out-Null
$evidenceRoot = Join-Path $resolvedRunRoot 'evidence'
[System.IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
Write-AppUIJson `
    -Path (Join-Path $evidenceRoot 'static-policy.json') `
    -Value $policy `
    -Depth 8
if ($StopAfter -eq 'StaticPolicy') {
    Write-Host "Static policy passed for $($identity.SourceCommit)."
    exit 0
}

$snapshot = Export-AppUICandidateSnapshot `
    -RepositoryPath $resolvedRepository `
    -SourceRef $identity.SourceCommit `
    -DestinationPath (Join-Path $resolvedRunRoot 'snapshot')
Copy-Item -LiteralPath $snapshot.IdentityPath `
    -Destination (Join-Path $evidenceRoot 'candidate-identity.json')
Copy-Item -LiteralPath $snapshot.ManifestPath `
    -Destination (Join-Path $evidenceRoot 'package-manifest.json')
if ($StopAfter -eq 'Snapshot') {
    Write-Host "Snapshot passed for $($identity.SourceCommit)."
    exit 0
}

$buildEnvironment = Test-AppUIBuildEnvironment `
    -UnityPath $UnityPath `
    -ExpectedUnityVersion '6000.0.25f1'
Write-AppUIJson `
    -Path (Join-Path $evidenceRoot 'build-environment.json') `
    -Value $buildEnvironment `
    -Depth 6
if ($buildEnvironment.Status -ne 'Passed') {
    throw "Build environment is blocked. Gate=$($buildEnvironment.gate) Reason=$($buildEnvironment.Reason). $($buildEnvironment.Details)"
}

$layout = New-AppUIConsumerValidationLayout `
    -RunRoot $resolvedRunRoot `
    -TemplatePath (Join-Path $snapshot.PackageRoot 'Validation~\Unity6000.0Consumer') `
    -PackageReference $snapshot.PackageRoot
$previousExpectedPackageVersion = $env:APPUI_EXPECTED_PACKAGE_VERSION
$env:APPUI_EXPECTED_PACKAGE_VERSION = $identity.PackageVersion
$previousValidationOutput = $env:APPUI_VALIDATION_OUTPUT

function Invoke-Gate {
    param(
        [string]$Mode,
        [string]$Name,
        [string[]]$Arguments
    )

    $consumerPath = if ($Mode -eq 'base') {
        $layout.BaseConsumerPath
    } else {
        $layout.TextMeshProConsumerPath
    }
    $logs = if ($Mode -eq 'base') {
        $layout.BaseLogRoot
    } else {
        $layout.TextMeshProLogRoot
    }

    $result = Invoke-AppUIUnityProcess `
        -UnityPath $UnityPath `
        -ProjectPath $consumerPath `
        -LogFile (Join-Path $logs ($Name + '.log')) `
        -Arguments $Arguments `
        -TimeoutSeconds $TimeoutSeconds
    if ($result.Status -ne 'Passed') {
        throw "Unity gate failed: $Mode/$Name. Status=$($result.Status) ExitCode=$($result.ExitCode)"
    }
}

$pipelineFailure = $null
try {
    $env:APPUI_VALIDATION_OUTPUT = $layout.BaseEvidenceRoot
    Invoke-Gate 'base' '01-import-sample' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerFixtureCommand.ImportBasicIntegration')
    Invoke-Gate 'base' '02-domain-reload' @('-quit')
    Invoke-Gate 'base' '03-generate-fixtures' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerFixtureCommand.CreateFixturesAndGenerateBindings')
    Invoke-Gate 'base' '04-binding-domain-reload' @('-quit')
    Invoke-Gate 'base' '05-bind-validate' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBindingCommand.BindAndValidate')
    Invoke-Gate 'base' '06-editmode' @(
        '-runTests', '-testPlatform', 'EditMode',
        '-testResults', (Join-Path $layout.BaseEvidenceRoot 'editmode.xml'))
    Read-AppUINUnit3Result `
        -Path (Join-Path $layout.BaseEvidenceRoot 'editmode.xml') `
        -RequirePassed | Out-Null
    Invoke-Gate 'base' '07-playmode' @(
        '-runTests', '-testPlatform', 'PlayMode',
        '-testResults', (Join-Path $layout.BaseEvidenceRoot 'playmode.xml'))
    Read-AppUINUnit3Result `
        -Path (Join-Path $layout.BaseEvidenceRoot 'playmode.xml') `
        -RequirePassed | Out-Null
    Invoke-Gate 'base' '08-build-mono' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBuildCommand.BuildMono')
    Invoke-Gate 'base' '09-build-il2cpp' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBuildCommand.BuildIl2Cpp')

    $env:APPUI_VALIDATION_OUTPUT = $layout.TextMeshProEvidenceRoot
    Invoke-Gate 'textmeshpro' '01-configure-define' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerTextMeshProCommand.Configure')
    Invoke-Gate 'textmeshpro' '02-domain-reload' @('-quit')
    Invoke-Gate 'textmeshpro' '03-import-sample' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerTextMeshProCommand.ImportSample')
    Invoke-Gate 'textmeshpro' '04-sample-domain-reload' @('-quit')
    Invoke-Gate 'textmeshpro' '05-bind-validate' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerTextMeshProCommand.ValidateSample')
    Invoke-Gate 'textmeshpro' '06-diagnostics' @(
        '-executeMethod',
        'Joi.H.AppUI.Integrations.TextMeshPro.Editor.TextMeshProIntegrationValidationCommandLine.Validate')
    Invoke-Gate 'textmeshpro' '07-editmode' @(
        '-runTests', '-testPlatform', 'EditMode',
        '-assemblyNames',
        'Joi.H.AppUI.Tests.TextMeshPro.Editor;Joi.H.AppUI.Tests.TextMeshPro.Runtime;Joi.H.AppUI.Samples.TextMeshPro.Tests',
        '-testResults', (Join-Path $layout.TextMeshProEvidenceRoot 'editmode.xml'))
    Read-AppUINUnit3Result `
        -Path (Join-Path $layout.TextMeshProEvidenceRoot 'editmode.xml') `
        -RequirePassed | Out-Null
    Invoke-Gate 'textmeshpro' '08-playmode' @(
        '-runTests', '-testPlatform', 'PlayMode',
        '-assemblyNames', 'Joi.H.AppUI.Tests.Runtime',
        '-testResults', (Join-Path $layout.TextMeshProEvidenceRoot 'playmode.xml'))
    Read-AppUINUnit3Result `
        -Path (Join-Path $layout.TextMeshProEvidenceRoot 'playmode.xml') `
        -RequirePassed | Out-Null
    Invoke-Gate 'textmeshpro' '09-build-mono' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBuildCommand.BuildTextMeshProMono')
    Invoke-Gate 'textmeshpro' '10-build-il2cpp' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBuildCommand.BuildTextMeshProIl2Cpp')
}
catch {
    $pipelineFailure = $_
}

$reportPath = Join-Path $evidenceRoot 'pretag-report.json'
if ($null -eq $pipelineFailure) {
    New-AppUIReleaseReport `
        -IdentityPath (Join-Path $evidenceRoot 'candidate-identity.json') `
        -EvidenceRoot $evidenceRoot `
        -OutputPath $reportPath `
        -ExpectedSourceCommit $identity.SourceCommit `
        -ExpectedSourceTree $identity.SourceTree `
        -ExpectedPackageVersion $identity.PackageVersion `
        -PlannedTag $PlannedTag | Out-Null
}

try {
    Test-AppUICandidateSnapshot `
        -PackageRoot $snapshot.PackageRoot `
        -IdentityPath $snapshot.IdentityPath `
        -ManifestPath $snapshot.ManifestPath | Out-Null

    $allLogRoot = Join-Path $resolvedRunRoot 'logs'
    if (Test-Path -LiteralPath $allLogRoot -PathType Container) {
        Test-AppUIArtifactSecrets -Path $allLogRoot -ThrowOnSecret | Out-Null
        $userProfilePath = [Environment]::GetFolderPath(
            [Environment+SpecialFolder]::UserProfile)
        New-AppUISanitizedLogArchive `
            -InputDirectory $allLogRoot `
            -OutputArchive (Join-Path $evidenceRoot (
                'appui-' + $PlannedTag + '-logs.zip')) `
            -RepositoryPath $resolvedRepository `
            -ConsumerPath $resolvedRunRoot `
            -UserProfilePath $userProfilePath | Out-Null
    }
}
catch {
    if ($null -eq $pipelineFailure) { $pipelineFailure = $_ }
}
finally {
    $env:APPUI_VALIDATION_OUTPUT = $previousValidationOutput
    $env:APPUI_EXPECTED_PACKAGE_VERSION = $previousExpectedPackageVersion
    foreach ($consumerPath in @(
        $layout.BaseConsumerPath,
        $layout.TextMeshProConsumerPath)) {
        try {
            Remove-AppUIEphemeralConsumerWorkspace `
                -RunRoot $resolvedRunRoot `
                -ConsumerPath $consumerPath
        }
        catch {
            if ($null -eq $pipelineFailure) { $pipelineFailure = $_ }
        }
    }
}

if ($null -ne $pipelineFailure) {
    throw $pipelineFailure
}

$report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($report.status -ne 'Passed') {
    throw "Pre-tag report did not pass: $reportPath"
}

Write-Host "Pre-tag validation passed: $reportPath"
