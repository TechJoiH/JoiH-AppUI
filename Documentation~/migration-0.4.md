# Migration to 0.4

0.4 把文本技术从基础程序集移到显式 Integration，并删除 Controller 对具体文本组件的
所有权。这是 1.0 前允许的破坏性边界整理。

## 迁移前

先固定旧 AppUI Tag/Commit，提交 Prefab/Definition/Settings，记录 Base 与 TMP 页面清单，
并确保旧版本 Generate、Bind、Validate 和 Player Build 可复现。不要在未建立基线时批量
改生成字段。

## Controller 文本 API

已移除基础 Controller 上为真实项目保留的 `SetText`、`SetTextStr` 等具体文本帮助方法。
直接改为页面自己的绑定 View：

```csharp
// before
SetText(titleKey);

// after: UGUI
binding.TitleText.text = localization.Get(titleKey);

// after: TMP Integration
binding.TitleText.text = localization.Get(titleKey);
```

本地化、格式化和字体选择属于项目/View，不回流到 `PanelBaseController`。

## TMP_InputField

旧行为：基础 Runtime 内建识别 TMP InputField。

新行为：显式把 `TextMeshProInputFieldPolicyResolver` 注入
`AppUIRuntimeConfiguration.FocusPolicyResolvers`。Resolver ID 必须唯一；多个 Resolver
同时匹配会拒绝注册而不是按顺序覆盖。

## TMP_Dropdown

旧行为：基础 Dropdown Policy 同时包含 UGUI 与 TMP 分支。

新行为：创建
`TextMeshProFocusDropdownControlPolicy(tmpDropdown, unchangedChildRegionId)`，并让 Dropdown
节点和原 ChildRegion 共享同一个 Policy。ChildRegionId 保持不变，避免破坏导航图和 Cancel
语义。

## Binding

基础 Provider 只生成 UGUI 字段。启用 `JOIH_APPUI_TMP`，等待可选 Editor 程序集编译，在
`UIBindingSettings.EnabledRuleProviderIds` 添加 `joih.appui.tmp`，然后重新 Generate、
Domain Reload、Bind、Validate。不要手工把 TMP 规则复制回基础 `UIBindingRuleSet`。

## Notice

基础框架不再创建或猜测文本。UGUI 项目实现自己的 `NoticeViewBase`；TMP 项目使用带已写入
`TMP_Text` 引用的 `TextMeshProNoticeView` Prefab。所有视觉默认 Disabled，启用后缺少
Prefab/Layer/View 会明确失败。

## 验证清单

1. Base asmdef 不引用 `Unity.TextMeshPro`；
2. 无 TMP 项目能安装、生成、绑定、测试和构建；
3. TMP 项目能通过 Integration 诊断；
4. InputField Cancel、Dropdown Region Cancel 和焦点恢复符合旧语义；
5. Notice 显示/清理并释放 Lease；
6. Mono 与 IL2CPP 都通过。

完整接入见 [TextMeshPro 可选集成](textmeshpro-integration.md)，Notice 语义见
[Notice System](notice-system.md)。
