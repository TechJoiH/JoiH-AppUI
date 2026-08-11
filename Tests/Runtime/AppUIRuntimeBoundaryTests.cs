using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
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
            return UniTask.ToCoroutine(async () =>
            {
                RuntimeFixture fixture = RuntimeFixture.Create(false, true);
                try
                {
                    UIOpenResult openResult = await fixture.Manager.OpenAsync(
                        PageId,
                        UIOpenArgs.FromExplicit("initial-data").WithSceneScopeId("scene-a"));

                    Assert.That(openResult.Success, Is.True);
                    Assert.That(fixture.Manager.IsOpen(PageId), Is.True);
                    Assert.That(fixture.Provider.AsyncLoadCount, Is.EqualTo(1));
                    Assert.That(TestPanelController.CreateCount, Is.EqualTo(1));
                    Assert.That(TestPanelController.InitCount, Is.EqualTo(1));
                    Assert.That(TestPanelController.RefreshCount, Is.EqualTo(1));
                    Assert.That(TestPanelController.ShowCount, Is.EqualTo(1));
                    Assert.That(TestPanelController.LastData, Is.EqualTo("initial-data"));
                    Assert.That(TestPanelController.LastInstance.gameObject.activeSelf, Is.True);

                    UIRefreshResult refreshResult = await fixture.Manager.RefreshAsync(PageId, "refreshed-data");

                    Assert.That(refreshResult.Success, Is.True);
                    Assert.That(refreshResult.State, Is.EqualTo(UIPageState.Open));
                    Assert.That(TestPanelController.RefreshCount, Is.EqualTo(2));
                    Assert.That(TestPanelController.LastData, Is.EqualTo("refreshed-data"));

                    UICloseRequest hideRequest = UICloseRequest.Default;
                    hideRequest.ReleaseOnClose = false;
                    UICloseResult hideResult = await fixture.Manager.CloseAsync(PageId, hideRequest);

                    Assert.That(hideResult.Success, Is.True);
                    Assert.That(hideResult.State, Is.EqualTo(UIPageState.Hidden));
                    Assert.That(TestPanelController.HideCount, Is.EqualTo(1));
                    Assert.That(TestPanelController.LastInstance.gameObject.activeSelf, Is.False);
                    Assert.That(fixture.Provider.ReleaseCount, Is.Zero);

                    UIOpenResult reopenResult = await fixture.Manager.OpenAsync(
                        PageId,
                        UIOpenArgs.FromExplicit("reopen-data").WithSceneScopeId("scene-a"));

                    Assert.That(reopenResult.Success, Is.True);
                    Assert.That(TestPanelController.CreateCount, Is.EqualTo(1));
                    Assert.That(TestPanelController.InitCount, Is.EqualTo(1));
                    Assert.That(TestPanelController.RefreshCount, Is.EqualTo(3));
                    Assert.That(TestPanelController.ShowCount, Is.EqualTo(2));
                    Assert.That(TestPanelController.LastData, Is.EqualTo("reopen-data"));
                    Assert.That(fixture.Provider.AsyncLoadCount, Is.EqualTo(1));

                    UICloseResult releaseResult = await fixture.Manager.CloseAsync(PageId);

                    Assert.That(releaseResult.Success, Is.True);
                    Assert.That(releaseResult.State, Is.EqualTo(UIPageState.Released));
                    Assert.That(fixture.Manager.IsOpen(PageId), Is.False);
                    Assert.That(TestPanelController.HideCount, Is.EqualTo(2));
                    Assert.That(TestPanelController.DisposeCount, Is.EqualTo(1));
                    Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));

                    await UniTask.Yield(PlayerLoopTiming.Update);
                    Assert.That(TestPanelController.LastInstance == null, Is.True);
                }
                finally
                {
                    await fixture.DisposeAsync();
                }
            });
        }

        [UnityTest]
        public IEnumerator NoticeFallback_CreatesViewAndClearsWithSceneScope()
        {
            return UniTask.ToCoroutine(async () =>
            {
                RuntimeFixture fixture = RuntimeFixture.Create(false, false);
                try
                {
                    ToastNoticeRequest request = ToastNoticeRequest.Create("fallback-toast");
                    request.Scope = UINoticeScope.Scene("scene-notice");
                    request.Duration = 10f;
                    ToastHandle handle = fixture.Manager.Notices.Toast(request);

                    Assert.That(handle.IsValid, Is.True);
                    Assert.That(CountActiveToastViews(fixture.NoticeRoot), Is.EqualTo(1));
                    Assert.That(fixture.Provider.SyncLoadCount, Is.Zero);

                    UIScopeReleaseResult result = await fixture.Manager.ReleaseScopeAsync(
                        UIPageScope.SceneScope,
                        "scene-notice");

                    Assert.That(result.Success, Is.True);
                    Assert.That(CountActiveToastViews(fixture.NoticeRoot), Is.Zero);
                }
                finally
                {
                    await fixture.DisposeAsync();
                }
            });
        }

        [UnityTest]
        public IEnumerator InterruptedOpen_LateLoadCannotCommitAndReleasesLease()
        {
            return UniTask.ToCoroutine(async () =>
            {
                RuntimeFixture fixture = RuntimeFixture.Create(true, true);
                UniTask<UIOpenResult> openTask = fixture.Manager.OpenAsync(
                    PageId,
                    UIOpenArgs.FromExplicit("late-data").WithSceneScopeId("scene-interrupted"));

                Assert.That(fixture.Provider.AsyncLoadCount, Is.EqualTo(1));
                Object.Destroy(fixture.Root);
                await UniTask.Yield(PlayerLoopTiming.Update);

                fixture.Provider.CompletePendingLoad();
                UIOpenResult result = await openTask;

                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Is.EqualTo(UIPageOpenError.OperationExpired));
                Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
                Assert.That(TestPanelController.CreateCount, Is.Zero);

                await fixture.DisposeAsync();
            });
        }

        [UnityTest]
        public IEnumerator CancelAsync_CloseOnCancel_ReleasesTopPage()
        {
            return UniTask.ToCoroutine(async () =>
            {
                RuntimeFixture fixture = RuntimeFixture.Create(false, true, true);
                try
                {
                    UIOpenResult openResult = await fixture.Manager.OpenAsync(
                        PageId,
                        UIOpenArgs.None.WithSceneScopeId("scene-cancel"));
                    Assert.That(openResult.Success, Is.True);

                    UICancelResult cancelResult = await fixture.Manager.CancelAsync();

                    Assert.That(cancelResult.Consumed, Is.True);
                    Assert.That(cancelResult.Outcome, Is.EqualTo(UICancelOutcome.Closed));
                    Assert.That(cancelResult.CloseResult.Success, Is.True);
                    Assert.That(fixture.Manager.IsOpen(PageId), Is.False);
                    Assert.That(TestPanelController.HideCount, Is.EqualTo(1));
                    Assert.That(TestPanelController.DisposeCount, Is.EqualTo(1));
                    Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
                }
                finally
                {
                    await fixture.DisposeAsync();
                }
            });
        }

        [UnityTest]
        public IEnumerator ProviderBackedNotice_UsesPrefabAndReleasesLeaseOnProviderClear()
        {
            return UniTask.ToCoroutine(async () =>
            {
                RuntimeFixture fixture = RuntimeFixture.Create(false, false, false, true);
                try
                {
                    Assert.That(fixture.Provider.SyncLoadCount, Is.EqualTo(1));

                    ToastHandle handle = fixture.Manager.Notices.Toast("provider-toast");

                    Assert.That(handle.IsValid, Is.True);
                    Assert.That(CountActiveProviderNoticeViews(fixture.NoticeRoot), Is.EqualTo(1));
                    Assert.That(fixture.Provider.ReleaseCount, Is.Zero);

                    fixture.Manager.ClearAssetProvider();

                    Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
                }
                finally
                {
                    await fixture.DisposeAsync();
                }

                Assert.That(fixture.Provider.ReleaseCount, Is.EqualTo(1));
            });
        }

        [UnityTest]
        public IEnumerator RaycastZone_PassChannelMask_AllowsConfiguredChannelOnly()
        {
            return UniTask.ToCoroutine(async () =>
            {
                InputRaycastFixture fixture = InputRaycastFixture.Create(false);
                try
                {
                    await UniTask.NextFrame();
                    await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
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
                }
                finally
                {
                    await fixture.DisposeAsync();
                }
            });
        }

        [UnityTest]
        public IEnumerator RaycastZone_InteractiveSelectableBlocksPassedChannel()
        {
            return UniTask.ToCoroutine(async () =>
            {
                InputRaycastFixture fixture = InputRaycastFixture.Create(true);
                try
                {
                    await UniTask.NextFrame();
                    await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                    Canvas.ForceUpdateCanvases();

                    bool blocked = AppUIInputHitResolver.Shared.TryGetFirstBlocker(
                        fixture.ScreenCenter,
                        AppUIInputChannel.ViewportPan,
                        out GameObject blocker);

                    Assert.That(blocked, Is.True);
                    Assert.That(blocker, Is.EqualTo(fixture.ZoneObject));
                }
                finally
                {
                    await fixture.DisposeAsync();
                }
            });
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
                bool includeProviderNotice = false)
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
                fixture.Manager.Initialize(
                    registry,
                    fixture.Provider,
                    roots,
                    null,
                    noticeSettings);
                return fixture;
            }

            public async UniTask DisposeAsync()
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
                await UniTask.Yield(PlayerLoopTiming.Update);
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
            private readonly UniTaskCompletionSource<UIAssetLoadResult<GameObject>> pendingLoad =
                new UniTaskCompletionSource<UIAssetLoadResult<GameObject>>();

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
                    UIAssetLoadStatus.NotFound,
                    "No synchronous test asset: " + assetId);
                return false;
            }

            public UniTask<UIAssetLoadResult<T>> LoadAsync<T>(string assetId)
                where T : Object
            {
                AsyncLoadCount++;
                if (typeof(T) != typeof(GameObject) || pagePrefab == null || assetId != PageAssetId)
                {
                    return UniTask.FromResult(UIAssetLoadResult<T>.Failure(
                        UIAssetLoadStatus.NotFound,
                        "Unknown test asset: " + assetId));
                }

                if (!delayPageLoad)
                {
                    return UniTask.FromResult(CreateSuccessResult<T>());
                }

                return AwaitPendingLoad<T>();
            }

            public void CompletePendingLoad()
            {
                pendingLoad.TrySetResult(UIAssetLoadResult<GameObject>.Success(
                    pagePrefab,
                    new UIAssetLease(() => ReleaseCount++)));
            }

            private async UniTask<UIAssetLoadResult<T>> AwaitPendingLoad<T>()
                where T : Object
            {
                UIAssetLoadResult<GameObject> result = await pendingLoad.Task;
                return new UIAssetLoadResult<T>(
                    result.Status,
                    result.Asset as T,
                    result.Lease,
                    result.ErrorMessage);
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

            public async UniTask DisposeAsync()
            {
                if (canvasObject != null)
                {
                    Object.Destroy(canvasObject);
                }

                if (eventSystemObject != null)
                {
                    Object.Destroy(eventSystemObject);
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private sealed class ProviderNoticeMarker : MonoBehaviour
        {
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
