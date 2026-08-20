# TextMeshPro 可选集成

Joi.H AppUI 0.4 的基础 Runtime、Editor、Consumer 和 UPM 依赖不引用 TextMeshPro。项目
明确选择 TMP 时，通过 `JOIH_APPUI_TMP` 启用包内可选程序集；安装包本身不会修改 Define、
Binding Settings、Runtime Configuration 或 Prefab。

## 1. 启用 Define

在 `Project Settings > Player > Scripting Define Symbols` 为实际目标 Build Target 添加：

```text
JOIH_APPUI_TMP
```

等待 Domain Reload，确认以下程序集编译：

```text
Joi.H.AppUI.Integrations.TextMeshPro.Runtime
Joi.H.AppUI.Integrations.TextMeshPro.Editor
```

不要给 `Joi.H.AppUI.Runtime`、`Joi.H.AppUI.Editor` 或基础 Consumer asmdef 添加
`Unity.TextMeshPro` 引用。

## 2. 导入独立 Sample

在 Package Manager 选中 Joi.H AppUI，导入 **TextMeshPro Integration**。Sample 自己
提供 Operation Factory、Execution Context、内存 Asset Provider、Definition、Prefab、
Settings 和 Scene，不依赖 Basic Integration。

## 3. Binding Provider

在唯一的 `UIBindingSettings.EnabledRuleProviderIds` 中显式添加：

```text
joih.appui.tmp
```

然后严格执行 Generate → Domain Reload → Bind → Validate。Provider 已注册但未选择时，
TMP 规则不会生效；选择了缺失 Provider、RuleId/Component 冲突时，操作在写入前失败。

## 4. InputField Resolver

把 Resolver 作为不可变 Runtime Configuration 的一部分注入：

```csharp
var configuration = new AppUIRuntimeConfiguration(
    loadStrategies,
    instanceStrategies,
    new IAppUIFocusControlPolicyResolver[]
    {
        new TextMeshProInputFieldPolicyResolver(),
    });

runtimeHost.Initialize(dependencies, configuration);
```

Resolver ID 为 `joih.appui.tmp.input-field`。显式节点 Policy 优先；多个外部 Resolver 同时
匹配属于配置冲突，会原子拒绝节点注册，不以顺序或优先级猜测。

## 5. Dropdown 与 ChildRegion

TMP Dropdown 必须显式创建 Policy：

```csharp
var policy = new TextMeshProFocusDropdownControlPolicy(
    binding.OptionsDropdown,
    "options");
```

把同一个 `policy` 注册给 Dropdown 节点以及 ID 为 `options` 的 ChildRegion。Region 打开、
折叠和 Cancel 都由该 Policy 协调；不要把 InputField Resolver 当作 Dropdown 自动识别器。

## 6. Notice Prefab

TMP Notice Prefab 必须预先包含并绑定：

- `CanvasGroup`；
- `TMP_Text`/`TextMeshProUGUI`；
- `TextMeshProNoticeView`；
- View 内已序列化的文本引用。

在 `AppUIRuntimeProfile` 中启用对应视觉并填写 Provider 能解析的 `PrefabAssetId`。没有 UGUI
或 TMP fallback；配置错误会明确阻止 Notice 初始化。

## 7. 诊断

打开 `Project Settings > Joi.H AppUI > TextMeshPro Integration`。诊断状态：

- `Pass`：当前事实已验证；
- `Warning`：可运行但存在明确影响；
- `Failure`：集成契约不成立，自动门禁失败；
- `NotVerifiable`：当前模式没有足够运行时事实，不等于成功或失败。

命令行执行
`Joi.H.AppUI.Integrations.TextMeshPro.Editor.TextMeshProIntegrationValidationCommandLine.Validate`
会在 `APPUI_VALIDATION_OUTPUT/textmeshpro-integration.json` 写入稳定 JSON。EditMode 可验证
Define、程序集、Provider、Binding、Notice；Host Resolver 只有在 Play Mode 初始化后可验证。

## 8. 停用

按以下顺序停用：替换 TMP 组件 → 移除 `joih.appui.tmp` → 移除 Resolver/Dropdown/Notice
引用 → 删除所有目标的 `JOIH_APPUI_TMP` → 重新 Generate/Bind/Validate → Mono/IL2CPP Build。
不要只删除 Define 并保留 TMP 生成字段或序列化引用。
