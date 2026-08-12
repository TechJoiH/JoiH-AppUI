using System;
using System.Reflection;
using System.Threading;
using Joi.H.AppUI.Editor.Binding;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Validation.Consumer.Tests
{
    public sealed class AppUIConsumerEditModeTests
    {
        private const string BindingPrefabPath =
            "Assets/AppUIConsumerGenerated/Prefabs/BindingPage.prefab";

        [Test]
        public void InstalledPackage_MatchesExpectedIdentityAndVersion()
        {
            string expectedVersion =
                Environment.GetEnvironmentVariable(
                    "APPUI_EXPECTED_PACKAGE_VERSION");
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AppUIRuntimeHost).Assembly);

            Assert.That(expectedVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo("com.joih.appui"));
            Assert.That(package.version, Is.EqualTo(expectedVersion));
        }

        [Test]
        public void AssetProvider_FailureCancellationAndLease_AreDeterministic()
        {
            GameObject asset = new GameObject("LeaseAsset");
            try
            {
                ConsumerOperationFactory factory =
                    new ConsumerOperationFactory();
                ConsumerAssetProvider provider =
                    new ConsumerAssetProvider(factory);
                provider.Register("asset", asset);

                Assert.That(provider.TryLoad<GameObject>(
                    "missing", out var missing), Is.False);
                Assert.That(missing.IsSuccess, Is.False);

                using (CancellationTokenSource cancellation =
                    new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    IUIOperation<UIAssetLoadResult<GameObject>> cancelled =
                        provider.Load<GameObject>(
                            "asset", cancellation.Token);
                    Assert.That(cancelled.Status,
                        Is.EqualTo(AppUIOperationStatus.Cancelled));
                }

                Assert.That(provider.TryLoad<GameObject>(
                    "asset", out var loaded), Is.True);
                loaded.Lease.Dispose();
                loaded.Lease.Dispose();
                Assert.That(provider.ReleaseCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void GeneratedBindingPrefab_HasSerializedReferencesAndNoErrors()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                BindingPrefabPath);
            Assert.That(prefab, Is.Not.Null,
                "Run CreateFixturesAndGenerateBindings and BindAndValidate first.");
            ConsumerBindingPageController controller =
                prefab.GetComponent<ConsumerBindingPageController>();
            Assert.That(controller, Is.Not.Null);

            SerializedObject serialized = new SerializedObject(controller);
            Assert.That(
                serialized.FindProperty("m_TitleTextTxt")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("m_ConfirmButtonBtn")
                    .objectReferenceValue,
                Is.Not.Null);

            UIBindingValidationReport report =
                UIBindingValidator.ValidateScope(controller);
            Assert.That(report.Errors, Is.Empty, report.ToString());
        }

        [Test]
        public void FocusNavigator_DefaultAndVerticalMove_UsePublicApi()
        {
            GameObject eventObject = new GameObject(
                "EventSystem", typeof(EventSystem));
            GameObject firstObject = new GameObject(
                "First", typeof(RectTransform), typeof(Button));
            GameObject secondObject = new GameObject(
                "Second", typeof(RectTransform), typeof(Button));
            EventSystem eventSystem =
                eventObject.GetComponent<EventSystem>();
            InvokeEventSystemLifecycle(eventSystem, "OnEnable");
            AppUIFocusGroupNavigator navigator =
                new AppUIFocusGroupNavigator();
            try
            {
                Button first = firstObject.GetComponent<Button>();
                Button second = secondObject.GetComponent<Button>();
                navigator.RegisterNode("list", first);
                navigator.RegisterNode("list", second);
                navigator.OpenGroup("list");

                Assert.That(navigator.FocusGroupFirst("list"), Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.SameAs(firstObject));
                Assert.That(
                    navigator.MoveWithinGroup("list", 1, false), Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.SameAs(secondObject));
            }
            finally
            {
                navigator.Dispose();
                InvokeEventSystemLifecycle(eventSystem, "OnDisable");
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
                UnityEngine.Object.DestroyImmediate(eventObject);
            }
        }

        private static void InvokeEventSystemLifecycle(
            EventSystem eventSystem,
            string methodName)
        {
            MethodInfo method = typeof(EventSystem).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(eventSystem, null);
        }
    }
}
