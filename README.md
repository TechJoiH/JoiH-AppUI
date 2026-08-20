# Joi.H AppUI

Joi.H AppUI 是面向 Unity 6 UGUI 的页面与交互框架。它统一页面定义、分层、作用域、生命周期、Binding、焦点导航和输入阻挡，但不替项目选择异步库、资源系统、文本技术或业务架构。

> 当前源码版本：`0.4.0-pre.1`。0.4 将基础 Runtime、Editor 和 Consumer 固定为纯 UGUI，把 TextMeshPro Binding、InputField、Dropdown、Notice 与诊断移到用户显式启用的可选 Integration。该版本尚未发布 Tag；当前最新 **Officially Supported Pre-release** 仍是 [v0.3.0-pre.1](https://github.com/TechJoiH/JoiH-AppUI/releases/tag/v0.3.0-pre.1)。1.0 前公共 API 与序列化字段仍可能调整。

> **Failed Release Attempt：**不可变 Tag `v0.2.0-pre.2` 已固定到 Commit `2ba1c90f732b429b3b76cd2d8bcba73a4bb486cc`，但 Tag URL 冒烟因发布工具未导出远端 Tag 解析命令而在进入 Unity 前失败，因此没有创建 GitHub Release，也不属于 Officially Supported Release。该 Tag 不会移动、删除或复用。

> **Failed Release Attempt：**不可变 Tag `v0.2.0-pre.3` 已固定到 Commit `df311323c4ca33f14f9128b4a6c7bc6c58836d2a`，Commit 与 Tag URL smoke 均通过，但正式 Artifact 审计发现 Binding/EditMode/PlayMode 仍包含外部验证 Run Root 的绝对路径，因此没有创建 GitHub Release。该 Tag 同样不会移动、删除或复用。

## 为什么做

当项目从几个 Canvas 扩展到多场景、HUD、弹窗、模态页、动态列表和手柄导航时，常见问题不是“画不出 UI”，而是页面打开方式、返回行为、资源释放、输入归属和焦点状态开始互相冲突。Joi.H AppUI 把这些重复问题变成可声明、可测试的协议：

- `UIPageDefinition` 保存 Layer、Scope、打开策略和输入规则；
- `PanelBaseController` 只负责页面数据、显示和交互；
- `IUIService` 提供统一的打开、刷新、关闭、取消与作用域释放入口；
- `IUIAssetProvider` 隔离 Addressables、AssetBundle 或项目自有资源系统；
- Binding 工具将 `B_` 节点生成字段、写入引用并验证；
- Focus 与 Input 模块统一鼠标、键盘和手柄下的导航与阻挡语义。
- 文本技术通过可选 Integration 接入；基础包不引用 TMP，也不替项目选择文本组件。

它不是新的渲染方案，不替代 UGUI，也不接管 EventSystem、场景切换、业务服务、资源框架和异步后端。

## 框架原则

AppUI Core 只定义契约，不提供默认异步或资源实现。接入项目必须显式提供：

| 接口 | 项目负责什么 |
|---|---|
| `IUIOperationFactory` | 创建中立 Operation；可在项目内适配 Task、Awaitable、协程、回调或其他方案 |
| `IUIAssetProvider` | 根据 AssetId 加载资源，并用 `UIAssetLease` 表达释放所有权 |
| `IAppUIExecutionContext` | 把外部完成回调切回 Unity 主线程 |

因此安装 AppUI 不会自动安装第三方异步包，也不会偷偷调用 Unity Resources API。Package Manager 中的 **Basic Integration** Sample 提供一套纯回调、显式引用的参考实现，但它只在用户主动导入时进入项目，并不是 Runtime 默认行为。

可选 Load/Instance Strategy 通过不可变 `AppUIRuntimeConfiguration` 在初始化时
一次性交付。重复或未知 StrategyId 会在 Manager 接收依赖前返回结构化失败，
不存在依赖 Awake 顺序的 last-write-wins 注册。

## 核心能力

| 能力 | 主要类型 | 作用 |
|---|---|---|
| 页面系统 | `IUIService`、`UIPageDefinition`、`PanelBaseController` | 统一 Open、Refresh、Pause、Resume、Close、Release |
| 分层与作用域 | `UILayerRoot`、`UILayerId`、`UIPageScope` | 管理 HUD、Overlay、Popup、Modal 与场景清理 |
| 中立异步协议 | `IUIOperation<T>`、`IUIOperationFactory` | 不绑定任何 await 后端，保留取消、失败与过期语义 |
| 资源边界 | `IUIAssetProvider`、`UIAssetLease` | 接入任意资源系统并保证租约只释放一次 |
| 实例所有权 | `IUIPageInstanceStrategy`、`UIPageInstanceAllocation` | 对称创建/释放、池化与 Lease 转移 |
| Binding | Scanner、Generator、Binder、Validator | 从 `B_` 层级生成并验证序列化引用 |
| 焦点导航 | `AppUIFocusScope`、`AppUIFocusChain`、`AppUIFocusGroupNavigator` | 默认焦点、方向移动、分组、滚动与虚拟化 |
| 输入策略 | `AppUIInputPolicyRoot`、`AppUIInputZone`、`AppUIInputHitResolver` | 声明 UI 输入阻挡与世界输入穿透 |
| 轻量提示 | `INoticeService` | Toast、Tooltip、FloatingText 等非页面提示 |
| 可选文本集成 | `Joi.H.AppUI.Integrations.TextMeshPro.*` | 用户按需启用 TMP Binding、焦点控件、Notice 与诊断 |

## Unity 支持范围

- 唯一 Official Target：Unity 6.0 / `6000.0`
- UGUI `2.0`
- 基础包只使用 UGUI；TextMeshPro 为可选、Define-gated Integration
- 无第三方 Inspector 或异步包硬依赖

选择 Unity 6.0 是因为它是 AppUI 当前主要开发、真实项目使用和发布验证环境；该目标不会随 Unity 最新 LTS 自动变化。Unity 6.1/6.2/6.3 不会因为版本号更高而自动获得官方支持。

Unity 2022.3 与 2021.3 当前属于 `Community Port`：允许用户自行移植，但官方不提供对应 Package、Tag、CI 或维护承诺。完整的五级状态定义见 [Unity 版本支持政策](Documentation~/supported-unity-versions.md)，自行适配见[社区 Unity 移植指南](Documentation~/community-unity-porting.md)。Community Verified 只代表存在社区外部证据，不等于官方维护。

## 安装

真实项目应安装经过验证的普通 SemVer Tag，不要长期跟随 `main`。打开 `Window > Package Manager`，点击 `+ > Add package from git URL...`，输入：

```text
https://github.com/TechJoiH/JoiH-AppUI.git#v0.3.0-pre.1
```

> 已验证的不可变 Pre-release Tag；不要改为 `main` 或无版本仓库 URL。

也可以直接加入 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.joih.appui": "https://github.com/TechJoiH/JoiH-AppUI.git#v0.3.0-pre.1"
  }
}
```

`v0.3.0-pre.1` 已完成完整 Consumer、Mono/IL2CPP、Commit SHA、Tag URL 与 Artifact 门禁，可以按上面的 URL 安装。`v0.2.0-pre.4` 仍保留为较早的已验证 Pre-release；`v0.2.0-pre.2` 与 `v0.2.0-pre.3` 是失败发布尝试，不在 Officially Supported Releases 中。不要把无版本仓库 URL 或 `main` 当成生产依赖。仓库若为 Private，Git 还需要使用已授权的 GitHub 凭据。

需要评估 0.4 源码候选时，应固定到经过你自己验证的 40 位 Commit；不要在 `v0.4.0-pre.1` Tag 实际发布前使用或假设该 Tag 存在。基础安装不启用 TMP。需要 TMP 时，按 [TextMeshPro 可选集成](Documentation~/textmeshpro-integration.md)显式添加 `JOIH_APPUI_TMP` 并导入对应 Sample。

## 最短接入路径

1. 安装包，在 Package Manager 中导入 **Basic Integration** Sample。
2. 创建 `UIBindingSettings`，选择 Sample 显式注册的
   `sample.basic.asset-guid` Resolver。
3. 在场景中准备 EventSystem、`GlobalUIRoot`、`AppUIManager`、`AppUIRuntimeHost` 与所需 `UILayerRoot`。
4. 创建 `UIPageDefinitionRegistry`、`AppUIRuntimeProfile` 和页面 Definition。
5. 将 `SampleAppUIInstaller` 放在 Runtime Root，并把页面 Prefab 以 AssetId 注册到它的列表。
6. 通过 `runtimeHost.Manager.Service` 调用页面操作。

```csharp
using System;
using Joi.H.AppUI;
using UnityEngine;

