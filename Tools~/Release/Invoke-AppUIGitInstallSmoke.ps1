[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryPath,

    [Parameter(Mandatory = $true)]
    [string]$SourceCommit,

    [Parameter(Mandatory = $true)]
    [string]$PackageReference,

    [Parameter(Mandatory = $true)]
    [string]$UnityPath,

    [Parameter(Mandatory = $true)]
    [string]$RunRoot,

    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$modulePath = Join-Path $PSScriptRoot 'AppUI.ReleaseTools.psm1'
Import-Module $modulePath -Force

if ($PackageReference -notmatch '^https://github\.com/TechJoiH/JoiH-AppUI\.git#(?:[0-9a-f]{40}|v[0-9A-Za-z][0-9A-Za-z.+-]*)$') {
    throw "Git install smoke only accepts an immutable AppUI commit SHA or SemVer tag URL."
}

$resolvedRunRoot = [System.IO.Path]::GetFullPath($RunRoot)
if (Test-Path -LiteralPath $resolvedRunRoot) {
    throw "Git smoke RunRoot already exists: $resolvedRunRoot"
}

[System.IO.Directory]::CreateDirectory($resolvedRunRoot) | Out-Null
$snapshot = Export-AppUICandidateSnapshot `
    -RepositoryPath $RepositoryPath `
    -SourceRef $SourceCommit `
    -DestinationPath (Join-Path $resolvedRunRoot 'snapshot')
$evidenceRoot = Join-Path $resolvedRunRoot 'evidence'
$consumerRoot = Join-Path $resolvedRunRoot 'consumer'
[System.IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
New-AppUIConsumerWorkspace `
    -TemplatePath (Join-Path $snapshot.PackageRoot 'Validation~\Unity6000.0Consumer') `
    -DestinationPath $consumerRoot `
    -PackageReference $PackageReference | Out-Null

$env:APPUI_EXPECTED_PACKAGE_VERSION = $snapshot.PackageVersion
$env:APPUI_VALIDATION_OUTPUT = $evidenceRoot
function Invoke-SmokeUnityStep {
    param([string]$Name, [string[]]$Arguments)

    $result = Invoke-AppUIUnityProcess `
        -UnityPath $UnityPath `
        -ProjectPath $consumerRoot `
        -LogFile (Join-Path $evidenceRoot ($Name + '.log')) `
        -Arguments $Arguments `
        -TimeoutSeconds $TimeoutSeconds
    if ($result.Status -ne 'Passed') {
        throw "Git install smoke step failed: $Name. Status=$($result.Status) ExitCode=$($result.ExitCode)"
    }
}

Invoke-SmokeUnityStep '01-import-sample' @(
    '-quit', '-executeMethod',
    'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerFixtureCommand.ImportBasicIntegration')
Invoke-SmokeUnityStep '02-domain-reload' @('-quit')
Invoke-SmokeUnityStep '03-generate-fixtures' @(
    '-quit', '-executeMethod',
    'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerFixtureCommand.CreateFixturesAndGenerateBindings')
Invoke-SmokeUnityStep '04-binding-domain-reload' @('-quit')
Invoke-SmokeUnityStep '05-bind-validate' @(
    '-quit', '-executeMethod',
    'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerBindingCommand.BindAndValidate')
Invoke-SmokeUnityStep '06-runtime-smoke' @(
    '-quit', '-executeMethod',
    'Joi.H.AppUI.Validation.Consumer.Editor.AppUIConsumerSmokeCommand.Run')

$sourceSmoke = Join-Path $evidenceRoot 'git-install-smoke.json'
$targetName = if ($PackageReference -match '#v') {
    'tag-git-install-smoke.json'
} else {
    'commit-git-install-smoke.json'
}
$targetSmoke = Join-Path $evidenceRoot $targetName
Move-Item -LiteralPath $sourceSmoke -Destination $targetSmoke
Write-Host "Git install smoke passed: $targetSmoke"
