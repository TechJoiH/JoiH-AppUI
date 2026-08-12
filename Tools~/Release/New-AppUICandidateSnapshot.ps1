[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryPath,

    [Parameter(Mandatory = $true)]
    [string]$SourceRef,

    [Parameter(Mandatory = $true)]
    [string]$DestinationPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$modulePath = Join-Path $PSScriptRoot 'AppUI.ReleaseTools.psm1'
Import-Module $modulePath -Force

$snapshot = Export-AppUICandidateSnapshot `
    -RepositoryPath $RepositoryPath `
    -SourceRef $SourceRef `
    -DestinationPath $DestinationPath

$snapshot | Select-Object `
    Repository,
    SourceCommit,
    SourceTree,
    PackageVersion,
    PackageManifestSha256,
    GeneratedAtUtc,
    CandidateRoot,
    PackageRoot,
    EvidenceRoot | Format-List
