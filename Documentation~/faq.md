# FAQ

## 安装后为什么还不能直接 Open？

“直接安装使用”表示包本身不需要额外第三方依赖即可编译；运行页面仍必须由项目选择并注入 Operation、Asset Provider 和 Execution Context。可先导入 Basic Integration Sample 作为参考实现。

## AppUI 为什么不自带异步实现？

因为框架不应替项目决定 Task、Awaitable、协程、回调或第三方库。Core 统一的是取消、完成、失败、过期和订阅语义，而不是语法。

## 我还能使用 UniTask 吗？

可以，但它只是项目自己的可选适配器。安装、版本和转换扩展都由项目维护，AppUI 包不会引用或自动安装它。

## 为什么没有 Resources Provider？

Resources 是具体资源策略。AppUI 只使用 `IUIAssetProvider`；项目可适配 Addressables、AssetBundle、远端缓存、Resources 或测试内存表。Basic Integration 使用显式对象引用，不调用 Resources。

## Operation Succeeded，为什么页面仍然打开失败？

Succeeded 表示框架正常产出了 `UIOpenResult`。继续检查 `UIOpenResult.Success` 和 `Error`。DefinitionNotFound、LayerNotFound、打开策略拒绝等是领域失败，不是异常。

## Cancelled 与 Expired 有什么区别？

Cancelled 表示取消请求被确认；Expired 表示结果已被新版本、新意图、场景或 Runtime 代次替代，不再允许提交。

## 为什么 Binding 要分 Generate 与 Bind 两步？

生成字段必须先被 Unity 编译，Binder 才能通过类型信息写入引用。两步之间必须等待 Domain Reload。

## Prefab 已存在，为什么 Open 报 DefinitionNotFound？

Prefab 不会自动注册。请确认 Definition 的 PageId、Registry 条目和 Runtime Profile 引用一致。

## 焦点移动为什么改变了选中视觉或触发业务？

Focus、业务 Selection 和 Hover 应分离。默认移动只改变 Focus；业务确认通常发生在 Click/Submit。检查是否把三种状态绑定到了同一个全选框或在 Select 回调里执行了业务命令。

## UI 上的点击为什么仍触发世界操作？

确认 EventSystem 与 GraphicRaycaster 存在，页面有可 Raycast 面，输入系统在执行世界命令前查询 `AppUIInputHitResolver`，并检查 `AppUIInputZone` 是否错误放行了该通道。

## 谁释放资源？

Provider 在成功结果中返回 `UIAssetLease`。AppUI 在页面/Notice 释放或晚到结果被丢弃时 Dispose Lease；项目实现 Lease 回调并保持可重复调用安全。
