# Joi.H AppUI 可选 TextMeshPro 集成设计

**状态：** 已确认设计基线，可进入实施计划

**目标版本：** `v0.4.0-pre.1`

**官方 Unity 目标：** Unity 6.0 / `6000.0`

**当前源码基线：** `v0.3.0-pre.1` 之后的 `origin/main@8f77f14`

**不可变历史版本：** `v0.3.0-pre.1` 保持不变

## 1. 背景与问题

Joi.H AppUI 当前没有在 `package.json` 中单独声明
`com.unity.textmeshpro`。Unity 6 的 `com.unity.ugui@2.0.0` 已包含
TextMeshPro，因此当前安装流程通常不会因为 TMP 再要求用户安装一个包。

但是 AppUI 的基础 Runtime、Editor、Binding、Focus、Notice、Controller 和
Consumer 验证工程直接引用了 TMP 类型与程序集。这带来的是架构耦合，而不只是包体问题：

- AppUI 的基础程序集无法在没有 TMP API 的社区移植环境中独立编译；
- 不使用 TMP 的项目仍被迫接受 TMP 类型进入 AppUI 公共和内部边界；
- Binding 与 Focus 的扩展能力仍依赖框架修改，无法由接入项目显式选择；
- Notice 会在配置缺失时自动创建 TMP 视图，掩盖真实配置错误；
- Unity 版本移植时，UGUI、TMP 和 Editor API 差异混在同一程序集内，难以定位。

0.4 的目标不是宣称“Unity 6 安装体积会减少”，也不只是“增加可选 TMP 支持”，而是：

> 建立 AppUI 基础能力与 UI 技术集成之间的正式边界，并以 TextMeshPro 作为第一套
> 官方 Integration 验证这套扩展模型。

TMP 从 AppUI 基础能力中解耦后，Provider、Policy 和宿主注入边界必须能够承载真实的
可选 UI 技术集成。

## 2. 目标

1. `Joi.H.AppUI.Core`、基础 Runtime 和基础 Editor 不引用任何 TMP 类型或程序集。
2. AppUI 在不定义 `JOIH_APPUI_TMP` 时可以直接安装、编译、运行和构建。
3. 使用 TMP 的项目仍通过同一个 UPM 包获得官方集成，不拆分第二个安装包。
4. TMP 集成仅在项目显式定义 `JOIH_APPUI_TMP` 后参与编译。
5. Binding、Focus 和 Notice 的 TMP 能力由清晰、确定、可验证的扩展边界提供。
6. AppUI 不自动安装 TMP、不自动修改 Scripting Define Symbols、不通过反射发现能力。
7. 保持 Unity 6.0 / `6000.0` 为唯一官方目标环境；旧版本仍属于 Community Port。
8. 以干净 Consumer 工程分别验证基础模式和 TMP 模式，而不是只让包内测试自证。

## 3. 非目标

- 不移除 Unity UGUI 依赖。
- 不为所有文本方案发明统一的 `IUIText`、`ITextLabel` 或虚拟组件层。
- 不在 AppUI Core 中定义字体、富文本、本地化或排版抽象。
- 不提供自动安装、自动启用或自动修复 TMP 的 Editor 脚本。
- 不在 0.4 同时实现 UIToolkit、FairyGUI 或其他 UI 技术适配。
- 不为 Unity 2022.3 建立第二条官方发布、CI、Tag 或 Bug 支持线。
- 不保留让基础程序集继续引用 TMP 的兼容垫片。
- 不修改已经发布的 `v0.3.0-pre.1` Tag。

## 4. 决策摘要

| 决策 | 结果 |
| --- | --- |
| 发布形态 | 单一 UPM 包 `com.joih.appui` |
| 官方版本 | `v0.4.0-pre.1` |
| TMP 开关 | 项目显式定义 `JOIH_APPUI_TMP` |
| TMP 注册 | Binding Provider 显式选择；InputField Resolver 显式注入；Dropdown Policy 显式构造 |
| 自动行为 | 不安装、不改 Define、不反射发现 |
| 基础文本 | 保留 UGUI `Text` Binding 规则 |
| Controller | 移除 TMP 与本地化辅助方法，不新增文本抽象 |
| Notice | 内容合同中立，具体视图由 Prefab 与派生类负责 |
| Focus | 内置 UGUI 策略；TMP 使用外部 Resolver 或显式 Policy |
| Binding | 内置基础规则始终启用；可选 Provider 显式启用 |
| 验证 | 同一 pristine 模板生成两个互不共享缓存的临时 Consumer |
| 兼容性 | Unity 6 官方支持；其他版本按社区移植教程处理 |

## 5. 程序集边界

