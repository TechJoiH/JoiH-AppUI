using System.Collections.Generic;
using System.Reflection;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusSpatialNavigationTests
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
        public void SpatialGroup_PrimaryAxisDistanceWinsAndUnusableCandidateIsSkipped()
        {
            const string pageId = "SpatialPrimaryPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button source = CreateButton(pageObject, "Source", Vector2.zero);
            Button nearer = CreateButton(pageObject, "Nearer", new Vector2(40f, 40f));
            Button aligned = CreateButton(pageObject, "Aligned", new Vector2(60f, 0f));
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .SetChain(
                        new AppUIFocusChainBuilder()
                            .SpatialGroup("spatial")
                            .Build())
                    .AddGroup("spatial")
                    .AddNode("spatial", new AppUIFocusNodeKey("source"), source, 0)
                    .AddNode("spatial", new AppUIFocusNodeKey("nearer"), nearer, 1)
                    .AddNode("spatial", new AppUIFocusNodeKey("aligned"), aligned, 2)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);

            Focus(runtime, "spatial", "source");
            Move(source, MoveDirection.Right);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(nearer.gameObject),
                "Primary-axis distance is the first spatial sort key.");

            nearer.interactable = false;
            Focus(runtime, "spatial", "source");
            Move(source, MoveDirection.Right);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(aligned.gameObject));
        }

        [Test]
        public void SpatialGroup_OverlapThenOffsetThenOrderBreakTiesDeterministically()
        {
            const string pageId = "SpatialTiePage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button source = CreateButton(pageObject, "Source", Vector2.zero);
            Button offset = CreateButton(pageObject, "Offset", new Vector2(50f, 30f));
            Button highOrder = CreateButton(pageObject, "HighOrder", new Vector2(50f, 0f));
            Button lowOrder = CreateButton(pageObject, "LowOrder", new Vector2(50f, 0f));
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .SetChain(
                        new AppUIFocusChainBuilder()
                            .SpatialGroup("spatial")
                            .Build())
                    .AddGroup("spatial")
                    .AddNode("spatial", new AppUIFocusNodeKey("source"), source, 0)
                    .AddNode("spatial", new AppUIFocusNodeKey("offset"), offset, 1)
                    .AddNode("spatial", new AppUIFocusNodeKey("high"), highOrder, 20)
                    .AddNode("spatial", new AppUIFocusNodeKey("low"), lowOrder, 5)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);

            Focus(runtime, "spatial", "source");
            Move(source, MoveDirection.Right);

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(lowOrder.gameObject));
        }

        [Test]
        public void SpatialGroup_LayoutMovementRefreshesCachedGeometry()
        {
            const string pageId = "SpatialRefreshPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button source = CreateButton(pageObject, "Source", Vector2.zero);
            Button first = CreateButton(pageObject, "First", new Vector2(40f, 0f));
            Button moved = CreateButton(pageObject, "Moved", new Vector2(80f, 0f));
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .SetChain(
                        new AppUIFocusChainBuilder()
                            .SpatialGroup("spatial")
                            .Build())
                    .AddGroup("spatial")
                    .AddNode("spatial", new AppUIFocusNodeKey("source"), source, 0)
                    .AddNode("spatial", new AppUIFocusNodeKey("first"), first, 1)
                    .AddNode("spatial", new AppUIFocusNodeKey("moved"), moved, 2)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);

            Focus(runtime, "spatial", "source");
            Move(source, MoveDirection.Right);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(first.gameObject));

            ((RectTransform)moved.transform).anchoredPosition = new Vector2(20f, 0f);
            Focus(runtime, "spatial", "source");
            Move(source, MoveDirection.Right);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(moved.gameObject));
        }

        [Test]
        public void RegionAutoAdjacent_ChoosesNearestNodeFromOpenGroupsOnly()
        {
            const string pageId = "AutoAdjacentPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button source = CreateButton(pageObject, "Source", Vector2.zero);
            Button near = CreateButton(pageObject, "Near", new Vector2(30f, 0f));
            Button far = CreateButton(pageObject, "Far", new Vector2(80f, 0f));
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .SetRegionAutoAdjacent(AppUIFocusDefinition.RootRegionId)
                    .SetChain(
                        new AppUIFocusChainBuilder()
                            .SingleGroup("source")
                            .SingleGroup("near")
                            .SingleGroup("far")
                            .Build())
                    .AddGroup("source", order: 0)
                    .AddGroup("near", order: 1)
                    .AddGroup("far", order: 2)
                    .AddNode("source", new AppUIFocusNodeKey("source"), source)
                    .AddNode("near", new AppUIFocusNodeKey("near"), near)
                    .AddNode("far", new AppUIFocusNodeKey("far"), far)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);

            Focus(runtime, "source", "source");
            Move(source, MoveDirection.Right);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(near.gameObject));

            Assert.That(runtime.Scope.CloseGroup("near"), Is.True);
            Focus(runtime, "source", "source");
            Move(source, MoveDirection.Right);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(far.gameObject));
        }

        [Test]
        public void RegionExplicitAdjacency_WinsBeforeAutoAdjacent()
        {
            const string pageId = "ExplicitBeforeAutoPage";
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
            Button source = CreateButton(pageObject, "Source", Vector2.zero);
            Button autoNear = CreateButton(pageObject, "AutoNear", new Vector2(30f, 0f));
            Button explicitFar = CreateButton(
                pageObject,
                "ExplicitFar",
                new Vector2(90f, 0f));
            AppUIFocusDefinition definition =
                new AppUIFocusDefinitionBuilder(pageId + "-scope")
                    .SetRegionAutoAdjacent(AppUIFocusDefinition.RootRegionId)
                    .AddRegionAdjacency(
                        AppUIFocusDefinition.RootRegionId,
                        "source",
                        MoveDirection.Right,
                        "explicit")
                    .SetChain(
                        new AppUIFocusChainBuilder()
                            .SingleGroup("source")
                            .SingleGroup("auto")
                            .SingleGroup("explicit")
                            .Build())
                    .AddGroup("source", order: 0)
                    .AddGroup("auto", order: 1)
                    .AddGroup("explicit", order: 2)
                    .AddNode("source", new AppUIFocusNodeKey("source"), source)
                    .AddNode("auto", new AppUIFocusNodeKey("auto"), autoNear)
                    .AddNode(
                        "explicit",
                        new AppUIFocusNodeKey("explicit"),
                        explicitFar)
                    .Build();
            TestRuntime runtime = CreateRuntime(pageId, pageObject, definition);
            Activate(runtime, 1);

            Focus(runtime, "source", "source");
            Move(source, MoveDirection.Right);

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(explicitFar.gameObject));
        }

        private TestRuntime CreateRuntime(
            string pageId,
            GameObject pageObject,
            AppUIFocusDefinition definition)
        {
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

        private void Activate(TestRuntime runtime, int revision)
        {
            UIPageInteractionHandle handle = runtime.Page.ToInteractionHandle();
            runtime.FocusService.ApplyInteractionSnapshot(
                new UIInteractionSnapshot(
                    revision,
                    handle,
                    new[] { new UIPageInteractionState(handle, true, 0, 0) }));
        }

        private static void Focus(
            TestRuntime runtime,
            string groupId,
            string nodeKey)
        {
            Assert.That(
                runtime.Scope.FocusNode(
                    new AppUIFocusNodeAddress(
                        groupId,
                        new AppUIFocusNodeKey(nodeKey)),
                    AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.Focused));
        }

        private void Move(Button source, MoveDirection moveDirection)
        {
            AxisEventData eventData = new AxisEventData(testEventSystem)
            {
                moveDir = moveDirection,
            };
            source.GetComponent<AppUIFocusGroupNode>().OnMove(eventData);
            Assert.That(eventData.used, Is.True);
        }

        private Button CreateButton(
            GameObject parent,
            string name,
            Vector2 anchoredPosition)
        {
            GameObject buttonObject = CreateObject(name, typeof(RectTransform));
            RectTransform rectTransform = (RectTransform)buttonObject.transform;
            rectTransform.SetParent(parent.transform, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(20f, 20f);
            rectTransform.anchoredPosition = anchoredPosition;
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
