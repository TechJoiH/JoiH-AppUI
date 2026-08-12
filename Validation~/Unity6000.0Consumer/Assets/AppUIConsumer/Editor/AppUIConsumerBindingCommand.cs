using System;
using System.Diagnostics;
using Joi.H.AppUI.Editor.Binding;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Validation.Consumer.Editor
{
    public static class AppUIConsumerBindingCommand
    {
        public static void BindAndValidate()
        {
            AppUIConsumerBatchCommand.Run(BindAndValidateCore);
        }

        private static void BindAndValidateCore()
        {
            DateTime started = DateTime.UtcNow;
            Stopwatch stopwatch = Stopwatch.StartNew();
            string[] prefabPaths =
            {
                AppUIConsumerFixturePaths.BasicPrefab,
                AppUIConsumerFixturePaths.PopupPrefab,
                AppUIConsumerFixturePaths.BindingPrefab,
                AppUIConsumerFixturePaths.FocusPrefab,
            };

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPaths[i]);
                UIBindingScopeBase scope = prefab != null
                    ? prefab.GetComponent<UIBindingScopeBase>()
                    : null;
                UIBindingBindResult bind = UIBindingPrefabBinder.Bind(scope);
                if (!bind.Success)
                {
                    throw new InvalidOperationException(
                        "Binding writeback failed for " + prefabPaths[i] +
                        ": " + string.Join(" | ", bind.Errors));
                }

                UIBindingValidationReport scopeReport =
                    UIBindingValidator.ValidateScope(scope);
                if (scopeReport.HasError)
                {
                    throw new InvalidOperationException(
                        "Binding scope validation failed for " +
                        prefabPaths[i] + ": " + scopeReport);
                }
            }

            UIBindingSettings settings =
                AssetDatabase.LoadAssetAtPath<UIBindingSettings>(
                    AppUIConsumerFixturePaths.BindingSettings);
            UIBindingValidationReport report =
                UIBindingValidateAllRunner.ValidateAll(settings);
            stopwatch.Stop();
            string output = System.IO.Path.Combine(
                AppUIConsumerFixturePaths.GetValidationOutputDirectory(),
                "binding-validation.json");
            int exitCode = report.HasError
                ? UIBindingValidationJsonReportWriter
                    .ExitCodeValidationFailed
                : UIBindingValidationJsonReportWriter.ExitCodeSuccess;
            if (!UIBindingValidationJsonReportWriter.TryWriteReport(
                    output,
                    AppUIConsumerFixturePaths.BindingSettings,
                    started,
                    DateTime.UtcNow,
                    stopwatch.ElapsedMilliseconds,
                    exitCode,
                    report,
                    null,
                    out _,
                    out string writeError))
            {
                throw new InvalidOperationException(writeError);
            }

            if (report.HasError)
            {
                throw new InvalidOperationException(
                    "AppUI Validate All failed: " + report);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