### 5.1 目标程序集图

    Joi.H.AppUI.Core
        ↑
    Joi.H.AppUI.Runtime ────────────────┐
        ↑                              │
    Joi.H.AppUI.Editor                 │
                                       │
    Joi.H.AppUI.Integrations.TextMeshPro.Runtime
        ↑
    Joi.H.AppUI.Integrations.TextMeshPro.Editor

基础程序集职责：

| 程序集 | 允许依赖 | 禁止依赖 |
| --- | --- | --- |
| `Joi.H.AppUI.Core` | .NET/C# 合同 | Unity UI、TMP、宿主框架 |
| `Joi.H.AppUI.Runtime` | Core、UnityEngine、UnityEngine.UI | TMP、宿主资源/异步实现 |
| `Joi.H.AppUI.Editor` | Runtime、UnityEditor、UnityEditor.UI | TMP、宿主 Editor SDK |

可选程序集职责：

| 程序集 | 依赖 | 编译约束 |
| --- | --- | --- |
| `Joi.H.AppUI.Integrations.TextMeshPro.Runtime` | Runtime、`Unity.TextMeshPro` | `JOIH_APPUI_TMP` |
| `Joi.H.AppUI.Integrations.TextMeshPro.Editor` | AppUI Editor、TMP Runtime、`Unity.TextMeshPro.Editor` | `JOIH_APPUI_TMP`，Editor only |
| `Joi.H.AppUI.Tests.TextMeshPro` | TMP Runtime/Editor 集成及测试框架 | `JOIH_APPUI_TMP`，测试专用 |

两个 TMP asmdef 都必须使用：

    "defineConstraints": [
      "JOIH_APPUI_TMP"
    ]

基础 Runtime、Editor 及其基础测试 asmdef 必须删除 `Unity.TextMeshPro` 和
`Unity.TextMeshPro.Editor` 引用。

### 5.2 源码目录

    Runtime/
      Bootstrap/
      Controller/
      Notice/
      Selection/
      ...

    Editor/
      Binding/
      ...

    Integrations/
      TextMeshPro/
        Runtime/
          Focus/
          Notice/
        Editor/
          Binding/

    Tests/
      Runtime/
      Editor/

    Tests.TextMeshPro/
      Runtime/
      Editor/

    Samples~/
      TextMeshPro Integration/

`Integrations/TextMeshPro` 属于官方可选实现，不属于 Core。基础目录不得通过
条件编译偷偷保留 TMP 分支；所有 TMP 类型只存在于集成目录和 TMP 专用测试/示例中。

## 6. 用户启用流程

### 6.1 不使用 TMP

用户只需从 Git URL、Tag 或本地包安装 `com.joih.appui`，完成 AppUI 必需端口注入，
即可使用基础 UGUI、页面、生命周期、Layer、Scope、Focus、Input、Binding 和 Notice
合同。不需要额外 Define。

### 6.2 使用 TMP

1. 安装 `com.joih.appui`。
2. 确认项目已有可用的 TMP 程序集。
3. 在 Player Settings 的 Scripting Define Symbols 中加入
   `JOIH_APPUI_TMP`。
4. 需要 TMP Binding 时，在 AppUI Project Settings 中启用 Provider
   `joih.appui.tmp`。
5. 需要 TMP InputField 原生编辑/Cancel 语义时，在组合根创建 Runtime
   Configuration 并注入 `TextMeshProInputFieldPolicyResolver`。
6. 需要 TMP Dropdown 子区域时，页面显式构造
   `TextMeshProFocusDropdownControlPolicy` 并提供 `ChildRegionId`。
7. 需要 TMP Notice 时，配置带 `TextMeshProNoticeView` 的 Prefab。
8. 打开 TextMeshPro Integration 状态页核对编辑期状态，并在 Play Mode 核对
   Runtime Host 快照。

Unity 6 官方目标环境通常不需要额外安装 TMP 包。社区移植到较旧 Unity 时，用户先安装
与该 Unity/UGUI 版本匹配的 TMP，再增加 Define。

AppUI 不提供“一键启用 TMP”，因为安装包、编辑项目级 Define、选择 Binding 规则和注入
运行时 Policy 都属于接入项目的明确决策。

### 6.3 错误状态

| 状态 | 结果 |
| --- | --- |
| 有 TMP，但未定义符号 | TMP 集成程序集不编译；基础 AppUI 正常工作 |
| 定义符号，但工程无 TMP 程序集 | Unity 明确报告程序集引用/编译错误 |
| 定义符号，但未启用 Binding Provider | TMP 运行时能力可用；生成器不生成 TMP 字段 |
| 设置选择 `joih.appui.tmp`，但 Provider 不可用 | Binding 生成和验证被阻止，并报告缺失 Provider |
| 注入 TMP Resolver，但没有 TMP 控件 | 正常；Resolver 返回不匹配 |
| 使用 TMP InputField 但未注入 Resolver | 该控件按未知 Selectable 处理，落到 FrameworkOnly |

