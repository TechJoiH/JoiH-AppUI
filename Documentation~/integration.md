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

非标准 AssetId 可在 Editor 程序集中实现 `IUIEditorAssetIdResolver` 并注册到 `UIEditorAssetIdResolverRegistry`。Binding 与 Definition 创建工具便能使用与运行时相同的 ID 规则。

## Shutdown 顺序

1. 停止新请求；
2. ReleaseScope/UnbindScene；
3. `AppUIRuntimeHost.Shutdown()`；
4. 销毁项目 Provider；
5. 销毁 UI Root。

这个顺序确保 AppUI 归还 Lease 时 Provider 仍然存活。

## Sample 的定位

Basic Integration 提供 `CallbackUIOperationFactory`、`UnityMainThreadExecutionContext` 和 `InMemoryUIAssetProvider`。它用于学习和冒烟验证，不代表推荐所有项目复制其内部实现。成熟项目通常直接适配已有调度器与资源服务。
