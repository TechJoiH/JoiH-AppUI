# Basic Integration Sample

这个 Sample 演示一种零第三方依赖的接入方式：

- `CallbackUIOperationFactory`：纯回调 Operation；
- `UnityMainThreadExecutionContext`：捕获 Unity 主线程上下文；
- `InMemoryUIAssetProvider`：显式 AssetId 到对象引用表，不使用 Resources；
- `SampleAppUIInstaller`：创建三项依赖并调用 `AppUIRuntimeHost.Initialize`。
- `BasicSampleAssetIdResolver`：显式注册 GUID AssetId 规则，不会自动成为全局默认。

使用：

1. 准备 `GlobalUIRoot`、`AppUIManager`、`AppUIRuntimeHost` 和 LayerRoot；
2. 创建 `UIBindingSettings`，将 `SelectedAssetIdResolverId` 设置为
   `sample.basic.asset-guid`；
3. 配置 Runtime Profile 与 Page Registry；
4. 添加 `SampleAppUIInstaller`；
5. 在 Assets 列表中将 Definition 的 PrefabAssetId（Prefab GUID）映射到页面 Prefab；
6. Play 后通过 `runtimeHost.Manager.Service.Open(...)` 发起请求。

`Tests/HostContractTests.cs` 展示如何继承可选 Host Integration Contract
Test Kit，验证 Operation、Asset、Execution 与生命周期适配器。

这些类型只随 Sample 导入，不在 AppUI Core/Runtime 中注册默认实现。生产项目可以全部替换为自己的 Operation、Addressables/AssetBundle Provider 和主线程调度器。