TMP Dropdown 需要
`ChildRegionId`，因此继续由页面在节点注册时显式传入 Dropdown Policy，不由只接收
`Selectable` 的 Resolver 猜测区域配置。

## 7. Controller 去 TMP 设计

### 7.1 删除的 API

`UIBaseController` 删除：

    public static Func<string, string> LocalizeText
    protected void SetText(TMP_Text target, string localizationKey)
    protected void SetTextStr(TMP_Text target, string value)

这些方法同时耦合了 TMP 和未经注入的全局本地化入口，不属于页面生命周期控制器的职责。

### 7.2 新职责边界

- Controller 负责页面生命周期、事件订阅、状态呈现编排和资源清理。
- 具体文本组件由页面 Binding 字段决定。
- 本地化由宿主业务/Presentation 服务决定。
- 页面可直接赋值，也可使用自己项目的强类型呈现辅助方法。

示例迁移：

    // 0.3
    SetText(binding.Title, "inventory.title");

    // 0.4：宿主决定本地化服务与控件
    binding.Title.text = localization.Get("inventory.title");

0.4 不新增文本接口。AppUI 不需要知道字符串来自本地化、服务器、配置表还是运行时格式化。

## 8. Notice 中立化设计

### 8.1 内容合同

基础 Runtime 新增中立值对象：

    public readonly struct UINoticeContent
    {
        public string Text { get; }
        public Color Color { get; }
        public float FontSize { get; }
    }

`Text`、`Color` 和 `FontSize` 是 Notice 的呈现意图，不绑定某个文本组件。
如果后续需要增加图标、样式 ID 或本地化键，应通过新的已评审合同扩展，0.4 不提前加入。

0.4 的边界是“Notice 不知道 TMPro”，不是“Notice 不知道文字和基础视觉参数”。
不继续增加 `INoticePresentation`、`INoticeTextStyle`、`ITextRenderer` 等抽象。

### 8.2 视图合同

    public abstract class NoticeViewBase : MonoBehaviour
    {
        public RectTransform RectTransform { get; }
        public CanvasGroup CanvasGroup { get; }

        public abstract void ApplyContent(in UINoticeContent content);
    }

`NoticeViewBase` 继续负责：

- 缓存和验证 `RectTransform`；
- 缓存和验证 `CanvasGroup`；
- 接收 alpha 与位置更新；
- 提供池化进入、复用、退出所需的基础生命周期。

派生视图负责：

- 找到并验证自己的具体文本/图形组件；
- 将 `UINoticeContent` 应用到具体组件；
- 在内容不受支持时给出明确错误，而不是创建替代组件。

TMP 集成提供 `TextMeshProNoticeView`。接入项目也可以实现 UGUI Text、
自研富文本或完全不含文字的派生视图。

### 8.3 移除隐式 fallback

删除：

- `NoticeViewBase` 自动补齐 `TMP_Text`；
- `NoticeService.CreateFallbackViewObject`；
- 自动创建 `TextMeshProUGUI` 子节点；
- Prefab 缺少视图脚本时自动 `AddComponent`；
- 配置缺失时继续显示一个“看似可用”的临时 Notice。

这些 fallback 会掩盖 AssetId、Prefab 和组件配置错误，并把 TMP 重新带回基础 Runtime。

### 8.4 启用与失败语义

`AppUINoticeVisualSettings` 新增 `Enabled`：

| 配置 | 行为 |
| --- | --- |
| `Enabled == false` | Notice 视觉服务不加载资源、不创建池；调用返回无效 Handle，并在每个 Runtime Epoch 内按稳定诊断键输出一次结构化 Warning |
| `Enabled == true` 且 AssetId 为空 | 初始化/验证失败 |
| `Enabled == true` 且加载失败 | 操作明确失败，租约按所有权合同释放 |
| Prefab 无 `NoticeViewBase` 派生类 | 验证失败，不自动补组件 |
| 派生视图内容应用失败 | 当前 Notice 失败回收并记录结构化诊断 |

“一次 Warning”的去重键为
`(RuntimeEpoch, DiagnosticCode, NoticeApiKind, ScopeId)`，至少包含：

- 诊断码，例如 `APPUI_NOTICE_DISABLED`；
- Notice API 类型；
- Scope；
- 当前配置来源；
- Runtime Epoch。

无效 Handle 必须可由调用方检测，不能伪装成已经展示的 Notice。

`NoticeService` 仍拥有：

- Notice 池；
- Scope 关联；
- Tick、淡入淡出与位置计算；
- Prefab 加载和 Asset Lease；
- 运行时 Shutdown 清理；
- 晚到完成、失败和取消时的一次性资源归还。

## 9. Focus 扩展设计

### 9.1 基础内置控件

