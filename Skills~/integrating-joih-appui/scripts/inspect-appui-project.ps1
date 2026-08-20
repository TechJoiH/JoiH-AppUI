[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$OutputPath = '',
    [ValidateRange(1, 10000)][int]$MaxSourceFiles = 2000,
    [ValidateSet('None', 'Binding', 'Runtime')][string]$CreateAttestation = 'None',
    [ValidateSet('Unspecified', 'Passed', 'Failed', 'Pending')][string]$AttestationStatus = 'Unspecified',
    [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$AttestationHostBoundariesStatus = 'Passed',
    [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$AttestationRuntimeRootStatus = 'Passed',
    [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$AttestationPageContractStatus = 'Passed',
    [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$AttestationBindingStatus = 'Passed',
    [ValidateSet('Passed', 'Failed', 'Pending', 'NotRun')][string]$AttestationRuntimeLifecycleStatus = 'Passed'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$issues = New-Object 'System.Collections.Generic.List[object]'
# CreateAttestation records an explicit project-owned validation outcome; it does not claim that Joi.H AppUI
# performed validation. Its Assets digest covers every successfully read selected project file, excluding
# imported AppUI Samples and the attestation documents themselves. Incomplete scans cannot produce attestations.
$validationEvidenceSchemaVersion = 'joih-appui-project-owned-validation-attestation.v1'
$validationEvidenceProducer = 'integrating-joih-appui/inspect-appui-project.ps1#create-attestation-v1'
$validationEvidenceOwner = 'Project'
$validationAssetsDigestAlgorithm = 'SHA-256'
$validationAssetsDigestScope = 'selected-project-assets-v1'
$validationAssetsDigestCanonicalization = 'ordinal-portable-path-nul-lower-sha256-lf-v1'

function Add-Issue {
    param(
        [Parameter(Mandatory = $true)][string]$Code,
        [Parameter(Mandatory = $true)][ValidateSet('Info', 'Warning', 'Error')][string]$Severity,
        [Parameter(Mandatory = $true)][string]$Message,
        [string]$Path = ''
    )

    $issue = [ordered]@{
        code = $Code
        severity = $Severity
        message = $Message
    }
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        $issue.path = $Path
    }
    $issues.Add([pscustomobject]$issue) | Out-Null
}

function Get-ObjectPropertyValue {
    param(
        [AllowNull()]$Object,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }
    return $property.Value
}

function Convert-ToPortableRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if ($pathFull.Equals($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return ''
    }

    $prefix = $rootFull + [System.IO.Path]::DirectorySeparatorChar
    if (-not $pathFull.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }
    return $pathFull.Substring($prefix.Length).Replace('\', '/')
}

function Test-PathWithinRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $pathFull = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if ($pathFull.Equals($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }
    $prefix = $rootFull + [System.IO.Path]::DirectorySeparatorChar
    return $pathFull.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-SafeOutputPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowNull()][string]$UnityRoot
    )

    if (Test-Path -LiteralPath $Path) {
        throw ("OutputPath must not already exist: {0}" -f $Path)
    }

    if (-not [string]::IsNullOrWhiteSpace($UnityRoot)) {
        foreach ($protectedDirectoryName in @('Assets', 'Packages', 'ProjectSettings')) {
            $protectedDirectory = Join-Path $UnityRoot $protectedDirectoryName
            if (Test-PathWithinRoot -Path $Path -Root $protectedDirectory) {
                throw ("OutputPath cannot target the Unity {0} directory." -f $protectedDirectoryName)
            }
        }
    }

    $current = [System.IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path -LiteralPath $current) {
            try {
                $item = Get-Item -Force -LiteralPath $current
                if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ("OutputPath cannot target or traverse a reparse point: {0}" -f $current)
                }
            }
            catch {
                if ($_.Exception.Message -like 'OutputPath cannot target or traverse a reparse point:*') {
                    throw
                }
                throw ("OutputPath safety could not be verified for: {0}" -f $current)
            }
        }

        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or
            $parent.Equals($current, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $current = $parent
    }
}

function Assert-NoReparsePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $current = [System.IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -Force -LiteralPath $current
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw ("Path cannot target or traverse a reparse point: {0}" -f $current)
            }
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or
            $parent.Equals($current, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $current = $parent
    }
}

function Write-Utf8NoBomNewFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content
    )

    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($Content)
    $stream = $null
    try {
        $stream = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        $stream.Write($bytes, 0, $bytes.Length)
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Resolve-UnityProjectRoot {
    param([Parameter(Mandatory = $true)][string]$StartPath)

    try {
        $current = [System.IO.Path]::GetFullPath($StartPath)
    }
    catch {
        return $null
    }

    if (Test-Path -LiteralPath $current -PathType Leaf) {
        $current = Split-Path -Parent $current
    }
    elseif (-not (Test-Path -LiteralPath $current -PathType Container)) {
        return $null
    }

    while (-not [string]::IsNullOrWhiteSpace($current)) {
        $assets = Join-Path $current 'Assets'
        $packages = Join-Path $current 'Packages'
        $projectSettings = Join-Path $current 'ProjectSettings'
        if ((Test-Path -LiteralPath $assets -PathType Container) -and
            (Test-Path -LiteralPath $packages -PathType Container) -and
            (Test-Path -LiteralPath $projectSettings -PathType Container)) {
            return [System.IO.Path]::GetFullPath($current).TrimEnd('\', '/')
        }

        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }
        $current = $parent
    }
    return $null
}

function Test-UnsafeKnownPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][bool]$Directory
    )

    try {
        $info = if ($Directory) {
            New-Object System.IO.DirectoryInfo($Path)
        }
        else {
            New-Object System.IO.FileInfo($Path)
        }
        $attributes = $info.Attributes
        return (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
            ($attributes -band [System.IO.FileAttributes]::Hidden) -ne 0)
    }
    catch {
        return $true
    }
}

function Read-JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$FailureCode,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }
    if (Test-UnsafeKnownPath -Path $Path -Directory $false) {
        Add-Issue -Code 'KNOWN_INPUT_UNSAFE' -Severity 'Warning' `
            -Message 'A hidden or reparse-point Unity input file was not read.' -Path $RelativePath
        return $null
    }
    try {
        $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
        return ($text | ConvertFrom-Json)
    }
    catch {
        Add-Issue -Code $FailureCode -Severity 'Warning' `
            -Message 'A known Unity package JSON file could not be parsed.' -Path $RelativePath
        return $null
    }
}

function Get-DependencyMap {
    param([AllowNull()]$Json)

    $map = @{}
    $dependencies = Get-ObjectPropertyValue -Object $Json -Name 'dependencies'
    if ($null -eq $dependencies) {
        return $map
    }

    foreach ($property in $dependencies.PSObject.Properties) {
        $map[$property.Name] = $property.Value
    }
    return $map
}

function Get-LockVersion {
    param([AllowNull()]$LockEntry)

    $value = Get-ObjectPropertyValue -Object $LockEntry -Name 'version'
    if ($null -eq $value) {
        return $null
    }
    return [string]$value
}

function Get-LockSource {
    param([AllowNull()]$LockEntry)

    $value = Get-ObjectPropertyValue -Object $LockEntry -Name 'source'
    if ($null -eq $value) {
        return $null
    }
    return [string]$value
}

function Test-GitPackageReference {
    param(
        [AllowNull()][string]$Reference,
        [AllowNull()][string]$LockSource
    )

    if (-not [string]::IsNullOrWhiteSpace($LockSource) -and $LockSource -eq 'git') {
        return $true
    }
    if ([string]::IsNullOrWhiteSpace($Reference)) {
        return $false
    }
    return ($Reference -match '^(?i:git\+|git@|ssh://|https?://)' -and
        ($Reference -match '(?i:\.git)(?:[?#]|$)' -or $Reference -match '^(?i:git\+|git@|ssh://)'))
}

