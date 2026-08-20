[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script:Passed = 0
$script:Failed = 0
$script:JunctionPaths = New-Object 'System.Collections.Generic.List[string]'
$script:AttestationByProject = @{}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param(
        [AllowNull()]$Expected,
        [AllowNull()]$Actual,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Expected -ne $Actual) {
        throw ("{0} Expected <{1}> but found <{2}>." -f $Message, $Expected, $Actual)
    }
}

function Invoke-Test {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Body
    )

    try {
        & $Body
        $script:Passed++
        Write-Host ("PASS {0}" -f $Name)
    }
    catch {
        $script:Failed++
        Write-Host ("FAIL {0}: {1}" -f $Name, $_.Exception.Message)
    }
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function New-OrdinaryFolder {
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $path = Join-Path $RunRoot $Name
    [System.IO.Directory]::CreateDirectory($path) | Out-Null
    return $path
}

function New-UnityFixture {
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$AppUIReference = '',
        [bool]$IncludeUnityVersion = $true,
        [hashtable]$ExtraDependencies = @{}
    )

    $root = Join-Path $RunRoot $Name
    [System.IO.Directory]::CreateDirectory((Join-Path $root 'Assets')) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $root 'Packages')) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $root 'ProjectSettings')) | Out-Null

    if ($IncludeUnityVersion) {
        Write-Utf8File -Path (Join-Path $root 'ProjectSettings\ProjectVersion.txt') `
            -Content "m_EditorVersion: 6000.0.25f1`nm_EditorVersionWithRevision: 6000.0.25f1 (fixture)"
    }

    $dependencies = [ordered]@{
        'com.unity.ugui' = '2.0.0'
    }
    if (-not [string]::IsNullOrWhiteSpace($AppUIReference)) {
        $dependencies['com.joih.appui'] = $AppUIReference
    }
    foreach ($name in $ExtraDependencies.Keys) {
        $dependencies[$name] = [string]$ExtraDependencies[$name]
    }

    $manifest = [ordered]@{
        dependencies = $dependencies
    } | ConvertTo-Json -Depth 10
    Write-Utf8File -Path (Join-Path $root 'Packages\manifest.json') -Content $manifest

    $lockDependencies = [ordered]@{
        'com.unity.ugui' = [ordered]@{
            version = '2.0.0'
            depth = 0
            source = 'registry'
            dependencies = [ordered]@{}
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($AppUIReference)) {
        $source = if ($AppUIReference -match '^(?:https?|ssh|git\+|git@)' -or $AppUIReference -match '\.git(?:[?#]|$)') { 'git' } else { 'registry' }
        $lockDependencies['com.joih.appui'] = [ordered]@{
            version = $AppUIReference
            depth = 0
            source = $source
            dependencies = [ordered]@{
                'com.unity.ugui' = '2.0.0'
            }
        }
    }
    foreach ($name in $ExtraDependencies.Keys) {
        $value = [string]$ExtraDependencies[$name]
        $source = if ($value -match '^(?:https?|ssh|git\+|git@)' -or $value -match '\.git(?:[?#]|$)') { 'git' } else { 'registry' }
        $lockDependencies[$name] = [ordered]@{
            version = $value
            depth = 0
            source = $source
            dependencies = [ordered]@{}
        }
    }

    $lock = [ordered]@{
        dependencies = $lockDependencies
    } | ConvertTo-Json -Depth 12
    Write-Utf8File -Path (Join-Path $root 'Packages\packages-lock.json') -Content $lock
    return $root
}

function Add-SourceFile {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    Write-Utf8File -Path (Join-Path $Root $RelativePath) -Content $Content
}

function Add-AppUIHostAndPorts {
    param([Parameter(Mandatory = $true)][string]$Root)

    Add-SourceFile -Root $Root -RelativePath 'Assets\AppUI\Runtime\ProjectAppUIHost.cs' -Content @'
using Joi.H.AppUI;
public sealed class ProjectOperationFactory : IUIOperationFactory { }
public sealed class ProjectAssetProvider : IUIAssetProvider { }
public sealed class ProjectExecutionContext : IAppUIExecutionContext { }
public sealed class ProjectAppUIInstaller {
    private AppUIManager manager;
    private AppUIRuntimeHost runtimeHost;
}
'@
}

function Add-RuntimeRoot {
    param([Parameter(Mandatory = $true)][string]$Root)

    Add-SourceFile -Root $Root -RelativePath 'Assets\AppUI\Runtime\UILayerComposition.cs' -Content @'
using Joi.H.AppUI;
public sealed class UILayerComposition {
    private UILayerRoot layerRoot;
}
'@
    Write-Utf8File -Path (Join-Path $Root 'Assets\AppUI\Settings\MainAppUIRuntimeProfile.asset') -Content @'
--- !u!114 &11400000
MonoBehaviour:
  m_Name: MainAppUIRuntimeProfile
  m_ScriptType: AppUIRuntimeProfile
'@
    Write-Utf8File -Path (Join-Path $Root 'Assets\AppUI\Settings\MainUIPageDefinitionRegistry.asset') -Content @'
--- !u!114 &11400000
MonoBehaviour:
  m_Name: MainUIPageDefinitionRegistry
  m_ScriptType: UIPageDefinitionRegistry
'@
}

function Add-PageContract {
    param([Parameter(Mandatory = $true)][string]$Root)

    Add-SourceFile -Root $Root -RelativePath 'Assets\AppUI\Pages\SettingsPanelController.cs' -Content @'
using Joi.H.AppUI;
public partial class SettingsPanelController : PanelBaseController { }
'@
    Write-Utf8File -Path (Join-Path $Root 'Assets\AppUI\Pages\SettingsPageDefinition.asset') -Content @'
--- !u!114 &11400000
MonoBehaviour:
  m_Name: SettingsPageDefinition
  m_ScriptType: UIPageDefinition
  pageId: settings
'@
    Write-Utf8File -Path (Join-Path $Root 'Assets\AppUI\Settings\MainUIBindingSettings.asset') -Content @'
--- !u!114 &11400000
MonoBehaviour:
  m_Name: MainUIBindingSettings
  m_ScriptType: UIBindingSettings
'@
}

