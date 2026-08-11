using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// App UI 绑定校验 JSON 报告写出器。
    /// 该类只服务显式 CI/构建报告输出，不参与 Validate All 的只读扫描和资产修复流程。
    /// </summary>
    public static class UIBindingValidationJsonReportWriter
    {
        /// <summary>JSON 报告结构版本；CI 解析脚本可据此做兼容判断。</summary>
        public const string SchemaVersion = "app-ui-binding-validation.v2";

        /// <summary>报告所属工具名称。</summary>
        public const string ToolName = "AppUIBindingValidateAll";

        /// <summary>默认报告路径，位于 Temp 下，避免写入 Unity 资产目录。</summary>
        public const string DefaultReportPath = "Temp/AppUIBindingValidationReport.json";

        /// <summary>校验通过时使用的退出码。</summary>
        public const int ExitCodeSuccess = 0;

        /// <summary>校验本身发现 error 时使用的退出码。</summary>
        public const int ExitCodeValidationFailed = 1;

        /// <summary>命令参数、Settings 解析或异常导致命令未能正常执行时使用的退出码。</summary>
        public const int ExitCodeCommandFailed = 2;

        /// <summary>
        /// 写出机器可读 JSON 报告。
        /// 报告文件是 CI 产物，只允许写入显式路径或默认 Temp 路径，并拒绝写进 Assets 目录。
        /// </summary>
        public static bool TryWriteReport(
            string requestedReportPath,
            string settingsPath,
            DateTime startedAtUtc,
            DateTime finishedAtUtc,
            long durationMs,
            int exitCode,
            UIBindingValidationReport report,
            Exception exception,
            out string writtenPath,
            out string error)
        {
            writtenPath = string.Empty;
            error = string.Empty;

            if (!TryResolveWritableReportPath(requestedReportPath, out string fullPath, out string displayPath, out error))
            {
                return false;
            }

            ReportDto dto = CreateDto(
                displayPath,
                settingsPath,
                startedAtUtc,
                finishedAtUtc,
                durationMs,
                exitCode,
                report,
                exception);

            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(dto, true);
                File.WriteAllText(fullPath, json);
                writtenPath = displayPath;
                return true;
            }
            catch (Exception writeException)
            {
                error = "Failed to write App UI validation JSON report: " + writeException;
                return false;
            }
        }

        /// <summary>
        /// 将请求路径解析为可写文件路径。
        /// 相对路径以工程根目录为基准；Assets 下的路径会被拒绝，避免 Unity 自动生成 meta 或污染资产。
        /// </summary>
        public static bool TryResolveWritableReportPath(
            string requestedReportPath,
            out string fullPath,
            out string displayPath,
            out string error)
        {
            string path = string.IsNullOrEmpty(requestedReportPath)
                ? DefaultReportPath
                : requestedReportPath;

            string projectRoot = GetProjectRoot();
            fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(projectRoot, path));

            string assetsRoot = Path.GetFullPath(Application.dataPath);
            if (IsSameOrChildPath(fullPath, assetsRoot))
            {
                displayPath = ToUnityPath(MakeProjectRelative(fullPath, projectRoot));
                error = "App UI validation report cannot be written under Assets: " + displayPath;
                return false;
            }

            displayPath = ToUnityPath(MakeProjectRelative(fullPath, projectRoot));
            error = string.Empty;
            return true;
        }

        private static ReportDto CreateDto(
            string reportPath,
            string settingsPath,
            DateTime startedAtUtc,
            DateTime finishedAtUtc,
            long durationMs,
            int exitCode,
            UIBindingValidationReport report,
            Exception exception)
        {
            ReportDto dto = new ReportDto();
            dto.schemaVersion = SchemaVersion;
            dto.tool = ToolName;
            dto.unityVersion = Application.unityVersion;
            dto.settingsPath = settingsPath ?? string.Empty;
            dto.reportPath = reportPath ?? string.Empty;
            dto.startedAtUtc = startedAtUtc.ToString("o", CultureInfo.InvariantCulture);
            dto.finishedAtUtc = finishedAtUtc.ToString("o", CultureInfo.InvariantCulture);
            dto.durationMs = durationMs;
            dto.success = exitCode == ExitCodeSuccess;
            dto.exitCode = exitCode;

            List<MessageDto> messages = new List<MessageDto>();
            if (report != null)
            {
                dto.errorCount = report.Errors.Count;
                dto.warningCount = report.Warnings.Count;
                dto.infoCount = report.Infos.Count;
                AppendMessages(messages, "error", report.Errors);
                AppendMessages(messages, "warning", report.Warnings);
                AppendMessages(messages, "info", report.Infos);
            }

            dto.messages = messages.ToArray();
            dto.exception = exception != null ? ExceptionDto.FromException(exception) : null;
            return dto;
        }

        private static void AppendMessages(
            List<MessageDto> messages,
            string level,
            IReadOnlyList<string> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                MessageDto message = new MessageDto();
                message.level = level;
                message.message = source[i] ?? string.Empty;
                messages.Add(message);
            }
        }

        private static string GetProjectRoot()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
        }

        private static bool IsSameOrChildPath(string candidatePath, string parentPath)
        {
            string normalizedCandidate = EnsureTrailingSeparator(Path.GetFullPath(candidatePath));
            string normalizedParent = EnsureTrailingSeparator(Path.GetFullPath(parentPath));
            return normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path + Path.DirectorySeparatorChar;
        }

        private static string MakeProjectRelative(string fullPath, string projectRoot)
        {
            string normalizedFullPath = Path.GetFullPath(fullPath);
            string normalizedProjectRoot = EnsureTrailingSeparator(Path.GetFullPath(projectRoot));
            if (normalizedFullPath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFullPath.Substring(normalizedProjectRoot.Length);
            }

            return normalizedFullPath;
        }

        private static string ToUnityPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        [Serializable]
        private sealed class ReportDto
        {
            public string schemaVersion;
            public string tool;
            public string unityVersion;
            public string settingsPath;
            public string reportPath;
            public string startedAtUtc;
            public string finishedAtUtc;
            public long durationMs;
            public bool success;
            public int exitCode;
            public int errorCount;
            public int warningCount;
            public int infoCount;
            public MessageDto[] messages;
            public ExceptionDto exception;
        }

        [Serializable]
        private sealed class MessageDto
        {
            public string level;
            public string message;
        }

        [Serializable]
        private sealed class ExceptionDto
        {
            public string type;
            public string message;
            public string stackTrace;

            /// <summary>
            /// 将异常压缩成 JSON 可序列化字段，便于 CI 留档，不把异常对象本身暴露给 JsonUtility。
            /// </summary>
            public static ExceptionDto FromException(Exception exception)
            {
                ExceptionDto dto = new ExceptionDto();
                dto.type = exception.GetType().FullName;
                dto.message = exception.Message;
                dto.stackTrace = exception.StackTrace ?? string.Empty;
                return dto;
            }
        }
    }
}
