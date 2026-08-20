using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace Joi.H.AppUI.Validation.Consumer.Editor
{
    /// <summary>
    /// Enables and validates the optional TextMeshPro integration without
    /// introducing TMP references into the base Consumer assemblies.
    /// </summary>
    public static class AppUIConsumerTextMeshProCommand
    {
        private const string Define = "JOIH_APPUI_TMP";
        private const string SampleName = "TextMeshPro Integration";

        public static void Configure()
        {
            AppUIConsumerBatchCommand.Run(ConfigureCore);
        }

        public static void ImportSample()
        {
            AppUIConsumerBatchCommand.Run(ImportSampleCore);
        }

        public static void ValidateSample()
        {
            AppUIConsumerBatchCommand.Run(ValidateSampleCore);
        }

        public static string GetInstalledPackageVersion()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(AppUIRuntimeHost).Assembly);
            if (package == null ||
                !string.Equals(package.name, "com.joih.appui", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Installed com.joih.appui package could not be resolved.");
            }

            return package.version;
        }

        private static void ConfigureCore()
        {
            NamedBuildTarget target = NamedBuildTarget.Standalone;
            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            string[] symbols = current.Split(new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToArray();
            if (symbols.Contains(Define, StringComparer.Ordinal)) return;

            PlayerSettings.SetScriptingDefineSymbols(
                target,
                string.Join(";", symbols.Concat(new[] { Define })));
            Debug.Log("<Joi.H.AppUI> Enabled JOIH_APPUI_TMP for Standalone.");
        }

        private static void ImportSampleCore()
        {
            string version = GetInstalledPackageVersion();
            foreach (Sample sample in Sample.FindByPackage("com.joih.appui", version))
            {
                if (!string.Equals(sample.displayName, SampleName, StringComparison.Ordinal))
                    continue;

                bool imported = sample.Import(
                    Sample.ImportOptions.OverridePreviousImports |
                    Sample.ImportOptions.HideImportWindow);
                if (!imported && !sample.isImported)
                    throw new InvalidOperationException("TextMeshPro Integration sample import failed.");
                return;
            }

            throw new InvalidOperationException(
                "TextMeshPro Integration sample was not found in com.joih.appui@" +
                version + ".");
        }

        private static void ValidateSampleCore()
        {
            const string typeName =
                "Joi.H.AppUI.Samples.TextMeshPro.Editor.TextMeshProSampleValidationCommand, " +
                "Joi.H.AppUI.Samples.TextMeshPro.Editor";
            Type validationType = Type.GetType(typeName, false);
            MethodInfo validate = validationType?.GetMethod(
                "Validate", BindingFlags.Public | BindingFlags.Static);
            if (validate == null)
            {
                throw new InvalidOperationException(
                    "Imported TextMeshPro Integration validation entry was not compiled.");
            }

            validate.Invoke(null, null);
        }
    }
}
