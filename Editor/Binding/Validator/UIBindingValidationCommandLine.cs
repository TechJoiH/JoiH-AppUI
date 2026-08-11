using System;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// App UI 绑定校验的 Unity 命令行入口。
    /// CI 通过 -executeMethod 调用本类，只运行只读 Validate All、写 JSON 报告并返回稳定退出码。
    /// </summary>
    public static class UIBindingValidationCommandLine
    {
        /// <summary>指定 UIBindingSettings 资产路径的命令行参数名。</summary>
        public const string SettingsPathArgument = "-appUIBindingSettingsPath";

        /// <summary>指定 JSON 报告输出路径的命令行参数名。</summary>
        public const string ReportPathArgument = "-appUIValidationReportPath";

        /// <summary>构建或 CI 可用的报告路径环境变量名。</summary>
        public const string ReportPathEnvironmentVariable = "APP_UI_VALIDATION_REPORT_PATH";

        /// <summary>
        /// Unity -executeMethod 入口。
        /// 流程：解析参数 -> 解析 Settings -> 执行只读 Validate All -> 写 JSON -> 用退出码结束 Editor。
        /// </summary>
        public static void ValidateAll()
        {
            DateTime startedAtUtc = DateTime.UtcNow;
            Stopwatch stopwatch = Stopwatch.StartNew();
            UIBindingValidationReport report = new UIBindingValidationReport();
            Exception commandException = null;
            int exitCode = UIBindingValidationJsonReportWriter.ExitCodeCommandFailed;
            string settingsPath = string.Empty;
            string reportPath = UIBindingValidationJsonReportWriter.DefaultReportPath;

            try
            {
                string[] args = Environment.GetCommandLineArgs();
                bool reportArgumentValid = TryReadOptionalArgument(args, ReportPathArgument, out reportPath, out string reportArgumentError);
                bool settingsArgumentValid = TryReadOptionalArgument(args, SettingsPathArgument, out string requestedSettingsPath, out string settingsArgumentError);

                if (reportArgumentValid && string.IsNullOrEmpty(reportPath))
                {
                    reportPath = Environment.GetEnvironmentVariable(ReportPathEnvironmentVariable);
                }

                if (!reportArgumentValid)
                {
                    report.AddError(reportArgumentError);
                }

                if (!settingsArgumentValid)
                {
                    report.AddError(settingsArgumentError);
                }

                if (report.HasError)
                {
                    exitCode = UIBindingValidationJsonReportWriter.ExitCodeCommandFailed;
                }
                else if (!UIBindingSettingsUtility.TryResolveSettingsForCommandLine(
                             requestedSettingsPath,
                             out UIBindingSettings settings,
                             out settingsPath,
                             out string settingsError))
                {
                    report.AddError(settingsError);
                }
                else
                {
                    // 这里复用唯一只读核心，命令行入口不接触生成、写回或 Definition 同步逻辑。
                    report = UIBindingValidateAllRunner.ValidateAll(settings);
                    exitCode = report.HasError
                        ? UIBindingValidationJsonReportWriter.ExitCodeValidationFailed
                        : UIBindingValidationJsonReportWriter.ExitCodeSuccess;
                }
            }
            catch (Exception exception)
            {
                commandException = exception;
                report.AddError("App UI binding validation command failed: " + exception.Message);
            }

            if (exitCode == UIBindingValidationJsonReportWriter.ExitCodeCommandFailed && !report.HasError)
            {
                report.AddError("App UI binding validation command failed before validation could run.");
            }

            stopwatch.Stop();
            DateTime finishedAtUtc = DateTime.UtcNow;

            // 报告写出失败也属于命令执行失败；此时 Console 仍会留下错误，方便 CI 排查路径配置。
            if (!UIBindingValidationJsonReportWriter.TryWriteReport(
                    reportPath,
                    settingsPath,
                    startedAtUtc,
                    finishedAtUtc,
                    stopwatch.ElapsedMilliseconds,
                    exitCode,
                    report,
                    commandException,
                    out string writtenPath,
                    out string writeError))
            {
                exitCode = UIBindingValidationJsonReportWriter.ExitCodeCommandFailed;
                Debug.LogError(writeError);
            }
            else
            {
                Debug.Log("<AppUIBindingValidation> JSON report written: " + writtenPath);
            }

            string textReport = report.ToString();
            if (!string.IsNullOrEmpty(textReport))
            {
                if (exitCode == UIBindingValidationJsonReportWriter.ExitCodeSuccess)
                {
                    Debug.Log(textReport);
                }
                else
                {
                    Debug.LogError(textReport);
                }
            }

            // CI 需要稳定退出码；非 batchmode 下也保持一致，避免手动 -executeMethod 时误判成功。
            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// 从当前进程命令行或环境变量读取报告路径。
        /// BuildPreprocess 使用该方法，在失败前写出和命令行一致的 JSON 报告。
        /// </summary>
        public static bool TryGetReportPathFromCommandLineOrEnvironment(out string reportPath)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (TryReadOptionalArgument(args, ReportPathArgument, out reportPath, out string _) &&
                !string.IsNullOrEmpty(reportPath))
            {
                return true;
            }

            reportPath = Environment.GetEnvironmentVariable(ReportPathEnvironmentVariable);
            return !string.IsNullOrEmpty(reportPath);
        }

        private static bool TryReadOptionalArgument(
            string[] args,
            string argumentName,
            out string value,
            out string error)
        {
            value = string.Empty;
            error = string.Empty;

            if (args == null)
            {
                return true;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Length || string.IsNullOrEmpty(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    error = "Missing value for command line argument: " + argumentName;
                    return false;
                }

                value = args[i + 1];
                return true;
            }

            return true;
        }
    }
}
