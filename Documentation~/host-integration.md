# Host Integration

Joi.H AppUI 不识别某个具体宿主框架。接入一个未知项目时，只在项目侧增加
Adapter、Lifecycle Bridge、Installer、可选 Strategy、Editor Resolver 和测试
Driver；不修改 AppUI Core、Runtime 或 Editor。

## 权限与所有权

| 能力 | 最终权威 |
|---|---|
| 场景、流程、应用生命周期 | 宿主 |
| 资源后端、异步实现、Unity 执行上下文 | 宿主 |
| 世界输入是否真正执行 | 宿主 |
| 页面生命周期、栈、Layer、Scope | AppUI |
| Focus、UI 输入策略、Binding 契约 | AppUI |

同一页面只能有一个生命周期管理者。由 AppUI 管理的页面，宿主只能调用
`IUIService`，不能再通过另一套 UI Manager 打开、关闭、池化或销毁它。

## 必需端口与可选配置

每个 Runtime 必须提供三项能力：

```csharp
AppUIRuntimeDependencies dependencies = new AppUIRuntimeDependencies(
    projectOperationFactory,
    projectAssetProvider,
    projectExecutionContext);
```

可选 Strategy 独立放在不可变配置快照中：

```csharp
AppUIRuntimeConfiguration configuration =
    new AppUIRuntimeConfiguration(
        new IUILoadStrategy[] { projectLoadStrategy },
        new IUIPageInstanceStrategy[] { projectInstanceStrategy });

AppUIInitializationResult result = runtimeHost.Initialize(
    dependencies,
    configuration);
```

一参数 `Initialize(dependencies)` 等价于使用
`AppUIRuntimeConfiguration.Empty`。StrategyId 必须是非空、区分大小写的稳定
字符串。重复 ID、空 ID，以及 Definition 引用未知 ID，都会在 Manager 接收
依赖前返回结构化初始化失败；不存在 last-write-wins 注册顺序。

## Operation 与取消

- Factory 拥有具体 Operation 实现和取消资源；
- AppUI 在执行命令期间持有 Producer Source；
- 终态最多写入一次，晚注册者获得同一终态；
- 释放某个订阅只阻止该订阅回调；
- `RequestCancellation` 只是请求，不能伪造终态；
- 组合场景操作把取消传给当前子操作，停止启动后续子操作，并只发布一个终态。

## Scene 生命周期竞争

宿主在 scene ready 时调用 `BindScene`，离开时调用 `UnbindScene` 或
`ReleaseScope`。AppUI 不扫描 Scene，也不轮询宿主流程。

每次 SceneScope 绑定都有内部 generation。Unbind/Release 会同步使当前
generation 失效，因此旧场景晚到的加载结果不能在同名 SceneScopeId 已重新
绑定后提交；晚到资源仍会按所有权释放一次。

## Asset 与 Instance 所有权

Provider 成功结果可以携带 `UIAssetLease`。实例创建前 Lease 由 AppUI 持有，
并通过 `UIAssetLeaseTransfer` 两阶段交给 `IUIPageInstanceStrategy`：

1. Strategy `Claim()`；
2. 返回 `UIPageInstanceAllocation`；
3. AppUI 验证实例和 Controller；
4. AppUI 原子接受 Allocation；
5. Release 时 Allocation 同时处理对象与 Lease。

失败、取消、过期、晚到或被拒绝的 Allocation 都必须归还 Lease。池化 Strategy
只有在同时保留活对象和 Lease 时才能返回 `RetainLease`，并在 eviction/shutdown
时释放两者。

## Editor AssetId

运行时 `IUIAssetProvider` 与 Editor `IUIEditorAssetIdResolver` 必须使用相同
AssetId 语义。Resolver 显式注册稳定 ID：

```csharp
public string ResolverId => "project.asset-guid";

UIEditorAssetIdResolverRegistry.Register(
    new ProjectAssetIdResolver(),
    out string error);
```

然后在 `UIBindingSettings.SelectedAssetIdResolverId` 选择该 ID。缺少 Settings、
缺少选择、选择未注册 ID、重复注册都会得到集中诊断。Resources Resolver 只是
可选实现，不会自动安装或选中；Binding 工具也不会按文件名搜索降级。

## 世界输入

AppUI 只报告 UI 是否阻挡某个输入通道：

```csharp
bool canRunWorldCommand =
    !AppUIInputHitResolver.Shared.IsPointerBlocked(
        pointerPosition,
        AppUIInputChannel.PrimaryPointer);
```

是否执行世界命令仍由宿主决定。

## Shutdown

```text
停止新请求
-> UnbindScene / ReleaseScope
-> AppUIRuntimeHost.Shutdown
-> instance pool eviction
-> asset provider shutdown
-> destroy UI root
```

Provider 必须活到 AppUI 和池化 Strategy 归还全部 Lease 之后。

## Contract Test Kit

把包加入消费项目 `Packages/manifest.json` 的 `testables`，在测试 asmdef 中
引用 `Joi.H.AppUI.Tests.HostIntegration`，然后继承：

- `AppUIOperationFactoryContractFixture`
- `AppUIAssetProviderContractFixture`
- `AppUIExecutionContextContractFixture`
- `AppUIHostLifecycleContractFixture`
- `AppUIInstanceStrategyContractFixture`

该程序集受 `UNITY_INCLUDE_TESTS` 约束，不进入普通 Player Build。完整代码见
Package Manager 中的 **Custom Host Integration** Sample。

## 禁止模式

- AppUI 与宿主 UI Manager 同时拥有同一页面；
- Runtime 扫描宿主程序集寻找 Adapter；
- 通过初始化顺序隐式选择 Editor Resolver；
- 把 Provider handle 暴露给 Controller；
- 池保留对象却提前释放唯一 Asset Lease；
- AppUI Shutdown 前先销毁 Provider；
- 依赖宿主串行场景切换掩盖生命周期竞争。