function Test-SemVer20 {
    param([AllowNull()][string]$Version)

    if ([string]::IsNullOrWhiteSpace($Version)) {
        return $false
    }
    $numeric = '(?:0|[1-9][0-9]*)'
    $prereleaseIdentifier = '(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)'
    $pattern = '\A{0}\.{0}\.{0}(?:-{1}(?:\.{1})*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z' -f `
        $numeric, $prereleaseIdentifier
    return [regex]::IsMatch($Version, $pattern, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
}

function Protect-PackageReference {
    param([AllowNull()][string]$Reference)

    if ([string]::IsNullOrWhiteSpace($Reference)) {
        return $Reference
    }

    $sanitized = $Reference
    if ($sanitized -match '^(?<scheme>[A-Za-z][A-Za-z0-9+.-]*://)(?<authority>[^/?#]*)(?<rest>.*)$') {
        $authority = $Matches['authority']
        $atIndex = $authority.LastIndexOf('@')
        if ($atIndex -ge 0) {
            $authority = $authority.Substring($atIndex + 1)
        }
        $sanitized = $Matches['scheme'] + $authority + $Matches['rest']
    }

    $fragment = ''
    $fragmentIndex = $sanitized.IndexOf('#')
    if ($fragmentIndex -ge 0) {
        $fragment = $sanitized.Substring($fragmentIndex)
        $sanitized = $sanitized.Substring(0, $fragmentIndex)
    }
    $queryIndex = $sanitized.IndexOf('?')
    if ($queryIndex -ge 0) {
        $base = $sanitized.Substring(0, $queryIndex)
        $query = $sanitized.Substring($queryIndex + 1)
        $protectedParts = New-Object 'System.Collections.Generic.List[string]'
        foreach ($part in ($query -split '&')) {
            $separator = $part.IndexOf('=')
            $key = if ($separator -ge 0) { $part.Substring(0, $separator) } else { $part }
            $normalizedKey = $key.Replace('+', ' ')
            $remainingDecodePasses = $normalizedKey.Length + 1
            while ($remainingDecodePasses -gt 0) {
                $remainingDecodePasses--
                try {
                    $decodedKey = [System.Uri]::UnescapeDataString($normalizedKey)
                }
                catch {
                    break
                }
                if ($decodedKey -eq $normalizedKey) {
                    break
                }
                $normalizedKey = $decodedKey
            }
            if ($normalizedKey -match '(?i)(?:token|secret|password|passwd|credential|signature|api[_-]?key|access[_-]?key|authorization|(?:^|[_-])(?:auth|sig|code|key)(?:$|[_-]))') {
                $protectedParts.Add($key + '=<redacted>') | Out-Null
            }
            else {
                $protectedParts.Add($part) | Out-Null
            }
        }
        $sanitized = $base + '?' + [string]::Join('&', $protectedParts.ToArray())
    }
    return $sanitized + $fragment
}

function Get-AppUIPackageFacts {
    param(
        [AllowNull()][string]$ManifestReference,
        [AllowNull()]$LockEntry
    )

    $lockReference = Get-LockVersion -LockEntry $LockEntry
    $lockSource = Get-LockSource -LockEntry $LockEntry
    $reference = if (-not [string]::IsNullOrWhiteSpace($ManifestReference)) {
        $ManifestReference
    }
    else {
        $lockReference
    }

    $installed = -not [string]::IsNullOrWhiteSpace($reference) -or $null -ne $LockEntry
    $installSource = 'Unknown'
    $gitRef = $null
    $gitRefKind = $null
    $version = $null
    $mutable = $null
    $immutability = 'Unknown'
    $tagIdentityVerified = $false

    if ($installed) {
        if (Test-GitPackageReference -Reference $reference -LockSource $lockSource) {
            $installSource = 'Git'
            if (-not [string]::IsNullOrWhiteSpace($reference)) {
                $hashIndex = $reference.LastIndexOf('#')
                if ($hashIndex -ge 0 -and $hashIndex -lt ($reference.Length - 1)) {
                    $gitRef = $reference.Substring($hashIndex + 1)
                }
            }

            if ([string]::IsNullOrWhiteSpace($gitRef)) {
                $gitRef = $null
                $gitRefKind = 'Unversioned'
                $mutable = $true
                $immutability = 'Mutable'
            }
            elseif ($gitRef.StartsWith('v', [System.StringComparison]::Ordinal) -and
                (Test-SemVer20 -Version $gitRef.Substring(1))) {
                $gitRefKind = 'TagCandidate'
                $version = $gitRef.Substring(1)
                $mutable = $null
                $immutability = 'UnverifiedOffline'
            }
            elseif ($gitRef -match '^[0-9a-fA-F]{40}$') {
                $gitRefKind = 'Commit'
                $mutable = $false
                $immutability = 'PinnedCommit'
            }
            else {
                $gitRefKind = 'Branch'
                $mutable = $true
                $immutability = 'Mutable'
            }
        }
        elseif (-not [string]::IsNullOrWhiteSpace($reference) -and
            $reference -match '^(?i:file:|\.\.?[\\/]|[A-Za-z]:[\\/])') {
            $installSource = 'LocalPath'
            $mutable = $true
            $immutability = 'Mutable'
        }
        elseif (Test-SemVer20 -Version $reference) {
            $installSource = 'Registry'
            $version = $reference
            $mutable = $false
            $immutability = 'VersionPinned'
        }
        else {
            $installSource = if (-not [string]::IsNullOrWhiteSpace($lockSource)) {
                $lockSource.Substring(0, 1).ToUpperInvariant() + $lockSource.Substring(1)
            }
            else {
                'Unknown'
            }
            $mutable = $true
            $immutability = 'Mutable'
        }
    }

    return [pscustomobject][ordered]@{
        installed = $installed
        manifestReference = Protect-PackageReference -Reference $ManifestReference
        lockReference = Protect-PackageReference -Reference $lockReference
        version = $version
        installSource = $installSource
        gitRef = $gitRef
        gitRefKind = $gitRefKind
        mutable = $mutable
        immutability = $immutability
        tagIdentityVerified = $tagIdentityVerified
    }
}

function Get-PackageFacts {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][hashtable]$ManifestDependencies,
        [Parameter(Mandatory = $true)][hashtable]$LockDependencies
    )

    $manifestReference = if ($ManifestDependencies.ContainsKey($Name)) {
        [string]$ManifestDependencies[$Name]
    }
    else {
        $null
    }
    $lockEntry = if ($LockDependencies.ContainsKey($Name)) {
        $LockDependencies[$Name]
    }
    else {
        $null
    }
    $lockVersion = Get-LockVersion -LockEntry $lockEntry
    $version = if (Test-SemVer20 -Version $lockVersion) {
        $lockVersion
    }
    elseif (Test-SemVer20 -Version $manifestReference) {
        $manifestReference
    }
    else {
        $lockVersion
    }

    return [pscustomobject][ordered]@{
        name = $Name
        installed = (-not [string]::IsNullOrWhiteSpace($manifestReference) -or $null -ne $lockEntry)
        manifestReference = Protect-PackageReference -Reference $manifestReference
        lockReference = Protect-PackageReference -Reference $lockVersion
        version = Protect-PackageReference -Reference $version
        source = Get-LockSource -LockEntry $lockEntry
    }
}

function Get-PackageCandidates {
    param(
        [Parameter(Mandatory = $true)][hashtable]$ManifestDependencies,
        [Parameter(Mandatory = $true)][hashtable]$LockDependencies,
        [Parameter(Mandatory = $true)][ValidateSet('Async', 'Asset')][string]$Kind
    )

    $names = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $ManifestDependencies.Keys) {
        $names.Add([string]$name) | Out-Null
    }
    foreach ($name in $LockDependencies.Keys) {
        $names.Add([string]$name) | Out-Null
    }

    $pattern = if ($Kind -eq 'Async') {
        '(?i)(unitask|async|awaitable|promise|reactive|\.rx(?:\.|$))'
    }
    else {
        '(?i)(addressable|assetbundle|asset-management|content-delivery|resources)'
    }

    $result = New-Object 'System.Collections.Generic.List[object]'
    foreach ($name in @($names | Sort-Object)) {
        if ($name -notmatch $pattern) {
            continue
        }
        $facts = Get-PackageFacts -Name $name -ManifestDependencies $ManifestDependencies -LockDependencies $LockDependencies
        $result.Add([pscustomobject][ordered]@{
            name = $name
            version = $facts.version
            source = $facts.source
            confidence = 'Candidate'
        }) | Out-Null
    }
    return @($result.ToArray())
}

function Get-ScriptingDefines {
    param([Parameter(Mandatory = $true)][string]$UnityRoot)

    $defines = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    $settingsPath = Join-Path $UnityRoot 'ProjectSettings\ProjectSettings.asset'
    if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
        return @()
    }
    $settingsDirectory = Join-Path $UnityRoot 'ProjectSettings'
    if ((Test-UnsafeKnownPath -Path $settingsDirectory -Directory $true) -or
        (Test-UnsafeKnownPath -Path $settingsPath -Directory $false)) {
        Add-Issue -Code 'SCRIPTING_DEFINES_UNSAFE' -Severity 'Warning' `
            -Message 'A hidden or reparse-point ProjectSettings input was not read.' `
            -Path 'ProjectSettings/ProjectSettings.asset'
        return @()
    }

    try {
        $lines = Get-Content -LiteralPath $settingsPath -Encoding UTF8
        $insideDefines = $false
        foreach ($line in $lines) {
            if ($line -match '^\s*scriptingDefineSymbols\s*:\s*(?<value>.*)$') {
                $insideDefines = $true
                $value = $Matches['value']
            }
            elseif ($insideDefines -and $line -match '^\s{4,}[^:]+:\s*(?<value>.*)$') {
                $value = $Matches['value']
            }
            elseif ($insideDefines -and $line -match '^\s{0,2}\S') {
                break
            }
            else {
                continue
            }

            foreach ($token in ($value -split '[;,\s]+')) {
                $candidate = $token.Trim('"', "'")
                if ($candidate -match '^[A-Za-z_][A-Za-z0-9_]*$') {
                    $defines.Add($candidate) | Out-Null
                }
            }
        }
    }
    catch {
        Add-Issue -Code 'SCRIPTING_DEFINES_UNREADABLE' -Severity 'Warning' `
            -Message 'ProjectSettings.asset could not be read for scripting defines.' `
            -Path 'ProjectSettings/ProjectSettings.asset'
    }
    return @($defines | Sort-Object)
}

function Test-ExcludedName {
    param([Parameter(Mandatory = $true)][string]$Name)

    if ($Name.StartsWith('.', [System.StringComparison]::Ordinal)) {
        return $true
    }
    return $Name -match '^(?i:library|temp|logs?|obj|secrets?|credentials?|privatekeys?|keys?)$'
}

function Test-SelectedInspectionFile {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    if (Test-ExcludedName -Name $File.Name) {
        return $false
    }
    if ($File.Name -match '(?i:\.(env|secret|pem|pfx|p12|key|cer|crt)$)' -or
        $File.Name -match '(?i:credential|secret|private.?key|token)') {
        return $false
    }

    if ($File.Name -ceq 'AppUIBindingValidationAttestation.json' -or
        $File.Name -ceq 'AppUIRuntimeValidationAttestation.json') {
        return $true
    }

    $extension = $File.Extension.ToLowerInvariant()
    if ($extension -eq '.cs' -or $extension -eq '.asmdef') {
        return $true
    }
    if ($extension -ne '.asset') {
        return $false
    }
    return $File.Name -match '(?i:appui|uibinding|uipage|uilayer|runtimeprofile|definition|registry|validation)'
}

function New-Candidate {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Symbol = '',
        [Parameter(Mandatory = $true)][string]$Evidence,
        [ValidateSet('Likely', 'Candidate')][string]$Confidence = 'Likely'
    )

    $candidate = [ordered]@{
        path = $Path
        confidence = $Confidence
        evidence = $Evidence
    }
    if (-not [string]::IsNullOrWhiteSpace($Symbol)) {
        $candidate.symbol = $Symbol
    }
    return [pscustomobject]$candidate
}

function Add-UniqueCandidate {
    param(
        [Parameter(Mandatory = $true)]$List,
        [Parameter(Mandatory = $true)]$Keys,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)]$Candidate
    )

    if ($Keys.Add($Key)) {
        $List.Add($Candidate) | Out-Null
    }
}

function Get-ValidationEvidenceState {
    param([Parameter(Mandatory = $true)]$Evidence)

    $hasPassed = $false
    $hasPending = $false
    foreach ($item in $Evidence) {
        if ($item.status -eq 'Failed') {
            return 'Failed'
        }
        if ($item.status -eq 'Passed') {
            $hasPassed = $true
        }
        elseif ($item.status -eq 'Pending') {
            $hasPending = $true
        }
    }
    if ($hasPending) {
        return 'Pending'
    }
    if ($hasPassed) {
        return 'Passed'
    }
    return 'Unknown'
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or
        (Test-UnsafeKnownPath -Path $Path -Directory $false)) {
        return $null
    }

    $stream = $null
    $sha256 = $null
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        $bytes = $sha256.ComputeHash($stream)
        return ([System.BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant())
    }
    catch {
        return $null
    }
    finally {
        if ($null -ne $sha256) {
            $sha256.Dispose()
        }
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Get-ValidationCheckState {
    param(
        [Parameter(Mandatory = $true)]$Evidence,
        [Parameter(Mandatory = $true)][string]$CheckName
    )

    $hasPassed = $false
    $hasPending = $false
    foreach ($item in $Evidence) {
        $value = Get-ObjectPropertyValue -Object $item.checks -Name $CheckName
        if ($value -eq 'Failed') {
            return 'Failed'
        }
        if ($value -eq 'Passed') {
            $hasPassed = $true
        }
        elseif ($value -eq 'Pending') {
            $hasPending = $true
        }
    }
    if ($hasPending) {
        return 'Pending'
    }
    if ($hasPassed) {
        return 'Passed'
    }
    return 'Unknown'
}

function Get-BindingEvidenceAggregateStatus {
    param([Parameter(Mandatory = $true)]$Fields)

    $hasPassed = $false
    $hasPending = $false
    foreach ($name in @('hostBoundariesStatus', 'runtimeRootStatus', 'pageContractStatus', 'bindingStatus')) {
        $value = $Fields[$name]
        if ($value -eq 'Failed') {
            return 'Failed'
        }
        if ($value -eq 'Pending') {
            $hasPending = $true
        }
        elseif ($value -eq 'Passed') {
            $hasPassed = $true
        }
    }
    if ($hasPending) {
        return 'Pending'
    }
    if ($hasPassed) {
        return 'Passed'
    }
    return 'Pending'
}

function Get-ExactObjectPropertyValue {
    param(
        [AllowNull()]$Object,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }
    foreach ($property in $Object.PSObject.Properties) {
        if ($property.Name -ceq $Name) {
            return $property.Value
        }
    }
    return $null
}

function Get-CanonicalValidationAssetsBinding {
    param([Parameter(Mandatory = $true)]$Entries)

    $records = New-Object 'System.Collections.Generic.List[string]'
    foreach ($entry in $Entries) {
        $records.Add(([string]$entry.path + [char]0 + [string]$entry.sha256)) | Out-Null
    }
    $records.Sort([System.StringComparer]::Ordinal)
    $canonical = if ($records.Count -eq 0) {
        ''
    }
    else {
        [string]::Join("`n", $records.ToArray()) + "`n"
    }
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($canonical)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $digestBytes = $sha256.ComputeHash($bytes)
        $digest = [System.BitConverter]::ToString($digestBytes).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
    return [pscustomobject][ordered]@{
        digest = $digest
        fileCount = $records.Count
    }
}

function New-ProjectOwnedValidationAttestation {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('Binding', 'Runtime')][string]$Kind,
        [Parameter(Mandatory = $true)][ValidateSet('Passed', 'Failed', 'Pending')][string]$Status,
        [Parameter(Mandatory = $true)]$ProjectBinding,
        [Parameter(Mandatory = $true)][string]$HostBoundariesStatus,
        [Parameter(Mandatory = $true)][string]$RuntimeRootStatus,
        [Parameter(Mandatory = $true)][string]$PageContractStatus,
        [Parameter(Mandatory = $true)][string]$BindingStatus,
        [Parameter(Mandatory = $true)][string]$RuntimeLifecycleStatus
    )

    $fields = @{
        hostBoundariesStatus = $HostBoundariesStatus
        runtimeRootStatus = $RuntimeRootStatus
        pageContractStatus = $PageContractStatus
        bindingStatus = $BindingStatus
        runtimeLifecycleStatus = $RuntimeLifecycleStatus
    }
    $expectedStatus = if ($Kind -eq 'Binding') {
        Get-BindingEvidenceAggregateStatus -Fields $fields
    }
    else {
        $RuntimeLifecycleStatus
    }
    if ($expectedStatus -eq 'NotRun' -or $Status -ne $expectedStatus) {
        throw 'AttestationStatus is inconsistent with the supplied validation check statuses.'
    }

    return [pscustomobject][ordered]@{
        schemaVersion = $validationEvidenceSchemaVersion
        producer = $validationEvidenceProducer
        owner = $validationEvidenceOwner
        validationKind = $Kind
        status = $Status
        unityVersion = $ProjectBinding.unityVersion
        manifestSha256 = $ProjectBinding.manifestSha256
        packagesLockSha256 = $ProjectBinding.packagesLockSha256
        projectVersionSha256 = $ProjectBinding.projectVersionSha256
        assetsDigestAlgorithm = $validationAssetsDigestAlgorithm
        assetsDigestScope = $validationAssetsDigestScope
        assetsDigestCanonicalization = $validationAssetsDigestCanonicalization
        assetsDigest = $ProjectBinding.assetsDigest
        assetsFileCount = $ProjectBinding.assetsFileCount
        hostBoundariesStatus = $HostBoundariesStatus
        runtimeRootStatus = $RuntimeRootStatus
        pageContractStatus = $PageContractStatus
        bindingStatus = $BindingStatus
        runtimeLifecycleStatus = $RuntimeLifecycleStatus
    }
}

function Test-ValidationEvidence {
    param(
        [Parameter(Mandatory = $true)][string]$Content,
        [Parameter(Mandatory = $true)][ValidateSet('Binding', 'Runtime')][string]$Kind,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)]$ProjectBinding
    )

    $document = $null
    try {
        $document = $Content | ConvertFrom-Json
    }
    catch {
        $document = $null
    }
    $reason = $null
    if ((Get-ExactObjectPropertyValue -Object $document -Name 'schemaVersion') -cne
        $validationEvidenceSchemaVersion) {
        $reason = 'SchemaMismatch'
    }
    elseif ((Get-ExactObjectPropertyValue -Object $document -Name 'producer') -cne
        $validationEvidenceProducer) {
        $reason = 'ProducerMismatch'
    }
    elseif ((Get-ExactObjectPropertyValue -Object $document -Name 'owner') -cne $validationEvidenceOwner) {
        $reason = 'OwnerMismatch'
    }
    elseif ((Get-ExactObjectPropertyValue -Object $document -Name 'validationKind') -cne $Kind) {
        $reason = 'KindMismatch'
    }
    elseif ((Get-ExactObjectPropertyValue -Object $document -Name 'unityVersion') -cne $ProjectBinding.unityVersion -or
        (Get-ExactObjectPropertyValue -Object $document -Name 'manifestSha256') -cne $ProjectBinding.manifestSha256 -or
        (Get-ExactObjectPropertyValue -Object $document -Name 'packagesLockSha256') -cne $ProjectBinding.packagesLockSha256 -or
        (Get-ExactObjectPropertyValue -Object $document -Name 'projectVersionSha256') -cne $ProjectBinding.projectVersionSha256) {
        $reason = 'ProjectFactsMismatch'
    }
    elseif ($ProjectBinding.scanComplete -and
        ((Get-ExactObjectPropertyValue -Object $document -Name 'assetsDigestAlgorithm') -cne
            $validationAssetsDigestAlgorithm -or
        (Get-ExactObjectPropertyValue -Object $document -Name 'assetsDigestScope') -cne
            $validationAssetsDigestScope -or
        (Get-ExactObjectPropertyValue -Object $document -Name 'assetsDigestCanonicalization') -cne
            $validationAssetsDigestCanonicalization -or
        (Get-ExactObjectPropertyValue -Object $document -Name 'assetsDigest') -cne $ProjectBinding.assetsDigest -or
        [string](Get-ExactObjectPropertyValue -Object $document -Name 'assetsFileCount') -cne
            [string]$ProjectBinding.assetsFileCount)) {
        $reason = 'AssetsDigestMismatch'
    }
    else {
        $status = [string](Get-ExactObjectPropertyValue -Object $document -Name 'status')
        if ($status -notmatch '^(?:Passed|Failed|Pending)$') {
            $reason = 'ContractInvalid'
        }
        $fields = @{}
        if ($null -eq $reason) {
            foreach ($name in @('hostBoundariesStatus', 'runtimeRootStatus', 'pageContractStatus',
                'bindingStatus', 'runtimeLifecycleStatus')) {
                $value = [string](Get-ExactObjectPropertyValue -Object $document -Name $name)
                if ($value -notmatch '^(?:Passed|Failed|Pending|NotRun)$') {
                    $reason = 'ContractInvalid'
                    break
                }
                $fields[$name] = $value
            }
        }
        if ($null -eq $reason) {
            $expectedStatus = if ($Kind -eq 'Binding') {
                Get-BindingEvidenceAggregateStatus -Fields $fields
            }
            else {
                $fields['runtimeLifecycleStatus']
            }
            if ($expectedStatus -eq 'NotRun' -or $status -ne $expectedStatus) {
                $reason = 'ContractInvalid'
            }
        }
    }

    if ($null -ne $reason) {
        return [pscustomobject][ordered]@{
            accepted = $false
            rejectedEvidence = [pscustomobject][ordered]@{
                path = $RelativePath
                reason = $reason
            }
        }
    }

    return [pscustomobject][ordered]@{
        accepted = $true
        evidence = [pscustomobject][ordered]@{
            path = $RelativePath
            status = $status
            binding = if ($ProjectBinding.scanComplete) { 'Bound' } else { 'Indeterminate' }
            schemaVersion = Get-ExactObjectPropertyValue -Object $document -Name 'schemaVersion'
            producer = Get-ExactObjectPropertyValue -Object $document -Name 'producer'
            owner = Get-ExactObjectPropertyValue -Object $document -Name 'owner'
            validationKind = Get-ExactObjectPropertyValue -Object $document -Name 'validationKind'
            assetsDigest = Get-ExactObjectPropertyValue -Object $document -Name 'assetsDigest'
            assetsFileCount = Get-ExactObjectPropertyValue -Object $document -Name 'assetsFileCount'
            checks = [pscustomobject][ordered]@{
                hostBoundariesStatus = $fields['hostBoundariesStatus']
                runtimeRootStatus = $fields['runtimeRootStatus']
                pageContractStatus = $fields['pageContractStatus']
                bindingStatus = $fields['bindingStatus']
                runtimeLifecycleStatus = $fields['runtimeLifecycleStatus']
            }
        }
    }
}

function New-EmptyCandidates {
    return New-Object 'System.Collections.Generic.List[object]'
}

function New-CandidateKeys {
    return New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
}

function Get-EmptyPackageFacts {
    param([Parameter(Mandatory = $true)][string]$Name)

    return [pscustomobject][ordered]@{
        name = $Name
        installed = $false
        manifestReference = $null
        lockReference = $null
        version = $null
        source = $null
    }
}

$requestedPath = try {
    [System.IO.Path]::GetFullPath($ProjectPath)
}
catch {
    $ProjectPath
}
$resolvedOutputPath = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $null
}
else {
    [System.IO.Path]::GetFullPath($OutputPath)
}
$unityRoot = Resolve-UnityProjectRoot -StartPath $ProjectPath

$projectFacts = [ordered]@{
    requestedPath = $requestedPath
    root = $unityRoot
    unityVersion = $null
    maxSourceFiles = $MaxSourceFiles
    scannedFileCount = 0
    sourceFileLimitReached = $false
    maxDirectories = [Math]::Max(32, ($MaxSourceFiles * 4))
    visitedDirectoryCount = 0
    directoryLimitReached = $false
    maxEnumeratedEntries = [Math]::Max(64, ($MaxSourceFiles * 16))
    enumeratedEntryCount = 0
    enumeratedFileEntryCount = 0
    enumeratedDirectoryEntryCount = 0
    entryLimitReached = $false
    scanLimitReached = $false
    scanIndeterminate = $false
    scanComplete = $false
    validationRelevantAssetsDigest = $null
    validationRelevantAssetCount = 0
}
$emptyAppUIFacts = [pscustomobject][ordered]@{
    installed = $false
    manifestReference = $null
    lockReference = $null
    version = $null
    installSource = 'Unknown'
    gitRef = $null
    gitRefKind = $null
    mutable = $null
    immutability = 'Unknown'
    tagIdentityVerified = $false
}
$packageFacts = [ordered]@{
    appUI = $emptyAppUIFacts
    ugui = Get-EmptyPackageFacts -Name 'com.unity.ugui'
    textMeshPro = Get-EmptyPackageFacts -Name 'com.unity.textmeshpro'
    asyncCandidates = @()
    assetCandidates = @()
}
$integrationFacts = [ordered]@{
    defines = @()
    asmdefDefineConstraints = @()
    textMeshProDefineEnabled = $false
    composition = [ordered]@{
        runtimeHost = [ordered]@{ present = $false; candidates = @() }
        appUIManager = [ordered]@{ present = $false; candidates = @() }
    }
    hostBoundaries = [ordered]@{
        operationFactory = [ordered]@{ present = $false; candidates = @() }
        assetProvider = [ordered]@{ present = $false; candidates = @() }
        executionContext = [ordered]@{ present = $false; candidates = @() }
        candidateCoverageComplete = $false
        complete = $false
    }
    runtimeRoot = [ordered]@{
        candidateCoverageComplete = $false
        complete = $false
        layerRoots = @()
        profiles = @()
        registries = @()
    }
    pageContract = [ordered]@{
        candidateCoverageComplete = $false
        complete = $false
        controllers = @()
        definitions = @()
    }
    binding = [ordered]@{
        settings = @()
        generatedBindings = @()
        candidateCoverageComplete = $false
        generationComplete = $false
    }
    validation = [ordered]@{
        contract = [ordered]@{
            schemaVersion = $validationEvidenceSchemaVersion
            producer = $validationEvidenceProducer
            owner = $validationEvidenceOwner
            binding = 'UnityVersion+ManifestSha256+PackagesLockSha256+ProjectVersionSha256+AssetsDigest'
            assetsDigestAlgorithm = $validationAssetsDigestAlgorithm
            assetsDigestScope = $validationAssetsDigestScope
            assetsDigestCanonicalization = $validationAssetsDigestCanonicalization
        }
        binding = [ordered]@{ status = 'Unknown'; evidence = @(); rejectedEvidence = @() }
        runtime = [ordered]@{ status = 'Unknown'; evidence = @(); rejectedEvidence = @() }
    }
}
$sampleFacts = [ordered]@{
    basicIntegration = [ordered]@{ imported = $false; paths = @() }
    customHostIntegration = [ordered]@{ imported = $false; paths = @() }
    textMeshProIntegration = [ordered]@{ imported = $false; paths = @() }
}
$status = 'NotAUnityProject'

if ($null -eq $unityRoot) {
    Add-Issue -Code 'UNITY_ROOT_NOT_FOUND' -Severity 'Error' `
        -Message 'No parent contains the Unity Assets, Packages and ProjectSettings directories.'
}
else {
    $projectVersionPath = Join-Path $unityRoot 'ProjectSettings\ProjectVersion.txt'
    $projectSettingsDirectory = Join-Path $unityRoot 'ProjectSettings'
    if ((Test-Path -LiteralPath $projectVersionPath -PathType Leaf) -and
        -not (Test-UnsafeKnownPath -Path $projectSettingsDirectory -Directory $true) -and
        -not (Test-UnsafeKnownPath -Path $projectVersionPath -Directory $false)) {
        try {
            $projectVersionText = Get-Content -LiteralPath $projectVersionPath -Raw -Encoding UTF8
            if ($projectVersionText -match '(?m)^m_EditorVersion:\s*(?<version>\S+)\s*$') {
                $projectFacts.unityVersion = $Matches['version']
            }
        }
        catch {
            Add-Issue -Code 'UNITY_VERSION_FILE_UNREADABLE' -Severity 'Warning' `
                -Message 'ProjectVersion.txt could not be read.' `
                -Path 'ProjectSettings/ProjectVersion.txt'
        }
    }
    if ([string]::IsNullOrWhiteSpace([string]$projectFacts.unityVersion)) {
        Add-Issue -Code 'UNITY_VERSION_UNVERIFIED' -Severity 'Error' `
            -Message 'Unity Editor version could not be verified from ProjectVersion.txt.' `
            -Path 'ProjectSettings/ProjectVersion.txt'
    }

    $manifestPath = Join-Path $unityRoot 'Packages\manifest.json'
    $lockPath = Join-Path $unityRoot 'Packages\packages-lock.json'
    $packagesDirectory = Join-Path $unityRoot 'Packages'
    $packageInputsSafe = -not (Test-UnsafeKnownPath -Path $packagesDirectory -Directory $true)
    if (-not $packageInputsSafe) {
        Add-Issue -Code 'PACKAGE_DIRECTORY_UNSAFE' -Severity 'Warning' `
            -Message 'A hidden or reparse-point Packages directory was not read.' -Path 'Packages'
        $manifest = $null
        $lock = $null
    }
    else {
        $manifest = Read-JsonFile -Path $manifestPath -FailureCode 'PACKAGE_MANIFEST_INVALID' `
            -RelativePath 'Packages/manifest.json'
        $lock = Read-JsonFile -Path $lockPath -FailureCode 'PACKAGE_LOCK_INVALID' `
            -RelativePath 'Packages/packages-lock.json'
    }
    $manifestDependencies = Get-DependencyMap -Json $manifest
    $lockDependencies = Get-DependencyMap -Json $lock
    $projectBinding = [pscustomobject][ordered]@{
        unityVersion = $projectFacts.unityVersion
        manifestSha256 = if ($packageInputsSafe) { Get-FileSha256 -Path $manifestPath } else { $null }
        packagesLockSha256 = if ($packageInputsSafe) { Get-FileSha256 -Path $lockPath } else { $null }
        projectVersionSha256 = Get-FileSha256 -Path $projectVersionPath
        assetsDigest = $null
        assetsFileCount = 0
        scanComplete = $false
    }

    $appUIManifestReference = if ($manifestDependencies.ContainsKey('com.joih.appui')) {
        [string]$manifestDependencies['com.joih.appui']
    }
    else {
        $null
    }
    $appUILockEntry = if ($lockDependencies.ContainsKey('com.joih.appui')) {
        $lockDependencies['com.joih.appui']
    }
    else {
        $null
    }
    $packageFacts.appUI = Get-AppUIPackageFacts -ManifestReference $appUIManifestReference -LockEntry $appUILockEntry
    $packageFacts.ugui = Get-PackageFacts -Name 'com.unity.ugui' `
        -ManifestDependencies $manifestDependencies -LockDependencies $lockDependencies
    $packageFacts.textMeshPro = Get-PackageFacts -Name 'com.unity.textmeshpro' `
        -ManifestDependencies $manifestDependencies -LockDependencies $lockDependencies
    $packageFacts.asyncCandidates = @(Get-PackageCandidates -ManifestDependencies $manifestDependencies `
        -LockDependencies $lockDependencies -Kind Async)
    $packageFacts.assetCandidates = @(Get-PackageCandidates -ManifestDependencies $manifestDependencies `
        -LockDependencies $lockDependencies -Kind Asset)

    if ($packageFacts.appUI.installed -and $packageFacts.appUI.immutability -eq 'Mutable') {
        Add-Issue -Code 'APPUI_MUTABLE_REFERENCE' -Severity 'Warning' `
            -Message 'The installed AppUI reference is mutable or cannot be proven immutable.' `
            -Path 'Packages/manifest.json'
    }
    elseif ($packageFacts.appUI.installed -and $packageFacts.appUI.gitRefKind -eq 'TagCandidate') {
        Add-Issue -Code 'APPUI_TAG_IDENTITY_UNVERIFIED' -Severity 'Info' `
            -Message 'The SemVer-shaped Git fragment is only a Tag candidate until its remote identity is verified.' `
            -Path 'Packages/manifest.json'
    }

    $defines = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    foreach ($define in @(Get-ScriptingDefines -UnityRoot $unityRoot)) {
        $defines.Add([string]$define) | Out-Null
    }
    $asmdefDefineConstraints = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)

    $operationCandidates = New-EmptyCandidates
    $operationKeys = New-CandidateKeys
    $assetProviderCandidates = New-EmptyCandidates
    $assetProviderKeys = New-CandidateKeys
    $executionCandidates = New-EmptyCandidates
    $executionKeys = New-CandidateKeys
    $hostCandidates = New-EmptyCandidates
    $hostKeys = New-CandidateKeys
    $managerCandidates = New-EmptyCandidates
    $managerKeys = New-CandidateKeys
    $layerCandidates = New-EmptyCandidates
    $layerKeys = New-CandidateKeys
    $profileCandidates = New-EmptyCandidates
    $profileKeys = New-CandidateKeys
    $registryCandidates = New-EmptyCandidates
    $registryKeys = New-CandidateKeys
    $controllerCandidates = New-EmptyCandidates
    $controllerKeys = New-CandidateKeys
    $definitionCandidates = New-EmptyCandidates
    $definitionKeys = New-CandidateKeys
    $bindingSettingsCandidates = New-EmptyCandidates
    $bindingSettingsKeys = New-CandidateKeys
    $generatedBindingCandidates = New-EmptyCandidates
    $generatedBindingKeys = New-CandidateKeys
    $bindingValidationEvidence = New-Object 'System.Collections.Generic.List[object]'
    $runtimeValidationEvidence = New-Object 'System.Collections.Generic.List[object]'
    $bindingRejectedEvidence = New-Object 'System.Collections.Generic.List[object]'
    $runtimeRejectedEvidence = New-Object 'System.Collections.Generic.List[object]'
    $bindingAttestationInputs = New-Object 'System.Collections.Generic.List[object]'
    $runtimeAttestationInputs = New-Object 'System.Collections.Generic.List[object]'
    $validationAssetEntries = New-Object 'System.Collections.Generic.List[object]'
    $basicSamplePaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $customSamplePaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $tmpSamplePaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

    $assetsRoot = Join-Path $unityRoot 'Assets'
    $priorityDirectories = New-Object 'System.Collections.Generic.Queue[string]'
    $pendingDirectories = New-Object 'System.Collections.Generic.Queue[string]'
    $pendingDirectories.Enqueue($assetsRoot)
    $stopScan = $false

    while (($priorityDirectories.Count -gt 0 -or $pendingDirectories.Count -gt 0) -and -not $stopScan) {
        if ($projectFacts.visitedDirectoryCount -ge $projectFacts.maxDirectories) {
            $projectFacts.directoryLimitReached = $true
            break
        }

        $directory = if ($priorityDirectories.Count -gt 0) {
            $priorityDirectories.Dequeue()
        }
        else {
            $pendingDirectories.Dequeue()
        }
        $projectFacts.visitedDirectoryCount++
        $directoryInfo = New-Object System.IO.DirectoryInfo($directory)
        if (($directoryInfo.Attributes -band [System.IO.FileAttributes]::Hidden) -ne 0) {
            continue
        }
        if (($directoryInfo.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            $relativeReparse = Convert-ToPortableRelativePath -Root $unityRoot -Path $directory
            Add-Issue -Code 'REPARSE_POINT_SKIPPED' -Severity 'Info' `
                -Message 'A reparse-point directory under Assets was not traversed.' -Path $relativeReparse
            continue
        }

        $relativeDirectory = Convert-ToPortableRelativePath -Root $unityRoot -Path $directory
        if ($relativeDirectory -match '^(?<root>Assets/Samples/[^/]+/[^/]+/Basic Integration)(?:/|$)') {
            $basicSamplePaths.Add($Matches['root']) | Out-Null
        }
        elseif ($relativeDirectory -match '^(?<root>Assets/Samples/[^/]+/[^/]+/Custom Host Integration)(?:/|$)') {
            $customSamplePaths.Add($Matches['root']) | Out-Null
        }
        elseif ($relativeDirectory -match '^(?<root>Assets/Samples/[^/]+/[^/]+/TextMeshPro Integration)(?:/|$)') {
            $tmpSamplePaths.Add($Matches['root']) | Out-Null
        }

        $entryEnumerator = $null
        try {
            $entryEnumerator = [System.IO.Directory]::EnumerateFileSystemEntries($directory).GetEnumerator()
            while (-not $stopScan) {
                if (-not $entryEnumerator.MoveNext()) {
                    break
                }
                if ($projectFacts.enumeratedEntryCount -ge $projectFacts.maxEnumeratedEntries) {
                    $projectFacts.entryLimitReached = $true
                    $stopScan = $true
                    break
                }
                $entryPath = $entryEnumerator.Current
                $entryAttributes = [System.IO.File]::GetAttributes($entryPath)
                $projectFacts.enumeratedEntryCount++
                if (($entryAttributes -band [System.IO.FileAttributes]::Directory) -ne 0) {
                    $projectFacts.enumeratedDirectoryEntryCount++
                    $subdirectoryInfo = New-Object System.IO.DirectoryInfo($entryPath)
                    if (Test-ExcludedName -Name $subdirectoryInfo.Name) {
                        continue
                    }
                    if (($entryAttributes -band [System.IO.FileAttributes]::Hidden) -ne 0) {
                        continue
                    }
                    if (($entryAttributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                        $relativeReparse = Convert-ToPortableRelativePath -Root $unityRoot -Path $subdirectoryInfo.FullName
                        Add-Issue -Code 'REPARSE_POINT_SKIPPED' -Severity 'Info' `
                            -Message 'A reparse-point directory under Assets was not traversed.' -Path $relativeReparse
                        continue
                    }
                    if ($subdirectoryInfo.FullName -match '(?i:[\\/]appui(?:[\\/]|$))') {
                        $priorityDirectories.Enqueue($subdirectoryInfo.FullName)
                    }
                    else {
                        $pendingDirectories.Enqueue($subdirectoryInfo.FullName)
                    }
                    continue
                }

                $projectFacts.enumeratedFileEntryCount++
            $fileInfo = New-Object System.IO.FileInfo($entryPath)
            if (($fileInfo.Attributes -band [System.IO.FileAttributes]::Hidden) -ne 0) {
                continue
            }
            if (($fileInfo.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                $relativeReparse = Convert-ToPortableRelativePath -Root $unityRoot -Path $fileInfo.FullName
                Add-Issue -Code 'REPARSE_POINT_SKIPPED' -Severity 'Info' `
                    -Message 'A reparse-point file under Assets was not read.' -Path $relativeReparse
                continue
            }
            if (-not (Test-SelectedInspectionFile -File $fileInfo)) {
                continue
            }
            if ($projectFacts.scannedFileCount -ge $MaxSourceFiles) {
                $projectFacts.sourceFileLimitReached = $true
                $stopScan = $true
                break
            }

            $projectFacts.scannedFileCount++
            $relativePath = Convert-ToPortableRelativePath -Root $unityRoot -Path $fileInfo.FullName
            if ($null -eq $relativePath) {
                Add-Issue -Code 'SOURCE_PATH_ESCAPE_BLOCKED' -Severity 'Warning' `
                    -Message 'A discovered file resolved outside the Unity project and was skipped.'
                continue
            }
            if ($fileInfo.Length -gt 2097152) {
                $projectFacts.scanIndeterminate = $true
                Add-Issue -Code 'SOURCE_FILE_TOO_LARGE' -Severity 'Warning' `
                    -Message 'A selected inspection file exceeded the 2 MiB read limit.' -Path $relativePath
                continue
            }

            try {
                $content = [System.IO.File]::ReadAllText($fileInfo.FullName)
            }
            catch {
                $projectFacts.scanIndeterminate = $true
                Add-Issue -Code 'SOURCE_FILE_UNREADABLE' -Severity 'Warning' `
                    -Message 'A selected inspection file could not be read.' -Path $relativePath
                continue
            }

            $isImportedAppUISample = $relativePath -match `
                '^Assets/Samples/[^/]+/[^/]+/(?:Basic Integration|Custom Host Integration|TextMeshPro Integration)(?:/|$)'
            $isBindingAttestation = $fileInfo.Name -ceq 'AppUIBindingValidationAttestation.json'
            $isRuntimeAttestation = $fileInfo.Name -ceq 'AppUIRuntimeValidationAttestation.json'
            $isAttestationMetadata = ($isBindingAttestation -or $isRuntimeAttestation -or
                $fileInfo.Name -ceq 'AppUIBindingValidationEvidence.asset' -or
                $fileInfo.Name -ceq 'AppUIRuntimeValidationEvidence.asset')
            if (-not $isImportedAppUISample -and -not $isAttestationMetadata) {
                $assetSha256 = Get-FileSha256 -Path $fileInfo.FullName
                if ([string]::IsNullOrWhiteSpace([string]$assetSha256)) {
                    $projectFacts.scanIndeterminate = $true
                    Add-Issue -Code 'SOURCE_FILE_HASH_UNAVAILABLE' -Severity 'Warning' `
                        -Message 'A selected inspection file could not be bound into the canonical Assets digest.' `
                        -Path $relativePath
                }
                else {
                    $validationAssetEntries.Add([pscustomobject][ordered]@{
                        path = $relativePath
                        sha256 = $assetSha256
                    }) | Out-Null
                }
            }
            if ($isBindingAttestation) {
                $bindingAttestationInputs.Add([pscustomobject][ordered]@{
                    path = $relativePath
                    content = $content
                }) | Out-Null
            }
            elseif ($isRuntimeAttestation) {
                $runtimeAttestationInputs.Add([pscustomobject][ordered]@{
                    path = $relativePath
                    content = $content
                }) | Out-Null
            }

            if ($fileInfo.Extension -eq '.asmdef') {
                try {
                    $asmdef = $content | ConvertFrom-Json
                    $constraints = Get-ObjectPropertyValue -Object $asmdef -Name 'defineConstraints'
                    foreach ($constraint in @($constraints)) {
                        $cleanConstraint = ([string]$constraint).TrimStart('!')
                        if ($cleanConstraint -match '^[A-Za-z_][A-Za-z0-9_]*$') {
                            $asmdefDefineConstraints.Add($cleanConstraint) | Out-Null
                        }
                    }
                }
                catch {
                    Add-Issue -Code 'ASMDEF_INVALID' -Severity 'Info' `
                        -Message 'A selected asmdef could not be parsed.' -Path $relativePath
                }
            }

            if ($isImportedAppUISample) {
                continue
            }

            if ($fileInfo.Extension -eq '.cs') {
                $typeMatches = [regex]::Matches($content,
                    '(?ms)\b(?:public\s+|internal\s+|private\s+|protected\s+|sealed\s+|abstract\s+|partial\s+|static\s+)*(?:class|struct)\s+(?<name>[A-Za-z_]\w*)(?:\s*<[^>{}]+>)?\s*:\s*(?<bases>[^\{]+)\{')
                foreach ($typeMatch in $typeMatches) {
                    $symbol = $typeMatch.Groups['name'].Value
                    $bases = $typeMatch.Groups['bases'].Value
                    if ($bases -match '\bIUIOperationFactory\b') {
                        Add-UniqueCandidate -List $operationCandidates -Keys $operationKeys `
                            -Key ($relativePath + '|' + $symbol) `
                            -Candidate (New-Candidate -Path $relativePath -Symbol $symbol `
                                -Evidence 'declares IUIOperationFactory')
                    }
                    if ($bases -match '\bIUIAssetProvider\b') {
                        Add-UniqueCandidate -List $assetProviderCandidates -Keys $assetProviderKeys `
                            -Key ($relativePath + '|' + $symbol) `
                            -Candidate (New-Candidate -Path $relativePath -Symbol $symbol `
                                -Evidence 'declares IUIAssetProvider')
                    }
                    if ($bases -match '\bIAppUIExecutionContext\b') {
                        Add-UniqueCandidate -List $executionCandidates -Keys $executionKeys `
                            -Key ($relativePath + '|' + $symbol) `
                            -Candidate (New-Candidate -Path $relativePath -Symbol $symbol `
                                -Evidence 'declares IAppUIExecutionContext')
                    }
                    if ($bases -match '\bPanelBaseController\b') {
                        Add-UniqueCandidate -List $controllerCandidates -Keys $controllerKeys `
                            -Key ($relativePath + '|' + $symbol) `
                            -Candidate (New-Candidate -Path $relativePath -Symbol $symbol `
                                -Evidence 'derives from PanelBaseController')
                    }
                }

                if ($content -match '\bAppUIRuntimeHost\b') {
                    Add-UniqueCandidate -List $hostCandidates -Keys $hostKeys -Key $relativePath `
                        -Candidate (New-Candidate -Path $relativePath -Evidence 'references AppUIRuntimeHost')
                }
                if ($content -match '\bAppUIManager\b') {
                    Add-UniqueCandidate -List $managerCandidates -Keys $managerKeys -Key $relativePath `
                        -Candidate (New-Candidate -Path $relativePath -Evidence 'references AppUIManager')
                }
                if ($content -match '\bUILayerRoot\b') {
                    Add-UniqueCandidate -List $layerCandidates -Keys $layerKeys -Key $relativePath `
                        -Candidate (New-Candidate -Path $relativePath -Evidence 'references UILayerRoot')
                }
                if ($fileInfo.Name -match '(?i:\.Bindings\.cs)$') {
                    Add-UniqueCandidate -List $generatedBindingCandidates -Keys $generatedBindingKeys `
                        -Key $relativePath `
                        -Candidate (New-Candidate -Path $relativePath -Evidence 'matches generated .Bindings.cs output')
                }
            }

            if ($fileInfo.Extension -eq '.asset') {
                if ($fileInfo.Name -match '(?i:runtimeprofile)' -or $content -match '\bAppUIRuntimeProfile\b') {
                    Add-UniqueCandidate -List $profileCandidates -Keys $profileKeys -Key $relativePath `
                        -Candidate (New-Candidate -Path $relativePath -Evidence 'matches AppUIRuntimeProfile asset')
                }
                if ($fileInfo.Name -match '(?i:registry)' -or $content -match '\bUIPageDefinitionRegistry\b') {
                    Add-UniqueCandidate -List $registryCandidates -Keys $registryKeys -Key $relativePath `
                        -Candidate (New-Candidate -Path $relativePath -Evidence 'matches UIPageDefinitionRegistry asset')
                }
                if (($fileInfo.Name -match '(?i:definition)') -and
                    ($fileInfo.Name -notmatch '(?i:registry)') -or
                    ($content -match '\bUIPageDefinition\b' -and $content -notmatch '\bUIPageDefinitionRegistry\b')) {
                    Add-UniqueCandidate -List $definitionCandidates -Keys $definitionKeys -Key $relativePath `
                        -Candidate (New-Candidate -Path $relativePath -Evidence 'matches UIPageDefinition asset')
                }
                if ($fileInfo.Name -match '(?i:binding.*settings|settings.*binding)' -or
                    $content -match '\bUIBindingSettings\b') {
                    Add-UniqueCandidate -List $bindingSettingsCandidates -Keys $bindingSettingsKeys `
                        -Key $relativePath `
                        -Candidate (New-Candidate -Path $relativePath -Evidence 'matches UIBindingSettings asset')
                }
            }
            }
        }
        catch {
            $projectFacts.scanIndeterminate = $true
            Add-Issue -Code 'SOURCE_DIRECTORY_UNREADABLE' -Severity 'Warning' `
                -Message 'An Assets directory entry could not be enumerated.' -Path $relativeDirectory
        }
        finally {
            if ($null -ne $entryEnumerator) {
                $entryEnumerator.Dispose()
            }
        }
    }

    $projectFacts.scanLimitReached = ($projectFacts.sourceFileLimitReached -or
        $projectFacts.directoryLimitReached -or $projectFacts.entryLimitReached)
    $projectFacts.scanComplete = (-not $projectFacts.scanLimitReached -and
        -not $projectFacts.scanIndeterminate)
    if ($projectFacts.sourceFileLimitReached) {
        Add-Issue -Code 'SOURCE_SCAN_LIMIT_REACHED' -Severity 'Warning' `
            -Message ("Assets inspection stopped at MaxSourceFiles={0}." -f $MaxSourceFiles)
    }
    if ($projectFacts.directoryLimitReached) {
        Add-Issue -Code 'SOURCE_DIRECTORY_LIMIT_REACHED' -Severity 'Warning' `
            -Message ("Assets traversal stopped at MaxDirectories={0}." -f $projectFacts.maxDirectories)
    }
    if ($projectFacts.entryLimitReached) {
        Add-Issue -Code 'SOURCE_ENTRY_LIMIT_REACHED' -Severity 'Warning' `
            -Message ("Assets traversal stopped at MaxEnumeratedEntries={0}." -f $projectFacts.maxEnumeratedEntries)
    }
    if ($projectFacts.scanIndeterminate) {
        Add-Issue -Code 'SOURCE_SCAN_INDETERMINATE' -Severity 'Warning' `
            -Message 'One or more selected inputs or directory enumerations could not be inspected completely.'
    }

    $assetsBinding = Get-CanonicalValidationAssetsBinding -Entries $validationAssetEntries
    $projectFacts.validationRelevantAssetsDigest = $assetsBinding.digest
    $projectFacts.validationRelevantAssetCount = $assetsBinding.fileCount
    $projectBinding.assetsDigest = $assetsBinding.digest
    $projectBinding.assetsFileCount = $assetsBinding.fileCount
    $projectBinding.scanComplete = $projectFacts.scanComplete

    if ($CreateAttestation -ne 'None') {
        if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
            throw 'OutputPath cannot be combined with CreateAttestation.'
        }
        if (-not $packageFacts.appUI.installed) {
            throw 'CreateAttestation requires an installed com.joih.appui package.'
        }
        if ($AttestationStatus -eq 'Unspecified') {
            throw 'CreateAttestation requires an explicit AttestationStatus outcome.'
        }
        if (-not $projectFacts.scanComplete) {
            throw 'CreateAttestation requires a complete, determinate Assets scan.'
        }
        foreach ($bindingValue in @($projectBinding.unityVersion, $projectBinding.manifestSha256,
            $projectBinding.packagesLockSha256, $projectBinding.projectVersionSha256,
            $projectBinding.assetsDigest)) {
            if ([string]::IsNullOrWhiteSpace([string]$bindingValue)) {
                throw 'CreateAttestation could not bind all required Unity, package, and Assets facts.'
            }
        }

        $attestation = New-ProjectOwnedValidationAttestation -Kind $CreateAttestation `
            -Status $AttestationStatus -ProjectBinding $projectBinding `
            -HostBoundariesStatus $AttestationHostBoundariesStatus `
            -RuntimeRootStatus $AttestationRuntimeRootStatus `
            -PageContractStatus $AttestationPageContractStatus `
            -BindingStatus $AttestationBindingStatus `
            -RuntimeLifecycleStatus $AttestationRuntimeLifecycleStatus
        $attestationName = if ($CreateAttestation -eq 'Binding') {
            'AppUIBindingValidationAttestation.json'
        }
        else {
            'AppUIRuntimeValidationAttestation.json'
        }
        $attestationPath = Join-Path $unityRoot (Join-Path 'Assets\AppUI\Validation' $attestationName)
        if (Test-Path -LiteralPath $attestationPath) {
            throw ("Attestation target already exists: {0}" -f $attestationPath)
        }
        Assert-NoReparsePath -Path $attestationPath
        $attestationDirectory = Split-Path -Parent $attestationPath
        [System.IO.Directory]::CreateDirectory($attestationDirectory) | Out-Null
        Assert-NoReparsePath -Path $attestationPath
        $attestationJson = $attestation | ConvertTo-Json -Depth 12
        Write-Utf8NoBomNewFile -Path $attestationPath -Content $attestationJson
        Write-Output $attestationJson
        return
    }

    foreach ($input in $bindingAttestationInputs) {
        $validationResult = Test-ValidationEvidence -Content $input.content -Kind Binding `
            -RelativePath $input.path -ProjectBinding $projectBinding
        if ($validationResult.accepted) {
            $bindingValidationEvidence.Add($validationResult.evidence) | Out-Null
        }
        else {
            $bindingRejectedEvidence.Add($validationResult.rejectedEvidence) | Out-Null
        }
    }
    foreach ($input in $runtimeAttestationInputs) {
        $validationResult = Test-ValidationEvidence -Content $input.content -Kind Runtime `
            -RelativePath $input.path -ProjectBinding $projectBinding
        if ($validationResult.accepted) {
            $runtimeValidationEvidence.Add($validationResult.evidence) | Out-Null
        }
        else {
            $runtimeRejectedEvidence.Add($validationResult.rejectedEvidence) | Out-Null
        }
    }

    $integrationFacts.defines = @($defines | Sort-Object)
    $integrationFacts.asmdefDefineConstraints = @($asmdefDefineConstraints | Sort-Object)
    $integrationFacts.textMeshProDefineEnabled = $defines.Contains('JOIH_APPUI_TMP')
    $integrationFacts.composition.runtimeHost = [ordered]@{
        present = $hostCandidates.Count -gt 0
        candidates = @($hostCandidates.ToArray())
    }
    $integrationFacts.composition.appUIManager = [ordered]@{
        present = $managerCandidates.Count -gt 0
        candidates = @($managerCandidates.ToArray())
    }
    $integrationFacts.hostBoundaries.operationFactory = [ordered]@{
        present = $operationCandidates.Count -gt 0
        candidates = @($operationCandidates.ToArray())
    }
    $integrationFacts.hostBoundaries.assetProvider = [ordered]@{
        present = $assetProviderCandidates.Count -gt 0
        candidates = @($assetProviderCandidates.ToArray())
    }
    $integrationFacts.hostBoundaries.executionContext = [ordered]@{
        present = $executionCandidates.Count -gt 0
        candidates = @($executionCandidates.ToArray())
    }
    $hostBoundariesCandidateCoverage = ($operationCandidates.Count -gt 0 -and
        $assetProviderCandidates.Count -gt 0 -and $executionCandidates.Count -gt 0)
    $integrationFacts.hostBoundaries.candidateCoverageComplete = $hostBoundariesCandidateCoverage

    $integrationFacts.runtimeRoot.layerRoots = @($layerCandidates.ToArray())
    $integrationFacts.runtimeRoot.profiles = @($profileCandidates.ToArray())
    $integrationFacts.runtimeRoot.registries = @($registryCandidates.ToArray())
    $runtimeRootCandidateCoverage = ($hostCandidates.Count -gt 0 -and $managerCandidates.Count -gt 0 -and
        $layerCandidates.Count -gt 0 -and $profileCandidates.Count -gt 0 -and $registryCandidates.Count -gt 0)
    $integrationFacts.runtimeRoot.candidateCoverageComplete = $runtimeRootCandidateCoverage
    $integrationFacts.pageContract.controllers = @($controllerCandidates.ToArray())
    $integrationFacts.pageContract.definitions = @($definitionCandidates.ToArray())
    $pageContractCandidateCoverage = ($controllerCandidates.Count -gt 0 -and $definitionCandidates.Count -gt 0)
    $integrationFacts.pageContract.candidateCoverageComplete = $pageContractCandidateCoverage
    $integrationFacts.binding.settings = @($bindingSettingsCandidates.ToArray())
    $integrationFacts.binding.generatedBindings = @($generatedBindingCandidates.ToArray())
    $bindingCandidateCoverage = ($bindingSettingsCandidates.Count -gt 0 -and $generatedBindingCandidates.Count -gt 0)
    $integrationFacts.binding.candidateCoverageComplete = $bindingCandidateCoverage

    $bindingValidationStatus = Get-ValidationEvidenceState -Evidence $bindingValidationEvidence
    $runtimeValidationStatus = Get-ValidationEvidenceState -Evidence $runtimeValidationEvidence
    $hostBoundariesValidationStatus = Get-ValidationCheckState -Evidence $bindingValidationEvidence `
        -CheckName 'hostBoundariesStatus'
    $runtimeRootValidationStatus = Get-ValidationCheckState -Evidence $bindingValidationEvidence `
        -CheckName 'runtimeRootStatus'
    $pageContractValidationStatus = Get-ValidationCheckState -Evidence $bindingValidationEvidence `
        -CheckName 'pageContractStatus'
    $bindingGenerationValidationStatus = Get-ValidationCheckState -Evidence $bindingValidationEvidence `
        -CheckName 'bindingStatus'

    $inspectionComplete = $projectFacts.scanComplete
    $integrationFacts.hostBoundaries.complete = ($inspectionComplete -and $hostBoundariesCandidateCoverage -and
        $hostBoundariesValidationStatus -eq 'Passed')
    $integrationFacts.runtimeRoot.complete = ($inspectionComplete -and $runtimeRootCandidateCoverage -and
        $runtimeRootValidationStatus -eq 'Passed')
    $integrationFacts.pageContract.complete = ($inspectionComplete -and $pageContractCandidateCoverage -and
        $pageContractValidationStatus -eq 'Passed')
    $integrationFacts.binding.generationComplete = ($inspectionComplete -and $bindingCandidateCoverage -and
        ($bindingGenerationValidationStatus -eq 'Passed' -or $bindingGenerationValidationStatus -eq 'Failed'))

    $displayBindingValidationStatus = $bindingValidationStatus
    $displayRuntimeValidationStatus = $runtimeValidationStatus
    if (-not $projectFacts.scanComplete) {
        if ($displayBindingValidationStatus -eq 'Passed') {
            $displayBindingValidationStatus = 'Indeterminate'
        }
        if ($displayRuntimeValidationStatus -eq 'Passed') {
            $displayRuntimeValidationStatus = 'Indeterminate'
        }
    }
    $integrationFacts.validation.binding = [ordered]@{
        status = $displayBindingValidationStatus
        evidence = @($bindingValidationEvidence.ToArray())
        rejectedEvidence = @($bindingRejectedEvidence.ToArray())
    }
    $integrationFacts.validation.runtime = [ordered]@{
        status = $displayRuntimeValidationStatus
        evidence = @($runtimeValidationEvidence.ToArray())
        rejectedEvidence = @($runtimeRejectedEvidence.ToArray())
    }

    $sampleFacts.basicIntegration = [ordered]@{
        imported = $basicSamplePaths.Count -gt 0
        paths = @($basicSamplePaths | Sort-Object)
    }
    $sampleFacts.customHostIntegration = [ordered]@{
        imported = $customSamplePaths.Count -gt 0
        paths = @($customSamplePaths | Sort-Object)
    }
    $sampleFacts.textMeshProIntegration = [ordered]@{
        imported = $tmpSamplePaths.Count -gt 0
        paths = @($tmpSamplePaths | Sort-Object)
    }

    if ([string]::IsNullOrWhiteSpace([string]$projectFacts.unityVersion)) {
        $status = 'UnityVersionUnverified'
    }
    elseif (-not $packageFacts.appUI.installed) {
        $status = 'AppUINotInstalled'
    }
    elseif ($hostCandidates.Count -eq 0) {
        $status = 'InstalledNotInitialized'
    }
    elseif (-not $hostBoundariesCandidateCoverage -or $hostBoundariesValidationStatus -ne 'Passed') {
        $status = 'HostBoundariesMissing'
    }
    elseif (-not $runtimeRootCandidateCoverage -or $runtimeRootValidationStatus -ne 'Passed') {
        $status = 'RuntimeRootIncomplete'
    }
    elseif (-not $pageContractCandidateCoverage -or $pageContractValidationStatus -ne 'Passed') {
        $status = 'PageContractIncomplete'
    }
    elseif ($bindingValidationStatus -eq 'Failed' -or $bindingGenerationValidationStatus -eq 'Failed') {
        $status = 'BindingInvalid'
        Add-Issue -Code 'BINDING_VALIDATION_FAILED' -Severity 'Error' `
            -Message 'Bound Binding validation evidence reports failure.'
    }
    elseif (-not $bindingCandidateCoverage -or $bindingGenerationValidationStatus -ne 'Passed') {
        $status = 'BindingGenerationPending'
    }
    elseif ($bindingValidationStatus -ne 'Passed' -or $runtimeValidationStatus -ne 'Passed') {
        $status = 'RuntimeValidationPending'
        if ($bindingValidationStatus -eq 'Unknown') {
            Add-Issue -Code 'BINDING_VALIDATION_EVIDENCE_MISSING' -Severity 'Warning' `
                -Message 'No discoverable passing Binding validation artifact was found.'
        }
        if ($runtimeValidationStatus -eq 'Failed') {
            Add-Issue -Code 'RUNTIME_VALIDATION_FAILED' -Severity 'Error' `
                -Message 'Discoverable Runtime validation evidence reports failure.'
        }
        elseif ($runtimeValidationStatus -eq 'Unknown') {
            Add-Issue -Code 'RUNTIME_VALIDATION_EVIDENCE_MISSING' -Severity 'Warning' `
                -Message 'No discoverable passing Runtime validation artifact was found.'
        }
    }
    else {
        if (-not $projectFacts.scanComplete) {
            $status = 'RuntimeValidationPending'
            Add-Issue -Code 'INSPECTION_TRUNCATED' -Severity 'Warning' `
                -Message 'Truncated project inspection cannot produce a definitive Ready status.'
        }
        else {
            $status = 'Ready'
        }
    }
}

$report = [pscustomobject][ordered]@{
    schemaVersion = 'joih-appui-project-inspection.v1'
    status = $status
    project = [pscustomobject]$projectFacts
    packages = [pscustomobject]$packageFacts
    integration = [pscustomobject]$integrationFacts
    samples = [pscustomobject]$sampleFacts
    issues = @($issues.ToArray())
    outputPath = $resolvedOutputPath
}
$json = $report | ConvertTo-Json -Depth 24

if ($null -ne $resolvedOutputPath) {
    Assert-SafeOutputPath -Path $resolvedOutputPath -UnityRoot $unityRoot
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
    }
    Assert-SafeOutputPath -Path $resolvedOutputPath -UnityRoot $unityRoot
    Write-Utf8NoBomNewFile -Path $resolvedOutputPath -Content $json
}

Write-Output $json
