[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$RepositoryPath = '',
    [string]$ConsumerPath = '',
    [string]$UserProfilePath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
Import-Module (Join-Path $PSScriptRoot 'AppUI.ReleaseTools.psm1') -Force

New-AppUIReleaseArtifacts `
    -SourceDirectory $SourceDirectory `
    -OutputDirectory $OutputDirectory `
    -Version $Version `
    -RepositoryPath $RepositoryPath `
    -ConsumerPath $ConsumerPath `
    -UserProfilePath $UserProfilePath | Format-List