基础 Runtime 只内置 Unity UGUI：

- `Button`
- `Toggle`
- `Slider`
- `Scrollbar`
- `InputField`
- `Dropdown`

`TMP_InputField` 与 `TMP_Dropdown` 分支从基础策略删除。

“内置”不表示每一种控件都有特殊 Native Move 行为。Slider、Scrollbar 与 InputField
由基础 Policy 提供专用语义；Button、Toggle 等普通控件可使用 FrameworkOnly；
UGUI Dropdown 因需要 `ChildRegionId`，由页面显式创建
`AppUIFocusDropdownControlPolicy`。

### 9.2 外部解析合同

    public interface IAppUIFocusControlPolicyResolver
    {
        string ResolverId { get; }

        bool TryResolve(
            Selectable selectable,
            out IAppUIFocusControlPolicy policy);
    }

Resolver 是宿主显式提供的运行时策略，不使用程序集扫描或静态注册。

`AppUIRuntimeConfiguration` 增加第三组不可变快照：

    public AppUIRuntimeConfiguration(
        IEnumerable<IUILoadStrategy> loadStrategies,
        IEnumerable<IUIPageInstanceStrategy> instanceStrategies,
        IEnumerable<IAppUIFocusControlPolicyResolver> focusPolicyResolvers)

已有二参数构造函数保留，并转发到空 Resolver 集合，避免仅使用 0.3 策略扩展的接入代码
发生无意义破坏。`Empty` 同样包含空 Resolver 快照。

### 9.3 解析顺序

节点注册时按以下顺序确定 Policy：

1. 注册调用显式传入的节点 Policy；
2. Runtime Configuration 中的宿主 Resolver；
3. AppUI 内置 UGUI Policy；
4. `FrameworkOnly`。

确定结果后，节点生命周期内不再动态切换 Policy。若配置变化，宿主应重新初始化或重新注册
节点，而不是修改已冻结的配置集合。

### 9.4 确定性与冲突

- `ResolverId` 必须非空，并使用 Ordinal 比较。
- 重复 `ResolverId` 是 Runtime Configuration 初始化错误。
- 对同一个 Selectable，若两个或以上外部 Resolver 返回成功，节点注册失败。
- 冲突诊断必须列出 Selectable 路径/类型与所有命中的 Resolver ID。
- 不允许“第一个成功”“最后一个覆盖”或依赖构造顺序的隐式优先级。
- Resolver 抛出异常时，节点注册失败并保留 Resolver ID 作为诊断上下文。
- Resolver 返回成功但 Policy 为空视为合同错误。

解析必须发生在节点集合改变之前：

- `AppUIFocusScope.RegisterNode` 遇到冲突时返回 `false`，保留注册前状态；
- 保留 `void` 签名的旧注册入口遇到冲突时不增加节点，并输出同一结构化错误；
- 批量替换节点先预解析完整快照，任一节点失败则保留上一份有效快照；
- 不允许先写入节点、再发现 Resolver 冲突而留下半注册状态。

### 9.5 Dropdown 拆分

当前 UGUI/TMP 混合实现拆分为：

    AppUIFocusDropdownControlPolicyBase
      ├ AppUIFocusDropdownControlPolicy
      └ TextMeshProFocusDropdownControlPolicy

基类拥有：

- Dropdown 子区域进入/离开；
- Cancel 状态机；
- 展开状态同步的公共时序；
- 事件绑定/解绑骨架；
- 节点与子节点 Focus 所有权。

具体实现拥有：

- 原生控件引用；
- 原生展开状态读取；
- Show/Hide/Collapse 调用；
- 原生事件绑定与解绑；
- 具体 Dropdown List 区域定位。

基础实现只引用 UGUI `Dropdown`。TMP 实现只存在于 TMP 集成程序集。

### 9.6 TMP 官方 Resolver

TMP 集成提供：

- `TextMeshProInputFieldPolicyResolver`
- `TextMeshProFocusDropdownControlPolicy`
- `TextMeshProDropdownRegionBridge`

`TextMeshProInputFieldPolicyResolver` 是注入 Runtime Configuration 的自动解析器。
`ResolverId` 固定为 `joih.appui.tmp.input-field`。
`TextMeshProFocusDropdownControlPolicy` 由页面使用 Dropdown 与
`ChildRegionId` 显式构造，并同时用于 Dropdown Node 与对应 ChildRegion；它不通过
Resolver 自动创建。

TMP InputField 的 Cancel 语义固定为：

1. 当前控件处于聚焦编辑状态；
2. Cancel 先结束/取消输入框编辑；
3. 输入框消费本次 Cancel；
4. 页面不因同一次 Cancel 关闭。

TMP Dropdown 展开后，列表区域的 Focus 节点属于 Dropdown 的临时子区域；折叠、控件失活、
页面 Pause/Close/Release 时必须解绑并清理事件。