function Add-GeneratedBinding {
    param([Parameter(Mandatory = $true)][string]$Root)

    Add-SourceFile -Root $Root -RelativePath 'Assets\AppUI\Pages\SettingsPanelController.Bindings.cs' -Content @'
public partial class SettingsPanelController {
    private UnityEngine.UI.Button B_Close;
}
'@
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function New-FakeUnityExecutable {
    param([Parameter(Mandatory = $true)][string]$Path)

    $source = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;

public static class FakeUnityCommandLine
{
    private static string Arg(string[] args, string name)
    {
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (String.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return String.Empty;
    }

    private static bool Has(string[] args, string value)
    {
        return args.Any(item => String.Equals(item, value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ExactBindingArguments(string[] args)
    {
        try
        {
            if (args.Length != 13 ||
                args[0] != "-batchmode" || args[1] != "-nographics" || args[2] != "-quit" ||
                args[3] != "-projectPath" || !Path.IsPathRooted(args[4]) ||
                args[5] != "-executeMethod" ||
                args[6] != "Joi.H.AppUI.Editor.Binding.UIBindingValidationCommandLine.ValidateAll" ||
                args[7] != "-appUIBindingSettingsPath" ||
                args[8] != "Assets/AppUI/Settings/MainUIBindingSettings.asset" ||
                args[9] != "-appUIValidationReportPath" ||
                Path.GetFileName(args[10]) != "app-ui-binding-validation.v2.json" ||
                args[11] != "-logFile" || Path.GetFileName(args[12]) != "binding-unity.log")
                return false;
            return String.Equals(Path.GetDirectoryName(Path.GetFullPath(args[10])),
                Path.GetDirectoryName(Path.GetFullPath(args[12])), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool ExactRuntimeArguments(string[] args)
    {
        try
        {
            if (args.Length != 13 ||
                args[0] != "-batchmode" || args[1] != "-nographics" ||
                args[2] != "-projectPath" || !Path.IsPathRooted(args[3]) ||
                args[4] != "-runTests" || args[5] != "-testPlatform" || args[6] != "EditMode" ||
                args[7] != "-testFilter" || String.IsNullOrEmpty(args[8]) ||
                args[9] != "-testResults" ||
                Path.GetFileName(args[10]) != "app-ui-lifecycle-tests.xml" ||
                args[11] != "-logFile" || Path.GetFileName(args[12]) != "runtime-unity.log")
                return false;
            return String.Equals(Path.GetDirectoryName(Path.GetFullPath(args[10])),
                Path.GetDirectoryName(Path.GetFullPath(args[12])), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void MutateProjectDuringValidation(string projectPath)
    {
        string sourcePath = Path.Combine(projectPath,
            "Assets", "AppUI", "Runtime", "ProjectAppUIHost.cs");
        string manifestPath = Path.Combine(projectPath, "Packages", "manifest.json");
        File.AppendAllText(sourcePath, Environment.NewLine + "// mutated during validation",
            new UTF8Encoding(false));
        File.AppendAllText(manifestPath, " ", new UTF8Encoding(false));
    }

    private static string Json(string value)
    {
        if (value == null) return String.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static void LogInvocation(string[] args)
    {
        string path = Environment.GetEnvironmentVariable("APPUI_FAKE_UNITY_INVOCATION_LOG");
        if (String.IsNullOrEmpty(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string line = String.Join("|", args.Select(item =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(item))).ToArray());
        File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
    }

    private static int WriteBinding(string[] args, string scenario)
    {
        string path = Arg(args, "-appUIValidationReportPath");
        if (scenario == "BindingNoReport") return 0;
        if (String.IsNullOrEmpty(path)) return 2;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        DateTime started = DateTime.UtcNow.AddMilliseconds(-2);
        if (scenario == "MutateProjectDuringValidation")
            MutateProjectDuringValidation(Arg(args, "-projectPath"));
        DateTime finished = DateTime.UtcNow;
        bool validationFailed = scenario == "BindingFail";
        bool commandFailed = scenario == "BindingCommandFail";
        int exitCode = commandFailed ? 2 : (validationFailed ? 1 : 0);
        int errorCount = exitCode == 0 ? 0 : 1;
        string json = "{" +
            "\"schemaVersion\":\"app-ui-binding-validation.v2\"," +
            "\"tool\":\"AppUIBindingValidateAll\"," +
            "\"unityVersion\":\"6000.0.25f1\"," +
            "\"settingsPath\":\"" + Json(Arg(args, "-appUIBindingSettingsPath")) + "\"," +
            "\"reportPath\":\"" + Json(path.Replace('\\', '/')) + "\"," +
            "\"startedAtUtc\":\"" + started.ToString("o", CultureInfo.InvariantCulture) + "\"," +
            "\"finishedAtUtc\":\"" + finished.ToString("o", CultureInfo.InvariantCulture) + "\"," +
            "\"durationMs\":2," +
            "\"success\":" + (exitCode == 0 ? "true" : "false") + "," +
            "\"exitCode\":" + exitCode.ToString(CultureInfo.InvariantCulture) + "," +
            "\"errorCount\":" + errorCount.ToString(CultureInfo.InvariantCulture) + "," +
            "\"warningCount\":0,\"infoCount\":0,\"messages\":[]}";
        File.WriteAllText(path, json, new UTF8Encoding(false));
        if (scenario == "BindingReplay") File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        if (scenario == "BindingFuture") File.SetLastWriteTimeUtc(path, new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return exitCode;
    }

    private static int WriteRuntime(string[] args, string scenario)
    {
        string path = Arg(args, "-testResults");
        if (scenario == "RuntimeTimeout")
        {
            Thread.Sleep(5000);
            return 0;
        }
        if (scenario == "RuntimeNoReport") return 0;
        if (scenario == "RuntimeProcessFail") return 3;
        if (String.IsNullOrEmpty(path)) return 3;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string prefix = Arg(args, "-testFilter");
        bool missingLifecycle = scenario == "RuntimeMissingLifecycle";
        bool failed = scenario == "RuntimeFail";
        var names = new List<string> {
            prefix + ".Open", prefix + ".Refresh", prefix + ".Close",
            prefix + ".Shutdown"
        };
        if (!missingLifecycle)
        {
            names.Insert(3, prefix + ".ReleaseScope");
            names.Insert(4, prefix + ".SceneRebind");
        }
        int total = names.Count;
        int passed = failed ? total - 1 : total;
        int failedCount = failed ? 1 : 0;
        string result = failed ? "Failed" : "Passed";
        var cases = new StringBuilder();
        for (int i = 0; i < names.Count; i++)
        {
            string caseResult = failed && i == 0 ? "Failed" : "Passed";
            cases.Append("<test-case fullname=\"")
                .Append(SecurityElement.Escape(names[i]))
                .Append("\" result=\"").Append(caseResult).Append("\" />");
        }
        string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<test-run id=\"2\" result=\"" + result + "\" total=\"" + total +
            "\" passed=\"" + passed + "\" failed=\"" + failedCount +
            "\" inconclusive=\"0\" skipped=\"0\"><test-suite result=\"" + result + "\">" +
            cases + "</test-suite></test-run>";
        File.WriteAllText(path, xml, new UTF8Encoding(false));
        if (scenario == "RuntimeReplay") File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        if (scenario == "RuntimeFuture") File.SetLastWriteTimeUtc(path, new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return failed ? 2 : 0;
    }

    public static int Main(string[] args)
    {
        LogInvocation(args);
        string scenario = Environment.GetEnvironmentVariable("APPUI_FAKE_UNITY_SCENARIO") ?? "Pass";
        if (Has(args, "-executeMethod"))
        {
            if (!ExactBindingArguments(args)) return 91;
            return WriteBinding(args, scenario);
        }
        if (Has(args, "-runTests"))
        {
            if (!ExactRuntimeArguments(args)) return 92;
            return WriteRuntime(args, scenario);
        }
        return 9;
    }
}
'@
    Add-Type -TypeDefinition $source -OutputAssembly $Path -OutputType ConsoleApplication
}

function Read-FakeUnityInvocations {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return @()
    }
    return @(Get-Content -LiteralPath $Path -Encoding UTF8 | ForEach-Object {
        $arguments = @()
        if (-not [string]::IsNullOrWhiteSpace($_)) {
            $arguments = @($_.Split('|') | ForEach-Object {
                [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_))
            })
        }
        ,$arguments
    })
}

function Invoke-ValidationProducer {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [string]$Scenario = 'Pass',
        [string]$LifecycleTestFilter = 'Joi.H.AppUI.Tests.Lifecycle',
        [string]$InvocationLogPath = '',
        [ValidateRange(1, 30)][int]$TimeoutSeconds = 10
    )

    if ([string]::IsNullOrWhiteSpace($InvocationLogPath)) {
        $InvocationLogPath = $OutputPath + '.unity-invocations.log'
    }
    $priorScenario = [Environment]::GetEnvironmentVariable('APPUI_FAKE_UNITY_SCENARIO')
    $priorLog = [Environment]::GetEnvironmentVariable('APPUI_FAKE_UNITY_INVOCATION_LOG')
    try {
        [Environment]::SetEnvironmentVariable('APPUI_FAKE_UNITY_SCENARIO', $Scenario)
        [Environment]::SetEnvironmentVariable('APPUI_FAKE_UNITY_INVOCATION_LOG', $InvocationLogPath)
        return @(& $script:AttestationProducerPath -ProjectPath $ProjectPath `
            -UnityPath $script:FakeUnityPath `
            -BindingSettingsPath (Join-Path $ProjectPath 'Assets\AppUI\Settings\MainUIBindingSettings.asset') `
            -LifecycleTestFilter $LifecycleTestFilter -OutputPath $OutputPath `
            -TimeoutSeconds $TimeoutSeconds -MaxSourceFiles 10000)
    }
    finally {
        [Environment]::SetEnvironmentVariable('APPUI_FAKE_UNITY_SCENARIO', $priorScenario)
        [Environment]::SetEnvironmentVariable('APPUI_FAKE_UNITY_INVOCATION_LOG', $priorLog)
    }
}

function New-RealValidationAttestation {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$ArtifactRoot,
        [string]$Name = 'appui-validation-attestation.json'
    )

    Assert-True -Condition (Test-Path -LiteralPath $script:AttestationProducerPath -PathType Leaf) `
        -Message ("Missing separate attestation producer: {0}" -f $script:AttestationProducerPath)
    [System.IO.Directory]::CreateDirectory($ArtifactRoot) | Out-Null
    $outputPath = Join-Path $ArtifactRoot $Name
    Invoke-ValidationProducer -ProjectPath $ProjectPath -OutputPath $outputPath | Out-Null
    Assert-True -Condition (Test-Path -LiteralPath $outputPath -PathType Leaf) `
        -Message 'Separate producer emitted no attestation.'
    return $outputPath
}

function Add-RealValidationAttestation {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [string]$SchemaVersion = 'joih-appui-project-validation-attestation.v3',
        [string]$Producer = 'integrating-joih-appui/new-appui-validation-attestation.ps1'
    )

    $key = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $artifactRoot = Join-Path $script:CurrentRunRoot `
        ("generated-attestation-{0}" -f [guid]::NewGuid().ToString('N'))
    $attestationPath = New-RealValidationAttestation -ProjectPath $Root -ArtifactRoot $artifactRoot
    if ($SchemaVersion -cne 'joih-appui-project-validation-attestation.v3' -or
        $Producer -cne 'integrating-joih-appui/new-appui-validation-attestation.ps1') {
        $attestation = [System.IO.File]::ReadAllText($attestationPath) | ConvertFrom-Json
        $attestation.schemaVersion = $SchemaVersion
        $attestation.producer = $Producer
        Write-Utf8File -Path $attestationPath -Content ($attestation | ConvertTo-Json -Depth 16)
    }
    $script:AttestationByProject[$key] = $attestationPath
}

function Invoke-Inspection {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [string]$OutputPath = '',
        [string]$AttestationPath = '',
        [int]$MaxSourceFiles = 2000
    )

    $arguments = @{
        ProjectPath = $ProjectPath
        MaxSourceFiles = $MaxSourceFiles
    }
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $arguments.OutputPath = $OutputPath
    }
    if (-not [string]::IsNullOrWhiteSpace($AttestationPath)) {
        $arguments.AttestationPath = $AttestationPath
    }
    else {
        $projectKey = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
        if ($script:AttestationByProject.ContainsKey($projectKey)) {
            $arguments.AttestationPath = $script:AttestationByProject[$projectKey]
        }
    }

    $json = & $script:InspectorPath @arguments
    Assert-True -Condition (-not [string]::IsNullOrWhiteSpace(($json -join ''))) `
        -Message 'Inspector emitted no JSON.'
    return (($json -join [Environment]::NewLine) | ConvertFrom-Json)
}

function Assert-Status {
    param(
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)][string]$ProjectPath
    )

    $result = Invoke-Inspection -ProjectPath $ProjectPath
    Assert-Equal -Expected 'joih-appui-project-inspection.v1' -Actual $result.schemaVersion `
        -Message 'Schema version changed.'
    Assert-Equal -Expected $Expected -Actual $result.status -Message 'Unexpected integration status.'
    Assert-True -Condition ($null -ne $result.project) -Message 'Project facts are missing.'
    Assert-True -Condition ($null -ne $result.packages) -Message 'Package facts are missing.'
    Assert-True -Condition ($null -ne $result.integration) -Message 'Integration facts are missing.'
    Assert-True -Condition ($null -ne $result.samples) -Message 'Sample facts are missing.'
    Assert-True -Condition ($null -ne $result.issues) -Message 'Issues collection is missing.'
}

$skillRoot = Split-Path -Parent $PSScriptRoot
$script:InspectorPath = Join-Path $skillRoot 'scripts\inspect-appui-project.ps1'
$script:AttestationProducerPath = Join-Path $skillRoot 'scripts\new-appui-validation-attestation.ps1'

if (-not (Test-Path -LiteralPath $script:InspectorPath -PathType Leaf)) {
    Write-Host ("FAIL Inspector script exists: missing {0}" -f $script:InspectorPath)
    Write-Host 'RESULT Passed=0 Failed=1'
    exit 1
}

$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
$runRoot = Join-Path $tempRoot ("integrating-appui-inspector-tests-{0}" -f [guid]::NewGuid().ToString('N'))
$script:CurrentRunRoot = $runRoot
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
$script:FakeUnityPath = Join-Path $runRoot 'FakeUnity.exe'
New-FakeUnityExecutable -Path $script:FakeUnityPath

try {
    $officialTag = 'https://github.com/TechJoiH/JoiH-AppUI.git#v0.4.0-pre.1'

    Invoke-Test -Name 'Inspector is a pure read-only command with only explicit report output' -Body {
        $tokens = $null
        $parseErrors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile(
            $script:InspectorPath, [ref]$tokens, [ref]$parseErrors)
        Assert-Equal -Expected 0 -Actual $parseErrors.Count -Message 'Inspector failed AST parsing.'
        $parameterNames = @($ast.ParamBlock.Parameters | ForEach-Object {
            $_.Name.VariablePath.UserPath
        } | Sort-Object)
        $expectedParameters = @('AttestationPath', 'MaxSourceFiles', 'OutputPath', 'ProjectPath')
        Assert-Equal -Expected ([string]::Join('|', $expectedParameters)) `
            -Actual ([string]::Join('|', $parameterNames)) `
            -Message 'Inspector retained a project-writing or caller-declared validation parameter.'
        $source = [System.IO.File]::ReadAllText($script:InspectorPath)
        Assert-True -Condition (-not $source.Contains('CreateAttestation')) `
            -Message 'Inspector still contains the former attestation creation flow.'
        $producerInvocations = @($ast.FindAll({
            param($node)
            if ($node -isnot [System.Management.Automation.Language.CommandAst]) {
                return $false
            }
            $commandName = $node.GetCommandName()
            return (-not [string]::IsNullOrWhiteSpace($commandName) -and
                $commandName.EndsWith('new-appui-validation-attestation.ps1',
                    [System.StringComparison]::OrdinalIgnoreCase))
        }, $true))
        Assert-Equal -Expected 0 -Actual $producerInvocations.Count `
            -Message 'Inspector invokes the separate producer.'
    }

    Invoke-Test -Name 'Separate producer accepts no caller report or status inputs' -Body {
        Assert-True -Condition (Test-Path -LiteralPath $script:AttestationProducerPath -PathType Leaf) `
            -Message ("Missing separate attestation producer: {0}" -f $script:AttestationProducerPath)
        $tokens = $null
        $parseErrors = $null
        $producerAst = [System.Management.Automation.Language.Parser]::ParseFile(
            $script:AttestationProducerPath, [ref]$tokens, [ref]$parseErrors)
        Assert-Equal -Expected 0 -Actual $parseErrors.Count -Message 'Producer failed AST parsing.'
        $producerParameterNames = @($producerAst.ParamBlock.Parameters | ForEach-Object {
            $_.Name.VariablePath.UserPath
        } | Sort-Object)
        $expected = @('BindingSettingsPath', 'LifecycleTestFilter', 'MaxSourceFiles',
            'OutputPath', 'ProjectPath', 'TimeoutSeconds', 'UnityPath')
        Assert-Equal -Expected ([string]::Join('|', $expected)) `
            -Actual ([string]::Join('|', $producerParameterNames)) `
            -Message 'Producer exposes caller-provided report or outcome parameters.'
        Assert-True -Condition (-not ($producerParameterNames -match 'Status|Report|Result')) `
            -Message 'Producer accepts a caller-declared report or validation outcome.'
    }

    Invoke-Test -Name 'Run-owned Binding reports reject replay and future timestamps' -Body {
        $fixture = New-UnityFixture -RunRoot $runRoot -Name 'run-owned-report-window' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $fixture
        Add-RuntimeRoot -Root $fixture
        Add-PageContract -Root $fixture
        Add-GeneratedBinding -Root $fixture
        $artifactRoot = Join-Path $runRoot 'run-owned-report-window-artifacts'
        [System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
        $prewritten = Join-Path $artifactRoot 'caller-prewritten-binding.json'
        Write-Utf8File -Path $prewritten -Content '{"success":true,"status":"Passed"}'
        $prewrittenHash = Get-FileSha256 -Path $prewritten
        foreach ($scenario in @('BindingReplay', 'BindingFuture')) {
            $output = Join-Path $artifactRoot ("{0}.attestation.json" -f $scenario)
            $blocked = $false
            try {
                Invoke-ValidationProducer -ProjectPath $fixture -OutputPath $output `
                    -Scenario $scenario | Out-Null
            }
            catch {
                $blocked = $true
            }
            Assert-True -Condition $blocked `
                -Message ("A {0} report escaped the verified process window." -f $scenario)
            Assert-Equal -Expected $false -Actual (Test-Path -LiteralPath $output) `
                -Message ("A {0} report left an attestation." -f $scenario)
        }
        Assert-Equal -Expected $prewrittenHash -Actual (Get-FileSha256 -Path $prewritten) `
            -Message 'Producer read or changed a caller-prewritten report.'
    }

    Invoke-Test -Name 'Project mutation during Unity validation prevents attestation' -Body {
        $fixture = New-UnityFixture -RunRoot $runRoot -Name 'mid-run-project-mutation' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $fixture
        Add-RuntimeRoot -Root $fixture
        Add-PageContract -Root $fixture
        Add-GeneratedBinding -Root $fixture
        $sourcePath = Join-Path $fixture 'Assets\AppUI\Runtime\ProjectAppUIHost.cs'
        $manifestPath = Join-Path $fixture 'Packages\manifest.json'
        $sourceHashBefore = Get-FileSha256 -Path $sourcePath
        $manifestHashBefore = Get-FileSha256 -Path $manifestPath
        $artifactRoot = Join-Path $runRoot 'mid-run-project-mutation-artifacts'
        [System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
        $output = Join-Path $artifactRoot 'must-not-exist.json'

        $blocked = $false
        try {
            Invoke-ValidationProducer -ProjectPath $fixture -OutputPath $output `
                -Scenario 'MutateProjectDuringValidation' | Out-Null
        }
        catch {
            $blocked = $true
        }

        Assert-True -Condition $blocked `
            -Message 'Producer attested a post-run snapshot different from its pre-run snapshot.'
        Assert-Equal -Expected $false -Actual (Test-Path -LiteralPath $output) `
            -Message 'Mid-run project mutation left a false current attestation.'
        Assert-True -Condition ((Get-FileSha256 -Path $sourcePath) -cne $sourceHashBefore) `
            -Message 'Fake Unity did not leave its validation-relevant Assets mutation in place.'
        Assert-True -Condition ((Get-FileSha256 -Path $manifestPath) -cne $manifestHashBefore) `
            -Message 'Fake Unity did not leave its manifest mutation in place.'
        foreach ($path in @($sourcePath, $manifestPath)) {
            $ageSeconds = ([datetime]::UtcNow - (Get-Item -LiteralPath $path).LastWriteTimeUtc).TotalSeconds
            Assert-True -Condition ($ageSeconds -ge 0 -and $ageSeconds -lt 30) `
                -Message 'Fake Unity mutation did not use an ordinary current file timestamp.'
        }
    }

    Invoke-Test -Name 'Runtime replay future and timeout evidence remain pending' -Body {
        $fixture = New-UnityFixture -RunRoot $runRoot -Name 'runtime-window-adversaries' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $fixture
        Add-RuntimeRoot -Root $fixture
        Add-PageContract -Root $fixture
        Add-GeneratedBinding -Root $fixture
        $artifactRoot = Join-Path $runRoot 'runtime-window-adversaries-artifacts'
        [System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
        foreach ($case in @(
            @{ scenario = 'RuntimeReplay'; reason = 'RuntimeReportRejected'; timeout = 10 },
            @{ scenario = 'RuntimeFuture'; reason = 'RuntimeReportRejected'; timeout = 10 },
            @{ scenario = 'RuntimeTimeout'; reason = 'RuntimeProcessTimedOut'; timeout = 1 }
        )) {
            $output = Join-Path $artifactRoot ($case.scenario + '.json')
            Invoke-ValidationProducer -ProjectPath $fixture -OutputPath $output `
                -Scenario $case.scenario -TimeoutSeconds $case.timeout | Out-Null
            $attestation = [System.IO.File]::ReadAllText($output) | ConvertFrom-Json
            Assert-Equal -Expected 'Passed' -Actual $attestation.binding.status `
                -Message ($case.scenario + ' discarded genuine Binding evidence.')
            Assert-Equal -Expected 'Pending' -Actual $attestation.runtime.status `
                -Message ($case.scenario + ' produced a definitive runtime outcome.')
            Assert-Equal -Expected $case.reason -Actual $attestation.runtime.reason `
                -Message ($case.scenario + ' was not rejected for the expected boundary.')
            $inspection = Invoke-Inspection -ProjectPath $fixture -AttestationPath $output
            Assert-Equal -Expected 'RuntimeValidationPending' -Actual $inspection.status `
                -Message ($case.scenario + ' produced Ready.')
        }
    }

    Invoke-Test -Name 'Secret-named explicit attestations are never trusted' -Body {
        $fixture = New-UnityFixture -RunRoot $runRoot -Name 'secret-named-explicit-evidence' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $fixture
        Add-RuntimeRoot -Root $fixture
        Add-PageContract -Root $fixture
        Add-GeneratedBinding -Root $fixture
        $artifactRoot = Join-Path $runRoot 'secret-named-evidence-artifacts'
        [System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
        $validOutput = Join-Path $artifactRoot 'valid-attestation.json'
        Invoke-ValidationProducer -ProjectPath $fixture -OutputPath $validOutput | Out-Null
        $envAttestation = Join-Path $artifactRoot '.secret'
        Write-Utf8File -Path $envAttestation -Content ([System.IO.File]::ReadAllText($validOutput))
        $result = Invoke-Inspection -ProjectPath $fixture -AttestationPath $envAttestation
        Assert-True -Condition ($result.status -ne 'Ready') `
            -Message 'Inspector trusted a secret-named explicit attestation.'
        Assert-True -Condition ($result.integration.validation.binding.rejectedEvidence.reason -contains `
            'AttestationUnreadable') -Message 'Secret-named attestation rejection was not explicit.'
    }

    Invoke-Test -Name 'Inspector reads explicit attestation without changing the Unity project' -Body {
        $fixture = New-UnityFixture -RunRoot $runRoot -Name 'inspector-no-write' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $fixture
        Add-RuntimeRoot -Root $fixture
        Add-PageContract -Root $fixture
        Add-GeneratedBinding -Root $fixture
        $attestation = Join-Path $runRoot 'inspector-no-write-attestation.json'
        Write-Utf8File -Path $attestation -Content '{"schemaVersion":"untrusted.fixture"}'

        $before = @(Get-ChildItem -LiteralPath $fixture -Recurse -Force | ForEach-Object {
            if ($_.PSIsContainer) {
                '{0}|{1}|directory' -f $_.FullName, $_.Attributes
            }
            else {
                '{0}|{1}|{2}|{3}' -f $_.FullName, $_.Attributes, $_.LastWriteTimeUtc.Ticks,
                    (Get-FileSha256 -Path $_.FullName)
            }
        } | Sort-Object)
        $result = Invoke-Inspection -ProjectPath $fixture -AttestationPath $attestation
        $after = @(Get-ChildItem -LiteralPath $fixture -Recurse -Force | ForEach-Object {
            if ($_.PSIsContainer) {
                '{0}|{1}|directory' -f $_.FullName, $_.Attributes
            }
            else {
                '{0}|{1}|{2}|{3}' -f $_.FullName, $_.Attributes, $_.LastWriteTimeUtc.Ticks,
                    (Get-FileSha256 -Path $_.FullName)
            }
        } | Sort-Object)
        Assert-Equal -Expected ([string]::Join("`n", $before)) -Actual ([string]::Join("`n", $after)) `
            -Message 'Read-only inspection changed project entries, timestamps, attributes, or bytes.'
        Assert-True -Condition ($result.status -ne 'Ready') `
            -Message 'An untrusted explicit attestation produced Ready.'
    }

    Invoke-Test -Name 'Every validation-relevant extension invalidates stale evidence' -Body {
        foreach ($case in @(
            @{ name = 'cs'; path = 'Assets\Validation\Extra.cs' },
            @{ name = 'asmdef'; path = 'Assets\Validation\Extra.asmdef' },
            @{ name = 'asmref'; path = 'Assets\Validation\Extra.asmref' },
            @{ name = 'asset'; path = 'Assets\Validation\Extra.asset' },
            @{ name = 'prefab'; path = 'Assets\AppUI\Validation\Panel.prefab' },
            @{ name = 'scene'; path = 'Assets\Scenes\Main.unity' },
            @{ name = 'meta'; path = 'Assets\AppUI\Pages\SettingsPanelController.cs.meta' },
            @{ name = 'inputactions'; path = 'Assets\Validation\Extra.inputactions' },
            @{ name = 'controller'; path = 'Assets\Validation\Extra.controller' },
            @{ name = 'anim'; path = 'Assets\Validation\Extra.anim' }
        )) {
            $fixture = New-UnityFixture -RunRoot $runRoot -Name ("stale-{0}" -f $case.name) `
                -AppUIReference $officialTag
            Add-AppUIHostAndPorts -Root $fixture
            Add-RuntimeRoot -Root $fixture
            Add-PageContract -Root $fixture
            Add-GeneratedBinding -Root $fixture
            Write-Utf8File -Path (Join-Path $fixture $case.path) -Content ("before-{0}" -f $case.name)
            $attestation = New-RealValidationAttestation -ProjectPath $fixture `
                -ArtifactRoot (Join-Path $runRoot ("stale-{0}-artifacts" -f $case.name))
            $before = Invoke-Inspection -ProjectPath $fixture -AttestationPath $attestation
            Assert-Equal -Expected 'Ready' -Actual $before.status `
                -Message ("Valid {0} fixture did not become Ready." -f $case.name)
            Write-Utf8File -Path (Join-Path $fixture $case.path) -Content ("after-{0}" -f $case.name)
            $after = Invoke-Inspection -ProjectPath $fixture -AttestationPath $attestation
            Assert-True -Condition ($after.status -ne 'Ready') `
                -Message ("A stale {0} retained Ready." -f $case.name)
            Assert-True -Condition ($after.integration.validation.binding.rejectedEvidence.reason -contains `
                'AssetsDigestMismatch') -Message ("A stale {0} digest mismatch was not explicit." -f $case.name)
        }
    }

    Invoke-Test -Name 'Hidden and reparse validation inputs make inspection indeterminate' -Body {
        $hiddenFixture = New-UnityFixture -RunRoot $runRoot -Name 'hidden-relevant-input' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $hiddenFixture
        Add-PageContract -Root $hiddenFixture
        $hiddenPath = Join-Path $hiddenFixture 'Assets\AppUI\HiddenPanel.prefab'
        Write-Utf8File -Path $hiddenPath -Content 'hidden-prefab'
        [System.IO.File]::SetAttributes($hiddenPath, [System.IO.FileAttributes]::Hidden)
        $hiddenResult = Invoke-Inspection -ProjectPath $hiddenFixture
        Assert-Equal -Expected $true -Actual ([bool]$hiddenResult.project.scanIndeterminate) `
            -Message 'A hidden validation-relevant file was silently skipped.'
        Assert-True -Condition ($hiddenResult.status -ne 'Ready') `
            -Message 'A hidden validation-relevant file retained Ready.'
        $hiddenArtifacts = Join-Path $runRoot 'hidden-relevant-artifacts'
        [System.IO.Directory]::CreateDirectory($hiddenArtifacts) | Out-Null
        $hiddenOutput = Join-Path $hiddenArtifacts 'attestation.json'
        $hiddenBlocked = $false
        try {
            Invoke-ValidationProducer -ProjectPath $hiddenFixture -OutputPath $hiddenOutput | Out-Null
        }
        catch {
            $hiddenBlocked = $true
        }
        Assert-True -Condition $hiddenBlocked `
            -Message 'Producer attested a hidden validation-relevant input.'
        Assert-Equal -Expected $false -Actual (Test-Path -LiteralPath $hiddenOutput) `
            -Message 'Hidden-input producer failure left an attestation.'

        $reparseFixture = New-UnityFixture -RunRoot $runRoot -Name 'reparse-relevant-input' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $reparseFixture
        Add-PageContract -Root $reparseFixture
        $outside = Join-Path $runRoot 'reparse-relevant-outside'
        Write-Utf8File -Path (Join-Path $outside 'Panel.prefab') -Content 'outside-prefab'
        $junction = Join-Path $reparseFixture 'Assets\LinkedValidationInputs'
        New-Item -ItemType Junction -Path $junction -Target $outside | Out-Null
        $script:JunctionPaths.Add($junction) | Out-Null
        $reparseResult = Invoke-Inspection -ProjectPath $reparseFixture
        Assert-Equal -Expected $true -Actual ([bool]$reparseResult.project.scanIndeterminate) `
            -Message 'A reparse validation-input subtree was silently skipped.'
        Assert-True -Condition ($reparseResult.status -ne 'Ready') `
            -Message 'A reparse validation-input subtree retained Ready.'
        $reparseArtifacts = Join-Path $runRoot 'reparse-relevant-artifacts'
        [System.IO.Directory]::CreateDirectory($reparseArtifacts) | Out-Null
        $reparseOutput = Join-Path $reparseArtifacts 'attestation.json'
        $reparseBlocked = $false
        try {
            Invoke-ValidationProducer -ProjectPath $reparseFixture -OutputPath $reparseOutput | Out-Null
        }
        catch {
            $reparseBlocked = $true
        }
        Assert-True -Condition $reparseBlocked `
            -Message 'Producer attested a reparse validation-input subtree.'
        Assert-Equal -Expected $false -Actual (Test-Path -LiteralPath $reparseOutput) `
            -Message 'Reparse-input producer failure left an attestation.'
    }

    Invoke-Test -Name 'Hidden project facts make validation binding indeterminate' -Body {
        $fixture = New-UnityFixture -RunRoot $runRoot -Name 'hidden-project-fact' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $fixture
        Add-RuntimeRoot -Root $fixture
        Add-PageContract -Root $fixture
        Add-GeneratedBinding -Root $fixture
        $settingsPath = Join-Path $fixture 'ProjectSettings\ProjectSettings.asset'
        Write-Utf8File -Path $settingsPath -Content 'PlayerSettings: {}'
        [System.IO.File]::SetAttributes($settingsPath, [System.IO.FileAttributes]::Hidden)
        $result = Invoke-Inspection -ProjectPath $fixture
        Assert-Equal -Expected $true -Actual ([bool]$result.project.scanIndeterminate) `
            -Message 'A hidden project fact retained a determinate validation binding.'
        Assert-True -Condition ($result.status -ne 'Ready') `
            -Message 'A hidden project fact retained Ready.'
    }

    Invoke-Test -Name 'Semicolon query keys and Git fragments are sanitized before JSON serialization' -Body {
        $reference = 'https://github.com/TechJoiH/JoiH-AppUI.git?path=/package;%2574oken=SEMICOLON_SECRET#main;token=FRAGMENT_SECRET'
        $fixture = New-UnityFixture -RunRoot $runRoot -Name 'semicolon-fragment-secret' `
            -AppUIReference $reference
        $raw = & $script:InspectorPath -ProjectPath $fixture
        $json = $raw -join [Environment]::NewLine
        Assert-True -Condition (-not $json.Contains('SEMICOLON_SECRET')) `
            -Message 'A semicolon-delimited recursively encoded query secret leaked.'
        Assert-True -Condition (-not $json.Contains('FRAGMENT_SECRET')) `
            -Message 'A sensitive Git fragment value leaked.'
        $result = $json | ConvertFrom-Json
        Assert-Equal -Expected 'main;token=<redacted>' -Actual $result.packages.appUI.gitRef `
            -Message 'Serialized gitRef was not sanitized.'
    }

    $ordinaryFolder = New-OrdinaryFolder -RunRoot $runRoot -Name 'ordinary'
    $unityVersionUnknown = New-UnityFixture -RunRoot $runRoot -Name 'unity-version-unknown' -IncludeUnityVersion $false
    $unityWithoutAppUI = New-UnityFixture -RunRoot $runRoot -Name 'unity-without-appui'
    $appUIManifestOnly = New-UnityFixture -RunRoot $runRoot -Name 'appui-manifest-only' -AppUIReference $officialTag

    $runtimeHostWithoutPorts = New-UnityFixture -RunRoot $runRoot -Name 'host-without-ports' -AppUIReference $officialTag
    Add-SourceFile -Root $runtimeHostWithoutPorts -RelativePath 'Assets\AppUI\Runtime\Host.cs' -Content @'
using Joi.H.AppUI;
public sealed class HostBootstrap {
    private AppUIRuntimeHost runtimeHost;
}
'@

    $portsWithoutRuntimeRoot = New-UnityFixture -RunRoot $runRoot -Name 'ports-without-root' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $portsWithoutRuntimeRoot

    $runtimeWithoutPageContract = New-UnityFixture -RunRoot $runRoot -Name 'runtime-without-page' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $runtimeWithoutPageContract
    Add-RuntimeRoot -Root $runtimeWithoutPageContract

    $pageWithoutBindings = New-UnityFixture -RunRoot $runRoot -Name 'page-without-bindings' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $pageWithoutBindings
    Add-RuntimeRoot -Root $pageWithoutBindings
    Add-PageContract -Root $pageWithoutBindings

    $allCandidatesWithoutEvidence = New-UnityFixture -RunRoot $runRoot `
        -Name 'all-candidates-without-evidence' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $allCandidatesWithoutEvidence
    Add-RuntimeRoot -Root $allCandidatesWithoutEvidence
    Add-PageContract -Root $allCandidatesWithoutEvidence
    Add-GeneratedBinding -Root $allCandidatesWithoutEvidence

    $runtimeValidationPending = New-UnityFixture -RunRoot $runRoot -Name 'runtime-validation-pending' -AppUIReference $officialTag
    Add-AppUIHostAndPorts -Root $runtimeValidationPending
    Add-RuntimeRoot -Root $runtimeValidationPending
    Add-PageContract -Root $runtimeValidationPending
    Add-GeneratedBinding -Root $runtimeValidationPending
    Add-SourceFile -Root $runtimeValidationPending -RelativePath 'Assets\AppUI\Pages\StatusWords.cs' -Content @'
// These words are not validation evidence: BindingInvalid RuntimeValidationPending Ready Passed.
public static class StatusWords { }
'@

    $completeFixture = New-UnityFixture -RunRoot $runRoot -Name 'complete' -AppUIReference $officialTag `
        -ExtraDependencies @{
            'com.unity.textmeshpro' = '3.0.6'
            'com.unity.addressables' = '2.3.16'
            'com.cysharp.unitask' = 'https://github.com/Cysharp/UniTask.git#2.5.10'
        }
    Add-AppUIHostAndPorts -Root $completeFixture
    Add-RuntimeRoot -Root $completeFixture
    Add-PageContract -Root $completeFixture
    Add-GeneratedBinding -Root $completeFixture
    Write-Utf8File -Path (Join-Path $completeFixture 'ProjectSettings\ProjectSettings.asset') -Content @'
PlayerSettings:
  scriptingDefineSymbols:
    Standalone: EXISTING_DEFINE;JOIH_APPUI_TMP
'@
    Add-SourceFile -Root $completeFixture `
        -RelativePath 'Assets\Samples\Joi.H AppUI\0.4.0-pre.1\Basic Integration\BasicSample.cs' `
        -Content 'public sealed class BasicSample { }'
    Add-SourceFile -Root $completeFixture `
        -RelativePath 'Assets\Samples\Joi.H AppUI\0.4.0-pre.1\Custom Host Integration\CustomSample.cs' `
        -Content 'public sealed class CustomSample { }'
    Add-SourceFile -Root $completeFixture `
        -RelativePath 'Assets\Samples\Joi.H AppUI\0.4.0-pre.1\TextMeshPro Integration\TMPSample.cs' `
        -Content 'public sealed class TMPSample { }'

    $envSentinel = 'APPUI_ENV_SENTINEL_8B7E55A9'
    Write-Utf8File -Path (Join-Path $completeFixture 'Assets\Config\.env') -Content ("TOKEN={0}" -f $envSentinel)
    Add-RealValidationAttestation -Root $completeFixture

    $outsideRoot = Join-Path $runRoot 'outside-project-tree'
    [System.IO.Directory]::CreateDirectory($outsideRoot) | Out-Null
    $reparseSentinel = 'APPUI_REPARSE_SENTINEL_0C72E14D'
    Write-Utf8File -Path (Join-Path $outsideRoot 'EscapedAppUIRuntimeHost.cs') `
        -Content ("// {0}`npublic sealed class Escaped {{ AppUIRuntimeHost host; }}" -f $reparseSentinel)
    $reparseEscapeFixture = New-UnityFixture -RunRoot $runRoot -Name 'reparse-escape' `
        -AppUIReference $officialTag
    $script:JunctionPath = Join-Path $reparseEscapeFixture 'Assets\LinkedOutside'
    New-Item -ItemType Junction -Path $script:JunctionPath -Target $outsideRoot | Out-Null
    $script:JunctionPaths.Add($script:JunctionPath) | Out-Null

    Invoke-Test -Name 'Status precedence covers integration progression and evidence-gated completion' -Body {
        Assert-Status -Expected 'NotAUnityProject' -ProjectPath $ordinaryFolder
        Assert-Status -Expected 'UnityVersionUnverified' -ProjectPath $unityVersionUnknown
        Assert-Status -Expected 'AppUINotInstalled' -ProjectPath $unityWithoutAppUI
        Assert-Status -Expected 'InstalledNotInitialized' -ProjectPath $appUIManifestOnly
        Assert-Status -Expected 'HostBoundariesMissing' -ProjectPath $runtimeHostWithoutPorts
        Assert-Status -Expected 'RuntimeRootIncomplete' -ProjectPath $portsWithoutRuntimeRoot
        Assert-Status -Expected 'PageContractIncomplete' -ProjectPath $runtimeWithoutPageContract
        Assert-Status -Expected 'BindingGenerationPending' -ProjectPath $pageWithoutBindings
        Assert-Status -Expected 'RuntimeValidationPending' -ProjectPath $allCandidatesWithoutEvidence
        Assert-Status -Expected 'RuntimeValidationPending' -ProjectPath $runtimeValidationPending
        Assert-Status -Expected 'Ready' -ProjectPath $completeFixture
    }

    Invoke-Test -Name 'Unity root and package facts are exact' -Body {
        $nestedPath = Join-Path $completeFixture 'Assets\AppUI\Pages'
        $result = Invoke-Inspection -ProjectPath $nestedPath
        Assert-Equal -Expected ([System.IO.Path]::GetFullPath($completeFixture)) -Actual $result.project.root `
            -Message 'Unity root discovery failed.'
        Assert-Equal -Expected '6000.0.25f1' -Actual $result.project.unityVersion `
            -Message 'Unity version parsing failed.'
        Assert-True -Condition ([bool]$result.packages.ugui.installed) -Message 'UGUI package was not detected.'
        Assert-Equal -Expected '2.0.0' -Actual $result.packages.ugui.version -Message 'UGUI version is wrong.'
        Assert-True -Condition ([bool]$result.packages.textMeshPro.installed) -Message 'TMP package was not detected.'
        Assert-Equal -Expected '3.0.6' -Actual $result.packages.textMeshPro.version -Message 'TMP version is wrong.'
        Assert-True -Condition ($result.packages.asyncCandidates.name -contains 'com.cysharp.unitask') `
            -Message 'Async package candidate was not reported.'
        Assert-True -Condition ($result.packages.assetCandidates.name -contains 'com.unity.addressables') `
            -Message 'Asset package candidate was not reported.'
    }

    Invoke-Test -Name 'SemVer Git fragment is an unverified offline Tag candidate' -Body {
        $result = Invoke-Inspection -ProjectPath $appUIManifestOnly
        Assert-Equal -Expected $officialTag -Actual $result.packages.appUI.manifestReference `
            -Message 'Manifest AppUI reference changed.'
        Assert-Equal -Expected 'Git' -Actual $result.packages.appUI.installSource `
            -Message 'Git source was not classified.'
        Assert-Equal -Expected 'v0.4.0-pre.1' -Actual $result.packages.appUI.gitRef `
            -Message 'Git fragment was not parsed exactly.'
        Assert-Equal -Expected 'TagCandidate' -Actual $result.packages.appUI.gitRefKind `
            -Message 'SemVer fragment was not classified as an offline Tag candidate.'
        Assert-Equal -Expected '0.4.0-pre.1' -Actual $result.packages.appUI.version `
            -Message 'AppUI version was not derived from the exact Tag.'
        Assert-Equal -Expected $null -Actual $result.packages.appUI.mutable `
            -Message 'Offline Tag candidate claimed a definitive mutable/immutable value.'
        Assert-Equal -Expected 'UnverifiedOffline' -Actual $result.packages.appUI.immutability `
            -Message 'Offline Tag identity uncertainty was not explicit.'
        Assert-Equal -Expected $false -Actual ([bool]$result.packages.appUI.tagIdentityVerified) `
            -Message 'Offline parsing claimed remote Tag identity verification.'
        Assert-True -Condition ($result.issues.code -contains 'APPUI_TAG_IDENTITY_UNVERIFIED') `
            -Message 'Offline Tag identity issue is missing.'
    }

    Invoke-Test -Name 'Mutable and commit Git references are distinguished' -Body {
        $branchFixture = New-UnityFixture -RunRoot $runRoot -Name 'git-branch' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#main'
        $unversionedFixture = New-UnityFixture -RunRoot $runRoot -Name 'git-unversioned' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git'
        $commitFixture = New-UnityFixture -RunRoot $runRoot -Name 'git-commit' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#0123456789abcdef0123456789abcdef01234567'

        $branch = Invoke-Inspection -ProjectPath $branchFixture
        Assert-Equal -Expected 'main' -Actual $branch.packages.appUI.gitRef -Message 'Branch ref changed.'
        Assert-Equal -Expected 'Branch' -Actual $branch.packages.appUI.gitRefKind -Message 'main was not a branch.'
        Assert-Equal -Expected $true -Actual ([bool]$branch.packages.appUI.mutable) -Message 'Branch was not mutable.'
        Assert-Equal -Expected 'Mutable' -Actual $branch.packages.appUI.immutability `
            -Message 'Branch mutability was not explicit.'

        $unversioned = Invoke-Inspection -ProjectPath $unversionedFixture
        Assert-Equal -Expected $null -Actual $unversioned.packages.appUI.gitRef -Message 'Unversioned Git gained a ref.'
        Assert-Equal -Expected 'Unversioned' -Actual $unversioned.packages.appUI.gitRefKind `
            -Message 'Unversioned Git was misclassified.'
        Assert-Equal -Expected $true -Actual ([bool]$unversioned.packages.appUI.mutable) `
            -Message 'Unversioned Git was not mutable.'
        Assert-Equal -Expected 'Mutable' -Actual $unversioned.packages.appUI.immutability `
            -Message 'Unversioned Git mutability was not explicit.'

        $commit = Invoke-Inspection -ProjectPath $commitFixture
        Assert-Equal -Expected 'Commit' -Actual $commit.packages.appUI.gitRefKind `
            -Message 'Full Git SHA was not immutable.'
        Assert-Equal -Expected $false -Actual ([bool]$commit.packages.appUI.mutable) `
            -Message 'Full Git SHA was marked mutable.'
        Assert-Equal -Expected 'PinnedCommit' -Actual $commit.packages.appUI.immutability `
            -Message 'Full Git SHA pin was not explicit.'
    }

    Invoke-Test -Name 'Only exact SemVer 2.0 Git fragments are Tag candidates' -Body {
        $invalidNumeric = New-UnityFixture -RunRoot $runRoot -Name 'invalid-semver-leading-zero' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#v1.2.3-01'
        $invalidEmpty = New-UnityFixture -RunRoot $runRoot -Name 'invalid-semver-empty-identifier' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#v1.2.3-alpha..1'
        $validComplex = New-UnityFixture -RunRoot $runRoot -Name 'valid-semver-complex' `
            -AppUIReference 'https://github.com/TechJoiH/JoiH-AppUI.git#v1.2.3-0A.0+build.01'

        $invalidNumericResult = Invoke-Inspection -ProjectPath $invalidNumeric
        Assert-Equal -Expected 'Branch' -Actual $invalidNumericResult.packages.appUI.gitRefKind `
            -Message 'SemVer numeric identifier with a leading zero was accepted.'
        $invalidEmptyResult = Invoke-Inspection -ProjectPath $invalidEmpty
        Assert-Equal -Expected 'Branch' -Actual $invalidEmptyResult.packages.appUI.gitRefKind `
            -Message 'SemVer with an empty prerelease identifier was accepted.'
        $validComplexResult = Invoke-Inspection -ProjectPath $validComplex
        Assert-Equal -Expected 'TagCandidate' -Actual $validComplexResult.packages.appUI.gitRefKind `
            -Message 'Valid SemVer 2.0 fragment was rejected.'
        Assert-Equal -Expected '1.2.3-0A.0+build.01' -Actual $validComplexResult.packages.appUI.version `
            -Message 'Valid complex SemVer version changed.'

        $unicodeDigit = [char]0x0661
        $unicodeNumeric = New-UnityFixture -RunRoot $runRoot -Name 'invalid-semver-unicode-digit' `
            -AppUIReference ("https://github.com/TechJoiH/JoiH-AppUI.git#v1.2.3-1{0}" -f $unicodeDigit)
        $unicodeNumericResult = Invoke-Inspection -ProjectPath $unicodeNumeric
        Assert-Equal -Expected 'Branch' -Actual $unicodeNumericResult.packages.appUI.gitRefKind `
            -Message 'A non-ASCII digit was accepted by the SemVer numeric grammar.'
    }

    Invoke-Test -Name 'Package URI facts redact userinfo and sensitive query values' -Body {
        $credentialReference = 'https://alice:USERINFO_SECRET@github.com/TechJoiH/JoiH-AppUI.git?path=/package&token=QUERY_SECRET&api_key=KEY_SECRET#v0.4.0-pre.1'
        $credentialFixture = New-UnityFixture -RunRoot $runRoot -Name 'credential-uri' `
            -AppUIReference $credentialReference

        $raw = & $script:InspectorPath -ProjectPath $credentialFixture
        $json = $raw -join [Environment]::NewLine
        Assert-True -Condition (-not $json.Contains('alice')) -Message 'URI username leaked into raw JSON.'
        Assert-True -Condition (-not $json.Contains('USERINFO_SECRET')) -Message 'URI password leaked into raw JSON.'
        Assert-True -Condition (-not $json.Contains('QUERY_SECRET')) -Message 'Sensitive token query value leaked.'
        Assert-True -Condition (-not $json.Contains('KEY_SECRET')) -Message 'Sensitive API key query value leaked.'

        $result = $json | ConvertFrom-Json
        $sanitized = 'https://github.com/TechJoiH/JoiH-AppUI.git?path=/package&token=<redacted>&api_key=<redacted>#v0.4.0-pre.1'
        Assert-Equal -Expected $sanitized -Actual $result.packages.appUI.manifestReference `
            -Message 'Manifest reference was not safely redacted.'
        Assert-Equal -Expected $sanitized -Actual $result.packages.appUI.lockReference `
            -Message 'Lock reference was not safely redacted.'
    }

    Invoke-Test -Name 'Percent-encoded sensitive query keys are fully decoded before classification' -Body {
        $deepEncodedKey = '%74oken'
        for ($encodePass = 0; $encodePass -lt 12; $encodePass++) {
            $deepEncodedKey = [System.Uri]::EscapeDataString($deepEncodedKey)
        }
        $encodedReference = 'https://github.com/TechJoiH/JoiH-AppUI.git?%74oken=ENCODED_SECRET&%2574oken=DOUBLE_ENCODED_SECRET&' + `
            $deepEncodedKey + '=DEEPLY_ENCODED_SECRET#main'
        $encodedFixture = New-UnityFixture -RunRoot $runRoot -Name 'encoded-query-key' `
            -AppUIReference $encodedReference

        $raw = & $script:InspectorPath -ProjectPath $encodedFixture
        $json = $raw -join [Environment]::NewLine
        Assert-True -Condition (-not $json.Contains('ENCODED_SECRET')) `
            -Message 'A percent-encoded token key leaked its value.'
        Assert-True -Condition (-not $json.Contains('DOUBLE_ENCODED_SECRET')) `
            -Message 'A repeatedly encoded token key leaked its value.'
        Assert-True -Condition (-not $json.Contains('DEEPLY_ENCODED_SECRET')) `
            -Message 'A deeply encoded token key leaked its value.'
    }

    Invoke-Test -Name 'Defines, Samples and likely integration candidates are reported' -Body {
        $result = Invoke-Inspection -ProjectPath $completeFixture
        Assert-True -Condition ($result.integration.defines -contains 'JOIH_APPUI_TMP') `
            -Message 'JOIH_APPUI_TMP define was not reported.'
        Assert-True -Condition ([bool]$result.integration.textMeshProDefineEnabled) `
            -Message 'TMP opt-in define was not enabled.'
        Assert-True -Condition ([bool]$result.integration.hostBoundaries.operationFactory.present) `
            -Message 'IUIOperationFactory candidate was not found.'
        Assert-True -Condition ([bool]$result.integration.hostBoundaries.assetProvider.present) `
            -Message 'IUIAssetProvider candidate was not found.'
        Assert-True -Condition ([bool]$result.integration.hostBoundaries.executionContext.present) `
            -Message 'IAppUIExecutionContext candidate was not found.'
        Assert-True -Condition ([bool]$result.samples.basicIntegration.imported) `
            -Message 'Basic Integration Sample was not detected.'
        Assert-True -Condition ([bool]$result.samples.customHostIntegration.imported) `
            -Message 'Custom Host Integration Sample was not detected.'
        Assert-True -Condition ([bool]$result.samples.textMeshProIntegration.imported) `
            -Message 'TextMeshPro Integration Sample was not detected.'
        Assert-True -Condition ($result.samples.basicIntegration.paths -contains `
            'Assets/Samples/Joi.H AppUI/0.4.0-pre.1/Basic Integration') `
            -Message 'Imported Sample path was not project-relative.'
    }

    Invoke-Test -Name 'An asmdef constraint does not enable the project TMP define' -Body {
        $constraintOnly = New-UnityFixture -RunRoot $runRoot -Name 'asmdef-constraint-only' `
            -AppUIReference $officialTag
        Add-SourceFile -Root $constraintOnly -RelativePath 'Assets\OptionalTMP.asmdef' -Content @'
{
  "name": "OptionalTMP",
  "defineConstraints": [ "JOIH_APPUI_TMP" ]
}
'@

        $result = Invoke-Inspection -ProjectPath $constraintOnly
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.textMeshProDefineEnabled) `
            -Message 'An asmdef consumer constraint was mistaken for an enabled scripting define.'
        Assert-True -Condition ($result.integration.asmdefDefineConstraints -contains 'JOIH_APPUI_TMP') `
            -Message 'Asmdef define constraints were not reported separately.'
    }

    Invoke-Test -Name 'Imported Sample code does not prove project initialization' -Body {
        $sampleOnly = New-UnityFixture -RunRoot $runRoot -Name 'imported-sample-only' `
            -AppUIReference $officialTag
        $sampleRoot = 'Assets\Samples\Joi.H AppUI\0.4.0-pre.1\Basic Integration'
        Add-SourceFile -Root $sampleOnly -RelativePath (Join-Path $sampleRoot 'SampleAppUIInstaller.cs') -Content @'
public sealed class SampleOperationFactory : IUIOperationFactory { }
public sealed class SampleAssetProvider : IUIAssetProvider { }
public sealed class SampleExecutionContext : IAppUIExecutionContext { }
public sealed class SamplePanelController : PanelBaseController {
    private AppUIRuntimeHost runtimeHost;
    private AppUIManager manager;
    private UILayerRoot layerRoot;
}
'@
        Add-SourceFile -Root $sampleOnly -RelativePath (Join-Path $sampleRoot 'SamplePanelController.Bindings.cs') `
            -Content 'public partial class SamplePanelController { }'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'SampleAppUIRuntimeProfile.asset')) `
            -Content 'm_ScriptType: AppUIRuntimeProfile'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'SampleUIPageDefinitionRegistry.asset')) `
            -Content 'm_ScriptType: UIPageDefinitionRegistry'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'SampleUIPageDefinition.asset')) `
            -Content 'm_ScriptType: UIPageDefinition'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'SampleUIBindingSettings.asset')) `
            -Content 'm_ScriptType: UIBindingSettings'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'AppUIBindingValidationReport.asset')) `
            -Content 'status: Passed'
        Write-Utf8File -Path (Join-Path $sampleOnly (Join-Path $sampleRoot 'AppUIRuntimeValidationReport.asset')) `
            -Content 'status: Passed'

        $result = Invoke-Inspection -ProjectPath $sampleOnly
        Assert-Equal -Expected 'InstalledNotInitialized' -Actual $result.status `
            -Message 'Merely imported Sample files were treated as a project-owned Runtime.'
        Assert-Equal -Expected $true -Actual ([bool]$result.samples.basicIntegration.imported) `
            -Message 'The imported Sample itself was not reported.'
    }

    Invoke-Test -Name 'Heuristic filenames regexes and comments never prove completion' -Body {
        $heuristicOnly = New-UnityFixture -RunRoot $runRoot -Name 'heuristic-only' -AppUIReference $officialTag
        Add-SourceFile -Root $heuristicOnly -RelativePath 'Assets\AppUI\HeuristicOnly.cs' -Content @'
// public sealed class CommentOperationFactory : IUIOperationFactory { }
// public sealed class CommentAssetProvider : IUIAssetProvider { }
// public sealed class CommentExecutionContext : IAppUIExecutionContext { }
// public sealed class CommentPanelController : PanelBaseController { }
// AppUIRuntimeHost AppUIManager UILayerRoot
'@
        Write-Utf8File -Path (Join-Path $heuristicOnly 'Assets\AppUI\MainAppUIRuntimeProfile.asset') `
            -Content 'm_Name: filename-only'
        Write-Utf8File -Path (Join-Path $heuristicOnly 'Assets\AppUI\MainUIPageDefinitionRegistry.asset') `
            -Content 'm_Name: filename-only'
        Write-Utf8File -Path (Join-Path $heuristicOnly 'Assets\AppUI\SettingsPageDefinition.asset') `
            -Content 'm_Name: filename-only'
        Write-Utf8File -Path (Join-Path $heuristicOnly 'Assets\AppUI\MainUIBindingSettings.asset') `
            -Content 'm_Name: filename-only'
        Add-SourceFile -Root $heuristicOnly -RelativePath 'Assets\AppUI\CommentPanelController.Bindings.cs' `
            -Content '// filename-only generated-binding candidate'

        $result = Invoke-Inspection -ProjectPath $heuristicOnly
        Assert-True -Condition ([bool]$result.integration.hostBoundaries.candidateCoverageComplete) `
            -Message 'Heuristic host-boundary coverage was not reported as a candidate fact.'
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.hostBoundaries.complete) `
            -Message 'Heuristic host-boundary text was treated as verified completion.'
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.runtimeRoot.complete) `
            -Message 'Heuristic Runtime-root filenames were treated as verified completion.'
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.pageContract.complete) `
            -Message 'Heuristic page-contract names were treated as verified completion.'
        Assert-Equal -Expected $false -Actual ([bool]$result.integration.binding.generationComplete) `
            -Message 'A .Bindings.cs filename was treated as verified generation.'
        Assert-True -Condition ($result.status -ne 'Ready') -Message 'Heuristic candidates produced Ready.'
        Assert-True -Condition (-not ($result.integration.hostBoundaries.operationFactory.candidates.confidence -contains 'Verified')) `
            -Message 'Text-search candidate confidence claimed verification.'
    }

    Invoke-Test -Name 'Validation states require explicit evidence' -Body {
        $pending = Invoke-Inspection -ProjectPath $runtimeValidationPending
        Assert-Equal -Expected 'Unknown' -Actual $pending.integration.validation.binding.status `
            -Message 'A caller-declared Binding outcome was trusted.'
        Assert-Equal -Expected 'Unknown' -Actual $pending.integration.validation.runtime.status `
            -Message 'Source text was treated as Runtime validation evidence.'

        $withoutEvidence = Invoke-Inspection -ProjectPath $allCandidatesWithoutEvidence
        Assert-Equal -Expected 'Unknown' -Actual $withoutEvidence.integration.validation.binding.status `
            -Message 'Candidate coverage was promoted to a Binding validation result.'

        $ready = Invoke-Inspection -ProjectPath $completeFixture
        Assert-Equal -Expected 'Passed' -Actual $ready.integration.validation.binding.status `
            -Message 'Explicit Binding pass evidence was ignored.'
        Assert-Equal -Expected 'Passed' -Actual $ready.integration.validation.runtime.status `
            -Message 'Explicit Runtime pass evidence was ignored.'
    }

    Invoke-Test -Name 'Validation evidence is exact produced and bound to current project facts' -Body {
        $ready = Invoke-Inspection -ProjectPath $completeFixture
        Assert-Equal -Expected 'joih-appui-project-validation-attestation.v3' `
            -Actual $ready.integration.validation.contract.schemaVersion `
            -Message 'Validation evidence schema contract was not reported.'
        Assert-Equal -Expected 'integrating-joih-appui/new-appui-validation-attestation.ps1' `
            -Actual $ready.integration.validation.contract.producer `
            -Message 'Validation evidence producer contract was not reported.'
        Assert-Equal -Expected 'Project' -Actual $ready.integration.validation.contract.owner `
            -Message 'The attestation contract did not identify the project as its owner.'
        Assert-Equal -Expected 'validation-relevant-project-files-v2' `
            -Actual $ready.integration.validation.contract.assetsDigestScope `
            -Message 'The attestation contract did not document its canonical Assets digest scope.'
        Assert-Equal -Expected 'Bound' -Actual $ready.integration.validation.binding.evidence[0].binding `
            -Message 'Valid Binding evidence was not bound to current facts.'

        $handAuthored = New-UnityFixture -RunRoot $runRoot -Name 'hand-authored-evidence' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $handAuthored
        Add-RuntimeRoot -Root $handAuthored
        Add-PageContract -Root $handAuthored
        Add-GeneratedBinding -Root $handAuthored
        $handAttestation = Join-Path $runRoot 'hand-authored-attestation.json'
        Write-Utf8File -Path $handAttestation -Content '{"status":"Passed"}'
        $handResult = Invoke-Inspection -ProjectPath $handAuthored -AttestationPath $handAttestation
        Assert-True -Condition ($handResult.status -ne 'Ready') `
            -Message 'Hand-authored status text produced Ready.'
        Assert-Equal -Expected 'Unknown' -Actual $handResult.integration.validation.binding.status `
            -Message 'Hand-authored Binding status was trusted.'
        Assert-True -Condition ($handResult.integration.validation.binding.rejectedEvidence.reason -contains 'SchemaMismatch') `
            -Message 'Schema-less status text was not rejected explicitly.'

        $wrongProducer = New-UnityFixture -RunRoot $runRoot -Name 'wrong-producer-evidence' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $wrongProducer
        Add-RuntimeRoot -Root $wrongProducer
        Add-PageContract -Root $wrongProducer
        Add-GeneratedBinding -Root $wrongProducer
        Add-RealValidationAttestation -Root $wrongProducer -Producer 'Hand.Authored.Status'
        $wrongProducerResult = Invoke-Inspection -ProjectPath $wrongProducer
        Assert-True -Condition ($wrongProducerResult.status -ne 'Ready') `
            -Message 'Unrecognized evidence producer produced Ready.'
        Assert-True -Condition ($wrongProducerResult.integration.validation.binding.rejectedEvidence.reason -contains 'ProducerMismatch') `
            -Message 'Unrecognized producer was not rejected explicitly.'

        $stale = New-UnityFixture -RunRoot $runRoot -Name 'stale-evidence' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $stale
        Add-RuntimeRoot -Root $stale
        Add-PageContract -Root $stale
        Add-GeneratedBinding -Root $stale
        Add-RealValidationAttestation -Root $stale
        $manifestPath = Join-Path $stale 'Packages\manifest.json'
        $manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
        $manifest.dependencies | Add-Member -NotePropertyName 'com.example.changed' -NotePropertyValue '1.0.0'
        Write-Utf8File -Path $manifestPath -Content ($manifest | ConvertTo-Json -Depth 10)
        $staleResult = Invoke-Inspection -ProjectPath $stale
        Assert-True -Condition ($staleResult.status -ne 'Ready') -Message 'Stale evidence produced Ready.'
        Assert-Equal -Expected 'Unknown' -Actual $staleResult.integration.validation.binding.status `
            -Message 'Stale Binding evidence was trusted.'
        Assert-True -Condition ($staleResult.integration.validation.binding.rejectedEvidence.reason -contains 'ProjectFactsMismatch') `
            -Message 'Stale project binding was not rejected explicitly.'
    }

    Invoke-Test -Name 'Genuine Binding failure produces bounded evidence and BindingInvalid' -Body {
        $failedBinding = New-UnityFixture -RunRoot $runRoot -Name 'failed-binding-producer' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $failedBinding
        Add-RuntimeRoot -Root $failedBinding
        Add-PageContract -Root $failedBinding
        Add-GeneratedBinding -Root $failedBinding
        $artifactRoot = Join-Path $runRoot 'failed-binding-artifacts'
        [System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
        $output = Join-Path $artifactRoot 'failed-binding-attestation.json'
        $invocationLog = Join-Path $artifactRoot 'failed-binding-invocations.log'
        Invoke-ValidationProducer -ProjectPath $failedBinding -OutputPath $output `
            -Scenario 'BindingFail' -InvocationLogPath $invocationLog | Out-Null
        Assert-True -Condition (Test-Path -LiteralPath $output -PathType Leaf) `
            -Message 'Genuine Binding failure did not produce a current attestation.'
        $attestation = [System.IO.File]::ReadAllText($output) | ConvertFrom-Json
        Assert-Equal -Expected 'Failed' -Actual $attestation.binding.status `
            -Message 'Binding validation failure was not preserved.'
        Assert-Equal -Expected 'NotRun' -Actual $attestation.runtime.status `
            -Message 'Runtime tests ran after Binding validation failed.'
        $invocations = @(Read-FakeUnityInvocations -Path $invocationLog)
        Assert-Equal -Expected 1 -Actual $invocations.Count `
            -Message 'Binding failure invoked Unity more than once.'
        Assert-True -Condition (-not ($invocations[0] -contains '-runTests')) `
            -Message 'Binding failure still invoked Unity Test Runner.'
        $inspection = Invoke-Inspection -ProjectPath $failedBinding -AttestationPath $output
        Assert-Equal -Expected 'BindingInvalid' -Actual $inspection.status `
            -Message 'A genuine current Binding failure remained unreachable.'
        Assert-Equal -Expected 'Failed' -Actual $inspection.integration.validation.binding.status `
            -Message 'Inspector did not expose the bound Binding failure.'
    }

    Invoke-Test -Name 'Binding pass without genuine lifecycle result remains RuntimeValidationPending' -Body {
        $missingLifecycle = New-UnityFixture -RunRoot $runRoot -Name 'missing-lifecycle-report' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $missingLifecycle
        Add-RuntimeRoot -Root $missingLifecycle
        Add-PageContract -Root $missingLifecycle
        Add-GeneratedBinding -Root $missingLifecycle
        $lifecycleRoot = Join-Path $runRoot 'missing-lifecycle-artifacts'
        [System.IO.Directory]::CreateDirectory($lifecycleRoot) | Out-Null
        $lifecycleOutput = Join-Path $lifecycleRoot 'missing-lifecycle-attestation.json'
        Invoke-ValidationProducer -ProjectPath $missingLifecycle -OutputPath $lifecycleOutput `
            -Scenario 'RuntimeMissingLifecycle' | Out-Null
        $lifecycleAttestation = [System.IO.File]::ReadAllText($lifecycleOutput) | ConvertFrom-Json
        Assert-Equal -Expected 'Passed' -Actual $lifecycleAttestation.binding.status `
            -Message 'Valid Binding pass was discarded when lifecycle evidence was incomplete.'
        Assert-Equal -Expected 'Failed' -Actual $lifecycleAttestation.runtime.status `
            -Message 'Missing required lifecycle names were not recorded as a runtime failure.'
        $lifecycleInspection = Invoke-Inspection -ProjectPath $missingLifecycle `
            -AttestationPath $lifecycleOutput
        Assert-Equal -Expected 'RuntimeValidationPending' -Actual $lifecycleInspection.status `
            -Message 'Missing lifecycle evidence produced a definitive Ready status.'

        $noRuntime = New-UnityFixture -RunRoot $runRoot -Name 'no-runtime-report' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $noRuntime
        Add-RuntimeRoot -Root $noRuntime
        Add-PageContract -Root $noRuntime
        Add-GeneratedBinding -Root $noRuntime
        $noRuntimeOutput = Join-Path $lifecycleRoot 'no-runtime-attestation.json'
        Invoke-ValidationProducer -ProjectPath $noRuntime -OutputPath $noRuntimeOutput `
            -Scenario 'RuntimeNoReport' | Out-Null
        $noRuntimeAttestation = [System.IO.File]::ReadAllText($noRuntimeOutput) | ConvertFrom-Json
        Assert-Equal -Expected 'Pending' -Actual $noRuntimeAttestation.runtime.status `
            -Message 'Missing Unity Test Runner output was treated as a result.'
        $noRuntimeInspection = Invoke-Inspection -ProjectPath $noRuntime `
            -AttestationPath $noRuntimeOutput
        Assert-Equal -Expected 'RuntimeValidationPending' -Actual $noRuntimeInspection.status `
            -Message 'Binding-only evidence produced Ready.'
    }

    Invoke-Test -Name 'Attestation producer rejects protected existing and reparse output paths' -Body {
        $fixture = New-UnityFixture -RunRoot $runRoot -Name 'producer-output-safety' `
            -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $fixture
        Add-RuntimeRoot -Root $fixture
        Add-PageContract -Root $fixture
        Add-GeneratedBinding -Root $fixture
        $artifactRoot = Join-Path $runRoot 'producer-output-safety-artifacts'
        [System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
        $protectedOutput = Join-Path $fixture 'Assets\validation-attestation.json'
        $protectedBlocked = $false
        try {
            Invoke-ValidationProducer -ProjectPath $fixture -OutputPath $protectedOutput | Out-Null
        }
        catch {
            $protectedBlocked = $true
        }
        Assert-True -Condition $protectedBlocked `
            -Message 'Producer wrote an attestation into Assets.'
        Assert-Equal -Expected $false -Actual (Test-Path -LiteralPath $protectedOutput) `
            -Message 'Protected producer output exists.'

        $existingOutput = Join-Path $artifactRoot 'existing-attestation.json'
        Write-Utf8File -Path $existingOutput -Content 'EXISTING_PRODUCER_OUTPUT'
        $existingBlocked = $false
        try {
            Invoke-ValidationProducer -ProjectPath $fixture -OutputPath $existingOutput | Out-Null
        }
        catch {
            $existingBlocked = $true
        }
        Assert-True -Condition $existingBlocked -Message 'Producer overwrote an existing output target.'
        Assert-Equal -Expected 'EXISTING_PRODUCER_OUTPUT' `
            -Actual ([System.IO.File]::ReadAllText($existingOutput)) `
            -Message 'Existing producer output content changed.'

        $externalOutputRoot = Join-Path $runRoot 'producer-output-junction-target'
        [System.IO.Directory]::CreateDirectory($externalOutputRoot) | Out-Null
        $outputJunction = Join-Path $runRoot 'producer-output-junction'
        New-Item -ItemType Junction -Path $outputJunction -Target $externalOutputRoot | Out-Null
        $script:JunctionPaths.Add($outputJunction) | Out-Null
        $reparseOutput = Join-Path $outputJunction 'attestation.json'
        $reparseBlocked = $false
        try {
            Invoke-ValidationProducer -ProjectPath $fixture -OutputPath $reparseOutput | Out-Null
        }
        catch {
            $reparseBlocked = $true
        }
        Assert-True -Condition $reparseBlocked `
            -Message 'Producer wrote through an OutputPath reparse ancestor.'
        Assert-Equal -Expected $false `
            -Actual (Test-Path -LiteralPath (Join-Path $externalOutputRoot 'attestation.json')) `
            -Message 'Producer output escaped through a junction.'
    }

    Invoke-Test -Name 'Producer invokes exact Unity commands and binds run-owned reports' -Body {
        $produced = New-UnityFixture -RunRoot $runRoot -Name 'project-owned-producer' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $produced
        Add-RuntimeRoot -Root $produced
        Add-PageContract -Root $produced
        Add-GeneratedBinding -Root $produced

        $artifactRoot = Join-Path $runRoot 'project-owned-producer-artifacts'
        [System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
        $output = Join-Path $artifactRoot 'attestation.json'
        $invocationLog = Join-Path $artifactRoot 'unity-invocations.log'
        $stdout = Invoke-ValidationProducer -ProjectPath $produced -OutputPath $output `
            -InvocationLogPath $invocationLog
        $stdoutJson = $stdout -join [Environment]::NewLine
        $fileJson = [System.IO.File]::ReadAllText($output)
        Assert-Equal -Expected $stdoutJson -Actual $fileJson -Message 'Producer file bytes differ from stdout JSON.'
        $bytes = [System.IO.File]::ReadAllBytes($output)
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        Assert-Equal -Expected $false -Actual $hasBom -Message 'Producer output has a UTF-8 BOM.'
        $attestation = $fileJson | ConvertFrom-Json
        Assert-Equal -Expected 'joih-appui-project-validation-attestation.v3' `
            -Actual $attestation.schemaVersion -Message 'Producer emitted the wrong schema.'
        Assert-Equal -Expected 'Project' -Actual $attestation.owner `
            -Message 'Producer impersonated a package-owned validator.'
        Assert-Equal -Expected 'integrating-joih-appui/new-appui-validation-attestation.ps1' `
            -Actual $attestation.producer -Message 'Producer identity was not the real script flow.'
        Assert-True -Condition (-not [string]::IsNullOrWhiteSpace([string]$attestation.assetsDigest)) `
            -Message 'Producer omitted the canonical Assets digest.'
        Assert-True -Condition ([int]$attestation.assetsFileCount -gt 0) `
            -Message 'Producer did not bind validation-relevant Assets files.'
        Assert-Equal -Expected 'app-ui-binding-validation.v2' -Actual $attestation.binding.schemaVersion `
            -Message 'Producer did not bind the exact Binding report schema.'
        Assert-Equal -Expected 'Passed' -Actual $attestation.binding.status `
            -Message 'Producer did not bind a genuine Binding pass.'
        Assert-Equal -Expected 'NUnit3' -Actual $attestation.runtime.format `
            -Message 'Producer did not bind the Unity Test Runner result format.'
        Assert-Equal -Expected 'Passed' -Actual $attestation.runtime.status `
            -Message 'Producer did not bind genuine lifecycle passes.'
        Assert-True -Condition ([guid]::Parse([string]$attestation.run.runId) -ne [guid]::Empty) `
            -Message 'Producer omitted its unique validation run ID.'
        Assert-True -Condition ([string]$attestation.binding.reportSha256 -match '^[0-9a-f]{64}$') `
            -Message 'Binding report hash was not recorded.'
        Assert-True -Condition ([string]$attestation.runtime.reportSha256 -match '^[0-9a-f]{64}$') `
            -Message 'Runtime report hash was not recorded.'

        $invocations = @(Read-FakeUnityInvocations -Path $invocationLog)
        Assert-Equal -Expected 2 -Actual $invocations.Count `
            -Message 'Producer did not invoke exactly Binding then Unity Test Runner.'
        $bindingOutputIndex = [Array]::IndexOf($invocations[0], '-appUIValidationReportPath')
        $runtimeOutputIndex = [Array]::IndexOf($invocations[1], '-testResults')
        Assert-True -Condition ($bindingOutputIndex -ge 0 -and $runtimeOutputIndex -ge 0) `
            -Message 'Unity commands omitted run-owned output paths.'
        $bindingRunPath = $invocations[0][$bindingOutputIndex + 1]
        $runtimeRunPath = $invocations[1][$runtimeOutputIndex + 1]
        $validationRunRoot = Split-Path -Parent $bindingRunPath
        $expectedBindingArguments = @(
            '-batchmode', '-nographics', '-quit',
            '-projectPath', ([System.IO.Path]::GetFullPath($produced)),
            '-executeMethod', 'Joi.H.AppUI.Editor.Binding.UIBindingValidationCommandLine.ValidateAll',
            '-appUIBindingSettingsPath', 'Assets/AppUI/Settings/MainUIBindingSettings.asset',
            '-appUIValidationReportPath', (Join-Path $validationRunRoot 'app-ui-binding-validation.v2.json'),
            '-logFile', (Join-Path $validationRunRoot 'binding-unity.log')
        )
        $expectedRuntimeArguments = @(
            '-batchmode', '-nographics',
            '-projectPath', ([System.IO.Path]::GetFullPath($produced)),
            '-runTests', '-testPlatform', 'EditMode',
            '-testFilter', 'Joi.H.AppUI.Tests.Lifecycle',
            '-testResults', (Join-Path $validationRunRoot 'app-ui-lifecycle-tests.xml'),
            '-logFile', (Join-Path $validationRunRoot 'runtime-unity.log')
        )
        Assert-Equal -Expected ([string]::Join([char]0, $expectedBindingArguments)) `
            -Actual ([string]::Join([char]0, $invocations[0])) `
            -Message 'Binding Unity argument array was not exact or ordered.'
        Assert-Equal -Expected ([string]::Join([char]0, $expectedRuntimeArguments)) `
            -Actual ([string]::Join([char]0, $invocations[1])) `
            -Message 'Runtime Unity argument array was not exact or ordered.'
        Assert-True -Condition ($bindingRunPath -ne $runtimeRunPath -and
            -not (Test-Path -LiteralPath $bindingRunPath) -and
            -not (Test-Path -LiteralPath $runtimeRunPath)) `
            -Message 'Unique temporary validation reports were not cleaned after hashing.'

        $withoutAttestation = Invoke-Inspection -ProjectPath $produced
        Assert-Equal -Expected 'RuntimeValidationPending' -Actual $withoutAttestation.status `
            -Message 'Inspector became Ready without explicit attestation input.'
        $ready = Invoke-Inspection -ProjectPath $produced -AttestationPath $output
        Assert-Equal -Expected 'Ready' -Actual $ready.status `
            -Message 'Current real Binding and Runtime evidence did not produce Ready.'

        $blocked = $false
        try {
            Invoke-ValidationProducer -ProjectPath $produced -OutputPath $output | Out-Null
        }
        catch {
            $blocked = $true
        }
        Assert-True -Condition $blocked -Message 'Producer overwrote an existing attestation path.'
    }

    Invoke-Test -Name 'Validation-relevant Assets changes invalidate project-owned attestations' -Body {
        $staleAssets = New-UnityFixture -RunRoot $runRoot -Name 'stale-assets-attestation' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $staleAssets
        Add-RuntimeRoot -Root $staleAssets
        Add-PageContract -Root $staleAssets
        Add-GeneratedBinding -Root $staleAssets
        Add-RealValidationAttestation -Root $staleAssets
        $changedSource = Join-Path $staleAssets 'Assets\AppUI\Pages\SettingsPanelController.cs'
        Write-Utf8File -Path $changedSource -Content @'
using Joi.H.AppUI;
public partial class SettingsPanelController : PanelBaseController { public int Revision = 2; }
'@

        $result = Invoke-Inspection -ProjectPath $staleAssets
        Assert-True -Condition ($result.status -ne 'Ready') `
            -Message 'Changed validation-relevant Assets retained Ready.'
        Assert-True -Condition ($result.integration.validation.binding.rejectedEvidence.reason -contains 'AssetsDigestMismatch') `
            -Message 'The stale Assets digest was not rejected explicitly.'
    }

    Invoke-Test -Name 'Source scan is bounded and reports truncation' -Body {
        $bounded = New-UnityFixture -RunRoot $runRoot -Name 'bounded' -AppUIReference $officialTag
        Add-SourceFile -Root $bounded -RelativePath 'Assets\A.cs' -Content 'public sealed class A { }'
        Add-SourceFile -Root $bounded -RelativePath 'Assets\B.cs' -Content 'public sealed class B { }'
        Add-SourceFile -Root $bounded -RelativePath 'Assets\C.cs' -Content 'public sealed class C { AppUIRuntimeHost host; }'
        Add-SourceFile -Root $bounded -RelativePath 'Assets\D.asmdef' -Content '{ "name": "D" }'

        $result = Invoke-Inspection -ProjectPath $bounded -MaxSourceFiles 2
        Assert-Equal -Expected 2 -Actual ([int]$result.project.maxSourceFiles) `
            -Message 'Inspector did not report the applied MaxSourceFiles bound.'
        Assert-Equal -Expected 2 -Actual ([int]$result.project.scannedFileCount) `
            -Message 'Inspector exceeded MaxSourceFiles.'
        Assert-Equal -Expected $true -Actual ([bool]$result.project.sourceFileLimitReached) `
            -Message 'Selected-file truncation was not reported separately.'
        Assert-Equal -Expected $true -Actual ([bool]$result.project.scanLimitReached) `
            -Message 'Bounded scan did not report truncation.'
        Assert-True -Condition ($result.issues.code -contains 'SOURCE_SCAN_LIMIT_REACHED') `
            -Message 'Bounded scan issue code is missing.'
    }

    Invoke-Test -Name 'Any source truncation prevents Ready and definitive validation passes' -Body {
        $truncatedReady = New-UnityFixture -RunRoot $runRoot -Name 'truncated-ready' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $truncatedReady
        Add-RuntimeRoot -Root $truncatedReady
        Add-PageContract -Root $truncatedReady
        Add-GeneratedBinding -Root $truncatedReady
        Add-RealValidationAttestation -Root $truncatedReady
        for ($index = 0; $index -lt 30; $index++) {
            Add-SourceFile -Root $truncatedReady `
                -RelativePath ("Assets\Overflow\Overflow{0:D2}.cs" -f $index) `
                -Content ("public sealed class Overflow{0:D2} {{ }}" -f $index)
        }

        $result = Invoke-Inspection -ProjectPath $truncatedReady -MaxSourceFiles 20
        Assert-Equal -Expected $true -Actual ([bool]$result.project.sourceFileLimitReached) `
            -Message 'Source-file truncation was not recorded.'
        Assert-Equal -Expected 'RuntimeValidationPending' -Actual $result.status `
            -Message 'Truncated inspection claimed a definitive integration state.'
        Assert-Equal -Expected 'Indeterminate' -Actual $result.integration.validation.binding.status `
            -Message 'Truncated inspection retained a definitive Binding pass.'
        Assert-Equal -Expected 'Indeterminate' -Actual $result.integration.validation.runtime.status `
            -Message 'Truncated inspection retained a definitive Runtime pass.'
    }

    Invoke-Test -Name 'Oversized selected files make inspection indeterminate' -Body {
        $oversized = New-UnityFixture -RunRoot $runRoot -Name 'oversized-selected' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $oversized
        Add-RuntimeRoot -Root $oversized
        Add-PageContract -Root $oversized
        Add-GeneratedBinding -Root $oversized
        Add-RealValidationAttestation -Root $oversized
        Add-SourceFile -Root $oversized -RelativePath 'Assets\AppUI\OversizedSelected.cs' `
            -Content ('x' * 2097153)

        $result = Invoke-Inspection -ProjectPath $oversized
        Assert-Equal -Expected $true -Actual ([bool]$result.project.scanIndeterminate) `
            -Message 'Oversized selected input did not mark inspection indeterminate.'
        Assert-Equal -Expected $false -Actual ([bool]$result.project.scanComplete) `
            -Message 'Oversized selected input retained a complete scan.'
        Assert-True -Condition ($result.status -ne 'Ready') -Message 'Indeterminate oversized scan produced Ready.'
        Assert-True -Condition ($result.integration.validation.binding.status -ne 'Passed') `
            -Message 'Indeterminate oversized scan retained a definitive pass.'
    }

    Invoke-Test -Name 'Unreadable selected files make inspection indeterminate' -Body {
        $unreadable = New-UnityFixture -RunRoot $runRoot -Name 'unreadable-selected' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $unreadable
        Add-RuntimeRoot -Root $unreadable
        Add-PageContract -Root $unreadable
        Add-GeneratedBinding -Root $unreadable
        $lockedPath = Join-Path $unreadable 'Assets\AppUI\LockedSelected.cs'
        Add-SourceFile -Root $unreadable -RelativePath 'Assets\AppUI\LockedSelected.cs' `
            -Content 'public sealed class LockedSelected { }'
        Add-RealValidationAttestation -Root $unreadable

        $lockStream = [System.IO.File]::Open($lockedPath, [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        try {
            $result = Invoke-Inspection -ProjectPath $unreadable
        }
        finally {
            $lockStream.Dispose()
        }
        Assert-Equal -Expected $true -Actual ([bool]$result.project.scanIndeterminate) `
            -Message 'Unreadable selected input did not mark inspection indeterminate.'
        Assert-Equal -Expected $false -Actual ([bool]$result.project.scanComplete) `
            -Message 'Unreadable selected input retained a complete scan.'
        Assert-True -Condition ($result.status -ne 'Ready') -Message 'Indeterminate unreadable scan produced Ready.'
    }

    Invoke-Test -Name 'Total file and directory entry enumeration has a hard budget' -Body {
        $entryBounded = New-UnityFixture -RunRoot $runRoot -Name 'entry-bounded' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $entryBounded
        Add-RuntimeRoot -Root $entryBounded
        Add-PageContract -Root $entryBounded
        Add-GeneratedBinding -Root $entryBounded
        Add-RealValidationAttestation -Root $entryBounded
        for ($index = 0; $index -lt 600; $index++) {
            Write-Utf8File -Path (Join-Path $entryBounded ("Assets\EntryFlood\Entry{0:D3}.txt" -f $index)) `
                -Content 'ignored but still an enumerated filesystem entry'
        }

        $result = Invoke-Inspection -ProjectPath $entryBounded -MaxSourceFiles 20
        Assert-Equal -Expected $true -Actual ([bool]$result.project.entryLimitReached) `
            -Message 'Total enumerated-entry budget was not enforced.'
        Assert-True -Condition ([int]$result.project.enumeratedEntryCount -le [int]$result.project.maxEnumeratedEntries) `
            -Message 'Inspector enumerated beyond its reported total-entry budget.'
        Assert-Equal -Expected ([int]$result.project.enumeratedEntryCount) `
            -Actual ([int]$result.project.enumeratedFileEntryCount + [int]$result.project.enumeratedDirectoryEntryCount) `
            -Message 'File and directory entry counts do not reconcile.'
        Assert-True -Condition ($result.status -ne 'Ready') -Message 'Entry-truncated inspection produced Ready.'
        Assert-True -Condition ($result.integration.validation.binding.status -ne 'Passed') `
            -Message 'Entry-truncated inspection retained a definitive Binding pass.'
    }

    Invoke-Test -Name 'Exact entry budget is complete when no additional entry exists' -Body {
        $exactBudget = New-UnityFixture -RunRoot $runRoot -Name 'exact-entry-budget' -AppUIReference $officialTag
        for ($index = 0; $index -lt 64; $index++) {
            Write-Utf8File -Path (Join-Path $exactBudget ("Assets\Entry{0:D2}.txt" -f $index)) -Content 'entry'
        }

        $result = Invoke-Inspection -ProjectPath $exactBudget -MaxSourceFiles 1
        Assert-Equal -Expected 64 -Actual ([int]$result.project.maxEnumeratedEntries) `
            -Message 'Fixture did not exercise the exact entry budget.'
        Assert-Equal -Expected 64 -Actual ([int]$result.project.enumeratedEntryCount) `
            -Message 'Inspector did not enumerate the exact-budget fixture.'
        Assert-Equal -Expected $false -Actual ([bool]$result.project.entryLimitReached) `
            -Message 'Reaching the exact final entry was misreported as truncation.'
        Assert-Equal -Expected $true -Actual ([bool]$result.project.scanComplete) `
            -Message 'An exactly exhausted, fully enumerated directory was marked incomplete.'
    }

    Invoke-Test -Name 'Directory traversal budget is reported separately' -Body {
        $directoryBounded = New-UnityFixture -RunRoot $runRoot -Name 'directory-bounded' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $directoryBounded
        Add-RuntimeRoot -Root $directoryBounded
        Add-PageContract -Root $directoryBounded
        Add-GeneratedBinding -Root $directoryBounded
        Add-RealValidationAttestation -Root $directoryBounded
        $deepRoot = Join-Path $directoryBounded 'Assets\ManyDirectories'
        for ($index = 0; $index -lt 150; $index++) {
            [System.IO.Directory]::CreateDirectory((Join-Path $deepRoot ("D{0:D3}" -f $index))) | Out-Null
        }

        $result = Invoke-Inspection -ProjectPath $directoryBounded -MaxSourceFiles 20
        Assert-Equal -Expected $true -Actual ([bool]$result.project.directoryLimitReached) `
            -Message 'Directory budget truncation was not reported separately.'
        Assert-True -Condition ([int]$result.project.visitedDirectoryCount -le [int]$result.project.maxDirectories) `
            -Message 'Inspector visited beyond its reported directory budget.'
        Assert-True -Condition ($result.status -ne 'Ready') -Message 'Directory-truncated inspection produced Ready.'
        Assert-True -Condition ($result.integration.validation.runtime.status -ne 'Passed') `
            -Message 'Directory-truncated inspection retained a definitive Runtime pass.'
    }

    Invoke-Test -Name 'Secret files and reparse escapes are never scanned' -Body {
        $json = & $script:InspectorPath -ProjectPath $reparseEscapeFixture
        $text = $json -join [Environment]::NewLine
        Assert-True -Condition (-not $text.Contains($envSentinel)) -Message '.env sentinel leaked into JSON.'
        Assert-True -Condition (-not $text.Contains($reparseSentinel)) -Message 'Reparse target sentinel leaked into JSON.'
        Assert-True -Condition (-not $text.Contains('EscapedAppUIRuntimeHost.cs')) `
            -Message 'A file beyond the project root was reported.'
    }

    Invoke-Test -Name 'Known Unity inputs are not read through reparse directories' -Body {
        $reparsePackages = New-UnityFixture -RunRoot $runRoot -Name 'reparse-packages'
        $externalPackages = Join-Path $runRoot 'outside-packages-input'
        [System.IO.Directory]::CreateDirectory($externalPackages) | Out-Null
        $knownInputSentinel = 'APPUI_KNOWN_INPUT_REPARSE_SENTINEL_518DC06A'
        Write-Utf8File -Path (Join-Path $externalPackages 'manifest.json') -Content @"
{
  "sentinel": "$knownInputSentinel",
  "dependencies": {
    "com.joih.appui": "$officialTag",
    "com.unity.ugui": "2.0.0"
  }
}
"@
        Write-Utf8File -Path (Join-Path $externalPackages 'packages-lock.json') -Content '{ "dependencies": {} }'
        $packagesPath = Join-Path $reparsePackages 'Packages'
        [System.IO.Directory]::Delete($packagesPath, $true)
        New-Item -ItemType Junction -Path $packagesPath -Target $externalPackages | Out-Null
        $script:JunctionPaths.Add($packagesPath) | Out-Null

        $json = & $script:InspectorPath -ProjectPath $reparsePackages
        $text = $json -join [Environment]::NewLine
        $result = $text | ConvertFrom-Json
        Assert-Equal -Expected 'AppUINotInstalled' -Actual $result.status `
            -Message 'A reparse-point Packages directory was treated as trusted input.'
        Assert-True -Condition (-not $text.Contains($knownInputSentinel)) `
            -Message 'A known-input reparse sentinel leaked into JSON.'
    }

    Invoke-Test -Name 'Hidden and secret validation inputs are never trusted as evidence' -Body {
        $isolated = New-UnityFixture -RunRoot $runRoot -Name 'hidden-secret-evidence' -AppUIReference $officialTag
        Add-AppUIHostAndPorts -Root $isolated
        Add-RuntimeRoot -Root $isolated
        Add-PageContract -Root $isolated
        Add-GeneratedBinding -Root $isolated
        Add-RealValidationAttestation -Root $isolated

        $hiddenPath = Join-Path $isolated 'Assets\AppUI\Validation\HiddenRuntimeValidationReport.asset'
        Write-Utf8File -Path $hiddenPath -Content 'status: Failed'
        [System.IO.File]::SetAttributes($hiddenPath, [System.IO.FileAttributes]::Hidden)
        Write-Utf8File -Path (Join-Path $isolated 'Assets\Secrets\AppUIRuntimeValidationReport.asset') `
            -Content 'status: Failed'

        $result = Invoke-Inspection -ProjectPath $isolated
        Assert-Equal -Expected $true -Actual ([bool]$result.project.scanIndeterminate) `
            -Message 'Hidden or excluded relevant inputs did not make the scan indeterminate.'
        Assert-True -Condition ($result.status -ne 'Ready') `
            -Message 'Hidden or excluded validation content retained Ready.'
        Assert-True -Condition ($result.integration.validation.runtime.status -ne 'Passed') `
            -Message 'Hidden or excluded validation content retained a definitive pass.'
        $runtimeEvidencePaths = @($result.integration.validation.runtime.evidence | ForEach-Object { $_.path })
        Assert-True -Condition (-not ($runtimeEvidencePaths -contains `
            'Assets/AppUI/Validation/HiddenRuntimeValidationReport.asset')) `
            -Message 'A hidden validation file was reported as evidence.'
    }

    Invoke-Test -Name 'OutputPath bytes equal stdout JSON and use UTF8 without BOM' -Body {
        $outputPath = Join-Path $runRoot 'reports\inspection.json'
        $stdout = & $script:InspectorPath -ProjectPath $completeFixture -OutputPath $outputPath
        $stdoutJson = $stdout -join [Environment]::NewLine
        Assert-True -Condition (Test-Path -LiteralPath $outputPath -PathType Leaf) `
            -Message 'OutputPath file was not created.'
        $fileJson = [System.IO.File]::ReadAllText($outputPath)
        Assert-Equal -Expected $stdoutJson -Actual $fileJson `
            -Message 'OutputPath raw JSON differs from stdout.'
        $bytes = [System.IO.File]::ReadAllBytes($outputPath)
        $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        Assert-Equal -Expected $false -Actual $hasUtf8Bom -Message 'OutputPath JSON has a UTF-8 BOM.'
        $fileResult = $fileJson | ConvertFrom-Json
        Assert-Equal -Expected ([System.IO.Path]::GetFullPath($outputPath)) -Actual $fileResult.outputPath `
            -Message 'Inspector did not report the requested output path.'
    }

    Invoke-Test -Name 'OutputPath cannot overwrite Unity project inputs' -Body {
        $manifestPath = Join-Path $completeFixture 'Packages\manifest.json'
        $before = [System.IO.File]::ReadAllText($manifestPath)
        $blocked = $false
        try {
            & $script:InspectorPath -ProjectPath $completeFixture -OutputPath $manifestPath | Out-Null
        }
        catch {
            $blocked = $true
        }

        Assert-True -Condition $blocked -Message 'Inspector allowed OutputPath to target Packages/manifest.json.'
        Assert-Equal -Expected $before -Actual ([System.IO.File]::ReadAllText($manifestPath)) `
            -Message 'Inspector modified Packages/manifest.json.'
    }

    Invoke-Test -Name 'OutputPath rejects every existing target before writing' -Body {
        $existingOutput = Join-Path $runRoot 'existing-output.json'
        $sentinel = 'EXISTING_OUTPUT_SENTINEL_51A77C'
        Write-Utf8File -Path $existingOutput -Content $sentinel
        $blocked = $false
        try {
            & $script:InspectorPath -ProjectPath $completeFixture -OutputPath $existingOutput | Out-Null
        }
        catch {
            $blocked = $true
        }

        Assert-True -Condition $blocked -Message 'Inspector overwrote an existing OutputPath target.'
        Assert-Equal -Expected $sentinel -Actual ([System.IO.File]::ReadAllText($existingOutput)) `
            -Message 'Existing OutputPath content changed.'

        $protectedManifest = Join-Path $completeFixture 'Packages\manifest.json'
        $manifestBefore = [System.IO.File]::ReadAllText($protectedManifest)
        $hardLinkOutput = Join-Path $runRoot 'existing-manifest-hardlink.json'
        New-Item -ItemType HardLink -Path $hardLinkOutput -Target $protectedManifest | Out-Null
        $hardLinkBlocked = $false
        try {
            & $script:InspectorPath -ProjectPath $completeFixture -OutputPath $hardLinkOutput | Out-Null
        }
        catch {
            $hardLinkBlocked = $true
        }
        Assert-True -Condition $hardLinkBlocked -Message 'Inspector wrote through an existing hard-link target.'
        Assert-Equal -Expected $manifestBefore -Actual ([System.IO.File]::ReadAllText($protectedManifest)) `
            -Message 'Packages/manifest.json changed through a hard-link OutputPath.'
    }

    Invoke-Test -Name 'OutputPath rejects reparse targets and protected aliases' -Body {
        $externalOutput = Join-Path $runRoot 'external-output-target'
        [System.IO.Directory]::CreateDirectory($externalOutput) | Out-Null
        $outputAlias = Join-Path $runRoot 'output-alias'
        New-Item -ItemType Junction -Path $outputAlias -Target $externalOutput | Out-Null
        $script:JunctionPaths.Add($outputAlias) | Out-Null
        $aliasBlocked = $false
        try {
            & $script:InspectorPath -ProjectPath $completeFixture `
                -OutputPath (Join-Path $outputAlias 'inspection.json') | Out-Null
        }
        catch {
            $aliasBlocked = $true
        }
        Assert-True -Condition $aliasBlocked -Message 'OutputPath traversed an existing reparse-point ancestor.'
        Assert-Equal -Expected $false -Actual (Test-Path -LiteralPath (Join-Path $externalOutput 'inspection.json')) `
            -Message 'OutputPath wrote through a reparse-point ancestor.'

        $protectedFixture = New-UnityFixture -RunRoot $runRoot -Name 'protected-output-alias' -AppUIReference $officialTag
        $protectedManifest = Join-Path $protectedFixture 'Packages\manifest.json'
        $protectedBefore = [System.IO.File]::ReadAllText($protectedManifest)
        $packagesAlias = Join-Path $runRoot 'packages-output-alias'
        New-Item -ItemType Junction -Path $packagesAlias -Target (Join-Path $protectedFixture 'Packages') | Out-Null
        $script:JunctionPaths.Add($packagesAlias) | Out-Null
        $protectedBlocked = $false
        try {
            & $script:InspectorPath -ProjectPath $protectedFixture `
                -OutputPath (Join-Path $packagesAlias 'manifest.json') | Out-Null
        }
        catch {
            $protectedBlocked = $true
        }
        Assert-True -Condition $protectedBlocked -Message 'Protected project file was reachable through a junction alias.'
        Assert-Equal -Expected $protectedBefore -Actual ([System.IO.File]::ReadAllText($protectedManifest)) `
            -Message 'Packages/manifest.json was overwritten through a junction alias.'
    }
}
finally {
    foreach ($junctionPath in $script:JunctionPaths) {
        if ([System.IO.Directory]::Exists($junctionPath)) {
            [System.IO.Directory]::Delete($junctionPath)
        }
    }

    $tempPrefix = $tempRoot + [System.IO.Path]::DirectorySeparatorChar
    $resolvedRunRoot = [System.IO.Path]::GetFullPath($runRoot)
    if (-not $resolvedRunRoot.StartsWith($tempPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw ("Refusing to clean test root outside the system temp directory: {0}" -f $resolvedRunRoot)
    }
    if (Test-Path -LiteralPath $resolvedRunRoot) {
        Remove-Item -LiteralPath $resolvedRunRoot -Recurse -Force
    }
}

if (Test-Path -LiteralPath $runRoot) {
    $script:Failed++
    Write-Host ("FAIL Disposable fixture cleanup: {0} still exists." -f $runRoot)
}
else {
    $script:Passed++
    Write-Host 'PASS Disposable fixture cleanup'
}

Write-Host ("RESULT Passed={0} Failed={1}" -f $script:Passed, $script:Failed)
if ($script:Failed -gt 0) {
    exit 1
}
