[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TemplatePath,

    [Parameter(Mandatory = $true)]
    [string]$DestinationPath,

    [Parameter(Mandatory = $true)]
    [string]$PackageReference
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$modulePath = Join-Path $PSScriptRoot 'AppUI.ReleaseTools.psm1'
Import-Module $modulePath -Force

$workspace = New-AppUIConsumerWorkspace `
    -TemplatePath $TemplatePath `
    -DestinationPath $DestinationPath `
    -PackageReference $PackageReference

$workspace | Format-List