## 10. Binding Provider 设计

### 10.1 基础规则

基础 Editor 内置 Provider 始终启用，包含：

- `UIGroupBase`
- `Button`
- `Toggle`
- UGUI `InputField`
- UGUI `Dropdown`
- `Slider`
- `Scrollbar`
- UGUI `Text`
- `Image`
- `RawImage`
- `Animator`
- `Canvas`
- `GameObject` fallback

### 10.2 Provider 合同

    public interface IUIBindingRuleProvider
    {
        string ProviderId { get; }
        IReadOnlyList<UIBindingComponentRule> Rules { get; }
    }

TMP Editor 集成提供 Provider：

    ProviderId = "joih.appui.tmp"

规则：

| 组件 | 后缀 | 生成类型 |
| --- | --- | --- |
| `TMP_Text` | `Txt` | `TMPro.TMP_Text` |
| `TMP_InputField` | `Input` | `TMPro.TMP_InputField` |
| `TMP_Dropdown` | `Dropdown` | `TMPro.TMP_Dropdown` |

### 10.3 设置与注册

`UIBindingSettings` 新增：

    EnabledRuleProviderIds

基础内置 Provider 不出现在该列表中，也不能被关闭。该列表只选择可选 Provider。

可选 Provider 的可用性由编译后的 Editor 集成明确注册到 Registry；选择权来自 Settings。
Registry 不依据程序集初始化先后自动启用 Provider。

TMP Editor 集成可在 `[InitializeOnLoad]` 入口调用：

    UIBindingRuleProviderRegistry.Register(
        new TextMeshProBindingRuleProvider());

这里的自动动作只声明“Provider 可用”，不会把 ID 写入 Settings，也不会启用规则。
Registry 对重复 ID 和规则冲突统一失败，所以不同程序集的初始化顺序不改变最终结果。

### 10.4 不可变快照

`UIBindingComponentRule` 改为不可变对象：

- 新增必填、非空并使用 Ordinal 比较的 `RuleId`；
- 所有字段改为只读属性；
- 构造时完成参数校验；
- Provider 注册时复制规则列表；
- Registry 构建最终规则快照；
- Scanner 和 Validator 在一次操作中读取同一份快照。

Provider 返回集合在注册后被修改，不得影响正在运行的 Binding 操作。

### 10.5 冲突和排序

以下情况阻止生成与验证：

- 重复 Provider ID；
- 重复 Rule ID；
- Settings 选择不存在的 Provider；
- 两个已启用可选 Provider 声明相同 `ComponentType`；
- 可选 Provider 试图覆盖基础内置 `ComponentType`；
- Rule 的 ComponentType、生成类型、后缀或 ID 无效。

排序规则：

1. Priority 降序；
2. Priority 相同时按 Rule ID 的 Ordinal 顺序；
3. 最后才执行 `GameObject` fallback。

任何冲突都不通过“后注册覆盖先注册”处理。程序集加载顺序和静态初始化顺序不得改变生成结果。

## 11. Editor 设置、集成状态与验证

TextMeshPro Integration 状态诊断是 0.4 的核心接入体验和正式验收项，不只是文档提示。
诊断只报告可验证事实和修复步骤，不替用户修改项目。

### 11.1 状态模型

每一项诊断使用四种状态：

| 状态 | 含义 |
| --- | --- |
| Pass | 已从当前工程或运行时快照验证 |
| Warning | 集成可继续，但某项可选能力不可用或存在高概率误配置 |
| Failure | 当前合同不满足，相关生成、验证或运行行为不能继续 |
| Not Verifiable | 当前阶段没有足够事实，不能伪装成 Pass 或 Failure |

诊断项必须同时显示：

- 稳定诊断码；
- 当前事实；
- 影响的能力；
- 人工修复步骤；
- 可验证时使用的 Asset、Provider、Resolver 或 Host 标识。

### 11.2 编辑期 TextMeshPro Integration 页面

`Joi.H.AppUI.Integrations.TextMeshPro.Editor` 在
`JOIH_APPUI_TMP` 生效并成功编译后提供：

    Project/Joi.H AppUI/Integrations/TextMeshPro

该页面至少显示：

- TextMeshPro API 是否可用；
- `JOIH_APPUI_TMP` 是否对当前 Build Target 生效；
- TMP Runtime/Editor 集成程序集是否已编译；
- `joih.appui.tmp` Provider 是否已注册；
- Provider 是否在当前 `UIBindingSettings` 中启用；
- TMP Binding 规则快照是否有效；
- 每个 `AppUIRuntimeProfile` 的已启用 Notice 配置能否解析 Prefab；
- Prefab 是否包含 `NoticeViewBase` 派生类，以及具体视图类型；
- Sample 所需的 `TextMeshProNoticeView` 是否已配置。

