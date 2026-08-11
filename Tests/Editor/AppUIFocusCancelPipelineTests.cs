using System;
using System.Collections.Generic;
using System.Reflection;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusCancelPipelineTests
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
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            AppUIInteractionSourceAuthority.Reset();
        }

        [Test]
        public void InputField_EditCancel_IsConsumedBeforePageAndLeavesNodeFocused()
        {
            TestRuntime runtime = CreateRuntime(
                "InputCancel",
                new AppUIFocusDefinitionBuilder("input-cancel")
                    .AddGroup("main")
                    .Build());
            InputField inputField = CreateInputField(runtime.Page.GameObject, "NameInput");
            AppUIFocusNodeAddress address = Register(
                runtime.Scope,
                "main",
                "name",
                inputField,
                null,
                0);
            Activate(runtime, 1);
            Assert.That(
                runtime.Scope.FocusNode(address, AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.Focused));
            SetInputFieldEditing(inputField, true);
            Assert.That(inputField.isFocused, Is.True);

            Assert.That(
                runtime.FocusService.TryHandleCancel(runtime.Page, out Exception exception),
                Is.EqualTo(AppUIFocusCancelDispatchResult.Consumed));
            Assert.That(exception, Is.Null);
            Assert.That(inputField.isFocused, Is.False);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(inputField.gameObject));

            Assert.That(
                runtime.FocusService.TryHandleCancel(runtime.Page, out exception),
                Is.EqualTo(AppUIFocusCancelDispatchResult.Continue),
                "Browse mode must allow the Cancel request to continue to the page stages.");
            Assert.That(exception, Is.Null);
        }

        [Test]
        public void ActiveChildRegion_CancelClosesRegionAndRestoresSourceNode()
        {
            RecordingRegionCancelHandler regionHandler =
                new RecordingRegionCancelHandler(AppUIFocusCancelHandlingResult.Consumed);
            TestRuntime runtime = CreateRuntime(
                "RegionCancel",
                new AppUIFocusDefinitionBuilder("region-cancel")
                    .AddRegion("popup", AppUIFocusDefinition.RootRegionId, "popup-group")
                    .SetRegionCancelHandler("popup", regionHandler)
                    .AddGroup("main")
                    .AddGroup("popup-group", "popup")
                    .Build());
            Button source = CreateButton(runtime.Page.GameObject, "Source");
            Button popup = CreateButton(runtime.Page.GameObject, "Popup");
            AppUIFocusNodeAddress sourceAddress = Register(
                runtime.Scope,
                "main",
                "source",
                source,
                null,
                0);
            Register(runtime.Scope, "popup-group", "popup", popup, null, 0);
            Activate(runtime, 1);
            runtime.Scope.FocusNode(sourceAddress, AppUIFocusChangeReason.Navigation);
            runtime.Scope.OpenRegion("popup", AppUIFocusRegionEntryPolicy.Default);

            Assert.That(
                runtime.FocusService.TryHandleCancel(runtime.Page, out Exception exception),
                Is.EqualTo(AppUIFocusCancelDispatchResult.Consumed));
            Assert.That(exception, Is.Null);
            Assert.That(regionHandler.CallCount, Is.EqualTo(1));
            Assert.That(regionHandler.LastContext.SourceNodeAddress, Is.EqualTo(sourceAddress));
            Assert.That(
                runtime.Scope.GetRegionStatus("popup"),
                Is.EqualTo(AppUIFocusRegionStatus.Closed));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(source.gameObject));
        }

        [Test]
        public void CurrentControlPolicy_ConsumesBeforeActiveChildRegion()
        {
            RecordingRegionCancelHandler regionHandler =
                new RecordingRegionCancelHandler(AppUIFocusCancelHandlingResult.Consumed);
            RecordingControlPolicy controlPolicy =
                new RecordingControlPolicy(AppUIFocusCancelHandlingResult.Consumed);
            TestRuntime runtime = CreateRuntime(
                "ControlBeforeRegion",
                new AppUIFocusDefinitionBuilder("control-before-region")
                    .AddRegion("popup", AppUIFocusDefinition.RootRegionId, "popup-group")
                    .SetRegionCancelHandler("popup", regionHandler)
                    .AddGroup("main")
                    .AddGroup("popup-group", "popup")
                    .Build());
            Button source = CreateButton(runtime.Page.GameObject, "Source");
            Button popup = CreateButton(runtime.Page.GameObject, "Popup");
            Register(runtime.Scope, "main", "source", source, null, 0);
            Register(runtime.Scope, "popup-group", "popup", popup, controlPolicy, 0);
            Activate(runtime, 1);
            runtime.Scope.FocusGroupFirst("main", AppUIFocusChangeReason.Navigation);
            runtime.Scope.OpenRegion("popup", AppUIFocusRegionEntryPolicy.Default);

            Assert.That(
                runtime.FocusService.TryHandleCancel(runtime.Page, out Exception exception),
                Is.EqualTo(AppUIFocusCancelDispatchResult.Consumed));
            Assert.That(exception, Is.Null);
            Assert.That(controlPolicy.CancelCallCount, Is.EqualTo(1));
            Assert.That(regionHandler.CallCount, Is.Zero);
            Assert.That(
                runtime.Scope.GetRegionStatus("popup"),
                Is.EqualTo(AppUIFocusRegionStatus.Active));
        }

        [Test]
        public void DropdownRegionPolicy_CollapsesAtRegionStageAndRestoresDropdown()
        {
            Dropdown dropdown = null;
            AppUIFocusDropdownControlPolicy dropdownPolicy = null;
            AppUIFocusDefinitionBuilder builder =
                new AppUIFocusDefinitionBuilder("dropdown-cancel")
                    .AddRegion("options", AppUIFocusDefinition.RootRegionId, "options-group")
                    .AddGroup("main")
                    .AddGroup("options-group", "options");
            TestRuntime runtime = CreateRuntime("DropdownCancel", builder.Build());
            dropdown = CreateDropdown(runtime.Page.GameObject, "Dropdown");
            dropdownPolicy = new AppUIFocusDropdownControlPolicy(dropdown, "options");

            // Policy depends on the concrete Dropdown, so rebuild the runtime definition once.
            runtime.Dispose();
            runtime = CreateRuntime(
                "DropdownCancelBound",
                new AppUIFocusDefinitionBuilder("dropdown-cancel-bound")
                    .AddRegion("options", AppUIFocusDefinition.RootRegionId, "options-group")
                    .SetRegionCancelHandler("options", dropdownPolicy)
                    .AddGroup("main")
                    .AddGroup("options-group", "options")
                    .Build(),
                dropdown.transform.parent.gameObject);
            Button option = CreateButton(runtime.Page.GameObject, "Option");
            AppUIFocusNodeAddress dropdownAddress = Register(
                runtime.Scope,
                "main",
                "dropdown",
                dropdown,
                dropdownPolicy,
                0);
            Register(runtime.Scope, "options-group", "option", option, null, 0);
            Activate(runtime, 1);
            runtime.Scope.FocusNode(dropdownAddress, AppUIFocusChangeReason.Navigation);
            runtime.Scope.OpenRegion("options", AppUIFocusRegionEntryPolicy.Default);

            Assert.That(
                runtime.FocusService.TryHandleCancel(runtime.Page, out Exception exception),
                Is.EqualTo(AppUIFocusCancelDispatchResult.Consumed));
            Assert.That(exception, Is.Null);
            Assert.That(runtime.Scope.GetRegionStatus("options"), Is.EqualTo(AppUIFocusRegionStatus.Closed));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(dropdown.gameObject));
        }

        [Test]
        public void ThrowingControlPolicy_BlocksLowerCancelStages()
        {
            RecordingRegionCancelHandler regionHandler =
                new RecordingRegionCancelHandler(AppUIFocusCancelHandlingResult.Consumed);
            TestRuntime runtime = CreateRuntime(
                "CancelFailure",
                new AppUIFocusDefinitionBuilder("cancel-failure")
                    .AddRegion("popup", AppUIFocusDefinition.RootRegionId, "popup-group")
                    .SetRegionCancelHandler("popup", regionHandler)
                    .AddGroup("main")
                    .AddGroup("popup-group", "popup")
                    .Build());
            Register(runtime.Scope, "main", "source", CreateButton(runtime.Page.GameObject, "Source"), null, 0);
            Register(
                runtime.Scope,
                "popup-group",
                "popup",
                CreateButton(runtime.Page.GameObject, "Popup"),
                new ThrowingCancelControlPolicy(),
                0);
            Activate(runtime, 1);
            runtime.Scope.FocusGroupFirst("main", AppUIFocusChangeReason.Navigation);
            runtime.Scope.OpenRegion("popup", AppUIFocusRegionEntryPolicy.Default);

            Assert.That(
                runtime.FocusService.TryHandleCancel(runtime.Page, out Exception exception),
                Is.EqualTo(AppUIFocusCancelDispatchResult.Failed));
            Assert.That(exception, Is.Not.Null);
            Assert.That(regionHandler.CallCount, Is.Zero);
            Assert.That(runtime.Scope.GetRegionStatus("popup"), Is.EqualTo(AppUIFocusRegionStatus.Active));
        }

        private TestRuntime CreateRuntime(
            string pageId,
            AppUIFocusDefinition definition,
            GameObject existingPageObject = null)
        {
            GameObject pageObject = existingPageObject ?? CreateObject(pageId, typeof(RectTransform));
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

        private static void Activate(TestRuntime runtime, int stackRevision)
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
            IAppUIFocusControlPolicy controlPolicy,
            int order)
        {
            AppUIFocusNodeKey key = new AppUIFocusNodeKey(nodeKey);
            Assert.That(
                scope.RegisterNode(groupId, key, selectable, controlPolicy, order),
                Is.True);
            return new AppUIFocusNodeAddress(groupId, key);
        }

        private Button CreateButton(GameObject parent, string name)
        {
            GameObject gameObject = CreateObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent.transform, false);
            Button button = gameObject.AddComponent<Button>();
            DisableNavigation(button);
            return button;
        }

        private InputField CreateInputField(GameObject parent, string name)
        {
            GameObject gameObject = CreateObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent.transform, false);
            InputField inputField = gameObject.AddComponent<InputField>();
            GameObject textObject = CreateObject(name + "Text", typeof(RectTransform));
            textObject.transform.SetParent(gameObject.transform, false);
            Text text = textObject.AddComponent<Text>();
            inputField.textComponent = text;
            DisableNavigation(inputField);
            return inputField;
        }

        private Dropdown CreateDropdown(GameObject parent, string name)
        {
            GameObject gameObject = CreateObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent.transform, false);
            Dropdown dropdown = gameObject.AddComponent<Dropdown>();
            DisableNavigation(dropdown);
            return dropdown;
        }

        private static void DisableNavigation(Selectable selectable)
        {
            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }

        private GameObject CreateObject(string name, params Type[] components)
        {
            GameObject gameObject = components != null && components.Length > 0
                ? new GameObject(name, components)
                : new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetInputFieldEditing(InputField inputField, bool editing)
        {
            FieldInfo field = typeof(InputField).GetField(
                "m_AllowInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(inputField, editing);
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

        private sealed class RecordingControlPolicy : IAppUIFocusControlPolicy
        {
            private readonly AppUIFocusCancelHandlingResult cancelResult;

            public RecordingControlPolicy(AppUIFocusCancelHandlingResult result)
            {
                cancelResult = result;
            }

            public int CancelCallCount { get; private set; }

            public AppUIFocusControlMoveMode GetMoveMode(in AppUIFocusMoveContext context)
            {
                return AppUIFocusControlMoveMode.FrameworkOnly;
            }

            public AppUIFocusCancelHandlingResult TryHandleCancel(
                in AppUIFocusCancelContext context)
            {
                CancelCallCount++;
                return cancelResult;
            }
        }

        private sealed class ThrowingCancelControlPolicy : IAppUIFocusControlPolicy
        {
            public AppUIFocusControlMoveMode GetMoveMode(in AppUIFocusMoveContext context)
            {
                return AppUIFocusControlMoveMode.FrameworkOnly;
            }

            public AppUIFocusCancelHandlingResult TryHandleCancel(
                in AppUIFocusCancelContext context)
            {
                throw new InvalidOperationException("Cancel policy failure.");
            }
        }

        private sealed class RecordingRegionCancelHandler : IAppUIFocusRegionCancelHandler
        {
            private readonly AppUIFocusCancelHandlingResult result;

            public RecordingRegionCancelHandler(AppUIFocusCancelHandlingResult handlingResult)
            {
                result = handlingResult;
            }

            public int CallCount { get; private set; }

            public AppUIFocusRegionCancelContext LastContext { get; private set; }

            public AppUIFocusCancelHandlingResult TryHandleCancel(
                in AppUIFocusRegionCancelContext context)
            {
                CallCount++;
                LastContext = context;
                return result;
            }
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

            public void Dispose()
            {
                FocusService.ClearScopes();
            }
        }
    }
}
