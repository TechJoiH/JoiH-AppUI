[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$OutputPath = '',
    [ValidateRange(1, 10000)][int]$MaxSourceFiles = 2000
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$issues = New-Object 'System.Collections.Generic.List[object]'

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
    $mutable = $false

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
            }
            elseif ($gitRef -match '^v(?<version>0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
                $gitRefKind = 'Tag'
                $version = $gitRef.Substring(1)
                $mutable = $false
            }
            elseif ($gitRef -match '^[0-9a-fA-F]{40}$') {
                $gitRefKind = 'Commit'
                $mutable = $false
            }
            else {
                $gitRefKind = 'Branch'
                $mutable = $true
            }
        }
        elseif (-not [string]::IsNullOrWhiteSpace($reference) -and
            $reference -match '^(?i:file:|\.\.?[\\/]|[A-Za-z]:[\\/])') {
            $installSource = 'LocalPath'
            $mutable = $true
        }
        elseif (-not [string]::IsNullOrWhiteSpace($reference) -and
            $reference -match '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
            $installSource = 'Registry'
            $version = $reference
            $mutable = $false
        }
        else {
            $installSource = if (-not [string]::IsNullOrWhiteSpace($lockSource)) {
                $lockSource.Substring(0, 1).ToUpperInvariant() + $lockSource.Substring(1)
            }
            else {
                'Unknown'
            }
            $mutable = $true
        }
    }

    return [pscustomobject][ordered]@{
        installed = $installed
        manifestReference = $ManifestReference
        lockReference = $lockReference
        version = $version
        installSource = $installSource
        gitRef = $gitRef
        gitRefKind = $gitRefKind
        mutable = $mutable
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
    $version = if (-not [string]::IsNullOrWhiteSpace($lockVersion) -and
        $lockVersion -match '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
        $lockVersion
    }
    elseif (-not [string]::IsNullOrWhiteSpace($manifestReference) -and
        $manifestReference -match '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
        $manifestReference
    }
    else {
        $lockVersion
    }

    return [pscustomobject][ordered]@{
        name = $Name
        installed = (-not [string]::IsNullOrWhiteSpace($manifestReference) -or $null -ne $lockEntry)
        manifestReference = $manifestReference
        lockReference = $lockVersion
        version = $version
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
    foreach ($item in $Evidence) {
        if ($item.status -eq 'Failed') {
            return 'Failed'
        }
        if ($item.status -eq 'Passed') {
            $hasPassed = $true
        }
    }
    if ($hasPassed) {
        return 'Passed'
    }
    return 'Unknown'
}

function Get-StatusFromValidationText {
    param([Parameter(Mandatory = $true)][string]$Content)

    if ($Content -match '(?im)^[\s"'']*(?:status|result|validationStatus)["'']?\s*:\s*["'']?(?<status>failed|fail|error|invalid)\b') {
        return 'Failed'
    }
    if ($Content -match '(?im)^\s*(?:errorCount|errors)\s*:\s*[1-9]\d*\b') {
        return 'Failed'
    }
    if ($Content -match '(?im)^[\s"'']*(?:status|result|validationStatus)["'']?\s*:\s*["'']?(?<status>passed|pass|success|valid|ready)\b') {
        return 'Passed'
    }
    return 'Unknown'
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
    scanLimitReached = $false
}
$emptyAppUIFacts = [pscustomobject][ordered]@{
    installed = $false
    manifestReference = $null
    lockReference = $null
    version = $null
    installSource = 'Unknown'
    gitRef = $null
    gitRefKind = $null
    mutable = $false
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
        complete = $false
    }
    runtimeRoot = [ordered]@{
        complete = $false
        layerRoots = @()
        profiles = @()
        registries = @()
    }
    pageContract = [ordered]@{
        complete = $false
        controllers = @()
        definitions = @()
    }
    binding = [ordered]@{
        settings = @()
        generatedBindings = @()
        generationComplete = $false
    }
    validation = [ordered]@{
        binding = [ordered]@{ status = 'Unknown'; evidence = @() }
        runtime = [ordered]@{ status = 'Unknown'; evidence = @() }
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
    if (Test-UnsafeKnownPath -Path $packagesDirectory -Directory $true) {
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

    if ($packageFacts.appUI.installed -and $packageFacts.appUI.mutable) {
        Add-Issue -Code 'APPUI_MUTABLE_REFERENCE' -Severity 'Warning' `
            -Message 'The installed AppUI reference is mutable or cannot be proven immutable.' `
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
    $basicSamplePaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $customSamplePaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $tmpSamplePaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

    $assetsRoot = Join-Path $unityRoot 'Assets'
    $pendingDirectories = New-Object 'System.Collections.Generic.Stack[string]'
    $pendingDirectories.Push($assetsRoot)
    $directoryBudget = [Math]::Max(128, ($MaxSourceFiles * 16))
    $visitedDirectoryCount = 0
    $stopScan = $false

    while ($pendingDirectories.Count -gt 0 -and -not $stopScan) {
        if ($visitedDirectoryCount -ge $directoryBudget) {
            $projectFacts.scanLimitReached = $true
            Add-Issue -Code 'SOURCE_DIRECTORY_LIMIT_REACHED' -Severity 'Warning' `
                -Message 'The bounded Assets directory traversal stopped before inspecting every directory.'
            break
        }

        $directory = $pendingDirectories.Pop()
        $visitedDirectoryCount++
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

        try {
            $files = @([System.IO.Directory]::EnumerateFiles($directory) | Sort-Object)
        }
        catch {
            Add-Issue -Code 'SOURCE_DIRECTORY_UNREADABLE' -Severity 'Warning' `
                -Message 'An Assets directory could not be enumerated.' -Path $relativeDirectory
            continue
        }

        foreach ($filePath in $files) {
            $fileInfo = New-Object System.IO.FileInfo($filePath)
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
                $projectFacts.scanLimitReached = $true
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
                Add-Issue -Code 'SOURCE_FILE_TOO_LARGE' -Severity 'Warning' `
                    -Message 'A selected inspection file exceeded the 2 MiB read limit.' -Path $relativePath
                continue
            }

            try {
                $content = [System.IO.File]::ReadAllText($fileInfo.FullName)
            }
            catch {
                Add-Issue -Code 'SOURCE_FILE_UNREADABLE' -Severity 'Warning' `
                    -Message 'A selected inspection file could not be read.' -Path $relativePath
                continue
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

            $isImportedAppUISample = $relativePath -match `
                '^Assets/Samples/[^/]+/[^/]+/(?:Basic Integration|Custom Host Integration|TextMeshPro Integration)(?:/|$)'
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

                $validationStatus = Get-StatusFromValidationText -Content $content
                if ($fileInfo.Name -match '(?i:binding.*validation|validation.*binding)') {
                    $bindingValidationEvidence.Add([pscustomobject][ordered]@{
                        path = $relativePath
                        status = $validationStatus
                        confidence = 'DiscoverableArtifact'
                    }) | Out-Null
                }
                elseif ($fileInfo.Name -match '(?i:runtime.*validation|validation.*runtime)') {
                    $runtimeValidationEvidence.Add([pscustomobject][ordered]@{
                        path = $relativePath
                        status = $validationStatus
                        confidence = 'DiscoverableArtifact'
                    }) | Out-Null
                }
            }
        }

        if ($stopScan) {
            break
        }

        try {
            $subdirectories = @([System.IO.Directory]::EnumerateDirectories($directory) | Sort-Object -Descending)
        }
        catch {
            Add-Issue -Code 'SOURCE_DIRECTORY_UNREADABLE' -Severity 'Warning' `
                -Message 'An Assets directory could not enumerate child directories.' -Path $relativeDirectory
            continue
        }
        foreach ($subdirectory in $subdirectories) {
            $subdirectoryInfo = New-Object System.IO.DirectoryInfo($subdirectory)
            if (Test-ExcludedName -Name $subdirectoryInfo.Name) {
                continue
            }
            if (($subdirectoryInfo.Attributes -band [System.IO.FileAttributes]::Hidden) -ne 0) {
                continue
            }
            if (($subdirectoryInfo.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                $relativeReparse = Convert-ToPortableRelativePath -Root $unityRoot -Path $subdirectoryInfo.FullName
                Add-Issue -Code 'REPARSE_POINT_SKIPPED' -Severity 'Info' `
                    -Message 'A reparse-point directory under Assets was not traversed.' -Path $relativeReparse
                continue
            }
            $pendingDirectories.Push($subdirectoryInfo.FullName)
        }
    }

    if ($projectFacts.scanLimitReached) {
        Add-Issue -Code 'SOURCE_SCAN_LIMIT_REACHED' -Severity 'Warning' `
            -Message ("Assets inspection stopped at MaxSourceFiles={0}." -f $MaxSourceFiles)
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
    $integrationFacts.hostBoundaries.complete =
        ($operationCandidates.Count -gt 0 -and $assetProviderCandidates.Count -gt 0 -and $executionCandidates.Count -gt 0)

    $integrationFacts.runtimeRoot.layerRoots = @($layerCandidates.ToArray())
    $integrationFacts.runtimeRoot.profiles = @($profileCandidates.ToArray())
    $integrationFacts.runtimeRoot.registries = @($registryCandidates.ToArray())
    $integrationFacts.runtimeRoot.complete =
        ($hostCandidates.Count -gt 0 -and $managerCandidates.Count -gt 0 -and
        $layerCandidates.Count -gt 0 -and $profileCandidates.Count -gt 0 -and $registryCandidates.Count -gt 0)
    $integrationFacts.pageContract.controllers = @($controllerCandidates.ToArray())
    $integrationFacts.pageContract.definitions = @($definitionCandidates.ToArray())
    $integrationFacts.pageContract.complete =
        ($controllerCandidates.Count -gt 0 -and $definitionCandidates.Count -gt 0)
    $integrationFacts.binding.settings = @($bindingSettingsCandidates.ToArray())
    $integrationFacts.binding.generatedBindings = @($generatedBindingCandidates.ToArray())
    $integrationFacts.binding.generationComplete =
        ($bindingSettingsCandidates.Count -gt 0 -and $generatedBindingCandidates.Count -gt 0)

    $bindingValidationStatus = Get-ValidationEvidenceState -Evidence $bindingValidationEvidence
    $runtimeValidationStatus = Get-ValidationEvidenceState -Evidence $runtimeValidationEvidence
    $integrationFacts.validation.binding = [ordered]@{
        status = $bindingValidationStatus
        evidence = @($bindingValidationEvidence.ToArray())
    }
    $integrationFacts.validation.runtime = [ordered]@{
        status = $runtimeValidationStatus
        evidence = @($runtimeValidationEvidence.ToArray())
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
    elseif (-not $integrationFacts.hostBoundaries.complete) {
        $status = 'HostBoundariesMissing'
    }
    elseif (-not $integrationFacts.runtimeRoot.complete) {
        $status = 'RuntimeRootIncomplete'
    }
    elseif (-not $integrationFacts.pageContract.complete) {
        $status = 'PageContractIncomplete'
    }
    elseif (-not $integrationFacts.binding.generationComplete) {
        $status = 'BindingGenerationPending'
    }
    elseif ($bindingValidationStatus -eq 'Failed') {
        $status = 'BindingInvalid'
        Add-Issue -Code 'BINDING_VALIDATION_FAILED' -Severity 'Error' `
            -Message 'Discoverable Binding validation evidence reports failure.'
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
        $status = 'Ready'
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
    if ($null -ne $unityRoot) {
        foreach ($protectedDirectoryName in @('Assets', 'Packages', 'ProjectSettings')) {
            $protectedDirectory = Join-Path $unityRoot $protectedDirectoryName
            if (Test-PathWithinRoot -Path $resolvedOutputPath -Root $protectedDirectory) {
                throw ("OutputPath cannot target the Unity {0} directory." -f $protectedDirectoryName)
            }
        }
    }
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
    }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($resolvedOutputPath, $json, $utf8NoBom)
}

Write-Output $json
