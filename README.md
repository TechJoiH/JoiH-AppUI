# Joi.H AppUI

Joi.H AppUI 是面向 Unity 6 UGUI 的页面与交互框架。它统一页面定义、分层、作用域、生命周期、Binding、焦点导航和输入阻挡，但不替项目选择异步库、资源系统或业务架构。

> 当前候选版本：`0.2.0-pre.2`。这是 Unity 6 Official Target 下的预发布候选，尚未完成 IL2CPP、远端 Commit、不可变 Tag 和 Tag URL 安装门禁，不能视为 Officially Supported Release。1.0 前 API 与序列化字段仍可能调整。

## 为什么做

当项目从几个 Canvas 扩展到多场景、HUD、弹窗、模态页、动态列表和手柄导航时，常见问题不是“画不出 UI”，而是页面打开方式、返回行为、资源释放、输入归属和焦点状态开始互相冲突。Joi.H AppUI 把这些重复问题变成可声明、可测试的协议：

- `UIPageDefinition` 保存 Layer、Scope、打开策略和输入规则；
- `PanelBaseController` 只负责页面数据、显示和交互；
- `IUIService` 提供统一的打开、刷新、关闭、取消与作用域释放入口；
- `IUIAssetProvider` 隔离 Addressables、AssetBundle 或项目自有资源系统；
- Binding 工具将 `B_` 节点生成字段、写入引用并验证；
- Focus 与 Input 模块统一鼠标、键盘和手柄下的导航与阻挡语义。

它不是新的渲染方案，不替代 UGUI，也不接管 EventSystem、场景切换、业务服务、资源框架和异步后端。

## 框架原则

AppUI Core 只定义契约，不提供默认异步或资源实现。接入项目必须显式提供：

| 接口 | 项目负责什么 |
|---|---|
| `IUIOperationFactory` | 创建中立 Operation；可在项目内适配 Task、Awaitable、协程、回调或其他方案 |
| `IUIAssetProvider` | 根据 AssetId 加载资源，并用 `UIAssetLease` 表达释放所有权 |
| `IAppUIExecutionContext` | 把外部完成回调切回 Unity 主线程 |

因此安装 AppUI 不会自动安装第三方异步包，也不会偷偷调用 Unity Resources API。Package Manager 中的 **Basic Integration** Sample 提供一套纯回调、显式引用的参考实现，但它只在用户主动导入时进入项目，并不是 Runtime 默认行为。

## 核心能力

| 能力 | 主要类型 | 作用 |
|---|---|---|
| 页面系统 | `IUIService`、`UIPageDefinition`、`PanelBaseController` | 统一 Open、Refresh、Pause、Resume、Close、Release |
| 分层与作用域 | `UILayerRoot`、`UILayerId`、`UIPageScope` | 管理 HUD、Overlay、Popup、Modal 与场景清理 |
| 中立异步协议 | `IUIOperation<T>`、`IUIOperationFactory` | 不绑定任何 await 后端，保留取消、失败与过期语义 |
| 资源边界 | `IUIAssetProvider`、`UIAssetLease` | 接入任意资源系统并保证租约只释放一次 |
| Binding | Scanner、Generator、Binder、Validator | 从 `B_` 层级生成并验证序列化引用 |
| 焦点导航 | `AppUIFocusScope`、`AppUIFocusChain`、`AppUIFocusGroupNavigator` | 默认焦点、方向移动、分组、滚动与虚拟化 |
| 输入策略 | `AppUIInputPolicyRoot`、`AppUIInputZone`、`AppUIInputHitResolver` | 声明 UI 输入阻挡与世界输入穿透 |
| 轻量提示 | `INoticeService` | Toast、Tooltip、FloatingText 等非页面提示 |

## Unity 支持范围

- 唯一 Official Target：Unity 6.0 / `6000.0`
- UGUI `2.0`
- TextMeshPro（Unity UI 栈）
- 无第三方 Inspector 或异步包硬依赖

选择 Unity 6.0 是因为它是 AppUI 当前主要开发、真实项目使用和发布验证环境；该目标不会随 Unity 最新 LTS 自动变化。Unity 6.1/6.2/6.3 不会因为版本号更高而自动获得官方支持。

Unity 2022.3 与 2021.3 当前属于 `Community Port`：允许用户自行移植，但官方不提供对应 Package、Tag、CI 或维护承诺。完整的五级状态定义见 [Unity 版本支持政策](Documentation~/supported-unity-versions.md)，自行适配见[社区 Unity 移植指南](Documentation~/community-unity-porting.md)。Community Verified 只代表存在社区外部证据，不等于官方维护。

## 安装

真实项目应安装经过验证的普通 SemVer Tag，不要长期跟随 `main`。打开 `Window > Package Manager`，点击 `+ > Add package from git URL...`，输入：

```text
https://github.com/TechJoiH/JoiH-AppUI.git#v0.2.0-pre.2
```

> Planned tag; install only after it appears on the GitHub Release page.

也可以直接加入 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.joih.appui": "https://github.com/TechJoiH/JoiH-AppUI.git#v0.2.0-pre.2"
  }
}
```

`v0.2.0-pre.2` 是计划中的下一候选 Tag，只有 GitHub Release 页面实际出现该 Tag 后才可按上面的 URL 安装；Tag 创建前这条地址不可用。评估尚未发布的候选时必须固定精确 Commit，并自行承担预发布风险；不要把无版本仓库 URL 或 `main` 当成生产依赖。仓库若为 Private，Git 还需要使用已授权的 GitHub 凭据。

## 最短接入路径

1. 安装包，在 Package Manager 中导入 **Basic Integration** Sample。
2. 在场景中准备 EventSystem、`GlobalUIRoot`、`AppUIManager`、`AppUIRuntimeHost` 与所需 `UILayerRoot`。
3. 创建 `UIPageDefinitionRegistry`、`AppUIRuntimeProfile` 和页面 Definition。
4. 将 `SampleAppUIInstaller` 放在 Runtime Root，并把页面 Prefab 以 AssetId 注册到它的列表。
5. 通过 `runtimeHost.Manager.Service` 调用页面操作。

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
- [Editor Tools & Validation](Documentation~/editor-tools-validation.md)
- [Unity 版本支持政策](Documentation~/supported-unity-versions.md)
- [社区 Unity 移植指南](Documentation~/community-unity-porting.md)
- [FAQ](Documentation~/faq.md)

## 当前验证与限制

- 已在独立、未安装第三方异步包的 Unity 6 消费项目完成 Domain Reload、EditMode 与 PlayMode 验证；最新数字见[验证与发布门禁](Documentation~/validation.md)。
- 当前尚无 Officially Supported Release；本机 IL2CPP 因缺少 Windows C++ toolchain 仍为环境 `Blocked`，远端 Commit、不可变 Tag 和 Tag URL 冒烟完成前不会提升状态。
- `0.x` 期间允许破坏性整理，升级前请阅读 [CHANGELOG](CHANGELOG.md)。
- 最终视觉、字体、动画手感、Prefab 点击区域仍需要在接入项目中人工验收。
- 对外分发许可证尚未确定；在 LICENSE 明确前，不应推定获得复制、修改或再分发授权。
- 贡献与安全说明见 [CONTRIBUTING](CONTRIBUTING.md) 和 [SECURITY](SECURITY.md)。
