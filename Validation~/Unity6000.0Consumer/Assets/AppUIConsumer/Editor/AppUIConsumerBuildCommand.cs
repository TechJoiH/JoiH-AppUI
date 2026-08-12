using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Joi.H.AppUI.Validation.Consumer.Editor
{
    public static class AppUIConsumerBuildCommand
    {
        [Serializable]
        private sealed class BuildGateReport
        {
            public string schemaVersion;
            public string result;
            public ulong totalSize;
            public long totalTimeMs;
            public string unityVersion;
            public string backend;
            public string outputRelativePath;
            public int totalErrors;
            public int totalWarnings;
        }

        public static void BuildMono()
        {
            AppUIConsumerBatchCommand.Run(
                () => Build(
                    ScriptingImplementation.Mono2x,
                    "WindowsMono"));
        }

        public static void BuildIl2Cpp()
        {
            AppUIConsumerBatchCommand.Run(
                () => Build(
                    ScriptingImplementation.IL2CPP,
                    "WindowsIL2CPP"));
        }

        private static void Build(
            ScriptingImplementation backend,
            string label)
        {
            BuildTargetGroup group = BuildTargetGroup.Standalone;
            ScriptingImplementation previous =
                PlayerSettings.GetScriptingBackend(
                    UnityEditor.Build.NamedBuildTarget.Standalone);
            string relativeOutput = "Builds/" + label +
                "/AppUIConsumerValidation.exe";
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string absoluteOutput = Path.GetFullPath(
                Path.Combine(projectRoot, relativeOutput));
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput));
            try
            {
                PlayerSettings.SetScriptingBackend(
                    UnityEditor.Build.NamedBuildTarget.Standalone,
                    backend);
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { AppUIConsumerFixturePaths.Scene },
                    locationPathName = absoluteOutput,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = group,
                    options = BuildOptions.Development,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                BuildGateReport gate = new BuildGateReport
                {
                    schemaVersion = "appui-consumer-build.v1",
                    result = summary.result.ToString(),
                    totalSize = summary.totalSize,
                    totalTimeMs = (long)summary.totalTime.TotalMilliseconds,
                    unityVersion = Application.unityVersion,
                    backend = backend.ToString(),
                    outputRelativePath = relativeOutput.Replace('\\', '/'),
                    totalErrors = summary.totalErrors,
                    totalWarnings = summary.totalWarnings,
                };
                AppUIConsumerFixturePaths.WriteJson(
                    "build-" + label.ToLowerInvariant() + ".json",
                    gate);
                if (summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        label + " build failed: " + summary.result +
                        ", errors=" + summary.totalErrors + ".");
                }
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(
                    UnityEditor.Build.NamedBuildTarget.Standalone,
                    previous);
            }
        }
    }
}
