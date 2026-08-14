# 生命周期

异步 UI 最容易出错的地方不是语法，而是“谁拥有状态、何时允许提交、晚到资源由谁释放”。

## 页面钩子顺序

首次打开：

```text
Load Asset + AppUI owns Lease
-> Instance Strategy Claim
-> Allocation validation + atomic accept
-> OnCreateEx
-> OnBindGeneratedFields
-> OnInitEx
-> OnDataLoadEx（有数据时）
-> OnRefreshEx
-> OnBeforeShowEx
-> BeginShowTransition
-> OnShowEx
```

刷新：

```text
OnDataLoadEx -> OnRefreshEx
```

仅隐藏关闭：

```text
CanCloseEx -> OnBeforeHideEx -> BeginHideTransition -> OnHideEx
```

释放关闭：

```text
Hide 流程 -> OnDisposeEx -> 解绑 -> Allocation.Dispose
-> Destroy/Pool -> UIAssetLease.Dispose 或 RetainLease
```

被上层全屏/阻挡页面影响时，PauseDepth 从 0 变为非 0 调用 `OnPauseEx`，回到 0 调用 `OnResumeEx`。

## Immediate 与等待

无动画返回 `UITransition.Immediate`。需要等待时返回 `UITransition.WaitFor(operation)`；该 Operation 由项目实现创建，成功结果为 `UITransitionResult`。

## 两层失败

```text
Operation Failed     = Provider、执行上下文或实现抛出异常
Operation Cancelled  = 请求取消
Operation Expired    = 旧版本/旧意图不再允许提交
Operation Succeeded  = 流程正常产出一个领域结果

UIOpenResult.Success = 页面是否真正打开
```

例如找不到 Definition 是 `Succeeded + UIOpenResult.Fail(DefinitionNotFound)`，不属于异常。

## 取消与过期

`RequestCancellation()` 是请求信号，不会假装工作已经结束。生产端仍须最终写入 Cancelled/Failed/Succeeded/Expired 之一。页面状态机在关键提交点检查 CancellationToken、页面版本、SceneScope 与 Runtime epoch。

新操作替代旧 pending intent、Runtime Shutdown 或场景所有权变化时，旧操作可能 Expired。调用方应将其视为“结果不再适用”，而不是重试错误。

Bind/Unbind/Release 是组合 Operation。外层取消会传给当前子操作，停止启动后续
页面规则，并只写入一个终态。每次 SceneScope 绑定都有 generation；旧场景晚到
结果即使遇到相同 SceneScopeId 已重新绑定，也不能提交到新 generation。

## 晚到结果与 Lease

Provider 可能在页面取消后才返回资源。AppUI 不实例化晚到页面，但仍会 Dispose 结果中的 `UIAssetLease`。Provider 必须让 Release 回调安全且幂等；AppUI 的 Lease 本身保证最多调用一次。

实例 Strategy 抛异常、遗弃 Claim、返回无效对象或 Controller 验证失败时，
`UIAssetLeaseTransfer` 仍把 Lease 归还 Provider。池化只有在保留活对象时才能保留
Lease，并必须在 eviction 或 Runtime shutdown 后显式释放。

## 订阅生命周期

`Register` 返回 `IDisposable`。页面/组件销毁前应释放订阅，避免完成回调访问失效对象。Operation 保证终态最多写入一次；晚注册者仍应收到相同终态。
