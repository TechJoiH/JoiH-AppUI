using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Unity Project Settings 中的 App UI 绑定设置页。
    /// </summary>
    public static class UIBindingSettingsProvider
    {
        public const string SettingsPath = "Project/App UI 绑定";

        public static void OpenSettings()
        {
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        /// <summary>
        /// 注册 Project Settings 面板，直接复用 UIBindingSettings 的默认 Inspector。
        /// </summary>
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "App UI 绑定",
                guiHandler = delegate(string searchContext)
                {
                    UIBindingSettings settings = UIBindingSettingsUtility.FindSettings();
                    if (settings == null)
                    {
                        // 设置资产不存在时只提示创建，不在设置页里自动创建资产。
                        EditorGUILayout.HelpBox(
                            "未找到 UIBindingSettings。请通过 Create/Joi.H AppUI/Binding Settings 创建。",
                            MessageType.Info);
                        return;
                    }

                    UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(settings);
                    if (editor != null)
                    {
                        editor.OnInspectorGUI();
                    }

                    EditorGUILayout.Space();
                    string[] providerIds = UIBindingRuleProviderRegistry.GetRegisteredProviderIds();
                    EditorGUILayout.LabelField(
                        "Registered binding Provider IDs",
                        providerIds.Length > 0 ? string.Join(", ", providerIds) : "<none>");
                    EditorGUILayout.LabelField(
                        "Enabled binding Provider IDs",
                        settings.EnabledRuleProviderIds.Count > 0
                            ? string.Join(", ", settings.EnabledRuleProviderIds)
                            : "<none>");

                    if (!UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out _, out string providerError))
                    {
                        EditorGUILayout.HelpBox(providerError, MessageType.Error);
                    }

                    EditorGUILayout.Space();
                    if (UIEditorAssetIdResolverRegistry.TryGetSelected(
                            settings,
                            out IUIEditorAssetIdResolver resolver,
                            out string resolverError))
                    {
                        EditorGUILayout.HelpBox(
                            "Active AssetId resolver: " + resolver.ResolverId,
                            MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            resolverError,
                            MessageType.Error);
                        string[] registeredIds =
                            UIEditorAssetIdResolverRegistry
                                .GetRegisteredResolverIds();
                        EditorGUILayout.LabelField(
                            "Registered resolver IDs",
                            registeredIds.Length > 0
                                ? string.Join(", ", registeredIds)
                                : "<none>");
                    }
                },
            };
        }
    }
}
