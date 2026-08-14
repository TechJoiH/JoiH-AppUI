# 页面系统

## 页面定义

页面至少需要稳定 PageId、Prefab AssetId、Layer、CanvasDomain、Scope 与 OpenPolicy。PageId 是调用和 Registry 索引键；AssetId 只对项目 Provider 有意义。

`LoadStrategyId` 与 `InstanceStrategyId` 为空时使用框架默认协议；非空时必须在
`AppUIRuntimeConfiguration` 中存在完全匹配的 Strategy。未知 ID 会阻止 Runtime
初始化，不会静默回退。

## 注册

`UIPageDefinitionRegistry` 是运行时查找入口。Prefab 存在并不等于已注册；Definition 必须加入当前 Runtime Profile 使用的 Registry。

## Controller 职责

Controller 应：

- 在 `OnDataLoadEx` 接收 ViewModel/参数；
- 在 `OnRefreshEx` 将状态写入视图；
- 在 `OnInitEx` 绑定一次性事件；
- 用 `RegisterDisposeAction` 对称解绑；
- 用 `CanCloseEx` 表达可关闭条件；
- 用 `BeginShowTransition` / `BeginHideTransition` 返回项目 Operation。

Controller 不应直接 Instantiate 页面、修改全局页面栈或猜测资源路径。

## 打开策略

`UIOpenPolicy` 决定页面已打开或忙碌时如何处理。常见语义包括拒绝、刷新现有实例和排队等待。排队意图可能被更新的同类意图替代，此时旧 Operation 以 `Expired` 结束。

## 页面 API

```csharp
IUIOperation<UIOpenResult> Open(string pageId, UIOpenArgs args);
IUIOperation<UIRefreshResult> Refresh(string pageId, UIRefreshArgs args);
IUIOperation<UICloseResult> Close(string pageId);
IUIOperation<UICancelResult> Cancel();
IUIOperation<UIScopeReleaseResult> ReleaseScope(
    UIPageScope scope,
    string sceneScopeId);
```

`Close` 默认释放页面实例；Controller 内需要自关闭时使用 `CloseSelf()`。统一 Cancel 只处理一个已解析目标，不会在一次请求中持续穿透整个栈。

## Scene Binding

`SceneUIBindingData` 声明场景 ready 时按 Order 打开的页面，以及退出时 Close/Release/保留的页面。规则串行执行；某一页面的业务关闭失败会进入聚合结果，Operation 级取消或异常会停止后续规则。每次 Bind 都创建新的 SceneScope generation，旧 generation 的 pending 结果不能提交到同 ID 的新场景。
