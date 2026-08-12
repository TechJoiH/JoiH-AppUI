[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryPath,

    [Parameter(Mandatory = $true)]
    [string]$CandidateCommit,

    [Parameter(Mandatory = $true)]
    [string]$PlannedTag
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
Import-Module (Join-Path $PSScriptRoot 'AppUI.ReleaseTools.psm1') -Force

Test-AppUIReleaseReadiness `
    -RepositoryPath $RepositoryPath `
    -CandidateCommit $CandidateCommit `
    -PlannedTag $PlannedTag | Format-List
