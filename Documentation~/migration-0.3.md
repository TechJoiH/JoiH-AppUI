# Migration to 0.3

`0.3.0-pre.1` 重新收敛宿主接入、场景竞争、实例与资源所有权，以及 Editor
AssetId 选择。它仍是 1.0 前预发布版本，允许必要的破坏性调整。

## 1. Destroy Strategy 改为 Instance Strategy

旧 API：

```text
UIPageDefinition.DestroyStrategyId
IUIDestroyStrategy
```

新 API：

```text
UIPageDefinition.InstanceStrategyId
IUIPageInstanceStrategy
UIPageInstanceAllocation
UIAssetLeaseTransfer
```

序列化资产通过 `[FormerlySerializedAs("DestroyStrategyId")]` 保留原字段值，
但 C# 代码、Editor 工具和文档必须改用 `InstanceStrategyId`。旧 Strategy 需要
同时实现创建与释放，并明确池化 Lease 的 retain/evict 行为。

## 2. Strategy 注册移入 Runtime Configuration

以下公开调用已移除：

```csharp
manager.RegisterLoadStrategy(strategy);
manager.RegisterInstanceStrategy(strategy);
```

改为在初始化时一次性交付不可变快照：

```csharp
AppUIRuntimeConfiguration configuration =
    new AppUIRuntimeConfiguration(loadStrategies, instanceStrategies);

runtimeHost.Initialize(dependencies, configuration);
```

重复、空白或 Definition 未注册 ID 现在是所有构建一致的结构化初始化失败，
不会覆盖先前注册项，也不会只在 Development Build 中记录日志。

## 3. Editor Resolver 必须显式注册与选择

以下全局单例 API 已移除：

```text
UIEditorAssetIdResolverRegistry.Current
SetResolver
ResetToResources
```

Resolver 现在实现 `ResolverId`，通过 `Register(resolver, out error)` 注册；项目在
`UIBindingSettings.SelectedAssetIdResolverId` 选择。包不再隐式安装 Resources
Resolver，也不在解析失败后按路径或文件名猜测。

## 4. SceneScope 重新绑定更严格

Unbind/Release 现在同步使 SceneScope generation 失效。旧场景的 pending Open
即使在同一个 SceneScopeId 重新绑定后成功加载，也只会清理结果，不能提交到新
场景。组合 Bind/Unbind/Release 的取消会传给当前子操作，并停止后续子操作。

## 5. Instance 与 Lease 的释放边界

自定义实例 Strategy 不再只提供 Destroy 回调。它必须返回
`UIPageInstanceAllocation`。AppUI 在 Controller 验证成功后才接受 Lease Claim；
被拒绝、抛异常或遗弃 Claim 都会把 Lease 归还 Provider。

池化实现必须满足：

```text
retain GameObject => retain Lease
pool eviction     => destroy object + dispose Lease
runtime shutdown  => return active allocation before provider shutdown
```

## 6. 推荐迁移顺序

1. 固定当前项目 Commit，并在独立分支升级包；
2. 替换 `DestroyStrategyId` 和旧 Strategy；
3. 把 Strategy 注册移动到 `AppUIRuntimeConfiguration`；
4. 为 Editor Resolver 添加 ID、显式注册和 Settings 选择；
5. 检查 Scene Bridge 是否由宿主显式调用；
6. 检查 Shutdown 时 Provider 是否最后销毁；
7. 继承 Host Integration Contract Test Kit；
8. 运行 Binding、EditMode、PlayMode、Mono 与 IL2CPP。

Basic Integration 展示最小三端口接入；Custom Host Integration 展示完整迁移后
结构。

## LICENSE 边界

当前仓库尚未选择分发许可证。技术迁移、测试和本地评估可以继续，但第三方
Adapter 分发、Community Verified 收录与外部 Adapter Index 属于 P2 生态门禁，
必须等待仓库出现明确 `LICENSE` 后再决定；本文不授予额外许可。
