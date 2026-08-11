using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 构建前绑定校验入口。
    /// 开启设置后会执行只读 Validate All，发现错误则阻断构建；若 CI 提供报告路径，会先写 JSON 报告。
    /// </summary>
    public sealed class UIBindingBuildPreprocessor : IPreprocessBuildWithReport
    {
        /// <summary>
        /// 构建预处理顺序。
        /// </summary>
        public int callbackOrder
        {
            get { return 0; }
        }

        /// <summary>
        /// 构建前执行只读校验；不会自动生成绑定、写回引用、保存 prefab 或创建 Definition。
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            UIBindingSettings settings = UIBindingSettingsUtility.FindSettings();
            if (settings == null || !settings.EnableBuildPreprocess)
            {
                return;
            }

            DateTime startedAtUtc = DateTime.UtcNow;
            Stopwatch stopwatch = Stopwatch.StartNew();
            UIBindingValidationReport validationReport =
                UIBindingValidateAllRunner.ValidateAll(settings);
            stopwatch.Stop();

            if (!validationReport.HasError)
            {
                return;
            }

            // 构建阶段只报告并失败，不做任何自动修复；报告路径存在时额外写 JSON，便于 CI 收集。
            if (UIBindingValidationCommandLine.TryGetReportPathFromCommandLineOrEnvironment(out string reportPath))
            {
                DateTime finishedAtUtc = DateTime.UtcNow;
                string settingsPath = AssetDatabase.GetAssetPath(settings);
                if (!UIBindingValidationJsonReportWriter.TryWriteReport(
                        reportPath,
                        settingsPath,
                        startedAtUtc,
                        finishedAtUtc,
                        stopwatch.ElapsedMilliseconds,
                        UIBindingValidationJsonReportWriter.ExitCodeValidationFailed,
                        validationReport,
                        null,
                        out string writtenPath,
                        out string writeError))
                {
                    Debug.LogError(writeError);
                }
                else
                {
                    Debug.Log("<AppUIBindingValidation> Build preprocess JSON report written: " + writtenPath);
                }
            }

            throw new BuildFailedException(validationReport.ToString());
        }
    }
}
