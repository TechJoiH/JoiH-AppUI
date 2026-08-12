[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$IdentityPath,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedSourceCommit,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedSourceTree,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedPackageVersion,

    [Parameter(Mandatory = $true)]
    [string]$PlannedTag,

    [ValidateSet('PreTag', 'Formal')]
    [string]$Mode = 'PreTag',

    [AllowEmptyString()]
    [string]$ResolvedTag = '',

    [AllowEmptyString()]
    [string]$RepositoryPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
Import-Module (Join-Path $PSScriptRoot 'AppUI.ReleaseTools.psm1') -Force

New-AppUIReleaseReport `
    -IdentityPath $IdentityPath `
    -EvidenceRoot $EvidenceRoot `
    -OutputPath $OutputPath `
    -ExpectedSourceCommit $ExpectedSourceCommit `
    -ExpectedSourceTree $ExpectedSourceTree `
    -ExpectedPackageVersion $ExpectedPackageVersion `
    -PlannedTag $PlannedTag `
    -Mode $Mode `
    -ResolvedTag $ResolvedTag `
    -RepositoryPath $RepositoryPath | Format-List
