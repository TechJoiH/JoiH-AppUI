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
$policy | ConvertTo-Json -Depth 8 | Set-Content `
    -LiteralPath (Join-Path $evidenceRoot 'static-policy.json') `
    -Encoding UTF8
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

$consumerRoot = Join-Path $resolvedRunRoot 'consumer'
New-AppUIConsumerWorkspace `
    -TemplatePath (Join-Path $snapshot.PackageRoot 'Validation~\Unity6000.0Consumer') `
    -DestinationPath $consumerRoot `
    -PackageReference $snapshot.PackageRoot | Out-Null
$env:APPUI_EXPECTED_PACKAGE_VERSION = $identity.PackageVersion
$env:APPUI_VALIDATION_OUTPUT = $evidenceRoot

function Invoke-Gate {
    param([string]$Name, [string[]]$Arguments)

    $result = Invoke-AppUIUnityProcess `
        -UnityPath $UnityPath `
        -ProjectPath $consumerRoot `
        -LogFile (Join-Path $evidenceRoot ($Name + '.log')) `
        -Arguments $Arguments `
        -TimeoutSeconds $TimeoutSeconds
    if ($result.Status -ne 'Passed') {
        throw "Unity gate failed: $Name. Status=$($result.Status) ExitCode=$($result.ExitCode)"
    }
}

$pipelineFailure = $null
try {
    Invoke-Gate '01-import-sample' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerFixtureCommand.ImportBasicIntegration')
    Invoke-Gate '02-domain-reload' @('-quit')
    Invoke-Gate '03-generate-fixtures' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerFixtureCommand.CreateFixturesAndGenerateBindings')
    Invoke-Gate '04-binding-domain-reload' @('-quit')
    Invoke-Gate '05-bind-validate' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBindingCommand.BindAndValidate')
    Invoke-Gate '06-editmode' @(
        '-runTests', '-testPlatform', 'EditMode',
        '-testResults', (Join-Path $evidenceRoot 'editmode.xml'))
    Read-AppUINUnit3Result `
        -Path (Join-Path $evidenceRoot 'editmode.xml') `
        -RequirePassed | Out-Null
    Invoke-Gate '07-playmode' @(
        '-runTests', '-testPlatform', 'PlayMode',
        '-testResults', (Join-Path $evidenceRoot 'playmode.xml'))
    Read-AppUINUnit3Result `
        -Path (Join-Path $evidenceRoot 'playmode.xml') `
        -RequirePassed | Out-Null
    Invoke-Gate '08-build-mono' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBuildCommand.BuildMono')
    Invoke-Gate '09-build-il2cpp' @(
        '-quit', '-executeMethod',
        'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBuildCommand.BuildIl2Cpp')
}
catch {
    $pipelineFailure = $_
}

$reportPath = Join-Path $evidenceRoot 'pretag-report.json'
if ((Test-Path -LiteralPath (Join-Path $evidenceRoot 'editmode.xml')) -and
    (Test-Path -LiteralPath (Join-Path $evidenceRoot 'playmode.xml')) -and
    (Test-Path -LiteralPath (Join-Path $evidenceRoot 'binding-validation.json')) -and
    (Test-Path -LiteralPath (Join-Path $evidenceRoot 'build-windowsmono.json')) -and
    (Test-Path -LiteralPath (Join-Path $evidenceRoot 'build-windowsil2cpp.json'))) {
    New-AppUIReleaseReport `
        -IdentityPath (Join-Path $evidenceRoot 'candidate-identity.json') `
        -EvidenceRoot $evidenceRoot `
        -OutputPath $reportPath `
        -ExpectedSourceCommit $identity.SourceCommit `
        -ExpectedSourceTree $identity.SourceTree `
        -ExpectedPackageVersion $identity.PackageVersion `
        -PlannedTag $PlannedTag | Out-Null
}

if ($null -ne $pipelineFailure) {
    throw $pipelineFailure
}

$report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($report.status -ne 'Passed') {
    throw "Pre-tag report did not pass: $reportPath"
}

Write-Host "Pre-tag validation passed: $reportPath"
