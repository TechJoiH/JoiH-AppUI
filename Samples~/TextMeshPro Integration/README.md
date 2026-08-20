# TextMeshPro Integration Sample

这是 Joi.H AppUI 的独立 TMP 接入样例，展示如何在基础包不依赖 TMP 的前提下，由消费项目显式启用官方集成。

使用前：

1. 确保项目可以使用 TextMeshPro。
2. 在 Standalone 的 Scripting Define Symbols 中加入 `JOIH_APPUI_TMP`。
3. 通过 Package Manager 导入本 Sample。
4. 在 `UIBindingSettings.EnabledRuleProviderIds` 中显式选择 `joih.appui.tmp`。
5. 宿主通过 `AppUIRuntimeConfiguration` 注入 `TextMeshProInputFieldPolicyResolver`。

样例包含 TMP InputField、显式 ChildRegion Dropdown Policy、TMP Notice View、Binding Settings 和可直接打开的场景。它自己提供 Operation Factory、Unity Execution Context 和内存 Asset Provider，不依赖 Basic Integration Sample。

打开 `Scenes/TextMeshProIntegration.unity` 即可验证 Open、Refresh、Close。项目设置中的
TextMeshPro Integration 诊断会检查 Define、程序集、`joih.appui.tmp` Provider、冻结
Binding 快照、Notice Prefab 和运行时 Resolver；EditMode 下 Runtime Host 可能显示
`NotVerifiable`，进入 Play Mode 并初始化本场景后可获得真实 Host 结果。

停用时先替换组件和生成字段，移除 Provider/Resolver/Dropdown/Notice 引用，再删除
`JOIH_APPUI_TMP`。详细教程见 `Documentation~/textmeshpro-integration.md`。
