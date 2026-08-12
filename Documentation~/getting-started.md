# 快速开始

目标：在独立 Unity 6 项目中完成安装、显式初始化，并让一个页面走完打开、刷新和关闭。

本教程只覆盖 AppUI 的唯一 Official Target：Unity 6.0 / `6000.0`。使用 Unity 2022.3、2021.3 或其他版本时，先阅读[社区 Unity 移植指南](community-unity-porting.md)；不要把本教程理解为那些版本已获官方支持。

## 1. 安装

在 Package Manager 选择 `Add package from git URL...`：

```text
https://github.com/TechJoiH/JoiH-AppUI.git#v0.2.0-pre.4
```

`v0.2.0-pre.4` 是计划中的候选 Tag，只有它实际出现在 GitHub Release 页面后才可按此地址作为官方预发布版本安装。`v0.2.0-pre.2` 与 `v0.2.0-pre.3` 虽然已有不可变 Tag，但发布门禁未完成且没有 GitHub Release，不属于 Officially Supported Releases。真实项目应使用[官方发布清单](supported-unity-versions.md#officially-supported-releases)中的不可变 Tag，不要使用无版本 URL 或 `main`；评估尚未发布的候选时必须固定精确 Commit。

AppUI 只依赖 UGUI，不要求安装第三方异步包。

## 2. 选择三项项目实现

Runtime 必须接收 `IUIOperationFactory`、`IUIAssetProvider` 和 `IAppUIExecutionContext`。你可以自行实现，或先从 Package Manager 导入 **Basic Integration** Sample。Sample 提供纯回调 Operation、Unity 主线程上下文和显式引用资源表；它们属于消费项目，不属于 Core 默认实现。

## 3. 准备 Runtime Root

场景中必须已有 EventSystem。创建：

```text
AppUIRoot
├── GlobalUIRoot
├── AppUIManager
├── AppUIRuntimeHost
├── SampleAppUIInstaller（仅使用 Sample 时）
└── SystemLayer
    └── UILayerRoot
```

`UILayerRoot` 最少配置：

- `LayerId = SystemLayer`
- `CanvasDomain = System`
- `ContentRoot = SystemLayer` 的 RectTransform

项目只需创建实际会使用的 Layer，但每个 Definition 必须存在匹配的 Layer 与 CanvasDomain。

## 4. 创建 Controller 与 Prefab

```csharp
using Joi.H.AppUI;

public sealed partial class SettingsPanelController : PanelBaseController
{
    protected override void OnDataLoadEx(object data)
    {
        // 保存或解析 ViewModel。
    }

    protected override void OnRefreshEx()
    {
        // 更新文本、按钮和列表。
    }
}
```

将 Controller 挂在页面 Prefab 根节点。一个页面实例必须能解析到一个主要 `PanelBaseController`。

## 5. 创建 Definition、Registry、Profile

通过 `Create > Joi.H AppUI` 创建：

- Page Definition
- Page Definition Registry
- Runtime Profile

页面 Definition 示例：

```text
DefinitionId = settings
PrefabAssetId = ui/settings
LayerId = SystemLayer
CanvasDomain = System
Scope = SceneScope
OpenPolicy = RejectIfOpeningOrOpen
```

把 Definition 加入 Registry，再把 Registry 配到 Runtime Profile，最后将 Profile 分配给 `AppUIRuntimeHost`。

如果使用 Basic Integration Sample，在 `SampleAppUIInstaller.assets` 中把 `ui/settings` 指向该 Prefab。若使用自己的 Provider，则 AssetId 的含义完全由项目决定。

## 6. 显式初始化

Sample Installer 的核心逻辑等价于：

```csharp
IUIOperationFactory operations = projectOperations;
IUIAssetProvider assets = projectAssets;
IAppUIExecutionContext execution = projectExecutionContext;

AppUIInitializationResult result = runtimeHost.Initialize(
    new AppUIRuntimeDependencies(operations, assets, execution));

if (!result.Success)
{
    Debug.LogError("AppUI initialization failed: " + result.Status);
}
```

框架不会在 Awake 中猜测依赖，也没有 Resources fallback。

## 7. 打开、刷新、关闭

```csharp
IUIService ui = runtimeHost.Manager.Service;

IUIOperation<UIOpenResult> open = ui.Open(
    "settings",
    UIOpenArgs.FromExplicit(viewModel)
        .WithSceneScopeId("MainMenu"));

open.Register(completion =>
{
    if (completion.Status != AppUIOperationStatus.Succeeded)
    {
        Debug.LogError("Open infrastructure failed: " + completion.Status);
        return;
    }

    if (!completion.Result.Success)
    {
        Debug.LogError("Open page failed: " + completion.Result.Error);
    }
});

ui.Refresh(
    "settings",
    new UIRefreshArgs(updatedViewModel)
        .WithSceneScopeId("MainMenu"));

ui.Close("settings");
```

`Register` 返回 `IDisposable`。长生命周期对象应保存并在销毁/解绑时释放订阅。Controller 内部可用 `RegisterDisposeAction(subscription.Dispose)`。

## 8. 退出 Runtime

推荐顺序：

1. 停止产生新的 UI 请求；
2. 调用 `ReleaseScope` 或 `UnbindScene`；
3. 调用 `AppUIRuntimeHost.Shutdown()`；
4. 再销毁项目自有 Provider 和 UI Root。

下一步阅读[核心概念](core-concepts.md)和[生命周期](lifecycle.md)。
