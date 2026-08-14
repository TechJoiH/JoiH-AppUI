# 核心概念

## Definition

`UIPageDefinition` 是页面的声明式契约，包含 PageId、Prefab AssetId、Layer、CanvasDomain、Scope、OpenPolicy、Cancel、输入阻挡和 Tick 开关。业务数据不放进 Definition。

## Controller

`PanelBaseController` 是页面实例的行为边界。Controller 接收数据、更新视图、订阅交互并响应生命周期；它不应自行管理页面栈、实例化其他页面或持有资源系统句柄。

## Layer

Layer 决定显示和输入优先级，`UILayerRoot` 提供实际父节点，`UICanvasDomain` 描述其 Canvas 领域。Definition 指向的 Layer/Domain 必须存在且匹配。

## Scope

Scope 决定页面所有权和批量释放边界：

- `GlobalScope`：跨场景保留，由 Runtime 或宿主显式释放；
- `SceneScope`：随指定 SceneScopeId 清理；
- `LoadingScope`、`TemporaryScope`：用于短期流程并由宿主显式释放。

SceneScopeId 是所有权标签，不是 Unity Scene 对象引用。

## Binding

需要被 Controller 访问的节点以 `B_` 命名。Editor Scanner 生成 partial 字段，Binder 写入 Prefab 引用，Validator 检查字段、所有权和嵌套边界。Binding 不做运行时反射生成。

## Provider

`IUIAssetProvider` 把项目资源系统适配为 AppUI 可理解的加载结果。成功结果可附带 `UIAssetLease`，AppUI 在页面释放或晚到结果被丢弃时调用它。Lease 的 `Dispose` 是幂等的。

## Runtime Configuration

`AppUIRuntimeDependencies` 只保存三项必需端口。可选
`IUILoadStrategy` / `IUIPageInstanceStrategy` 通过不可变
`AppUIRuntimeConfiguration` 在初始化时交付。StrategyId 是 Definition 与宿主
配置之间的稳定键，不通过初始化顺序覆盖。

## Instance Allocation

`IUIPageInstanceStrategy` 同时定义创建和释放，返回
`UIPageInstanceAllocation`。`UIAssetLeaseTransfer` 在 Controller 验证成功后才把
Lease 所有权交给 Allocation；池化 Strategy 必须让对象和 Lease 同生共灭。

## Operation

`IUIOperation<T>` 是中立的完成协议，不是 Task、Awaitable、协程或任何第三方类型。项目通过 `IUIOperationFactory` 决定其实现。

Operation 终态：

- `Succeeded`：调度成功，随后读取 `Result`；
- `Cancelled`：请求被取消且没有业务结果；
- `Failed`：基础设施/实现异常，读取 `Exception`；
- `Expired`：操作被更新版本、场景变化或新意图取代。

页面结果（如 `UIOpenResult.Success`）是第二层。Operation Succeeded 仍可能携带 `DefinitionNotFound` 等正常业务失败。

## Execution Context

资源或动画可能从任意线程完成，`IAppUIExecutionContext` 负责把状态提交回 Unity 主线程。Core 只调用接口，不创建线程或 PlayerLoop 调度器。
