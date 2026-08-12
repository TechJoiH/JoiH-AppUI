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

## 资产 ID

运行时 AssetId 由项目 Provider 定义。若不是普通 Unity AssetDatabase 路径，项目可在 Editor 程序集中实现 `IUIEditorAssetIdResolver` 并注册到 `UIEditorAssetIdResolverRegistry`，使创建 Definition 与验证工具使用相同规则。

## 常见失败

- Prefab 根没有主要 Controller；
- 多个 Controller 争用同一页面作用域；
- `B_` 节点改名但未重新生成；
- 字段已生成但未执行 Bind References；
- Prefab Variant 覆盖了必要引用；
- 动态 Group 项被错误绑定到父 Controller。

入口见 `Tools > Joi.H AppUI > Binding Validation`。旧的[Binding 工作流](binding-workflow.md)保留为简明清单。
