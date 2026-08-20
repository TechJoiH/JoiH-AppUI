# Binding System

Binding 将 Prefab 层级中的运行时引用变成可生成、可审查、可验证的契约。

## 命名

只有 Controller 运行时真正需要访问的节点使用 `B_` 前缀，例如 `B_TitleText`、`B_CloseButton`。纯装饰和静态布局节点不要加前缀。

页面根由 `PanelBaseController` 派生类拥有；动态列表项由 `UIGroupBase` 派生类拥有。嵌套 Group 的绑定不会泄漏进父页面。

## 两阶段流程

1. Scanner 读取 Prefab 与 Controller；
2. Generate Bindings 生成 partial 字段；
3. 等待 Unity 编译与 Domain Reload；
4. Bind References 将 Prefab 对象写入序列化字段；
5. Validate 检查缺失引用、类型、所有权、Variant 和生成漂移。

生成代码后立即绑定、但不等待编译，会因为新字段尚未进入类型系统而失败。这个边界是刻意保留的。

## Rule Provider 与冻结快照

基础 Binding Provider 只提供 UGUI 规则。可选技术通过
`IUIBindingRuleProvider` 注册稳定 `ProviderId`，但只有出现在
`UIBindingSettings.EnabledRuleProviderIds` 中才参与当前操作。每次 Generate、Bind、
Validate 或 Definition Sync 开始时只构建一次不可变 `UIBindingRuleSnapshot`，后续阶段
共享同一快照。

缺失 Provider、重复 ProviderId、重复 RuleId，或多个启用 Provider 争用同一 Component
Type 都会在写文件/Prefab 前失败。框架没有优先级覆盖或 first/last-wins 行为。TMP 的
Provider ID 是 `joih.appui.tmp`，基础项目不应选择它。

## 资产 ID

运行时 AssetId 由项目 Provider 定义。项目在 Editor 程序集中实现
`IUIEditorAssetIdResolver`，为它提供稳定 `ResolverId` 并显式注册，然后在
`UIBindingSettings.SelectedAssetIdResolverId` 选择。Definition 创建、同步、
Validate All 与 Focus Prefab 验证都使用同一选择。没有隐式 Resources Resolver，
也不会在失败后按路径或文件名猜测。

可在 `Project Settings > App UI 绑定` 查看当前选择、已注册 ID 和集中错误。

## 常见失败

- Prefab 根没有主要 Controller；
- 多个 Controller 争用同一页面作用域；
- `B_` 节点改名但未重新生成；
- 字段已生成但未执行 Bind References；
- Prefab Variant 覆盖了必要引用；
- 动态 Group 项被错误绑定到父 Controller。

入口见 `Tools > Joi.H AppUI > Binding Validation`。旧的[Binding 工作流](binding-workflow.md)保留为简明清单。
