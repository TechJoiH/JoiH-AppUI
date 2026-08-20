# Focus System

Focus System 统一键盘、手柄和鼠标下的可导航目标，但刻意区分三种状态：

- Focus：EventSystem 当前导航目标；
- Selection：业务已经确认的选项；
- Hover：指针悬停的视觉反馈。

Focus 变化不应自动等同于业务选择或点击。

## 主要组成

- `IUIDefaultFocusProvider`：页面打开时声明默认目标；
- `AppUIFocusScope`：一个页面/交互域的焦点所有权；
- `AppUIFocusGroupNavigator`：Group 内移动和跨 Group 路由；
- `AppUIFocusChain`：显式方向边界；
- `AppUIFocusRegion`：局部进入、退出与 Cancel 规则；
- `IAppUIFocusVisibilityAdapter`：确保普通 ScrollRect 目标可见；
- `IAppUIFocusVirtualizationAdapter`：在虚拟列表中异步实现目标。
- `IAppUIFocusControlPolicyResolver`：把可选控件技术解析成显式焦点 Policy。

## 控件 Policy 解析顺序

节点显式传入的 Policy 永远优先；没有显式 Policy 时，框架调用 Runtime
Configuration 中的所有外部 Resolver。恰好一个匹配时采用它；两个及以上匹配、返回
null 或抛出异常都会拒绝本次注册且不修改旧焦点快照。没有外部匹配时才使用 UGUI
内建规则，最后回退到 FrameworkOnly。Resolver 没有优先级，也不存在首个匹配获胜。

TMP InputField 由 `TextMeshProInputFieldPolicyResolver` 提供。TMP Dropdown 必须由页面
显式构造 `TextMeshProFocusDropdownControlPolicy` 并使用稳定 ChildRegionId；它不会由
InputField Resolver 或基础 UGUI Policy 猜测。

## 页面打开与恢复

默认焦点应由页面提供者声明，不在 `OnShowEx` 中临时调用 `Select()`。页面被遮挡后恢复时，Focus Scope 根据 reopen policy 恢复历史目标或重新解析默认目标。

## 虚拟化

虚拟列表的 `EnsureRealized` 返回 `IUIOperation<AppUIFocusRealizationResult>`。新移动请求会取消旧请求，并通过版本检查阻止晚到目标抢回焦点。实现仍由项目决定，不要求某种异步库。

## Cancel

Cancel 先交给焦点区域/控件策略；未处理时再进入页面 Cancel 规则。一次 Cancel 只解析并处理一个目标页面。

## 调试

- `Tools > Joi.H AppUI > Validate Focus P0`
- `Tools > Joi.H AppUI > Open Focus Runtime Trace`

重点检查默认焦点、跨 Group 边界、滚动可见、虚拟化晚到结果、关闭按钮返回链以及 Focus/Selection 视觉是否混淆。
