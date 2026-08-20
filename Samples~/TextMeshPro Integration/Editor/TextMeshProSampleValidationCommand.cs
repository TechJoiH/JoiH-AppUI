using System;
using System.IO;
using Joi.H.AppUI;
using Joi.H.AppUI.Editor.Binding;
using Joi.H.AppUI.Integrations.TextMeshPro;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Samples.TextMeshPro.Editor
{
    public static class TextMeshProSampleValidationCommand
    {
        public const string SampleRoot = "Assets/Samples/Joi.H AppUI/0.4.0-pre.1/TextMeshPro Integration";
        public const string ScenePath = SampleRoot + "/Scenes/TextMeshProIntegration.unity";

        [Serializable]
        private sealed class ValidationReport
        {
            public string schemaVersion;
            public bool success;
            public int errorCount;
            public string unityVersion;
        }

        public static void Validate()
        {
            UIBindingSettings settings = AssetDatabase.LoadAssetAtPath<UIBindingSettings>(
                SampleRoot + "/Settings/TextMeshProBindingSettings.asset");
            string error = string.Empty;
            if (settings == null ||
                !UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out _, out error))
                throw new InvalidOperationException(error ?? "TMP sample Binding settings are missing.");
            GameObject page = AssetDatabase.LoadAssetAtPath<GameObject>(
                SampleRoot + "/Prefabs/TextMeshProPage.prefab");
            TextMeshProSamplePageController controller =
                page != null ? page.GetComponent<TextMeshProSamplePageController>() : null;
            if (controller == null) throw new InvalidOperationException("TMP sample page prefab is missing.");
            UIBindingValidationReport report = UIBindingValidator.ValidateScope(controller, BuildSnapshot(settings));
            if (report.HasError) throw new InvalidOperationException(report.ToString());
            GameObject notice = AssetDatabase.LoadAssetAtPath<GameObject>(
                SampleRoot + "/Prefabs/TextMeshProNotice.prefab");
            if (notice == null || notice.GetComponent<TextMeshProNoticeView>() == null)
                throw new InvalidOperationException("TMP sample Notice prefab is invalid.");
            string output = Environment.GetEnvironmentVariable("APPUI_VALIDATION_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(
                    Path.Combine(output, "binding-validation.json"),
                    JsonUtility.ToJson(new ValidationReport
                    {
                        schemaVersion = "appui-binding-validation.v1",
                        success = true,
                        errorCount = 0,
                        unityVersion = Application.unityVersion,
                    }, true));
            }
            Debug.Log("<Joi.H.AppUI> TextMeshPro Integration sample validation passed.");
        }

        private static UIBindingRuleSnapshot BuildSnapshot(UIBindingSettings settings)
        {
            if (!UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out UIBindingRuleSnapshot snapshot, out string error))
                throw new InvalidOperationException(error);
            return snapshot;
        }
    }
}
