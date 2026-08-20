# TextMeshPro Integration Sample

这是 Joi.H AppUI 的独立 TMP 接入样例，展示如何在基础包不依赖 TMP 的前提下，由消费项目显式启用官方集成。

使用前：

1. 确保项目可以使用 TextMeshPro。
2. 在 Standalone 的 Scripting Define Symbols 中加入 `JOIH_APPUI_TMP`。
3. 通过 Package Manager 导入本 Sample。
4. 在 `UIBindingSettings.EnabledRuleProviderIds` 中显式选择 `joih.appui.tmp`。
5. 宿主通过 `AppUIRuntimeConfiguration` 注入 `TextMeshProInputFieldPolicyResolver`。

样例包含 TMP InputField、显式 ChildRegion Dropdown Policy、TMP Notice View、Binding Settings 和可直接打开的场景。它自己提供 Operation Factory、Unity Execution Context 和内存 Asset Provider，不依赖 Basic Integration Sample。
