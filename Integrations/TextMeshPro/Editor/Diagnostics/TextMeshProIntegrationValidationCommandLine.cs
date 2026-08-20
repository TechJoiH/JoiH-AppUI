using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Integrations.TextMeshPro.Editor
{
    internal static class TextMeshProIntegrationValidationCommandLine
    {
        public static void Validate()
        {
            int exitCode = 0;
            try
            {
                IReadOnlyList<TextMeshProIntegrationDiagnostic> diagnostics =
                    TextMeshProIntegrationDiagnostics.Collect();
                DiagnosticReport report = DiagnosticReport.Create(diagnostics);
                string root = Environment.GetEnvironmentVariable("APPUI_VALIDATION_OUTPUT");
                if (string.IsNullOrWhiteSpace(root))
                {
                    root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "AppUIValidation");
                }

                Directory.CreateDirectory(root);
                File.WriteAllText(
                    Path.Combine(root, "textmeshpro-integration.json"),
                    JsonUtility.ToJson(report, true));
                exitCode = report.success ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        [Serializable]
        private sealed class DiagnosticReport
        {
            public string schemaVersion;
            public bool success;
            public DiagnosticEntry[] diagnostics;

            public static DiagnosticReport Create(IReadOnlyList<TextMeshProIntegrationDiagnostic> source)
            {
                DiagnosticReport result = new DiagnosticReport
                {
                    schemaVersion = "appui-textmeshpro-integration.v1",
                    success = true,
                    diagnostics = new DiagnosticEntry[source.Count],
                };
                for (int i = 0; i < source.Count; i++)
                {
                    result.diagnostics[i] = DiagnosticEntry.Create(source[i]);
                    if (source[i].State == TextMeshProIntegrationDiagnosticState.Failure)
                        result.success = false;
                }

                return result;
            }
        }

        [Serializable]
        private sealed class DiagnosticEntry
        {
            public string code;
            public string state;
            public string fact;
            public string impact;
            public string fix;
            public string context;

            public static DiagnosticEntry Create(TextMeshProIntegrationDiagnostic source)
            {
                return new DiagnosticEntry
                {
                    code = source.Code,
                    state = source.State.ToString(),
                    fact = source.Fact,
                    impact = source.Impact,
                    fix = source.Fix,
                    context = source.Context != null ? AssetDatabase.GetAssetPath(source.Context) : string.Empty,
                };
            }
        }
    }
}