该页面位于可选 Editor 程序集，基础 Editor 不引用 TMP 类型、程序集或 TMP 专有源代码。
Define 尚未生效时，完整 TMP 页面不会参与编译；此时入口由安装文档和 Sample README
说明。若 `UIBindingSettings` 已选择 `joih.appui.tmp` 但程序集不可用，基础 Binding
验证仍会以“已选择的 Provider 不存在”阻止生成，并显示缺失的 Provider ID。

启用 TMP 不代表项目必须使用 TMP Notice。普通项目只要每项 `Enabled` Notice 配置满足
基础 `NoticeViewBase` 合同即可；“未配置 TextMeshProNoticeView”只表示 TMP Notice
呈现能力未使用，不作为全局失败。TMP Sample 和 TMP Consumer 则必须把它作为自己的
验收合同。

### 11.3 运行时配置快照

`AppUIRuntimeConfiguration` 由宿主组合根在运行时构造，Editor 在非运行状态无法可靠
证明 Resolver 已注入。因此：

- `AppUIRuntimeHost` 在初始化后公开只读 Configuration/Diagnostics 快照；
- 快照只公开不可变 Resolver ID 列表和必要状态，不允许 Editor 修改运行时配置；
- TMP Integration 页面在 Play Mode 按每个已初始化 Host 显示
  `joih.appui.tmp.input-field` 是否存在；
- Host 未初始化时显示 Not Verifiable，不显示虚假的绿色通过；
- TMP 集成已启用但 Resolver 缺失时显示 Warning，并说明 TMP InputField 将按
  FrameworkOnly 处理；
- 多个 Host 分别显示，不通过进程级静态状态合并成一个结论。

Runtime Configuration 本身不要求所有项目必须注入 TMP Resolver；只有使用 TMP InputField
原生编辑/Cancel 语义的项目需要它。TMP Sample 与 TMP Consumer 把该 Resolver 设为
强制验收项。

### 11.4 只诊断，不代替用户决策

设置界面不自动：

- 添加/删除 Scripting Define Symbols；
- 安装或升级 TMP；
- 启用 Binding Provider；
- 注入 Focus Resolver；
- 为已有 Prefab 添加 `TextMeshProNoticeView`；
- 修改 Runtime Configuration；
- 重写用户生成代码。

Editor 诊断可以读取编译符号、程序集状态、Provider Registry、Asset 和运行时快照，
但这些信息只能用于显示和验证，不能用于自动注册、选择或启用运行时实现。

Binding Sync/Validation 在开始生成前冻结一次规则快照。若设置无效，整个操作停止，不生成
部分文件，也不覆盖上一次有效产物。可在编辑期确定的诊断同时进入命令行验证；Resolver
注入与 TMP 控件行为通过 PlayMode/Consumer 测试验证。

## 12. Sample 与 Consumer 验证

### 12.1 Package Manager Sample

新增 `Samples~/TextMeshPro Integration`，内容包括：

- 手动启用 `JOIH_APPUI_TMP` 的说明；
- 启用 `joih.appui.tmp` 的说明；
- TMP Binding 最小页面；
- TMP InputField Cancel；
- TMP Dropdown 与子列表 Focus Region；
- 使用 `TextMeshProNoticeView` 的 Notice Prefab；
- 在组合根注入 TMP Focus Resolver 的示例。

Sample 不执行项目级自动修改。导入后若未完成启用步骤，应显示可执行的配置错误说明。

### 12.2 干净 Consumer

验证目标是“外部项目安装发布包后可用”，不是包源码自身可以编译。

仓库只维护一份不包含 `Library`、`Temp`、`Logs`、`obj` 和本机状态的 pristine
Consumer 模板。每次候选验证从该模板复制两个独立临时目录：

    Candidate Package
      ├ CleanConsumer.Base
      │   └ install the same candidate package
      └ CleanConsumer.TextMeshPro
          └ install the same candidate package

两个目录不共享：

- `Library` 编译/导入缓存；
- Binding 生成产物；
- ProjectSettings 修改；
- Domain Reload 状态；
- Player Build 输出；
- Unity 的临时与用户状态。

验证任务可以为了机器资源顺序执行，但环境必须独立。临时 Consumer 不加入 Unity Hub，
发布任务在收集 XML、日志、构建摘要和诊断报告后统一删除；失败时也只保留报告，不把临时
工程留在用户工作区。

#### CleanConsumer.Base：基础模式

- 不定义 `JOIH_APPUI_TMP`；
- Consumer 源码无 `using TMPro`；
- 使用 `UnityEngine.UI.Text` 完成 Binding 页面；
- 执行包解析；
- 执行 Binding 生成与验证；
- 执行 EditMode；
- 执行 PlayMode；
- 执行 Mono Player Build；
- 执行 IL2CPP Player Build。

