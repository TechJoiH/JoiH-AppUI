using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Joi.H.AppUI.Validation.Consumer.Editor
{
    public static class AppUIConsumerSmokeCommand
    {
        [Serializable]
        private sealed class SmokeReport
        {
            public string schemaVersion;
            public string packageName;
            public string packageVersion;
            public string unityVersion;
            public bool initialized;
            public bool openPassed;
            public bool closePassed;
        }

        public static void Run()
        {
            AppUIConsumerBatchCommand.Run(RunCore);
        }

        private static void RunCore()
        {
            string expectedVersion = Environment.GetEnvironmentVariable(
                "APPUI_EXPECTED_PACKAGE_VERSION");
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(AppUIRuntimeHost).Assembly);
            if (package == null ||
                !string.Equals(package.name, "com.joih.appui",
                    StringComparison.Ordinal) ||
                !string.Equals(package.version, expectedVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Installed AppUI package identity/version mismatch.");
            }

            EditorSceneManager.OpenScene(
                AppUIConsumerFixturePaths.Scene,
                OpenSceneMode.Single);
            ConsumerRuntimeInstaller installer =
                UnityEngine.Object.FindFirstObjectByType<
                    ConsumerRuntimeInstaller>();
            if (installer == null)
            {
                throw new InvalidOperationException(
                    "ConsumerRuntimeInstaller is missing from validation scene.");
            }

            AppUIInitializationResult initialization =
                installer.InitializeForValidation();
            UIOpenResult open = Complete(
                installer.Manager.Open(
                    ConsumerRuntimeInstaller.BasicPageId));
            UICloseResult close = Complete(
                installer.Manager.Close(
                    ConsumerRuntimeInstaller.BasicPageId));
            SmokeReport report = new SmokeReport
            {
                schemaVersion = "appui-git-install-smoke.v1",
                packageName = package.name,
                packageVersion = package.version,
                unityVersion = Application.unityVersion,
                initialized = initialization.Success,
                openPassed = open != null && open.Success,
                closePassed = close != null && close.Success,
            };
            if (!report.initialized || !report.openPassed ||
                !report.closePassed)
            {
                throw new InvalidOperationException(
                    "Consumer AppUI open/close smoke failed.");
            }

            AppUIConsumerFixturePaths.WriteJson(
                "git-install-smoke.json", report);
            installer.Host.Shutdown();
        }

        private static TResult Complete<TResult>(
            IUIOperation<TResult> operation)
        {
            if (operation == null || !operation.IsTerminal ||
                !operation.TryGetCompletion(out var completion) ||
                completion.Status != AppUIOperationStatus.Succeeded)
            {
                throw new InvalidOperationException(
                    "Consumer operation did not complete successfully.",
                    operation != null &&
                    operation.TryGetCompletion(out var value)
                        ? value.Exception
                        : null);
            }

            return completion.Result;
        }
    }
}
