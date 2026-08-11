using System.Collections.Generic;
using System.Reflection;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusRegionTests
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
        public void ChildRegion_OpenSuspendsParent_AndCloseRestoresSourceBeforeHistory()
        {
            TestRuntime runtime = CreateRuntime(
                "RegionRestore",
                new AppUIFocusDefinitionBuilder("region-restore")
                    .AddRegion("popup", AppUIFocusDefinition.RootRegionId, "popup-group")
                    .AddGroup("main")
                    .AddGroup("popup-group", "popup")
                    .Build());
            Button historyButton = CreateButton(runtime.Page.GameObject, "History");
            Button sourceButton = CreateButton(runtime.Page.GameObject, "Source");
            Button popupButton = CreateButton(runtime.Page.GameObject, "Popup");
            AppUIFocusNodeAddress historyAddress = Register(
                runtime.Scope,
                "main",
                "history",
                historyButton,
                0);
            AppUIFocusNodeAddress sourceAddress = Register(
                runtime.Scope,
                "main",
                "source",
                sourceButton,
                1);
            Register(runtime.Scope, "popup-group", "popup", popupButton, 0);
            Activate(runtime, 1);

            Assert.That(
                runtime.Scope.FocusNode(historyAddress, AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(
                runtime.Scope.FocusNode(sourceAddress, AppUIFocusChangeReason.Programmatic),
                Is.EqualTo(AppUIFocusRequestResult.Focused));

            Assert.That(
                runtime.Scope.OpenRegion("popup", AppUIFocusRegionEntryPolicy.Default),
                Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(
                runtime.Scope.GetRegionStatus(AppUIFocusDefinition.RootRegionId),
                Is.EqualTo(AppUIFocusRegionStatus.Suspended));
            Assert.That(
                runtime.Scope.GetRegionStatus("popup"),
                Is.EqualTo(AppUIFocusRegionStatus.Active));
            Assert.That(runtime.Scope.ActiveRegionId, Is.EqualTo("popup"));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(popupButton.gameObject));

            Assert.That(
                runtime.Scope.CloseRegion("popup"),
                Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(runtime.Scope.ActiveRegionId, Is.EqualTo(AppUIFocusDefinition.RootRegionId));
            Assert.That(
                runtime.Scope.GetRegionStatus("popup"),
                Is.EqualTo(AppUIFocusRegionStatus.Closed));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(sourceButton.gameObject),
                "SourceNodeAddress must win over the older parent Region history.");
        }

        [Test]
        public void ChildRegion_ReopenRestoresItsOwnHistory()
        {
            TestRuntime runtime = CreateRuntime(
                "RegionHistory",
                new AppUIFocusDefinitionBuilder("region-history")
                    .AddRegion("popup", AppUIFocusDefinition.RootRegionId, "popup-group")
                    .AddGroup("main")
                    .AddGroup("popup-group", "popup")
                    .Build());
            Button root = CreateButton(runtime.Page.GameObject, "Root");
            Button first = CreateButton(runtime.Page.GameObject, "First");
            Button second = CreateButton(runtime.Page.GameObject, "Second");
            AppUIFocusNodeAddress rootAddress = Register(runtime.Scope, "main", "root", root, 0);
            Register(runtime.Scope, "popup-group", "first", first, 0);
            AppUIFocusNodeAddress secondAddress = Register(
                runtime.Scope,
                "popup-group",
                "second",
                second,
                1);
            Activate(runtime, 1);
            runtime.Scope.FocusNode(rootAddress, AppUIFocusChangeReason.Navigation);

            runtime.Scope.OpenRegion("popup", AppUIFocusRegionEntryPolicy.Default);
            runtime.Scope.FocusNode(secondAddress, AppUIFocusChangeReason.Navigation);
            runtime.Scope.CloseRegion("popup");

            Assert.That(
                runtime.Scope.OpenRegion(
                    "popup",
                    AppUIFocusRegionEntryPolicy.LastFocusedOrDefault),
                Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(second.gameObject));
        }

        [Test]
        public void OpeningSibling_ClosesPreviousBranchAndKeepsSingleActiveLeaf()
        {
            TestRuntime runtime = CreateRuntime(
                "RegionSibling",
                new AppUIFocusDefinitionBuilder("region-sibling")
                    .AddRegion("first", AppUIFocusDefinition.RootRegionId, "first-group")
                    .AddRegion("nested", "first", "nested-group")
                    .AddRegion("second", AppUIFocusDefinition.RootRegionId, "second-group")
                    .AddGroup("main")
                    .AddGroup("first-group", "first")
                    .AddGroup("nested-group", "nested")
                    .AddGroup("second-group", "second")
                    .Build());
            Register(runtime.Scope, "main", "root", CreateButton(runtime.Page.GameObject, "Root"), 0);
            Register(runtime.Scope, "first-group", "first", CreateButton(runtime.Page.GameObject, "First"), 0);
            Register(runtime.Scope, "nested-group", "nested", CreateButton(runtime.Page.GameObject, "Nested"), 0);
            Button second = CreateButton(runtime.Page.GameObject, "Second");
            Register(runtime.Scope, "second-group", "second", second, 0);
            Activate(runtime, 1);
            runtime.Scope.FocusGroupFirst("main", AppUIFocusChangeReason.Navigation);

            runtime.Scope.OpenRegion("first");
            runtime.Scope.OpenRegion("nested");
            Assert.That(runtime.Scope.ActiveRegionId, Is.EqualTo("nested"));

            Assert.That(
                runtime.Scope.OpenRegion("second"),
                Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(runtime.Scope.GetRegionStatus("first"), Is.EqualTo(AppUIFocusRegionStatus.Closed));
            Assert.That(runtime.Scope.GetRegionStatus("nested"), Is.EqualTo(AppUIFocusRegionStatus.Closed));
            Assert.That(runtime.Scope.GetRegionStatus("second"), Is.EqualTo(AppUIFocusRegionStatus.Active));
            Assert.That(runtime.Scope.ActiveRegionId, Is.EqualTo("second"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(second.gameObject));
        }

        [Test]
        public void ScopeSuspendResume_RestoresOnlyPreviousLeafAsActive()
        {
            TestRuntime runtime = CreateRuntime(
                "RegionSuspend",
                new AppUIFocusDefinitionBuilder("region-suspend")
                    .AddRegion("popup", AppUIFocusDefinition.RootRegionId, "popup-group")
                    .AddGroup("main")
                    .AddGroup("popup-group", "popup")
                    .Build());
            Register(runtime.Scope, "main", "root", CreateButton(runtime.Page.GameObject, "Root"), 0);
            Register(runtime.Scope, "popup-group", "popup", CreateButton(runtime.Page.GameObject, "Popup"), 0);
            Activate(runtime, 1);
            runtime.Scope.FocusGroupFirst("main", AppUIFocusChangeReason.Navigation);
            runtime.Scope.OpenRegion("popup");

            runtime.FocusService.ApplyInteractionSnapshot(UIInteractionSnapshot.Empty);
            Assert.That(runtime.Scope.Status, Is.EqualTo(AppUIFocusScopeStatus.Suspended));
            Assert.That(runtime.Scope.ActiveRegionId, Is.Empty);
            Assert.That(
                runtime.Scope.GetRegionStatus(AppUIFocusDefinition.RootRegionId),
                Is.EqualTo(AppUIFocusRegionStatus.Suspended));
            Assert.That(
                runtime.Scope.GetRegionStatus("popup"),
                Is.EqualTo(AppUIFocusRegionStatus.Suspended));

            Activate(runtime, 2);
            Assert.That(runtime.Scope.ActiveRegionId, Is.EqualTo("popup"));
            Assert.That(
                runtime.Scope.GetRegionStatus(AppUIFocusDefinition.RootRegionId),
                Is.EqualTo(AppUIFocusRegionStatus.Suspended));
            Assert.That(
                runtime.Scope.GetRegionStatus("popup"),
                Is.EqualTo(AppUIFocusRegionStatus.Active));
        }

        [Test]
        public void SemanticRegionActions_OpenChildAndExitToRecordedSource()
        {
            AppUIFocusChain chain = new AppUIFocusChainBuilder()
                .SingleGroup("main")
                    .AtBoundary(MoveDirection.Down).FocusRegionDefault("popup")
                .SingleGroup("popup-group")
                    .AtBoundary(MoveDirection.Up).ExitToParentRegion()
                .Build();
            TestRuntime runtime = CreateRuntime(
                "RegionActions",
                new AppUIFocusDefinitionBuilder("region-actions")
                    .AddRegion("popup", AppUIFocusDefinition.RootRegionId, "popup-group")
                    .AddGroup("main")
                    .AddGroup("popup-group", "popup")
                    .SetChain(chain)
                    .Build());
            Button root = CreateButton(runtime.Page.GameObject, "Root");
            Button popup = CreateButton(runtime.Page.GameObject, "Popup");
            Register(runtime.Scope, "main", "root", root, 0);
            Register(runtime.Scope, "popup-group", "popup", popup, 0);
            Activate(runtime, 1);
            runtime.Scope.FocusGroupFirst("main", AppUIFocusChangeReason.Navigation);

            AxisEventData down = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Down,
            };
            root.GetComponent<AppUIFocusGroupNode>().OnMove(down);
            Assert.That(down.used, Is.True);
            Assert.That(runtime.Scope.ActiveRegionId, Is.EqualTo("popup"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(popup.gameObject));

            AxisEventData up = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Up,
            };
            popup.GetComponent<AppUIFocusGroupNode>().OnMove(up);
            Assert.That(up.used, Is.True);
            Assert.That(runtime.Scope.ActiveRegionId, Is.EqualTo(AppUIFocusDefinition.RootRegionId));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(root.gameObject));
        }

        [Test]
        public void RegionAdjacency_RoutesOnlyWithinActiveRegion()
        {
            AppUIFocusChain chain = new AppUIFocusChainBuilder()
                .SingleGroup("left")
                .SingleGroup("right")
                .Build();
            TestRuntime runtime = CreateRuntime(
                "RegionAdjacency",
                new AppUIFocusDefinitionBuilder("region-adjacency")
                    .AddGroup("left")
                    .AddGroup("right")
                    .AddRegionAdjacency(
                        AppUIFocusDefinition.RootRegionId,
                        "left",
                        MoveDirection.Right,
                        "right")
                    .SetChain(chain)
                    .Build());
            Button left = CreateButton(runtime.Page.GameObject, "Left");
            Button right = CreateButton(runtime.Page.GameObject, "Right");
            Register(runtime.Scope, "left", "left", left, 0);
            AppUIFocusNodeAddress rightAddress = Register(
                runtime.Scope,
                "right",
                "right",
                right,
                0);
            Activate(runtime, 1);
            runtime.Scope.FocusGroupFirst("left", AppUIFocusChangeReason.Navigation);

            AxisEventData move = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Right,
            };
            left.GetComponent<AppUIFocusGroupNode>().OnMove(move);

            Assert.That(move.used, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(right.gameObject));
            Assert.That(
                ((AppUIFocusScope)runtime.Scope).LastFocusedAddress,
                Is.EqualTo(rightAddress),
                "A semantic cross-Group move must write navigation history.");
        }

        [Test]
        public void RegionLifecycle_InvalidatesCommitRequestCapturedBeforeLeafChange()
        {
            TestRuntime runtime = CreateRuntime(
                "RegionRevision",
                new AppUIFocusDefinitionBuilder("region-revision")
                    .AddRegion("popup", AppUIFocusDefinition.RootRegionId, "popup-group")
                    .AddGroup("main")
                    .AddGroup("popup-group", "popup")
                    .Build());
            Button root = CreateButton(runtime.Page.GameObject, "Root");
            Button popup = CreateButton(runtime.Page.GameObject, "Popup");
            AppUIFocusNodeAddress rootAddress = Register(
                runtime.Scope,
                "main",
                "root",
                root,
                0);
            Register(runtime.Scope, "popup-group", "popup", popup, 0);
            Activate(runtime, 1);
            AppUIFocusScope concreteScope = (AppUIFocusScope)runtime.Scope;
            Assert.That(
                runtime.FocusService.NodeRegistry.TryResolveNode(
                    runtime.Page.ToInteractionHandle(),
                    rootAddress,
                    out AppUIFocusResolvedNode rootNode),
                Is.True);
            Assert.That(
                concreteScope.TryCreateCommitRequest(
                    rootNode,
                    AppUIFocusChangeReason.Navigation,
                    out AppUIFocusCommitRequest staleRequest,
                    out _),
                Is.True);

            runtime.Scope.OpenRegion("popup");
            runtime.Scope.CloseRegion("popup");

            Assert.That(
                runtime.FocusService.Committer.Commit(in staleRequest),
                Is.EqualTo(AppUIFocusRequestResult.StaleRevision));
        }

        private TestRuntime CreateRuntime(
            string pageId,
            AppUIFocusDefinition definition)
        {
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            UIPageInstanceRegistry registry = new UIPageInstanceRegistry();
            UIPageInstance page = new UIPageInstance
            {
                PageId = pageId,
                GameObject = pageObject,
                RectTransform = pageObject.transform as RectTransform,
                OperationVersion = 1,
                State = UIPageState.Open,
                StackVisible = true,
            };
            registry.Register(page);

            UIFocusService focusService = new UIFocusService();
            focusService.ConfigureInstanceRegistry(registry);
            IAppUIFocusScopeHandle scope = focusService.AttachScope(page, definition);
            return new TestRuntime(page, focusService, scope);
        }

        private void Activate(TestRuntime runtime, int stackRevision)
        {
            UIPageInteractionHandle handle = runtime.Page.ToInteractionHandle();
            runtime.FocusService.ApplyInteractionSnapshot(
                new UIInteractionSnapshot(
                    stackRevision,
                    handle,
                    new[] { new UIPageInteractionState(handle, true, 0, 0) }));
        }

        private static AppUIFocusNodeAddress Register(
            IAppUIFocusScopeHandle scope,
            string groupId,
            string nodeKey,
            Selectable selectable,
            int order)
        {
            AppUIFocusNodeKey key = new AppUIFocusNodeKey(nodeKey);
            Assert.That(scope.RegisterNode(groupId, key, selectable, order), Is.True);
            return new AppUIFocusNodeAddress(groupId, key);
        }

        private Button CreateButton(GameObject parent, string name)
        {
            GameObject gameObject = CreateObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent.transform, false);
            Button button = gameObject.AddComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }

        private GameObject CreateObject(string name, params System.Type[] components)
        {
            GameObject gameObject = components != null && components.Length > 0
                ? new GameObject(name, components)
                : new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
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
                IAppUIFocusScopeHandle scope)
            {
                Page = page;
                FocusService = focusService;
                Scope = scope;
            }

            public UIPageInstance Page { get; }

            public UIFocusService FocusService { get; }

            public IAppUIFocusScopeHandle Scope { get; }
        }
    }
}
