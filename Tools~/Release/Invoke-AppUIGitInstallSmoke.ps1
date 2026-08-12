[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageReference,

    [Parameter(Mandatory = $true)]
    [string]$UnityPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedPackageVersion,

    [Parameter(Mandatory = $true)]
    [string]$RunRoot,

    [string]$RepositoryPath = (Get-Location).Path,

    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$modulePath = Join-Path $PSScriptRoot 'AppUI.ReleaseTools.psm1'
Import-Module $modulePath -Force

$referenceMatch = [regex]::Match(
    $PackageReference,
    '^https://github\.com/TechJoiH/JoiH-AppUI\.git#(?<fragment>.+)$')
$referenceFragment = if ($referenceMatch.Success) {
    $referenceMatch.Groups['fragment'].Value
} else {
    ''
}
$validSemVerTag = -not [string]::IsNullOrWhiteSpace($referenceFragment) -and
    (Test-AppUISemVerTag -Tag $referenceFragment)
if (-not $referenceMatch.Success -or
    ($referenceFragment -notmatch '^[0-9a-f]{40}$' -and
     -not $validSemVerTag)) {
    throw "Git install smoke only accepts an immutable AppUI commit SHA or SemVer tag URL."
}
$sourceRef = $referenceFragment
if ($validSemVerTag) {
    $remoteTagIdentity = Resolve-AppUIRemoteTagIdentity `
        -RepositoryPath $RepositoryPath `
        -Tag $referenceFragment
    $sourceRef = $remoteTagIdentity.SourceCommit
}

$resolvedRunRoot = [System.IO.Path]::GetFullPath($RunRoot)
if (Test-Path -LiteralPath $resolvedRunRoot) {
    throw "Git smoke RunRoot already exists: $resolvedRunRoot"
}

[System.IO.Directory]::CreateDirectory($resolvedRunRoot) | Out-Null
$snapshot = Export-AppUICandidateSnapshot `
    -RepositoryPath $RepositoryPath `
    -SourceRef $sourceRef `
    -DestinationPath (Join-Path $resolvedRunRoot 'snapshot')
if ($snapshot.PackageVersion -ne $ExpectedPackageVersion) {
    throw "Git smoke package version mismatch. Expected=$ExpectedPackageVersion Actual=$($snapshot.PackageVersion)"
}
if ($validSemVerTag -and $snapshot.SourceTree -ne $remoteTagIdentity.SourceTree) {
    throw "Git smoke remote Tag tree mismatch. Tag=$referenceFragment"
}
$evidenceRoot = Join-Path $resolvedRunRoot 'evidence'
$consumerRoot = Join-Path $resolvedRunRoot 'consumer'
[System.IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
New-AppUIConsumerWorkspace `
    -TemplatePath (Join-Path $snapshot.PackageRoot 'Validation~\Unity6000.0Consumer') `
    -DestinationPath $consumerRoot `
    -PackageReference $PackageReference | Out-Null

$env:APPUI_EXPECTED_PACKAGE_VERSION = $ExpectedPackageVersion
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
$smoke = Get-Content -LiteralPath $targetSmoke -Raw -Encoding UTF8 | ConvertFrom-Json
$identity = Get-Content -LiteralPath $snapshot.IdentityPath -Raw -Encoding UTF8 | ConvertFrom-Json
$smoke | Add-Member -NotePropertyName repository -NotePropertyValue $identity.repository -Force
$smoke | Add-Member -NotePropertyName sourceCommit -NotePropertyValue $identity.sourceCommit -Force
$smoke | Add-Member -NotePropertyName sourceTree -NotePropertyValue $identity.sourceTree -Force
$smoke | Add-Member -NotePropertyName packageManifestSha256 -NotePropertyValue $identity.packageManifestSha256 -Force
$smoke | Add-Member -NotePropertyName packageReference -NotePropertyValue $PackageReference -Force
Write-AppUIJson -Path $targetSmoke -Value $smoke -Depth 8
Write-Host "Git install smoke passed: $targetSmoke"