IUIOperation<UIOpenResult> operation =
    runtimeHost.Manager.Service.Open(
        "settings",
        UIOpenArgs.None.WithSceneScopeId("MainMenu"));

IDisposable subscription = operation.Register(completion =>
{
    if (completion.Status == AppUIOperationStatus.Failed)
    {
        Debug.LogException(completion.Exception);
        return;
    }

    if (completion.Status == AppUIOperationStatus.Succeeded &&
        !completion.Result.Success)
    {
        Debug.LogError("Open failed: " + completion.Result.Error);
    }
});
```

这里有两层结果：`completion.Status` 表示调度/基础设施结果；`completion.Result.Success` 表示页面业务操作结果。不要只检查其中一层。订阅者不再需要回调时应 `Dispose()`。

更完整的安装、初始化、Controller 和页面创建流程见[快速开始](Documentation~/getting-started.md)。

## Controller 示例

```csharp
using Joi.H.AppUI;

public sealed partial class SettingsPanelController : PanelBaseController
{
    protected override void OnDataLoadEx(object data)
    {
        // 接收 Open 或 Refresh 传入的数据。
    }

    protected override void OnRefreshEx()
    {
        // 将当前页面状态写入绑定视图。
    }
}
```

需要异步显示动画时，Controller 返回项目创建的 Operation：

```csharp
protected override UITransition BeginShowTransition()
{
    return UITransition.WaitFor(projectTransitions.PlayShow(this));
}
```

AppUI 只等待 `IUIOperation<UITransitionResult>`，不关心 `projectTransitions` 底层使用什么技术。

## Binding 工作流

运行时需要访问的 Prefab 节点使用 `B_` 前缀：

1. Generate Bindings；
2. 等待 Unity 编译与 Domain Reload；
3. Bind References；
4. Validate Bindings。

生成与绑定不能在尚未完成编译时强行合并。详见 [Binding System](Documentation~/binding-system.md)。

## 文档

- [文档总览](Documentation~/index.md)
- [快速开始](Documentation~/getting-started.md)
- [核心概念](Documentation~/core-concepts.md)
- [架构设计](Documentation~/architecture.md)
- [页面系统](Documentation~/page-system.md)
- [生命周期](Documentation~/lifecycle.md)
- [Binding System](Documentation~/binding-system.md)
- [Focus System](Documentation~/focus-system.md)
- [Input Policy](Documentation~/input-policy.md)
- [Notice System](Documentation~/notice-system.md)
- [TextMeshPro 可选集成](Documentation~/textmeshpro-integration.md)
- [Editor Tools & Validation](Documentation~/editor-tools-validation.md)
- [Host Integration](Documentation~/host-integration.md)
- [Migration to 0.3](Documentation~/migration-0.3.md)
- [Migration to 0.4](Documentation~/migration-0.4.md)
- [Unity 版本支持政策](Documentation~/supported-unity-versions.md)
- [社区 Unity 移植指南](Documentation~/community-unity-porting.md)
- [FAQ](Documentation~/faq.md)

## 当前验证与限制

- 框架开发阶段已在独立、未安装第三方异步包的 Unity 6 消费项目完成 Domain Reload、EditMode 与 PlayMode 验证；这些历史结果属于 `Historical Development Evidence`，不能跨候选 Commit 充当发布证据，当前状态见[验证与发布门禁](Documentation~/validation.md)。
- `v0.3.0-pre.1` 已在 Unity `6000.0.25f1` 的外部干净 Consumer 中完成 EditMode 166/166、PlayMode 30/30、Binding 0 Error/0 Warning、Mono/IL2CPP、Commit SHA 与 Tag URL smoke，并发布十项经过秘密、路径和远端 SHA-256 审计的证据。它是当前已验证的 Pre-release。
- `0.4.0-pre.1` 的发布门禁使用两个互不共享 Library 的临时 Consumer：Base 模式证明无 TMP 编译依赖，TextMeshPro 模式证明 Define、Sample、诊断、Binding、测试与 Mono/IL2CPP；在不可变 Tag 和正式证据发布前，它仍是源码候选。
- 发布前的包内 EditMode 150/150、PlayMode 14/14、Basic 11/11 与 Custom Host 10/10 属于 `Historical Development Evidence`；正式结论只采用绑定精确 Tag/Commit/Tree 的外部 `Current Release Evidence`。
- `0.x` 期间允许破坏性整理，升级前请阅读 [CHANGELOG](CHANGELOG.md)。
- 最终视觉、字体、动画手感、Prefab 点击区域仍需要在接入项目中人工验收。
- 对外分发许可证尚未确定；在 LICENSE 明确前，不应推定获得复制、修改或再分发授权。第三方 Adapter 分发、Community Verified 收录与外部 Adapter Index 继续作为 P2 生态门禁，不因技术 Sample/Test Kit 已完成而自动开放。
- 贡献与安全说明见 [CONTRIBUTING](CONTRIBUTING.md) 和 [SECURITY](SECURITY.md)。
