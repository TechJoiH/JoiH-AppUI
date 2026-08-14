using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIRuntimeBoundaryTests
    {
        private const string PageId = "integration-page";
        private const string PageAssetId = "ui/integration-page";
        private const string NoticeAssetId = "ui/provider-toast";

        [UnityTest]
        public IEnumerator AssetLease_RemainsIdempotentAcrossFrames()
        {
            int releaseCount = 0;
            UIAssetLease lease = new UIAssetLease(() => releaseCount++);

            yield return null;
            lease.Dispose();
            yield return null;
            lease.Dispose();

            Assert.That(releaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PageLifecycle_OpenRefreshHideReopenAndRelease_IsComplete()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(false, true);
            IUIOperation<UIOpenResult> openOperation = fixture.Manager.Open(
                PageId,
                UIOpenArgs.FromExplicit("initial-data")
                    .WithSceneScopeId("scene-a"));

            yield return WaitFor(openOperation);
            AssertSucceeded(openOperation, out UIOpenResult openResult);
            Assert.That(openResult.Success, Is.True);
            Assert.That(fixture.Manager.IsOpen(PageId), Is.True);
            Assert.That(fixture.Provider.AsyncLoadCount, Is.EqualTo(1));
            Assert.That(TestPanelController.CreateCount, Is.EqualTo(1));
            Assert.That(TestPanelController.InitCount, Is.EqualTo(1));
            Assert.That(TestPanelController.RefreshCount, Is.EqualTo(1));
            Assert.That(TestPanelController.ShowCount, Is.EqualTo(1));
            Assert.That(TestPanelController.LastData, Is.EqualTo("initial-data"));
            Assert.That(TestPanelController.LastInstance.gameObject.activeSelf, Is.True);

            IUIOperation<UIRefreshResult> refreshOperation =
                fixture.Manager.Refresh(PageId, "refreshed-data");
            yield return WaitFor(refreshOperation);
            AssertSucceeded(
                refreshOperation,
                out UIRefreshResult refreshResult);

            Assert.That(refreshResult.Success, Is.True);
            Assert.That(refreshResult.State, Is.EqualTo(UIPageState.Open));
            Assert.That(TestPanelController.RefreshCount, Is.EqualTo(2));
            Assert.That(TestPanelController.LastData, Is.EqualTo("refreshed-data"));

            UICloseRequest hideRequest = UICloseRequest.Default;
            hideRequest.ReleaseOnClose = false;
            IUIOperation<UICloseResult> hideOperation =
                fixture.Manager.Close(PageId, hideRequest);
            yield return WaitFor(hideOperation);
            AssertSucceeded(hideOperation, out UICloseResult hideResult);

            Assert.That(hideResult.Success, Is.True);
            Assert.That(hideResult.State, Is.EqualTo(UIPageState.Hidden));
            Assert.That(TestPanelController.HideCount, Is.EqualTo(1));
            Assert.That(TestPanelController.LastInstance.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Provider.ReleaseCount, Is.Zero);

            IUIOperation<UIOpenResult> reopenOperation = fixture.Manager.Open(
                PageId,
                UIOpenArgs.FromExplicit("reopen-data")
                    .WithSceneScopeId("scene-a"));
            yield return WaitFor(reopenOperation);
            AssertSucceeded(reopenOperation, out UIOpenResult reopenResult);

            Assert.That(reopenResult.Success, Is.True);
            Assert.That(TestPanelController.CreateCount, Is.EqualTo(1));
            Assert.That(TestPanelController.InitCount, Is.EqualTo(1));
            Assert.That(TestPanelController.RefreshCount, Is.EqualTo(3));
            Assert.That(TestPanelController.ShowCount, Is.EqualTo(2));
            Assert.That(TestPanelController.LastData, Is.EqualTo("reopen-data"));
            Assert.That(fixture.Provider.AsyncLoadCount, Is.EqualTo(1));

            IUIOperation<UICloseResult> releaseOperation =
                fixture.Manager.Close(PageId);
            yield return WaitFor(releaseOperation);
            AssertSucceeded(releaseOperation, out UICloseResult releaseResult);

            Assert.That(releaseResult.Success, Is.True);
            Assert.That(releaseResult.State, Is.EqualTo(UIPageState.Released));
            Assert.That(fixture.Manager.IsOpen(PageId), Is.False);
            Assert.That(TestPanelController.HideCount, Is.EqualTo(2));
            Assert.That(TestPanelController.DisposeCount, Is.EqualTo(1));
            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));

            yield return null;
            Assert.That(TestPanelController.LastInstance == null, Is.True);
            yield return fixture.Dispose();
        }

        private static IEnumerator WaitFor<TResult>(
            IUIOperation<TResult> operation)
        {
            Assert.That(operation, Is.Not.Null);
            while (!operation.IsTerminal)
            {
                yield return null;
            }
        }

        private static void AssertSucceeded<TResult>(
            IUIOperation<TResult> operation,
            out TResult result)
        {
            Assert.That(
                operation.TryGetCompletion(
                    out AppUIOperationCompletion<TResult> completion),
                Is.True);
            Assert.That(completion.Status,
                Is.EqualTo(AppUIOperationStatus.Succeeded));
            Assert.That(completion.Exception, Is.Null);
            result = completion.Result;
        }

        [UnityTest]
        public IEnumerator NoticeFallback_CreatesViewAndClearsWithSceneScope()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(false, false);
            ToastNoticeRequest request = ToastNoticeRequest.Create(
                "fallback-toast");
            request.Scope = UINoticeScope.Scene("scene-notice");
            request.Duration = 10f;
            ToastHandle handle = fixture.Manager.Notices.Toast(request);

            Assert.That(handle.IsValid, Is.True);
            Assert.That(
                CountActiveToastViews(fixture.NoticeRoot),
                Is.EqualTo(1));
            Assert.That(fixture.Provider.SyncLoadCount, Is.Zero);

            IUIOperation<UIScopeReleaseResult> releaseOperation =
                fixture.Manager.ReleaseScope(
                    UIPageScope.SceneScope,
                    "scene-notice");
            yield return WaitFor(releaseOperation);
            AssertSucceeded(
                releaseOperation,
                out UIScopeReleaseResult result);

            Assert.That(result.Success, Is.True);
            Assert.That(
                CountActiveToastViews(fixture.NoticeRoot),
                Is.Zero);
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator InterruptedOpen_LateLoadCannotCommitAndReleasesLease()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(true, true);
            IUIOperation<UIOpenResult> openOperation = fixture.Manager.Open(
                PageId,
                UIOpenArgs.FromExplicit("late-data")
                    .WithSceneScopeId("scene-interrupted"));

            Assert.That(fixture.Provider.AsyncLoadCount, Is.EqualTo(1));
            Object.Destroy(fixture.Root);
            yield return null;
            Assert.That(
                openOperation.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelled));

            fixture.Provider.CompletePendingLoad();

            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
            Assert.That(TestPanelController.CreateCount, Is.Zero);
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator ReleaseScope_PendingOpenLateLoadCannotCommitAndReleasesLease()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(true, true);
            IUIOperation<UIOpenResult> openOperation = fixture.Manager.Open(
                PageId,
                UIOpenArgs.None.WithSceneScopeId("scene-released"));

            Assert.That(openOperation.IsTerminal, Is.False);
            IUIOperation<UIScopeReleaseResult> releaseOperation =
                fixture.Manager.ReleaseScope(
                    UIPageScope.SceneScope,
                    "scene-released");
            yield return WaitFor(releaseOperation);
            AssertSucceeded(releaseOperation, out UIScopeReleaseResult result);

            Assert.That(result.Success, Is.True);
            Assert.That(
                openOperation.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelled));

            fixture.Provider.CompletePendingLoad();

            Assert.That(fixture.Manager.IsOpen(PageId), Is.False);
            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
            Assert.That(TestPanelController.CreateCount, Is.Zero);
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator UnbindThenRebindSameScope_OldLateLoadCannotCommit()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(true, true);
            SceneUIBindingData binding = new SceneUIBindingData
            {
                SceneId = "SharedScene",
                SceneScopeId = "shared-scope",
                OpenOnSceneReady = new List<SceneUIOpenRule>
                {
                    new SceneUIOpenRule
                    {
                        PageId = PageId,
                        OpenArgs = UIOpenArgs.FromExplicit("scene-generation"),
                    },
                },
            };

            IUIOperation<UISceneBindResult> firstBind =
                fixture.Manager.BindScene(binding);
            Assert.That(firstBind.IsTerminal, Is.False);
            Assert.That(fixture.Provider.AsyncLoadCount, Is.EqualTo(1));

            IUIOperation<UISceneExitResult> unbind =
                fixture.Manager.UnbindScene(binding);
            yield return WaitFor(unbind);
            Assert.That(
                firstBind.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelled));

            IUIOperation<UISceneBindResult> secondBind =
                fixture.Manager.BindScene(binding);
            Assert.That(secondBind.IsTerminal, Is.False);
            Assert.That(fixture.Provider.AsyncLoadCount, Is.EqualTo(2));

            fixture.Provider.CompleteNextPendingLoad();
            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
            Assert.That(fixture.Manager.IsOpen(PageId), Is.False);
            Assert.That(secondBind.IsTerminal, Is.False);

            fixture.Provider.CompleteNextPendingLoad();
            yield return WaitFor(secondBind);
            AssertSucceeded(secondBind, out UISceneBindResult secondResult);

            Assert.That(secondResult.Success, Is.True);
            Assert.That(fixture.Manager.IsOpen(PageId), Is.True);
            Assert.That(TestPanelController.CreateCount, Is.EqualTo(1));
            Assert.That(TestPanelController.LastData,
                Is.EqualTo("scene-generation"));
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator ReleaseScopeThenRebind_OldGenerationCannotCommit()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(true, true);
            SceneUIBindingData binding = new SceneUIBindingData
            {
                SceneId = "ReleasedScene",
                SceneScopeId = "released-generation",
                OpenOnSceneReady = new List<SceneUIOpenRule>
                {
                    new SceneUIOpenRule { PageId = PageId },
                },
            };

            IUIOperation<UISceneBindResult> firstBind =
                fixture.Manager.BindScene(binding);
            UISceneScopeGenerationRegistry generations =
                GetPrivateField<UISceneScopeGenerationRegistry>(
                    fixture.Manager,
                    typeof(AppUIManager),
                    "sceneScopeGenerations");
            UISceneScopeStamp retired =
                generations.GetCurrent(binding.SceneScopeId);
            IUIOperation<UIScopeReleaseResult> release =
                fixture.Manager.ReleaseScope(
                    UIPageScope.SceneScope,
                    binding.SceneScopeId);
            yield return WaitFor(release);
            Assert.That(
                firstBind.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelled));

            IUIOperation<UISceneBindResult> secondBind =
                fixture.Manager.BindScene(binding);
            UISceneScopeStamp rebound =
                generations.GetCurrent(binding.SceneScopeId);
            Assert.That(rebound, Is.Not.EqualTo(retired));
            Assert.That(secondBind.IsTerminal, Is.False);
            Assert.That(fixture.Provider.AsyncLoadCount, Is.EqualTo(2));

            fixture.Provider.CompleteNextPendingLoad();
            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
            Assert.That(secondBind.IsTerminal, Is.False);

            fixture.Provider.CompleteNextPendingLoad();
            yield return WaitFor(secondBind);
            AssertSucceeded(secondBind, out UISceneBindResult result);

            Assert.That(result.Success, Is.True);
            Assert.That(fixture.Manager.IsOpen(PageId), Is.True);
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator CreateFailure_UnclaimedLeaseReturnsToAppUI()
        {
            ClaimThenThrowInstanceStrategy strategy =
                new ClaimThenThrowInstanceStrategy();
            RuntimeFixture fixture = RuntimeFixture.Create(
                false,
                true,
                instanceStrategy: strategy);
            LogAssert.Expect(
                LogType.Error,
                new Regex("intentional instance creation failure"));

            IUIOperation<UIOpenResult> operation =
                fixture.Manager.Open(PageId);
            yield return WaitFor(operation);

            Assert.That(operation.Status,
                Is.EqualTo(AppUIOperationStatus.Failed));
            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
            Assert.That(fixture.Manager.IsOpen(PageId), Is.False);
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator PoolingStrategy_ReturnKeepsLeaseUntilPoolEviction()
        {
            RetainingPoolInstanceStrategy strategy =
                new RetainingPoolInstanceStrategy();
            RuntimeFixture fixture = RuntimeFixture.Create(
                false,
                true,
                instanceStrategy: strategy);

            IUIOperation<UIOpenResult> open =
                fixture.Manager.Open(PageId);
            yield return WaitFor(open);
            AssertSucceeded(open, out UIOpenResult openResult);
            Assert.That(openResult.Success, Is.True);

            IUIOperation<UICloseResult> close =
                fixture.Manager.Close(PageId);
            yield return WaitFor(close);
            AssertSucceeded(close, out UICloseResult closeResult);

            Assert.That(closeResult.Success, Is.True);
            Assert.That(strategy.HasRetainedInstance, Is.True);
            Assert.That(fixture.Provider.ReleaseCount, Is.Zero);

            strategy.Evict();
            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator DelayedShow_OperationCompletesOnlyAfterTransition()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(false, true);
            IUIOperationSource<UITransitionResult> transitionSource =
                new ManualUIOperationFactory()
                    .Create<UITransitionResult>(
                        AppUIOperationDescriptor.Create("DelayedShow"));
            transitionSource.TrySetRunning();
            TestPanelController.NextShowTransition =
                UITransition.WaitFor(transitionSource.Operation);

            IUIOperation<UIOpenResult> openOperation =
                fixture.Manager.Open(PageId);

            Assert.That(openOperation.IsTerminal, Is.False);
            Assert.That(TestPanelController.ShowCount, Is.Zero);
            transitionSource.TrySetSucceeded(UITransitionResult.Ok());
            yield return WaitFor(openOperation);
            AssertSucceeded(openOperation, out UIOpenResult result);

            Assert.That(result.Success, Is.True);
            Assert.That(TestPanelController.ShowCount, Is.EqualTo(1));
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator Cancel_CloseOnCancel_ReleasesTopPage()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(false, true, true);
            IUIOperation<UIOpenResult> openOperation = fixture.Manager.Open(
                PageId,
                UIOpenArgs.None.WithSceneScopeId("scene-cancel"));
            yield return WaitFor(openOperation);
            AssertSucceeded(openOperation, out UIOpenResult openResult);
            Assert.That(openResult.Success, Is.True);

            IUIOperation<UICancelResult> cancelOperation =
                fixture.Manager.Cancel();
            yield return WaitFor(cancelOperation);
            AssertSucceeded(cancelOperation, out UICancelResult cancelResult);

            Assert.That(cancelResult.Consumed, Is.True);
            Assert.That(
                cancelResult.Outcome,
                Is.EqualTo(UICancelOutcome.Closed));
            Assert.That(cancelResult.CloseResult.Success, Is.True);
            Assert.That(fixture.Manager.IsOpen(PageId), Is.False);
            Assert.That(TestPanelController.HideCount, Is.EqualTo(1));
            Assert.That(TestPanelController.DisposeCount, Is.EqualTo(1));
            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator ProviderBackedNotice_UsesPrefabAndReleasesLeaseOnShutdown()
        {
            RuntimeFixture fixture = RuntimeFixture.Create(
                false,
                false,
                false,
                true);
            Assert.That(fixture.Provider.SyncLoadCount, Is.EqualTo(1));

            ToastHandle handle =
                fixture.Manager.Notices.Toast("provider-toast");

            Assert.That(handle.IsValid, Is.True);
            Assert.That(
                CountActiveProviderNoticeViews(fixture.NoticeRoot),
                Is.EqualTo(1));
            Assert.That(fixture.Provider.ReleaseCount, Is.Zero);

            fixture.Manager.Shutdown();

            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
            yield return fixture.Dispose();
            Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RaycastZone_PassChannelMask_AllowsConfiguredChannelOnly()
        {
            InputRaycastFixture fixture = InputRaycastFixture.Create(false);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            bool panBlocked = AppUIInputHitResolver.Shared.TryGetFirstBlocker(
                fixture.ScreenCenter,
                AppUIInputChannel.ViewportPan,
                out GameObject panBlocker);
            bool pointerBlocked = AppUIInputHitResolver.Shared.TryGetFirstBlocker(
                fixture.ScreenCenter,
                AppUIInputChannel.PrimaryPointer,
                out GameObject pointerBlocker);

            Assert.That(panBlocked, Is.False);
            Assert.That(panBlocker, Is.Null);
            Assert.That(pointerBlocked, Is.True);
            Assert.That(pointerBlocker, Is.EqualTo(fixture.PolicySurface));
            yield return fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator RaycastZone_InteractiveSelectableBlocksPassedChannel()
        {
            InputRaycastFixture fixture = InputRaycastFixture.Create(true);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            bool blocked = AppUIInputHitResolver.Shared.TryGetFirstBlocker(
                fixture.ScreenCenter,
                AppUIInputChannel.ViewportPan,
                out GameObject blocker);

            Assert.That(blocked, Is.True);
            Assert.That(blocker, Is.EqualTo(fixture.ZoneObject));
            yield return fixture.Dispose();
        }

        private static int CountActiveToastViews(RectTransform root)
        {
            ToastNoticeView[] views = root.GetComponentsInChildren<ToastNoticeView>(true);
            int count = 0;
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private static T GetPrivateField<T>(
            object target,
            Type declaringType,
            string fieldName)
        {
            FieldInfo field = declaringType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                "Missing test field: " + fieldName);
            return (T)field.GetValue(target);
        }

        private static int CountActiveProviderNoticeViews(RectTransform root)
        {
            ProviderNoticeMarker[] views = root.GetComponentsInChildren<ProviderNoticeMarker>(true);
            int count = 0;
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class RuntimeFixture
        {
            private readonly List<Object> ownedObjects = new List<Object>(4);

            public GameObject Root { get; private set; }
            public RectTransform NoticeRoot { get; private set; }
            public AppUIManager Manager { get; private set; }
            public CountingAssetProvider Provider { get; private set; }

            public static RuntimeFixture Create(
                bool delayPageLoad,
                bool includePage,
                bool closeOnCancel = false,
                bool includeProviderNotice = false,
                IUIPageInstanceStrategy instanceStrategy = null)
            {
                TestPanelController.ResetState();
                RuntimeFixture fixture = new RuntimeFixture();
                fixture.Root = new GameObject("AppUI Runtime Integration Root", typeof(RectTransform), typeof(Canvas));
                fixture.Manager = fixture.Root.AddComponent<AppUIManager>();

                UILayerRoot[] roots = new UILayerRoot[UILayerRuntimeConfigurator.BuiltInLayerIds.Length];
                for (int i = 0; i < roots.Length; i++)
                {
                    UILayerId layerId = UILayerRuntimeConfigurator.BuiltInLayerIds[i];
                    UILayerRuntimeConfigurator.TryGetDefaultLayerSetting(
                        layerId,
                        out UICanvasDomain canvasDomain,
                        out _);

                    GameObject layerObject = new GameObject(layerId.ToString(), typeof(RectTransform));
                    layerObject.transform.SetParent(fixture.Root.transform, false);
                    UILayerRoot layerRoot = layerObject.AddComponent<UILayerRoot>();
                    layerRoot.Configure(layerId, canvasDomain, (RectTransform)layerObject.transform);
                    roots[i] = layerRoot;

                    if (layerId == UILayerId.NoticeLayer)
                    {
                        fixture.NoticeRoot = (RectTransform)layerObject.transform;
                    }
                }

                UIPageDefinitionRegistry registry = ScriptableObject.CreateInstance<UIPageDefinitionRegistry>();
                fixture.ownedObjects.Add(registry);

                GameObject pagePrefab = null;
                if (includePage)
                {
                    pagePrefab = new GameObject("IntegrationPage", typeof(RectTransform));
                    pagePrefab.AddComponent<TestPanelController>();
                    pagePrefab.SetActive(false);
                    fixture.ownedObjects.Add(pagePrefab);

                    UIPageDefinition definition = ScriptableObject.CreateInstance<UIPageDefinition>();
                    definition.name = "IntegrationPageDefinition";
                    definition.LayerId = UILayerId.PopupLayer;
                    definition.CanvasDomain = UICanvasDomain.Overlay;
                    definition.Scope = UIPageScope.SceneScope;
                    definition.OpenPolicy = UIOpenPolicy.RejectIfOpeningOrOpen;
                    definition.CloseOnCancel = closeOnCancel;
                    definition.RequiresRaycaster = false;
                    definition.InstanceStrategyId =
                        instanceStrategy != null
                            ? instanceStrategy.StrategyId
                            : string.Empty;
                    SetDefinitionIdentity(definition, PageId, PageAssetId);
                    fixture.ownedObjects.Add(definition);

                    SetPrivateField(
                        registry,
                        typeof(UIPageDefinitionRegistry),
                        "m_Pages",
                        new List<UIPageDefinition> { definition });
                }

                GameObject noticePrefab = null;
                AppUINoticeSettings noticeSettings = AppUINoticeSettings.CreateDefault();
                if (includeProviderNotice)
                {
                    noticePrefab = new GameObject(
                        "ProviderToastPrefab",
                        typeof(RectTransform),
                        typeof(CanvasGroup),
                        typeof(ProviderNoticeMarker));
                    noticePrefab.SetActive(false);
                    fixture.ownedObjects.Add(noticePrefab);
                    SetPrivateField(
                        noticeSettings.Toast,
                        typeof(AppUINoticeVisualSettings),
                        "prefabAssetId",
                        NoticeAssetId);
                }

                fixture.Provider = new CountingAssetProvider(
                    pagePrefab,
                    noticePrefab,
                    delayPageLoad);
                ManualUIOperationFactory operationFactory =
                    new ManualUIOperationFactory();
                fixture.Provider.SetOperationFactory(operationFactory);
                fixture.Manager.Initialize(
                    registry,
                    new AppUIRuntimeDependencies(
                        operationFactory,
                        fixture.Provider,
                        new ImmediateAppUIExecutionContext()),
                    roots,
                    null,
                    noticeSettings,
                    instanceStrategy != null
                        ? new AppUIRuntimeConfiguration(
                            null,
                            new IUIPageInstanceStrategy[]
                            {
                                instanceStrategy,
                            })
                        : AppUIRuntimeConfiguration.Empty);
                return fixture;
            }

            public IEnumerator Dispose()
            {
                if (Root != null)
                {
                    Object.Destroy(Root);
                }

                for (int i = 0; i < ownedObjects.Count; i++)
                {
                    if (ownedObjects[i] != null)
                    {
                        Object.Destroy(ownedObjects[i]);
                    }
                }

                ownedObjects.Clear();
                yield return null;
            }

            private static void SetDefinitionIdentity(
                UIPageDefinition definition,
                string definitionId,
                string prefabAssetId)
            {
                SetPrivateField(
                    definition,
                    typeof(UIDefinitionAssetBase),
                    "m_DefinitionId",
                    definitionId);
                SetPrivateField(
                    definition,
                    typeof(UIDefinitionAssetBase),
                    "m_PrefabAssetId",
                    prefabAssetId);
            }

            private static void SetPrivateField(
                object target,
                Type declaringType,
                string fieldName,
                object value)
            {
                FieldInfo field = declaringType.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "Missing test setup field: " + fieldName);
                field.SetValue(target, value);
            }
        }

        private sealed class CountingAssetProvider : IUIAssetProvider
        {
            private readonly GameObject pagePrefab;
            private readonly GameObject noticePrefab;
            private readonly bool delayPageLoad;
            private IUIOperationFactory operationFactory;
            private readonly Queue<Action> pendingLoadCompletions =
                new Queue<Action>();

            public CountingAssetProvider(
                GameObject pagePrefab,
                GameObject noticePrefab,
                bool delayPageLoad)
            {
                this.pagePrefab = pagePrefab;
                this.noticePrefab = noticePrefab;
                this.delayPageLoad = delayPageLoad;
            }

            public int AsyncLoadCount { get; private set; }
            public int SyncLoadCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public void SetOperationFactory(
                IUIOperationFactory value)
            {
                operationFactory = value ??
                    throw new ArgumentNullException(nameof(value));
            }

            public bool TryLoad<T>(string assetId, out UIAssetLoadResult<T> result)
                where T : Object
            {
                SyncLoadCount++;
                if (assetId == NoticeAssetId && noticePrefab != null && typeof(T) == typeof(GameObject))
                {
                    result = UIAssetLoadResult<T>.Success(
                        noticePrefab as T,
                        new UIAssetLease(() => ReleaseCount++));
                    return true;
                }

                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.SynchronousLoadUnsupported,
                    "The test provider loads page assets asynchronously: " +
                    assetId);
                return false;
            }

            public IUIOperation<UIAssetLoadResult<T>> Load<T>(
                string assetId,
                CancellationToken cancellationToken)
                where T : Object
            {
                IUIOperationSource<UIAssetLoadResult<T>> source =
                    operationFactory.Create<UIAssetLoadResult<T>>(
                        AppUIOperationDescriptor.Create(
                            "RuntimeTestLoad",
                            cancellationToken));
                source.TrySetRunning();
                AsyncLoadCount++;
                if (typeof(T) != typeof(GameObject) ||
                    pagePrefab == null ||
                    assetId != PageAssetId)
                {
                    source.TrySetSucceeded(
                        UIAssetLoadResult<T>.Failure(
                            UIAssetLoadStatus.NotFound,
                            "Unknown test asset: " + assetId));
                    return source.Operation;
                }

                if (!delayPageLoad)
                {
                    source.TrySetSucceeded(CreateSuccessResult<T>());
                    return source.Operation;
                }

                pendingLoadCompletions.Enqueue(() =>
                    source.TrySetSucceeded(CreateSuccessResult<T>()));
                return source.Operation;
            }

            public void CompletePendingLoad()
            {
                CompleteNextPendingLoad();
            }

            public void CompleteNextPendingLoad()
            {
                Assert.That(pendingLoadCompletions.Count, Is.GreaterThan(0));
                pendingLoadCompletions.Dequeue().Invoke();
            }

            private UIAssetLoadResult<T> CreateSuccessResult<T>()
                where T : Object
            {
                return UIAssetLoadResult<T>.Success(
                    pagePrefab as T,
                    new UIAssetLease(() => ReleaseCount++));
            }
        }

        private sealed class InputRaycastFixture
        {
            private GameObject eventSystemObject;
            private GameObject canvasObject;

            public GameObject PolicySurface { get; private set; }
            public GameObject ZoneObject { get; private set; }
            public Vector2 ScreenCenter { get; private set; }

            public static InputRaycastFixture Create(bool interactiveZone)
            {
                InputRaycastFixture fixture = new InputRaycastFixture();
                fixture.eventSystemObject = new GameObject(
                    "AppUI Test EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));

                fixture.canvasObject = new GameObject(
                    "AppUI Test Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster));
                Canvas canvas = fixture.canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                fixture.PolicySurface = new GameObject(
                    "PolicySurface",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(AppUIInputPolicyRoot));
                fixture.PolicySurface.transform.SetParent(fixture.canvasObject.transform, false);
                RectTransform policyRect = (RectTransform)fixture.PolicySurface.transform;
                policyRect.anchorMin = Vector2.zero;
                policyRect.anchorMax = Vector2.one;
                policyRect.offsetMin = Vector2.zero;
                policyRect.offsetMax = Vector2.zero;
                Image policyImage = fixture.PolicySurface.GetComponent<Image>();
                policyImage.color = new Color(1f, 1f, 1f, 0.01f);
                policyImage.raycastTarget = true;
                fixture.PolicySurface.GetComponent<AppUIInputPolicyRoot>().SetDefaultPolicy(
                    AppUIInputZoneMode.BlockAll);

                fixture.ZoneObject = new GameObject("PassZone", typeof(RectTransform), typeof(AppUIInputZone));
                fixture.ZoneObject.transform.SetParent(fixture.PolicySurface.transform, false);
                RectTransform zoneRect = (RectTransform)fixture.ZoneObject.transform;
                zoneRect.anchorMin = new Vector2(0.5f, 0.5f);
                zoneRect.anchorMax = new Vector2(0.5f, 0.5f);
                zoneRect.sizeDelta = new Vector2(320f, 240f);
                zoneRect.anchoredPosition = Vector2.zero;
                fixture.ZoneObject.GetComponent<AppUIInputZone>().SetPolicy(
                    AppUIInputZoneMode.PassChannelMask,
                    AppUIInputChannelMask.ViewportPan);

                if (interactiveZone)
                {
                    Image zoneImage = fixture.ZoneObject.AddComponent<Image>();
                    zoneImage.color = new Color(1f, 1f, 1f, 0.01f);
                    zoneImage.raycastTarget = true;
                    fixture.ZoneObject.AddComponent<Button>();
                }

                fixture.ScreenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                return fixture;
            }

            public IEnumerator Dispose()
            {
                if (canvasObject != null)
                {
                    Object.Destroy(canvasObject);
                }

                if (eventSystemObject != null)
                {
                    Object.Destroy(eventSystemObject);
                }

                yield return null;
            }
        }

        private sealed class ProviderNoticeMarker : MonoBehaviour
        {
        }

        private sealed class ClaimThenThrowInstanceStrategy :
            IUIPageInstanceStrategy
        {
            public string StrategyId => "claim-then-throw";

            public UIPageInstanceAllocation Create(
                UIPageInstanceCreationRequest request)
            {
                request.AssetLeaseTransfer.Claim();
                throw new InvalidOperationException(
                    "intentional instance creation failure");
            }
        }

        private sealed class RetainingPoolInstanceStrategy :
            IUIPageInstanceStrategy
        {
            private GameObject retainedInstance;
            private UIAssetLease retainedLease;

            public string StrategyId => "retaining-pool";
            public bool HasRetainedInstance => retainedInstance != null;

            public UIPageInstanceAllocation Create(
                UIPageInstanceCreationRequest request)
            {
                UIAssetLeaseClaim claim =
                    request.AssetLeaseTransfer.Claim();
                GameObject instance = Object.Instantiate(
                    request.Prefab,
                    request.Parent,
                    false);
                instance.name = request.Prefab.name;
                instance.SetActive(false);
                return new UIPageInstanceAllocation(
                    instance,
                    claim,
                    context =>
                    {
                        retainedInstance = context.GameObject;
                        retainedLease = context.AssetLease;
                        retainedInstance.SetActive(false);
                        return UIPageInstanceReleaseDisposition.RetainLease;
                    });
            }

            public void Evict()
            {
                if (retainedInstance != null)
                {
                    Object.Destroy(retainedInstance);
                    retainedInstance = null;
                }

                retainedLease?.Dispose();
                retainedLease = null;
            }
        }

        private sealed class TestPanelController : PanelBaseController
        {
            public static int CreateCount { get; private set; }
            public static int InitCount { get; private set; }
            public static int RefreshCount { get; private set; }
            public static int ShowCount { get; private set; }
            public static int HideCount { get; private set; }
            public static int DisposeCount { get; private set; }
            public static object LastData { get; private set; }
            public static TestPanelController LastInstance { get; private set; }
            public static UITransition NextShowTransition { get; set; }

            public static void ResetState()
            {
                CreateCount = 0;
                InitCount = 0;
                RefreshCount = 0;
                ShowCount = 0;
                HideCount = 0;
                DisposeCount = 0;
                LastData = null;
                LastInstance = null;
                NextShowTransition = UITransition.Immediate;
            }

            protected override void OnCreateEx(UIControllerContext context)
            {
                CreateCount++;
                LastInstance = this;
            }

            protected override void OnInitEx()
            {
                InitCount++;
            }

            protected override void OnDataLoadEx(object data)
            {
                LastData = data;
            }

            protected override void OnRefreshEx()
            {
                RefreshCount++;
            }

            protected override void OnShowEx()
            {
                ShowCount++;
            }

            protected override UITransition BeginShowTransition()
            {
                UITransition transition = NextShowTransition;
                NextShowTransition = UITransition.Immediate;
                return transition;
            }

            protected override void OnHideEx()
            {
                HideCount++;
            }

            protected override void OnDisposeEx()
            {
                DisposeCount++;
            }
        }
    }
}
