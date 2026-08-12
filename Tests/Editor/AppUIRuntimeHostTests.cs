using System;
using System.Reflection;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIRuntimeHostTests
    {
        private HostFixture fixture;

        [TearDown]
        public void TearDown()
        {
            fixture?.Dispose();
            fixture = null;
        }

        [Test]
        public void Manager_OnlyExposesDependencySetInitialization()
        {
            MethodInfo[] initializers = typeof(AppUIManager)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == "Initialize")
                .ToArray();

            Assert.That(initializers, Has.Length.EqualTo(1));
            ParameterInfo[] parameters = initializers[0].GetParameters();
            Assert.That(
                parameters.Any(parameter =>
                    parameter.ParameterType == typeof(AppUIRuntimeDependencies)),
                Is.True);
            Assert.That(
                parameters.Any(parameter =>
                    parameter.ParameterType == typeof(IUIAssetProvider)),
                Is.False);
        }

        [Test]
        public void Initialize_MissingOperationFactory_DoesNotEnterInitializedState()
        {
            fixture = HostFixture.CreateValid();
            AppUIRuntimeDependencies dependencies =
                new AppUIRuntimeDependencies(
                    null,
                    fixture.AssetProvider,
                    fixture.ExecutionContext);

            AppUIInitializationResult result =
                fixture.Host.Initialize(dependencies);

            Assert.That(result.Status,
                Is.EqualTo(
                    AppUIInitializationStatus.MissingOperationFactory));
            Assert.That(fixture.Host.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_MissingAssetProvider_DoesNotEnterInitializedState()
        {
            fixture = HostFixture.CreateValid();
            AppUIRuntimeDependencies dependencies =
                new AppUIRuntimeDependencies(
                    fixture.OperationFactory,
                    null,
                    fixture.ExecutionContext);

            AppUIInitializationResult result =
                fixture.Host.Initialize(dependencies);

            Assert.That(result.Status,
                Is.EqualTo(AppUIInitializationStatus.MissingAssetProvider));
            Assert.That(fixture.Host.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_MissingExecutionContext_DoesNotEnterInitializedState()
        {
            fixture = HostFixture.CreateValid();
            AppUIRuntimeDependencies dependencies =
                new AppUIRuntimeDependencies(
                    fixture.OperationFactory,
                    fixture.AssetProvider,
                    null);

            AppUIInitializationResult result =
                fixture.Host.Initialize(dependencies);

            Assert.That(result.Status,
                Is.EqualTo(
                    AppUIInitializationStatus.MissingExecutionContext));
            Assert.That(fixture.Host.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_MissingManager_ReturnsStructuredFailure()
        {
            fixture = HostFixture.CreateWithoutManager();

            AppUIInitializationResult result =
                fixture.Host.Initialize(fixture.CreateDependencies());

            Assert.That(result.Status,
                Is.EqualTo(AppUIInitializationStatus.MissingManager));
            Assert.That(fixture.Host.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_MissingRegistry_ReturnsStructuredFailure()
        {
            fixture = HostFixture.CreateWithoutRegistry();

            AppUIInitializationResult result =
                fixture.Host.Initialize(fixture.CreateDependencies());

            Assert.That(result.Status,
                Is.EqualTo(AppUIInitializationStatus.MissingRegistry));
            Assert.That(fixture.Host.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_SameDependencies_IsIdempotent()
        {
            fixture = HostFixture.CreateValid();
            AppUIRuntimeDependencies dependencies =
                fixture.CreateDependencies();

            AppUIInitializationResult first =
                fixture.Host.Initialize(dependencies);
            AppUIInitializationResult second =
                fixture.Host.Initialize(dependencies);

            Assert.That(first.Status,
                Is.EqualTo(AppUIInitializationStatus.Success));
            Assert.That(second.Status,
                Is.EqualTo(AppUIInitializationStatus.AlreadyInitialized));
            Assert.That(second.Success, Is.True);
            Assert.That(fixture.Host.IsInitialized, Is.True);
        }

        [Test]
        public void Initialize_DifferentDependenciesWhileRunning_IsRejected()
        {
            fixture = HostFixture.CreateValid();
            AppUIRuntimeDependencies first = fixture.CreateDependencies();
            AppUIRuntimeDependencies second = fixture.CreateDependencies();

            Assert.That(fixture.Host.Initialize(first).Success, Is.True);
            AppUIInitializationResult repeated =
                fixture.Host.Initialize(second);

            Assert.That(repeated.Status,
                Is.EqualTo(
                    AppUIInitializationStatus
                        .AlreadyInitializedWithDifferentDependencies));
            Assert.That(repeated.Success, Is.False);
        }

        [Test]
        public void Shutdown_AllowsInitializationWithNewDependencies()
        {
            fixture = HostFixture.CreateValid();
            AppUIRuntimeDependencies first = fixture.CreateDependencies();
            AppUIRuntimeDependencies second = fixture.CreateDependencies();

            Assert.That(fixture.Host.Initialize(first).Success, Is.True);
            fixture.Host.Shutdown();
            AppUIInitializationResult result =
                fixture.Host.Initialize(second);

            Assert.That(result.Status,
                Is.EqualTo(AppUIInitializationStatus.Success));
            Assert.That(fixture.Host.IsInitialized, Is.True);
        }

        private sealed class HostFixture : IDisposable
        {
            private readonly GameObject root;
            private readonly UIPageDefinitionRegistry registry;

            private HostFixture(
                bool includeManager,
                bool includeRegistry)
            {
                root = new GameObject("AppUIRuntimeHostTests");
                root.SetActive(false);
                if (includeManager)
                {
                    Manager = root.AddComponent<AppUIManager>();
                }

                Host = root.AddComponent<AppUIRuntimeHost>();
                if (includeRegistry)
                {
                    registry = ScriptableObject.CreateInstance<
                        UIPageDefinitionRegistry>();
                    SetPrivateField(Host, "pageRegistry", registry);
                }

                SetPrivateField(Host, "uiManager", Manager);
                SetPrivateField(Host, "layerRoots", CreateLayerRoots(root));
                SetPrivateField(
                    Host,
                    "noticeSettings",
                    CreateNoticeSettingsWithoutPrewarm());

                OperationFactory = new ManualUIOperationFactory();
                AssetProvider = new StubAssetProvider();
                ExecutionContext =
                    new ImmediateAppUIExecutionContext();
            }

            public AppUIRuntimeHost Host { get; }

            public AppUIManager Manager { get; }

            public ManualUIOperationFactory OperationFactory { get; }

            public StubAssetProvider AssetProvider { get; }

            public ImmediateAppUIExecutionContext ExecutionContext { get; }

            public static HostFixture CreateValid()
            {
                return new HostFixture(true, true);
            }

            public static HostFixture CreateWithoutManager()
            {
                return new HostFixture(false, true);
            }

            public static HostFixture CreateWithoutRegistry()
            {
                return new HostFixture(true, false);
            }

            public AppUIRuntimeDependencies CreateDependencies()
            {
                return new AppUIRuntimeDependencies(
                    new ManualUIOperationFactory(),
                    new StubAssetProvider(),
                    new ImmediateAppUIExecutionContext());
            }

            public void Dispose()
            {
                if (Host != null && Host.IsInitialized)
                {
                    Host.Shutdown();
                }

                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                if (registry != null)
                {
                    UnityEngine.Object.DestroyImmediate(registry);
                }
            }

            private static void SetPrivateField(
                object target,
                string fieldName,
                object value)
            {
                FieldInfo field = target.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, fieldName);
                field.SetValue(target, value);
            }

            private static UILayerRoot[] CreateLayerRoots(GameObject owner)
            {
                Array layerIds = Enum.GetValues(typeof(UILayerId));
                UILayerRoot[] roots = new UILayerRoot[layerIds.Length];
                for (int i = 0; i < layerIds.Length; i++)
                {
                    UILayerId layerId = (UILayerId)layerIds.GetValue(i);
                    Assert.That(
                        UILayerRuntimeConfigurator.TryGetDefaultLayerSetting(
                            layerId,
                            out UICanvasDomain domain,
                            out _),
                        Is.True,
                        layerId.ToString());
                    GameObject layerObject = new GameObject(
                        layerId.ToString(),
                        typeof(RectTransform),
                        typeof(UILayerRoot));
                    layerObject.transform.SetParent(owner.transform, false);
                    UILayerRoot layerRoot =
                        layerObject.GetComponent<UILayerRoot>();
                    layerRoot.Configure(
                        layerId,
                        domain,
                        layerObject.transform as RectTransform);
                    roots[i] = layerRoot;
                }

                return roots;
            }

            private static AppUINoticeSettings
                CreateNoticeSettingsWithoutPrewarm()
            {
                AppUINoticeSettings settings =
                    AppUINoticeSettings.CreateDefault();
                settings.Toast.ConfigureDefaults(
                    1f, 0.1f, 0f, 0, 1, 16, Color.white);
                settings.Tooltip.ConfigureDefaults(
                    1f, 0.1f, 0f, 0, 1, 16, Color.white);
                settings.FloatingText.ConfigureDefaults(
                    1f, 0.1f, 0f, 0, 1, 16, Color.white);
                settings.DamageNumber.ConfigureDefaults(
                    1f, 0.1f, 0f, 0, 1, 16, Color.white);
                return settings;
            }
        }

        private sealed class StubAssetProvider : IUIAssetProvider
        {
            public bool TryLoad<T>(
                string assetId,
                out UIAssetLoadResult<T> result)
                where T : UnityEngine.Object
            {
                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.SynchronousLoadUnsupported,
                    "Synchronous loading is not supported by this test provider.");
                return false;
            }

            public IUIOperation<UIAssetLoadResult<T>> Load<T>(
                string assetId,
                CancellationToken cancellationToken)
                where T : UnityEngine.Object
            {
                IUIOperationSource<UIAssetLoadResult<T>> source =
                    new ManualUIOperationFactory()
                        .Create<UIAssetLoadResult<T>>(
                            AppUIOperationDescriptor.Create(
                                "HostTestLoad",
                                cancellationToken));
                source.TrySetRunning();
                source.TrySetSucceeded(
                    UIAssetLoadResult<T>.Failure(
                        UIAssetLoadStatus.NotFound,
                        assetId));
                return source.Operation;
            }
        }
    }
}
