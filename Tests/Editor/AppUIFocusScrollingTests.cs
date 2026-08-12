using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusScrollingTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>(24);
        private EventSystem testEventSystem;

        [SetUp]
        public void SetUp()
        {
            GameObject eventSystemObject = CreateObject("EventSystem");
            testEventSystem = eventSystemObject.AddComponent<EventSystem>();
            InvokeEventSystemLifecycle(testEventSystem, "OnEnable");
            Assert.That(EventSystem.current, Is.SameAs(testEventSystem));
        }

        [TearDown]
        public void TearDown()
        {
            if (testEventSystem != null)
            {
                testEventSystem.SetSelectedGameObject(null);
                InvokeEventSystemLifecycle(testEventSystem, "OnDisable");
                testEventSystem = null;
            }

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            AppUIInteractionSourceAuthority.Reset();
        }

        [Test]
        public void ScrollRectVisibilityAdapter_MovesOnlyWhenTargetLeavesViewport()
        {
            RectTransform root = CreateRect("Root", null, new Vector2(200f, 100f));
            RectTransform viewport = CreateRect(
                "Viewport",
                root,
                new Vector2(200f, 100f));
            RectTransform content = CreateRect(
                "Content",
                viewport,
                new Vector2(200f, 300f));
            RectTransform visible = CreateRect(
                "Visible",
                content,
                new Vector2(160f, 30f));
            RectTransform outside = CreateRect(
                "Outside",
                content,
                new Vector2(160f, 30f));
            visible.anchoredPosition = Vector2.zero;
            outside.anchoredPosition = new Vector2(0f, -120f);

            ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            AppUIFocusScrollRectVisibilityAdapter adapter =
                new AppUIFocusScrollRectVisibilityAdapter(scrollRect);

            Vector2 initialPosition = content.anchoredPosition;
            Assert.That(adapter.EnsureVisible(visible), Is.False);
            Assert.That(content.anchoredPosition, Is.EqualTo(initialPosition));

            Assert.That(adapter.EnsureVisible(outside), Is.True);
            Assert.That(content.anchoredPosition, Is.Not.EqualTo(initialPosition));
            Bounds targetBounds =
                RectTransformUtility.CalculateRelativeRectTransformBounds(
                    viewport,
                    outside);
            Assert.That(targetBounds.min.y, Is.GreaterThanOrEqualTo(-50.01f));
            Assert.That(targetBounds.max.y, Is.LessThanOrEqualTo(50.01f));
        }

        [Test]
        public void VisibilityAdapter_RunsAfterSuccessfulCommitOnly()
        {
            const string pageId = "VisibilityPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button first = CreateButton(pageObject, "First");
            Button target = CreateButton(pageObject, "Target");
            RecordingVisibilityAdapter visibilityAdapter =
                new RecordingVisibilityAdapter();
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .AddGroup("main")
                    .SetGroupVisibilityAdapter("main", visibilityAdapter)
                    .AddNode("main", new AppUIFocusNodeKey("first"), first, 0)
                    .AddNode("main", new AppUIFocusNodeKey("target"), target, 1)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);

            Assert.That(
                runtime.ScopeHandle.FocusNode(
                    new AppUIFocusNodeAddress(
                        "main",
                        new AppUIFocusNodeKey("first")),
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(visibilityAdapter.CallCount, Is.EqualTo(1));
            Assert.That(visibilityAdapter.LastTarget, Is.SameAs(first.transform));

            Assert.That(runtime.ScopeHandle.CloseGroup("main"), Is.True);
            Assert.That(
                runtime.ScopeHandle.FocusNode(
                    new AppUIFocusNodeAddress(
                        "main",
                        new AppUIFocusNodeKey("target")),
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.GroupClosed));
            Assert.That(visibilityAdapter.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Virtualization_KeepsOldFocusUntilRealizedThenRegistersScrollsAndCommits()
        {
            const string pageId = "VirtualPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button current = CreateButton(pageObject, "Current");
            Button realized = CreateButton(pageObject, "Realized");
            ControllableVirtualizationAdapter virtualizationAdapter =
                new ControllableVirtualizationAdapter();
            RecordingVisibilityAdapter visibilityAdapter =
                new RecordingVisibilityAdapter();
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .AddGroup("main")
                    .SetGroupVisibilityAdapter("main", visibilityAdapter)
                    .SetGroupVirtualizationAdapter("main", virtualizationAdapter)
                    .AddNode("main", new AppUIFocusNodeKey("current"), current, 0)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);
            AppUIFocusNodeAddress currentAddress = new AppUIFocusNodeAddress(
                "main",
                new AppUIFocusNodeKey("current"));
            AppUIFocusNodeAddress virtualAddress = new AppUIFocusNodeAddress(
                "main",
                new AppUIFocusNodeKey("virtual-42"));
            Assert.That(
                runtime.ScopeHandle.FocusNode(
                    currentAddress,
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.Focused));
            visibilityAdapter.Reset();

            AppUIFocusRequestResult pending = runtime.ScopeHandle.FocusNode(
                virtualAddress,
                AppUIFocusChangeReason.Navigation);

            Assert.That(pending, Is.EqualTo(AppUIFocusRequestResult.PendingRealization));
            Assert.That(virtualizationAdapter.Requests.Count, Is.EqualTo(1));
            Assert.That(
                virtualizationAdapter.Requests[0].Request.NodeAddress,
                Is.EqualTo(virtualAddress));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(current.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(currentAddress));
            Assert.That(visibilityAdapter.CallCount, Is.Zero);

            virtualizationAdapter.Complete(
                0,
                AppUIFocusRealizationResult.Realized(realized, 42));
            yield return null;

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(realized.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(virtualAddress));
            Assert.That(visibilityAdapter.CallCount, Is.EqualTo(1));
            Assert.That(visibilityAdapter.LastTarget, Is.SameAs(realized.transform));
        }

        [UnityTest]
        public IEnumerator Virtualization_NewerRequestCancelsAndSupersedesOlderRequest()
        {
            const string pageId = "VirtualReplacePage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button current = CreateButton(pageObject, "Current");
            Button firstRealized = CreateButton(pageObject, "FirstRealized");
            Button secondRealized = CreateButton(pageObject, "SecondRealized");
            ControllableVirtualizationAdapter virtualizationAdapter =
                new ControllableVirtualizationAdapter();
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .AddGroup("main")
                    .SetGroupVirtualizationAdapter("main", virtualizationAdapter)
                    .AddNode("main", new AppUIFocusNodeKey("current"), current, 0)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);
            AppUIFocusNodeAddress currentAddress = new AppUIFocusNodeAddress(
                "main",
                new AppUIFocusNodeKey("current"));
            AppUIFocusNodeAddress firstAddress = new AppUIFocusNodeAddress(
                "main",
                new AppUIFocusNodeKey("virtual-1"));
            AppUIFocusNodeAddress secondAddress = new AppUIFocusNodeAddress(
                "main",
                new AppUIFocusNodeKey("virtual-2"));
            runtime.ScopeHandle.FocusNode(
                currentAddress,
                AppUIFocusChangeReason.Navigation);

            Assert.That(
                runtime.ScopeHandle.FocusNode(
                    firstAddress,
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.PendingRealization));
            Assert.That(
                runtime.ScopeHandle.FocusNode(
                    secondAddress,
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.PendingRealization));
            Assert.That(virtualizationAdapter.Requests.Count, Is.EqualTo(2));
            Assert.That(
                virtualizationAdapter.Requests[0].CancellationToken
                    .IsCancellationRequested,
                Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(current.gameObject));

            virtualizationAdapter.Complete(
                1,
                AppUIFocusRealizationResult.Realized(secondRealized, 2));
            virtualizationAdapter.Complete(
                0,
                AppUIFocusRealizationResult.Realized(firstRealized, 1));
            yield return null;

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(secondRealized.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(secondAddress));
        }

        [UnityTest]
        public IEnumerator Virtualization_ScopeSuspensionCancelsPendingCommit()
        {
            const string pageId = "VirtualSuspendPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button current = CreateButton(pageObject, "Current");
            Button realized = CreateButton(pageObject, "Realized");
            ControllableVirtualizationAdapter virtualizationAdapter =
                new ControllableVirtualizationAdapter();
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .AddGroup("main")
                    .SetGroupVirtualizationAdapter("main", virtualizationAdapter)
                    .AddNode("main", new AppUIFocusNodeKey("current"), current, 0)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);
            AppUIFocusNodeAddress virtualAddress = new AppUIFocusNodeAddress(
                "main",
                new AppUIFocusNodeKey("virtual"));
            runtime.ScopeHandle.FocusNode(
                new AppUIFocusNodeAddress(
                    "main",
                    new AppUIFocusNodeKey("current")),
                AppUIFocusChangeReason.Navigation);
            Assert.That(
                runtime.ScopeHandle.FocusNode(
                    virtualAddress,
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.PendingRealization));

            runtime.FocusService.ApplyInteractionSnapshot(
                CreateSnapshot(2, runtime.Page.ToInteractionHandle(), false));
            Assert.That(
                virtualizationAdapter.Requests[0].CancellationToken
                    .IsCancellationRequested,
                Is.True);
            virtualizationAdapter.Complete(
                0,
                AppUIFocusRealizationResult.Realized(realized, 1));
            yield return null;

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.Not.SameAs(realized.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.Not.EqualTo(virtualAddress));
        }

        [UnityTest]
        public IEnumerator DefaultFocus_MissingAddressStartsVirtualizationWithoutFallback()
        {
            const string pageId = "VirtualDefaultPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            FixedVirtualDefaultFocusController controller =
                pageObject.AddComponent<FixedVirtualDefaultFocusController>();
            Button fallback = CreateButton(pageObject, "Fallback");
            Button realized = CreateButton(pageObject, "Realized");
            AppUIFocusNodeAddress virtualAddress = new AppUIFocusNodeAddress(
                "main",
                new AppUIFocusNodeKey("virtual-default"));
            controller.Configure(virtualAddress);
            ControllableVirtualizationAdapter virtualizationAdapter =
                new ControllableVirtualizationAdapter();
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .AddGroup("main")
                    .SetGroupVirtualizationAdapter("main", virtualizationAdapter)
                    .AddNode("main", new AppUIFocusNodeKey("fallback"), fallback, 0)
                    .Build();
            TestRuntime runtime = CreateRuntime(
                pageId,
                pageObject,
                definition,
                controller);
            Activate(runtime, 1);

            Assert.That(
                runtime.FocusService.TryHandleSemanticSelection(
                    runtime.Page,
                    AppUIFocusChangeReason.FirstOpened),
                Is.True);
            Assert.That(virtualizationAdapter.Requests.Count, Is.EqualTo(1));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);

            virtualizationAdapter.Complete(
                0,
                AppUIFocusRealizationResult.Realized(realized, 1));
            yield return null;

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(realized.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(virtualAddress));
        }

        private TestRuntime CreateRuntime(
            string pageId,
            GameObject pageObject,
            AppUIFocusDefinition definition,
            PanelBaseController controller = null)
        {
            UIPageInstanceRegistry registry = new UIPageInstanceRegistry();
            UIPageInstance page = new UIPageInstance
            {
                PageId = pageId,
                GameObject = pageObject,
                RectTransform = pageObject.transform as RectTransform,
                Controller = controller,
                OperationVersion = 1,
                State = UIPageState.Open,
                StackVisible = true,
            };
            registry.Register(page);

            UIFocusService focusService = new UIFocusService();
            focusService.ConfigureInstanceRegistry(registry);
            focusService.ConfigureExecutionContext(
                new ImmediateAppUIExecutionContext());
            IAppUIFocusScopeHandle scopeHandle = focusService.AttachScope(
                page,
                definition);
            return new TestRuntime(
                page,
                focusService,
                scopeHandle,
                (AppUIFocusScope)scopeHandle);
        }

        private void Activate(TestRuntime runtime, int stackRevision)
        {
            runtime.FocusService.ApplyInteractionSnapshot(
                CreateSnapshot(
                    stackRevision,
                    runtime.Page.ToInteractionHandle(),
                    true));
        }

        private Button CreateButton(GameObject parent, string name)
        {
            GameObject buttonObject = CreateObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent.transform, false);
            Button button = buttonObject.AddComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }

        private RectTransform CreateRect(
            string name,
            RectTransform parent,
            Vector2 size)
        {
            GameObject gameObject = CreateObject(name, typeof(RectTransform));
            RectTransform rectTransform = (RectTransform)gameObject.transform;
            if (parent != null)
            {
                rectTransform.SetParent(parent, false);
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = Vector2.zero;
            return rectTransform;
        }

        private GameObject CreateObject(
            string name,
            params System.Type[] components)
        {
            GameObject gameObject = components != null && components.Length > 0
                ? new GameObject(name, components)
                : new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static UIInteractionSnapshot CreateSnapshot(
            int revision,
            UIPageInteractionHandle handle,
            bool topInteractive)
        {
            UIPageInteractionState[] states =
            {
                new UIPageInteractionState(handle, true, 0, 0),
            };
            return new UIInteractionSnapshot(
                revision,
                topInteractive ? handle : default,
                states);
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

        private readonly struct TestRuntime
        {
            public TestRuntime(
                UIPageInstance page,
                UIFocusService focusService,
                IAppUIFocusScopeHandle scopeHandle,
                AppUIFocusScope scope)
            {
                Page = page;
                FocusService = focusService;
                ScopeHandle = scopeHandle;
                Scope = scope;
            }

            public UIPageInstance Page { get; }

            public UIFocusService FocusService { get; }

            public IAppUIFocusScopeHandle ScopeHandle { get; }

            public AppUIFocusScope Scope { get; }
        }

        private sealed class RecordingVisibilityAdapter :
            IAppUIFocusVisibilityAdapter
        {
            public int CallCount { get; private set; }

            public RectTransform LastTarget { get; private set; }

            public bool EnsureVisible(RectTransform target)
            {
                CallCount++;
                LastTarget = target;
                return true;
            }

            public void Reset()
            {
                CallCount = 0;
                LastTarget = null;
            }
        }

        private sealed class ControllableVirtualizationAdapter :
            IAppUIFocusVirtualizationAdapter
        {
            public readonly List<RequestSlot> Requests = new List<RequestSlot>(4);

            public IUIOperation<AppUIFocusRealizationResult> EnsureRealized(
                AppUIFocusRealizationRequest request,
                CancellationToken cancellationToken)
            {
                RequestSlot slot = new RequestSlot(
                    request,
                    cancellationToken);
                Requests.Add(slot);
                return slot.Source.Operation;
            }

            public void Complete(int index, AppUIFocusRealizationResult result)
            {
                Requests[index].Source.TrySetSucceeded(result);
            }
        }

        private sealed class RequestSlot
        {
            public RequestSlot(
                AppUIFocusRealizationRequest request,
                CancellationToken cancellationToken)
            {
                Request = request;
                CancellationToken = cancellationToken;
                Source = new ManualUIOperationFactory()
                    .Create<AppUIFocusRealizationResult>(
                        AppUIOperationDescriptor.Create(
                            "FocusRealization",
                            cancellationToken));
                Source.TrySetRunning();
            }

            public AppUIFocusRealizationRequest Request { get; }

            public CancellationToken CancellationToken { get; }

            public IUIOperationSource<AppUIFocusRealizationResult> Source
            {
                get;
            }
        }
    }

    internal sealed class FixedVirtualDefaultFocusController :
        PanelBaseController,
        IAppUIDefaultFocusTargetProvider
    {
        private AppUIFocusNodeAddress targetAddress;

        public void Configure(AppUIFocusNodeAddress address)
        {
            targetAddress = address;
        }

        public bool TryGetDefaultFocus(
            UIDefaultFocusReason reason,
            out AppUIFocusTarget target)
        {
            target = AppUIFocusTarget.FromNodeAddress(targetAddress);
            return target.IsValid;
        }
    }
}
