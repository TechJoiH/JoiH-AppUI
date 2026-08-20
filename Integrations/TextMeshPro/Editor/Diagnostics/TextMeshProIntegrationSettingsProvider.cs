using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Integrations.TextMeshPro.Editor
{
    internal static class TextMeshProIntegrationSettingsProvider
    {
        private const string SettingsPath = "Project/Joi.H AppUI/Integrations/TextMeshPro";
        private static IReadOnlyList<TextMeshProIntegrationDiagnostic> diagnostics;

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "TextMeshPro",
                activateHandler = (_, __) => Refresh(),
                guiHandler = _ => Draw(),
            };
        }

        private static void Refresh()
        {
            diagnostics = TextMeshProIntegrationDiagnostics.Collect();
        }

        private static void Draw()
        {
            if (diagnostics == null) Refresh();
            EditorGUILayout.HelpBox(
                "This page is read-only. It reports current facts and never changes Defines, Providers, Resolvers, Prefabs, or Host configuration.",
                MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(100f))) Refresh();
            if (GUILayout.Button("Open documentation", GUILayout.Width(160f)))
            {
                Application.OpenURL("https://github.com/TechJoiH/JoiH-AppUI/blob/main/Documentation~/textmeshpro-integration.md");
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < diagnostics.Count; i++)
            {
                TextMeshProIntegrationDiagnostic diagnostic = diagnostics[i];
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    diagnostic.State + "  " + diagnostic.Code,
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Fact", diagnostic.Fact, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(diagnostic.Impact))
                    EditorGUILayout.LabelField("Impact", diagnostic.Impact, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(diagnostic.Fix))
                    EditorGUILayout.LabelField("Fix", diagnostic.Fix, EditorStyles.wordWrappedLabel);
                if (diagnostic.Context != null)
                    EditorGUILayout.ObjectField("Context", diagnostic.Context, typeof(Object), false);
            }
        }
    }
}
