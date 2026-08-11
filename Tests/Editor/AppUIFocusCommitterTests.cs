using System.Collections.Generic;
using System.Reflection;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusCommitterTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>(16);
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
        public void Commit_ValidNavigation_WritesFocusAndFullHistoryWithoutRevisionChange()
        {
            TestRuntime runtime = CreateRuntime("NavigationPage");
            Button first = CreateButton(runtime.Page.GameObject, "First");
            AppUIFocusNodeAddress firstAddress = Register(
                runtime.ScopeHandle,
                "main",
                "first",
                first,
                0);
            Activate(runtime, 1);
            int scopeRevision = runtime.Scope.Revision;
            int regionRevision = runtime.Scope.RootRegionRevision;

            AppUIFocusRequestResult result = runtime.Scope.CommitFocus(
                first,
                AppUIFocusChangeReason.Navigation);

            Assert.That(result, Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(first.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(firstAddress));
            Assert.That(runtime.Scope.LastFocusedAddress, Is.EqualTo(firstAddress));
            Assert.That(runtime.Scope.Revision, Is.EqualTo(scopeRevision));
            Assert.That(runtime.Scope.RootRegionRevision, Is.EqualTo(regionRevision));
        }

        [Test]
        public void ScopeHandle_FocusNodeByAddress_UsesValidatedCommitPipeline()
        {
            TestRuntime runtime = CreateRuntime("ScopeHandlePage");
            Button button = CreateButton(runtime.Page.GameObject, "Target");
            AppUIFocusNodeAddress address = Register(
                runtime.ScopeHandle,
                "main",
                "target",
                button,
                0);
            Activate(runtime, 1);

            AppUIFocusRequestResult result = runtime.ScopeHandle.FocusNode(
                address,
                AppUIFocusChangeReason.Programmatic);

            Assert.That(result, Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(button.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(address));
            Assert.That(
                runtime.Scope.LastFocusedAddress.IsValid,
                Is.False,
                "Programmatic focus must not overwrite restoration history.");

            Assert.That(runtime.ScopeHandle.CloseGroup("main"), Is.True);
            Assert.That(
                runtime.ScopeHandle.FocusNode(address),
                Is.EqualTo(AppUIFocusRequestResult.GroupClosed));
        }

        [Test]
        public void ScopeHandle_FocusGroupFirst_UsesStableGroupOrderAndCommitPipeline()
        {
            TestRuntime runtime = CreateRuntime("GroupFirstPage");
            Button later = CreateButton(runtime.Page.GameObject, "Later");
            Button first = CreateButton(runtime.Page.GameObject, "First");
            Register(runtime.ScopeHandle, "main", "later", later, 10);
            AppUIFocusNodeAddress firstAddress = Register(
                runtime.ScopeHandle,
                "main",
                "first",
                first,
                1);
            Activate(runtime, 1);

            AppUIFocusRequestResult result = runtime.ScopeHandle.FocusGroupFirst(
                "main",
                AppUIFocusChangeReason.Programmatic);

            Assert.That(result, Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(first.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(firstAddress));
        }

        [Test]
        public void ScopeAnchor_NodeAddress_ResolvesThroughRegistryBeforeNavigationCommit()
        {
            const string pageId = "AnchorPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button back = CreateButton(pageObject, "Back");
            Button first = CreateButton(pageObject, "First");
            Button selected = CreateButton(pageObject, "Selected");
            AppUIFocusNodeAddress backAddress = new AppUIFocusNodeAddress(
                "back",
                new AppUIFocusNodeKey("back"));
            AppUIFocusNodeAddress selectedAddress = new AppUIFocusNodeAddress(
                "target",
                new AppUIFocusNodeKey("selected"));
            FixedFocusAnchorTargetProvider provider =
                new FixedFocusAnchorTargetProvider(selectedAddress);
            AppUIFocusChainBuilder chainBuilder = new AppUIFocusChainBuilder();
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .SetAnchorTargetProvider(provider)
                    .SetChain(
                        chainBuilder
                            .SingleGroup("back")
                                .AtBoundary(MoveDirection.Down).FocusAnchor("selected")
                            .VerticalGroup("target")
                            .Build())
                    .AddGroup("back", order: 0)
                    .AddGroup("target", order: 1)
                    .AddNode("back", backAddress.NodeKey, back)
                    .AddNode("target", new AppUIFocusNodeKey("first"), first, 0)
                    .AddNode("target", selectedAddress.NodeKey, selected, 1)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);
            Assert.That(
                runtime.ScopeHandle.FocusNode(
                    backAddress,
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.Focused));

            AppUIFocusGroupNode groupNode =
                back.GetComponent<AppUIFocusGroupNode>();
            Assert.That(groupNode, Is.Not.Null);
            AxisEventData eventData = new AxisEventData(testEventSystem)
            {
                moveDir = MoveDirection.Down,
            };
            groupNode.OnMove(eventData);

            Assert.That(eventData.used, Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(selected.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(selectedAddress));
        }

        [Test]
        public void ScopeMovePolicy_ConsumesMoveWithoutChangingSelection()
        {
            const string pageId = "MovePolicyPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button source = CreateButton(pageObject, "Source");
            Button target = CreateButton(pageObject, "Target");
            FixedMoveInputPolicy movePolicy = new FixedMoveInputPolicy(true);
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .SetMoveInputPolicy(movePolicy)
                    .SetChain(
                        new AppUIFocusChainBuilder()
                            .VerticalGroup("main")
                            .Build())
                    .AddGroup("main")
                    .AddNode("main", new AppUIFocusNodeKey("source"), source, 0)
                    .AddNode("main", new AppUIFocusNodeKey("target"), target, 1)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);
            Assert.That(
                runtime.ScopeHandle.FocusNode(
                    new AppUIFocusNodeAddress(
                        "main",
                        new AppUIFocusNodeKey("source")),
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.Focused));

            AppUIFocusGroupNode groupNode = source.GetComponent<AppUIFocusGroupNode>();
            Assert.That(groupNode, Is.Not.Null);
            AxisEventData eventData = new AxisEventData(testEventSystem)
            {
                moveDir = MoveDirection.Down,
            };
            groupNode.OnMove(eventData);

            Assert.That(movePolicy.CallCount, Is.EqualTo(1));
            Assert.That(eventData.used, Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(source.gameObject));
        }

        [Test]
        public void DefaultFocusTarget_NodeAddress_IsResolvedBeforeLegacyProviderFallback()
        {
            const string pageId = "DefaultTargetPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            FixedDefaultFocusTargetController controller =
                pageObject.AddComponent<FixedDefaultFocusTargetController>();
            Button first = CreateButton(pageObject, "First");
            Button selected = CreateButton(pageObject, "Selected");
            AppUIFocusNodeAddress selectedAddress = new AppUIFocusNodeAddress(
                "main",
                new AppUIFocusNodeKey("selected"));
            controller.Configure(selectedAddress);
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .AddGroup("main")
                    .AddNode("main", new AppUIFocusNodeKey("first"), first, 0)
                    .AddNode("main", selectedAddress.NodeKey, selected, 1)
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
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(selected.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(selectedAddress));
        }

        [Test]
        public void CancelPolicy_ScopeAddress_PreviewsCloseWithoutWritingHistory()
        {
            TestRuntime runtime = CreateRuntime("CancelPreviewPage");
            Button close = CreateButton(runtime.Page.GameObject, "Close");
            AppUIFocusNodeAddress closeAddress = Register(
                runtime.ScopeHandle,
                "main",
                "close",
                close,
                0);
            Activate(runtime, 1);
            AppUIFocusCancelPolicy policy = new AppUIFocusCancelPolicy(
                runtime.ScopeHandle,
                closeAddress,
                close,
                null);

            Assert.That(policy.HandleCancel(), Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(close.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(closeAddress));
            Assert.That(runtime.Scope.LastFocusedAddress.IsValid, Is.False);
        }

        [Test]
        public void Commit_StaleStackRevision_IsRejectedWithoutChangingSelection()
        {
            TestRuntime runtime = CreateRuntime("StalePage");
            Button button = CreateButton(runtime.Page.GameObject, "Target");
            Register(runtime.ScopeHandle, "main", "target", button, 0);
            Activate(runtime, 1);
            Assert.That(
                runtime.FocusService.NodeRegistry.TryResolveNode(
                    button,
                    out AppUIFocusResolvedNode resolvedNode),
                Is.True);
            Assert.That(
                runtime.Scope.TryCreateCommitRequest(
                    resolvedNode,
                    AppUIFocusChangeReason.Navigation,
                    out AppUIFocusCommitRequest request,
                    out _),
                Is.True);

            Activate(runtime, 2);
            AppUIFocusRequestResult result =
                runtime.FocusService.Committer.Commit(in request);

            Assert.That(result, Is.EqualTo(AppUIFocusRequestResult.StaleRevision));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
            Assert.That(runtime.Scope.LastFocusedAddress.IsValid, Is.False);
        }

        [Test]
        public void Commit_ReentrantOnSelect_UsesSinglePendingSlotWithoutRecursiveFocusWrite()
        {
            TestRuntime runtime = CreateRuntime("ReentrantPage");
            Button first = CreateButton(runtime.Page.GameObject, "First");
            Button second = CreateButton(runtime.Page.GameObject, "Second");
            ReentrantFocusRequestHandler handler =
                first.gameObject.AddComponent<ReentrantFocusRequestHandler>();
            Register(runtime.ScopeHandle, "main", "first", first, 0);
            AppUIFocusNodeAddress secondAddress = Register(
                runtime.ScopeHandle,
                "main",
                "second",
                second,
                1);
            handler.Configure(runtime.Scope, second);
            Activate(runtime, 1);

            AppUIFocusRequestResult result = runtime.Scope.CommitFocus(
                first,
                AppUIFocusChangeReason.Navigation);

            Assert.That(result, Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(handler.ReentrantResult, Is.EqualTo(AppUIFocusRequestResult.Deferred));
            Assert.That(handler.SelectCallbackCount, Is.EqualTo(1));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(second.gameObject));
            Assert.That(runtime.Scope.LastFocusedAddress, Is.EqualTo(secondAddress));
        }

        [Test]
        public void Observer_ExternalRegisteredSelection_IsAcceptedWithoutSecondFocusWrite()
        {
            TestRuntime runtime = CreateRuntime("ExternalPage");
            Button first = CreateButton(runtime.Page.GameObject, "First");
            Button second = CreateButton(runtime.Page.GameObject, "Second");
            Register(runtime.ScopeHandle, "main", "first", first, 0);
            AppUIFocusNodeAddress secondAddress = Register(
                runtime.ScopeHandle,
                "main",
                "second",
                second,
                1);
            Activate(runtime, 1);
            Assert.That(
                runtime.Scope.CommitFocus(
                    first,
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.Focused));

            EventSystem.current.SetSelectedGameObject(second.gameObject);
            runtime.FocusService.ReconcileSelection();

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(second.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(secondAddress));
            Assert.That(runtime.Scope.LastFocusedAddress, Is.EqualTo(secondAddress));
        }

        [Test]
        public void Observer_UnregisteredSelection_RepairsOnceToLatestValidHistory()
        {
            TestRuntime runtime = CreateRuntime("RepairPage");
            Button first = CreateButton(runtime.Page.GameObject, "First");
            AppUIFocusNodeAddress firstAddress = Register(
                runtime.ScopeHandle,
                "main",
                "first",
                first,
                0);
            GameObject externalObject = CreateObject("External");
            Activate(runtime, 1);
            runtime.Scope.CommitFocus(first, AppUIFocusChangeReason.Navigation);

            EventSystem.current.SetSelectedGameObject(externalObject);
            runtime.FocusService.ReconcileSelection();

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(first.gameObject));
            Assert.That(runtime.Scope.LastFocusedAddress, Is.EqualTo(firstAddress));
            Assert.That(
                ((UIFocusCommitter)runtime.FocusService.Committer).HasPendingRepair,
                Is.False);

            runtime.FocusService.ReconcileSelection();
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(first.gameObject));
        }

        [Test]
        public void SuspendedScope_DefersRequest_AndRepairsFromLatestStructureWhenReactivated()
        {
            TestRuntime runtime = CreateRuntime("SuspendedPage");
            Button first = CreateButton(runtime.Page.GameObject, "First");
            AppUIFocusNodeAddress firstAddress = Register(
                runtime.ScopeHandle,
                "main",
                "first",
                first,
                0);
            Activate(runtime, 1);
            runtime.FocusService.ApplyInteractionSnapshot(
                CreateSnapshot(2, runtime.Page.ToInteractionHandle(), false));

            AppUIFocusRequestResult deferred = runtime.Scope.CommitFocus(
                first,
                AppUIFocusChangeReason.Navigation);
            Assert.That(
                deferred,
                Is.EqualTo(AppUIFocusRequestResult.DeferredWhileSuspended));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);

            Activate(runtime, 3);
            runtime.FocusService.ReconcileSelection();

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(first.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(firstAddress));
        }

        [Test]
        public void CancelPreviewAndRestore_DoNotOverwriteNavigationHistory()
        {
            TestRuntime runtime = CreateRuntime("HistoryPage");
            Button content = CreateButton(runtime.Page.GameObject, "Content");
            Button close = CreateButton(runtime.Page.GameObject, "Close");
            AppUIFocusNodeAddress contentAddress = Register(
                runtime.ScopeHandle,
                "main",
                "content",
                content,
                0);
            Register(runtime.ScopeHandle, "main", "close", close, 1);
            Activate(runtime, 1);
            runtime.Scope.CommitFocus(content, AppUIFocusChangeReason.Navigation);

            runtime.Scope.CommitFocus(close, AppUIFocusChangeReason.CancelPreview);
            Assert.That(runtime.Scope.CurrentFocusedAddress.NodeKey.Value, Is.EqualTo("close"));
            Assert.That(runtime.Scope.LastFocusedAddress, Is.EqualTo(contentAddress));

            Assert.That(
                runtime.FocusService.TryHandleSemanticSelection(
                    runtime.Page,
                    AppUIFocusChangeReason.SelectionRepair),
                Is.True);
            Assert.That(runtime.Scope.LastFocusedAddress, Is.EqualTo(contentAddress));

            runtime.FocusService.RestoreFocus(runtime.Page);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(content.gameObject));
            Assert.That(runtime.Scope.LastFocusedAddress, Is.EqualTo(contentAddress));
        }

        private TestRuntime CreateRuntime(string pageId)
        {
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .AddGroup("main")
                    .Build();
            return CreateRuntime(pageId, pageObject, definition);
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
            IAppUIFocusScopeHandle scopeHandle = focusService.AttachScope(
                page,
                definition);
            return new TestRuntime(
                registry,
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

        private AppUIFocusNodeAddress Register(
            IAppUIFocusScopeHandle scope,
            string groupId,
            string nodeKey,
            Selectable selectable,
            int order)
        {
            AppUIFocusNodeKey key = new AppUIFocusNodeKey(nodeKey);
            Assert.That(
                scope.RegisterNode(groupId, key, selectable, order),
                Is.True);
            return new AppUIFocusNodeAddress(groupId, key);
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
                UIPageInstanceRegistry registry,
                UIPageInstance page,
                UIFocusService focusService,
                IAppUIFocusScopeHandle scopeHandle,
                AppUIFocusScope scope)
            {
                Registry = registry;
                Page = page;
                FocusService = focusService;
                ScopeHandle = scopeHandle;
                Scope = scope;
            }

            public UIPageInstanceRegistry Registry { get; }

            public UIPageInstance Page { get; }

            public UIFocusService FocusService { get; }

            public IAppUIFocusScopeHandle ScopeHandle { get; }

            public AppUIFocusScope Scope { get; }
        }

        private sealed class FixedFocusAnchorTargetProvider :
            IAppUIFocusAnchorTargetProvider
        {
            private readonly AppUIFocusNodeAddress targetAddress;

            public FixedFocusAnchorTargetProvider(
                AppUIFocusNodeAddress focusTargetAddress)
            {
                targetAddress = focusTargetAddress;
            }

            public bool TryGetFocusAnchor(
                string anchorId,
                out AppUIFocusTarget target)
            {
                if (anchorId == "selected")
                {
                    target = AppUIFocusTarget.FromNodeAddress(targetAddress);
                    return true;
                }

                target = default;
                return false;
            }
        }

        private sealed class FixedMoveInputPolicy : IAppUIFocusMoveInputPolicy
        {
            private readonly bool shouldConsume;

            public FixedMoveInputPolicy(bool consume)
            {
                shouldConsume = consume;
            }

            public int CallCount { get; private set; }

            public bool ShouldConsumeWithoutNavigation(AxisEventData eventData)
            {
                CallCount++;
                return shouldConsume;
            }
        }
    }

    internal sealed class ReentrantFocusRequestHandler : MonoBehaviour, ISelectHandler
    {
        private AppUIFocusScope scope;
        private Selectable target;

        public int SelectCallbackCount { get; private set; }

        public AppUIFocusRequestResult ReentrantResult { get; private set; }

        public void Configure(AppUIFocusScope focusScope, Selectable focusTarget)
        {
            scope = focusScope;
            target = focusTarget;
        }

        public void OnSelect(BaseEventData eventData)
        {
            SelectCallbackCount++;
            ReentrantResult = scope.CommitFocus(
                target,
                AppUIFocusChangeReason.Navigation);
        }
    }

    internal sealed class FixedDefaultFocusTargetController :
        PanelBaseController,
        IAppUIDefaultFocusTargetProvider
    {
        private AppUIFocusNodeAddress targetAddress;

        public void Configure(AppUIFocusNodeAddress focusTargetAddress)
        {
            targetAddress = focusTargetAddress;
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
