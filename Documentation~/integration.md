# 接入与资源适配

## 组合根

`AppUIRuntimeHost` 负责把 Profile、Registry、LayerRoot 与项目提供的三项能力交给 Manager。它不自动初始化、不创建 EventSystem、不加载场景，也不把 Root 标为 DontDestroyOnLoad。

```csharp
AppUIInitializationResult result = runtimeHost.Initialize(
    new AppUIRuntimeDependencies(
        projectOperationFactory,
        projectAssetProvider,
        projectExecutionContext));
```

缺少任一项会返回结构化失败，不存在 fallback。

可选 Load/Instance Strategy 不放进三项必需端口，也不通过 Manager 按顺序注册：

```csharp
AppUIRuntimeConfiguration configuration =
    new AppUIRuntimeConfiguration(loadStrategies, instanceStrategies);

AppUIInitializationResult result = runtimeHost.Initialize(
    dependencies,
    configuration);
```

配置是初始化快照。空白/重复 StrategyId 与 Definition 引用未知 ID 会在
Manager 接收依赖前返回结构化失败。

## 自定义 Provider

```csharp
public interface IUIAssetProvider
{
    bool TryLoad<T>(string assetId, out UIAssetLoadResult<T> result)
        where T : UnityEngine.Object;

    IUIOperation<UIAssetLoadResult<T>> Load<T>(
        string assetId,
        CancellationToken cancellationToken)
        where T : UnityEngine.Object;
}
```

若不支持同步加载，`TryLoad` 返回 false，并将状态设为 `SynchronousLoadUnsupported`，Runtime 才会调用 `Load`。若同步查询明确得到 NotFound，则不应再启动异步请求。

成功加载可返回：

```csharp
UIAssetLoadResult<GameObject>.Success(
    prefab,
    new UIAssetLease(handle.Release));
```

Provider 的异步实现只需返回项目 Operation；可以在项目程序集内把现有异步类型映射到 `IUIOperation<T>`。不要把具体 handle 暴露给 Controller。

## Editor AssetId

非标准 AssetId 在 Editor 程序集中实现 `IUIEditorAssetIdResolver`，提供稳定
`ResolverId`，再调用 `UIEditorAssetIdResolverRegistry.Register`。项目必须在
`UIBindingSettings.SelectedAssetIdResolverId` 显式选择；缺失、未注册和重复 ID
都会报错。框架不会默认选择 Resources，也不会按文件名降级搜索。

## Shutdown 顺序

1. 停止新请求；
2. ReleaseScope/UnbindScene；
3. `AppUIRuntimeHost.Shutdown()`；
4. 清空自定义实例池并释放其保留 Lease；
5. 销毁项目 Provider；
6. 销毁 UI Root。

这个顺序确保 AppUI 归还 Lease 时 Provider 仍然存活。

## Sample 的定位

Basic Integration 提供最小三端口实现。Custom Host Integration 进一步展示
Runtime Configuration、显式 Scene Bridge、世界输入查询、池化 Strategy、Editor
Resolver 和 Host Contract Test Kit。两者都只随用户主动导入进入项目，不会被
Runtime 自动注册。完整规则见 [Host Integration](host-integration.md)。
