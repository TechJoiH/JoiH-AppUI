using System.IO;
using UnityEditor.PackageManager;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Verifies that validation entry points in this installed package do not call
    /// known asset-writing APIs.
    /// </summary>
    public static class UIBindingValidateAllReadOnlyGuard
    {
        private static readonly string[] AuditedFiles =
        {
            "Editor/Binding/Validator/UIBindingValidateAllRunner.cs",
            "Editor/Binding/Validator/UIBindingValidator.cs",
            "Editor/Binding/Validator/UIBindingBuildPreprocessor.cs",
            "Editor/Binding/Validator/UIBindingValidationWindow.cs",
            "Editor/Binding/Validator/UIBindingValidationCommandLine.cs",
            "Editor/Binding/Validator/UIBindingOwnershipValidator.cs",
            "Editor/Binding/Validator/UIBindingVariantValidator.cs",
            "Editor/Selection/AppUIFocusP0Validator.cs",
            "Editor/Selection/AppUIFocusProjectValidator.cs",
            "Editor/Binding/Settings/UIBindingSettingsUtility.cs",
            "Editor/Binding/Generator/UIBindingFileUtility.cs",
            "Editor/Binding/Scanner/UIBindingScanner.cs",
        };

        private static readonly string[] ForbiddenTokens =
        {
            "UIBindingGenerator.Generate",
            "UIBindingPrefabBinder.Bind",
            "UIDefinitionSyncUtility.Apply",
            "UIBindingSerializedFieldWriter.TryWrite",
            "UIBindingPrefabSaveUtility.Save",
            "AssetDatabase.SaveAssets",
            "AssetDatabase.CreateAsset",
            "AssetDatabase.ImportAsset",
            "AssetDatabase.Refresh",
            "EditorUtility.SetDirty",
            "ApplyModifiedProperties",
            "File.WriteAllText",
        };

        public static void AppendAuditErrors(UIBindingValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            PackageInfo packageInfo = PackageInfo.FindForAssembly(
                typeof(UIBindingValidateAllReadOnlyGuard).Assembly);
            if (packageInfo == null ||
                string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                report.AddError(
                    "Validate All readonly audit could not resolve the AppUI " +
                    "package path.");
                return;
            }

            for (int fileIndex = 0; fileIndex < AuditedFiles.Length; fileIndex++)
            {
                string relativePath = AuditedFiles[fileIndex];
                string fullPath = Path.Combine(
                    packageInfo.resolvedPath,
                    relativePath);
                if (!File.Exists(fullPath))
                {
                    report.AddError(
                        "Validate All readonly audit cannot find package file: " +
                        relativePath);
                    continue;
                }

                string source = File.ReadAllText(fullPath);
                for (int tokenIndex = 0;
                     tokenIndex < ForbiddenTokens.Length;
                     tokenIndex++)
                {
                    string token = ForbiddenTokens[tokenIndex];
                    if (source.Contains(token))
                    {
                        report.AddError(
                            "Validate All readonly audit found forbidden write " +
                            "API in " + relativePath + ": " + token);
                    }
                }
            }
        }
    }
}