基础程序集静态扫描：

    TMPro|TMP_|TextMeshPro

扫描范围包括 Core、基础 Runtime、基础 Editor 及其基础测试，匹配数必须为 0。文档、
可选集成、TMP Sample 和 TMP 专用测试不属于此扫描范围。

#### CleanConsumer.TextMeshPro：TMP 模式

- 从 pristine 模板独立创建，不复用 Base 的 `Library` 或生成结果；
- 加入 `JOIH_APPUI_TMP`；
- 启用 `joih.appui.tmp`；
- 注入 `joih.appui.tmp.input-field` Resolver；
- 验证 TMP Runtime/Editor 程序集编译；
- 验证 TMP Binding 字段；
- 验证 InputField Cancel 不关闭页面；
- 验证 Dropdown 子区域 Focus 与折叠清理；
- 验证 TMP Notice 内容、池化和 Scope 清理；
- 执行 TMP 专用 EditMode/PlayMode；
- 执行 Mono Player Build；
- 执行 IL2CPP Player Build。

两个 Consumer 互不替代。只有它们从各自 pristine 环境完成全部门禁，候选版本才允许
发布。

## 13. 迁移设计

### 13.1 从 0.3 到 0.4

0.4 是预发布阶段允许的明确 Breaking Change。使用 TMP 的项目按以下顺序迁移：

1. 更新到 `v0.4.0-pre.1`。
2. 增加 `JOIH_APPUI_TMP`。
3. 在 Binding Settings 启用 `joih.appui.tmp`。
4. 将 `SetText` / `SetTextStr` 调用替换为直接呈现或宿主本地化服务。
5. 为 Notice Prefab 添加 `TextMeshProNoticeView` 并设置 AssetId。
6. 对 TMP InputField，在 `AppUIRuntimeConfiguration` 中注入
   `TextMeshProInputFieldPolicyResolver`。
7. 对 TMP Dropdown，将旧基础程序集 Policy 替换为
   `TextMeshProFocusDropdownControlPolicy`，仍由页面显式传入 Dropdown 和
   `ChildRegionId`；Dropdown 不进入普通 Resolver。
8. 重新生成 Binding。
9. 检查 TextMeshPro Integration 诊断页与运行时 Host 快照。
10. 运行项目自己的 EditMode、PlayMode、Mono 与 IL2CPP 验证。

### 13.2 已生成 Binding 文件

旧生成文件中的 `TMPro.*` 字段，在项目仍拥有 TMP 时可以继续编译；这只是迁移缓冲，
不是 0.4 的 Provider 注册方式。下一次生成或验证前必须启用
`joih.appui.tmp`，并以重新生成结果作为权威产物。

### 13.3 不提供的兼容行为

- 基础 Controller 不保留 TMP 重载；
- 基础 Focus 不保留条件编译 TMP 分支；
- 基础 Binding 不保留 TMP 默认规则；
- Notice 不再自动生成 TMP 子节点；
- 未启用 Provider 时，不依据 Prefab 中出现 TMP 组件自动开启；
- 未注入 Resolver 时，不依据类型名称反射创建 TMP Policy。

## 14. 文档变化

实施时同步更新：

- `README.md`：依赖表标注 TMP 为可选集成；
- `Documentation~/getting-started.md`：基础安装不要求 TMP；
- `Documentation~/architecture.md`：基础程序集与可选集成边界；
- `Documentation~/binding-system.md`：Provider、选择和冲突规则；
- `Documentation~/focus-system.md`：Resolver、顺序和 TMP 行为；
- Notice 相关文档：Enabled、Prefab 合同和失败语义；
- `Documentation~/faq.md`：Unity 6、TMP、Define、Provider 常见问题；
- `Documentation~/textmeshpro-integration.md`：完整接入教程；
- `Documentation~/migration-0.4.md`：0.3 到 0.4 迁移；
- `CHANGELOG.md`：记录 Breaking Change 与新集成；
- Package Manager Sample 的 README。

所有文档统一说明：

> TMP 是同一 AppUI 包内的可选官方集成。AppUI 不会自动安装 TMP 或修改项目设置。

## 15. 实施阶段边界

设计批准后的实施计划按以下阶段拆分，每阶段先建立失败测试/验证门，再修改生产代码：

1. **基础无 TMP 门禁**：asmdef 与静态扫描先失败，定义目标边界。
2. **Controller 与 Notice**：删除 TMP Helper，建立中立内容和显式 Prefab 合同。
3. **Focus**：建立 Resolver、确定性冲突检查和 Dropdown 拆分。
4. **Binding Provider**：不可变规则、Settings 选择、冲突验证。
5. **TMP 官方集成与诊断**：可选 asmdef、实现、状态页、测试、Sample 与文档。
6. **Consumer 与发布门禁**：从 pristine 模板生成相互隔离的 Base/TMP Consumer，
   分别形成候选发布证据。

