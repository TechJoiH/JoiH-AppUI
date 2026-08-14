# Custom Host Integration Sample

这个 Sample 展示一个未知宿主如何在**不修改 AppUI Core、Runtime 或 Editor**
的前提下完成完整接入。它不代表必须采用这些具体实现，所有类型只在用户主动
导入 Sample 后进入消费项目。

## 包含内容

- `CustomHostOperationFactory`：宿主拥有的 callback Operation；
- `CustomHostAssetProvider`：显式 GUID/AssetId 目录和可观察 Lease；
- `CustomHostExecutionContext`：把外部完成切回 Unity 上下文；
- `CustomHostInstaller`：创建三项必需端口与可选 Runtime Configuration；
- `CustomHostSceneBridge`：由宿主场景/流程系统显式调用 Bind/Unbind；
- `CustomHostWorldInputGate`：世界输入执行前查询 AppUI 阻挡；
- `CustomHostPooledInstanceStrategy`：演示对象与 Asset Lease 的对称池化；
- `CustomHostAssetIdResolver`：显式注册 GUID Editor Resolver；
- `CustomHostContractTests`：继承公开 Test Kit 验证五类边界。

## 接入步骤

1. 在全局 UI 根对象上配置 `AppUIManager`、`AppUIRuntimeHost`、所有
   `UILayerRoot`、Runtime Profile 和 Page Registry。
2. 添加 `CustomHostInstaller`，在 `assets` 中配置 GUID AssetId 与对象引用。
3. 创建 `UIBindingSettings`，把 `SelectedAssetIdResolverId` 设置为
   `sample.custom-host.asset-guid`。
4. 需要池化的 Definition 把 `InstanceStrategyId` 设置为
   `sample.custom-host.pool`，并启用 Installer 的 sample pooling。
5. 场景 ready 时由宿主调用 `CustomHostSceneBridge.NotifySceneReady()`；场景
   离开时调用 `NotifySceneLeaving()`。Sample 不使用 Unity 场景回调自动猜测。
6. 世界点击、拖拽或缩放真正执行前，调用
   `CustomHostWorldInputGate.CanProcessWorldInput(...)`。
7. 应用退出或宿主 UI 子系统关闭时调用 `CustomHostInstaller.Shutdown()`。

Shutdown 顺序固定为：

```text
AppUIRuntimeHost.Shutdown
  -> active allocation returns to strategy
  -> pooled strategy evicts objects and leases
  -> asset provider shuts down
```

不要让宿主的另一套 UI Manager 同时管理 AppUI 页面，也不要在 Provider 仍有
AppUI Lease 时先销毁资源后端。
