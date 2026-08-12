# Input Policy

Input Policy 解决“指针位于 UI 上时，哪些输入应被 UI 消费，哪些仍允许传给世界”的问题。

## 输入通道

AppUI 暴露通用 `AppUIInputChannel`，例如 PrimaryPointer、SecondaryPointer、PointerMotion、ViewportPan、ViewportZoom、ContextAction。项目把自己的 Input System Action 映射到这些通道；AppUI 不引用项目输入程序集。

## 判定链

```text
屏幕位置 + 输入通道
-> EventSystem Raycast
-> AppUIInputPolicyRoot 页面默认策略
-> AppUIInputZone 局部覆盖
-> 交互 Selectable 强制阻挡
-> AppUIInputHitResolver 返回是否阻挡
```

`AppUIInputPolicyRoot` 决定页面默认行为，`AppUIInputZone` 用于局部 `BlockAll`、`PassAll`、仅阻挡交互元素或按通道穿透。

## UI 输入与世界输入

业务世界系统在执行点击、拖动、相机缩放或放置命令前，调用 `AppUIInputHitResolver.Shared.IsPointerBlocked(position, channel)`。AppUI 只回答阻挡语义，不直接禁用世界系统。

装饰 Graphic 应关闭不必要的 `raycastTarget`；真正可交互的 Button、Toggle 和输入面必须由 Prefab 明确创作。不要在 Controller 中临时切换大量 raycastTarget 来修复穿透。

## 模态规则

全屏或模态页面需要一个真实可 Raycast 的阻挡面，即使视觉上透明。`BlockLowerLayerInput` 影响下层页面的输入权与 PauseDepth；局部 passthrough 只放行已声明通道，不代表所有世界操作都通过。

验证入口：`Tools > Joi.H AppUI > Validate Input Policies`。