每一阶段完成后定向清理已经被替代的代码、测试、诊断、文档和临时入口，再进入下一阶段。
不在同一阶段顺带实现其他 UI 技术或旧 Unity 官方兼容线。

## 16. 验收标准

### 16.1 基础隔离

- Core、基础 Runtime、基础 Editor asmdef 无 TMP 引用；
- 基础源码无 `TMPro`、`TMP_`、`TextMeshPro` 类型引用；
- 不定义 `JOIH_APPUI_TMP` 时包可解析、编译、运行和构建；
- 基础 Consumer 使用 UGUI Text 完成全链路验证。

### 16.2 TMP 能力

- 定义 `JOIH_APPUI_TMP` 后 TMP 可选程序集参与编译；
- `joih.appui.tmp` 可生成和验证三类 TMP Binding；
- TMP InputField Cancel 行为符合输入优先规则；
- TMP Dropdown Focus Region 可建立、折叠并完整解绑；
- `TextMeshProNoticeView` 支持内容、池化、Scope 与 Shutdown 清理。

### 16.3 确定性

- 重复/缺失 Binding Provider 阻止操作并明确诊断；
- Binding 规则冲突不依赖注册顺序解决；
- 多个 Focus Resolver 命中同一控件时节点注册失败；
- Runtime Configuration 和 Binding Rule Registry 使用不可变快照；
- 不通过反射或程序集初始化顺序选择实现。

### 16.4 用户控制

- AppUI 不安装 TMP；
- AppUI 不修改 Scripting Define Symbols；
- AppUI 不自动启用 Provider；
- AppUI 不自动注入 Resolver；
- AppUI 不自动为 Notice Prefab 添加或创建 TMP 组件。

### 16.5 集成诊断

- TMP Integration 页面显示 API、Define、程序集、Provider、规则和 Notice 状态；
- Edit Mode 不把 Runtime Resolver 状态伪装成已验证；
- Play Mode 按已初始化 Host 显示 Resolver ID 快照；
- 每个 Warning/Failure 都包含稳定诊断码、影响和人工修复步骤；
- 诊断不会自动修改 Define、Settings、Prefab 或 Runtime Configuration；
- TMP Sample 和 TMP Consumer 的诊断报告无 Failure，且必需项全部 Pass。

### 16.6 发布

- Base 与 TMP Consumer 来自两个不共享缓存的 pristine 临时环境；
- 两个 Consumer 的 EditMode、PlayMode、Mono 与 IL2CPP 均有候选提交证据；
- 临时 Consumer 已删除，只保留验证报告和构建摘要；
- `package.json`、README 和 Changelog 版本一致为 `0.4.0-pre.1`；
- `v0.3.0-pre.1` Tag 指向保持不变；
- 新 Tag 只在候选提交通过 Tag URL 冒烟后创建 Pre-release。

## 17. 风险与约束

### 17.1 UGUI Text 的长期状态

基础 Binding 暂时保留 UGUI `Text`，因为它是当前 UGUI 依赖中的可用基础组件，并能证明
无 TMP 模式。它不是对长期文本技术路线的承诺。未来如需移除，应单独设计，不与 0.4
的 TMP 解耦混为一项 Breaking Change。

### 17.2 单包条件编译

同一个包内使用 Define 能保持安装简单，但用户必须显式配置项目。文档、Settings 状态与
错误诊断必须足够清楚，不能把配置错误表现为“某些类型突然消失”。

### 17.3 可选程序集 API 演进

TMP 集成是官方实现，但仍位于 1.0 前的预发布 API。它可以独立演进；基础 Core 不得为了
兼容某个具体 TMP 版本反向引入 TMP 合同。

### 17.4 社区移植

去除基础 TMP 引用会降低旧 Unity 移植的一个障碍，但不等于已经兼容旧 Unity。UGUI 内部
API、Editor API、C# 编译器和 Player 构建差异仍需社区 Fork 在真实 Consumer 中验证。

## 18. 完成定义

当且仅当以下条件同时成立，0.4 的“TMP 可选化”才算完成：

1. 基础包在无 TMP Define 的 pristine Base Consumer 中独立通过完整消费验证；
2. 同一候选包在另一个 pristine TMP Consumer 中恢复 Binding、Focus、Input 和
   Notice 能力；
3. 所有扩展选择都是显式、确定、可诊断且不会自动修改项目；
4. Editor 和 Play Mode 诊断能区分 Pass、Warning、Failure 与 Not Verifiable；
5. 迁移文档能让 0.3 TMP 用户完成升级；
6. 发布证据来自两个相互隔离的干净 Consumer，而不是仅来自包内测试；
7. 历史 Tag 未被移动或重写。
