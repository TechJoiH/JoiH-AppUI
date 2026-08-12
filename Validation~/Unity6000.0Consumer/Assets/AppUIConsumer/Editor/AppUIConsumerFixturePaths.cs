using System;
using System.IO;
using UnityEngine;

namespace Joi.H.AppUI.Validation.Consumer.Editor
{
    internal static class AppUIConsumerFixturePaths
    {
        public const string GeneratedRoot =
            "Assets/AppUIConsumerGenerated";
        public const string Prefabs = GeneratedRoot + "/Prefabs";
        public const string Definitions = GeneratedRoot + "/Definitions";
        public const string Settings = GeneratedRoot + "/Settings";
        public const string Scenes = GeneratedRoot + "/Scenes";
        public const string BasicPrefab = Prefabs + "/BasicPage.prefab";
        public const string PopupPrefab = Prefabs + "/Popup.prefab";
        public const string BindingPrefab = Prefabs + "/BindingPage.prefab";
        public const string FocusPrefab = Prefabs + "/FocusList.prefab";
        public const string Registry =
            Definitions + "/ConsumerPageRegistry.asset";
        public const string LayerSettings =
            Settings + "/ConsumerLayerSettings.asset";
        public const string RuntimeProfile =
            Settings + "/ConsumerRuntimeProfile.asset";
        public const string BindingSettings =
            Settings + "/ConsumerBindingSettings.asset";
        public const string Scene =
            Scenes + "/AppUIConsumerValidation.unity";

        public static string GetValidationOutputDirectory()
        {
            string requested = Environment.GetEnvironmentVariable(
                "APPUI_VALIDATION_OUTPUT");
            string root = string.IsNullOrWhiteSpace(requested)
                ? Path.Combine(
                    Path.GetDirectoryName(Application.dataPath),
                    "ValidationOutput")
                : Path.GetFullPath(requested);
            Directory.CreateDirectory(root);
            return root;
        }

        public static void WriteJson(string fileName, object value)
        {
            string path = Path.Combine(
                GetValidationOutputDirectory(), fileName);
            File.WriteAllText(path, JsonUtility.ToJson(value, true));
        }
    }
}
