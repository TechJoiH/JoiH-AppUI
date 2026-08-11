using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusSemanticNavigationTests
    {
        private const string ListGroupId = "list";
        private const string CloseGroupId = "close";
        private const string TargetGroupId = "target";

        private readonly List<GameObject> createdObjects = new List<GameObject>(16);
        private AppUIFocusGroupNavigator navigator;
        private EventSystem testEventSystem;

        [SetUp]
        public void SetUp()
        {
            testEventSystem = CreateObject("EventSystem").AddComponent<EventSystem>();
            InvokeEventSystemLifecycle(testEventSystem, "OnEnable");
            Assert.That(EventSystem.current, Is.SameAs(testEventSystem));
            navigator = new AppUIFocusGroupNavigator();
        }

        [TearDown]
        public void TearDown()
        {
            navigator?.Dispose();
            navigator = null;

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

        [Test]
        public void VerticalGroup_ExhaustsInternalMoveBeforeTopBoundary()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            Button close = CreateButton("Close");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .VerticalGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Up).FocusGroupFirst(CloseGroupId)
                    .SingleGroup(CloseGroupId)
                    .Build());
            Register(ListGroupId, first, second);
            Register(CloseGroupId, close);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(CloseGroupId);
            navigator.FocusNode(ListGroupId, second);

            bool movedInside = navigator.MoveFocus(ListGroupId, second, MoveDirection.Up);

            Assert.That(movedInside, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(first.gameObject));

            bool movedAcrossBoundary =
                navigator.MoveFocus(ListGroupId, first, MoveDirection.Up);

            Assert.That(movedAcrossBoundary, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
        }

        [Test]
        public void VerticalGroup_SkipsDisabledNodesInsideGroup()
        {
            Button first = CreateButton("First");
            Button disabled = CreateButton("Disabled");
            Button third = CreateButton("Third");
            disabled.interactable = false;
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(builder.VerticalGroup(ListGroupId).Build());
            Register(ListGroupId, first, disabled, third);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, first);

            bool handled = navigator.MoveFocus(ListGroupId, first, MoveDirection.Down);

            Assert.That(handled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(third.gameObject));
        }

        [Test]
        public void HorizontalGroup_WithCycleWrapsInsideGroup()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .HorizontalGroup(ListGroupId, AppUIFocusWrapPolicy.Cycle)
                    .Build());
            Register(ListGroupId, first, second);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, second);

            bool handled = navigator.MoveFocus(ListGroupId, second, MoveDirection.Right);

            Assert.That(handled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(first.gameObject));
        }

        [Test]
        public void GridGroup_UsesBoundaryOnlyAfterGridMoveFails()
        {
            Button[] nodes =
            {
                CreateButton("Grid0"),
                CreateButton("Grid1"),
                CreateButton("Grid2"),
                CreateButton("Grid3"),
                CreateButton("Grid4"),
            };
            Button close = CreateButton("Close");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .GridGroup(
                        ListGroupId,
                        3,
                        AppUIFocusGridShortRowPolicy.ClampToLastItem)
                        .AtBoundary(MoveDirection.Down).FocusGroupFirst(CloseGroupId)
                    .SingleGroup(CloseGroupId)
                    .Build());
            Register(ListGroupId, nodes);
            Register(CloseGroupId, close);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(CloseGroupId);
            navigator.FocusNode(ListGroupId, nodes[1]);

            navigator.MoveFocus(ListGroupId, nodes[1], MoveDirection.Down);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(nodes[4].gameObject));

            navigator.MoveFocus(ListGroupId, nodes[4], MoveDirection.Down);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
        }

        [Test]
        public void SemanticGroup_WithoutBoundaryTargetConsumesAndKeepsFocus()
        {
            Button only = CreateButton("Only");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(builder.VerticalGroup(ListGroupId).Build());
            Register(ListGroupId, only);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, only);

            bool handled = navigator.MoveFocus(ListGroupId, only, MoveDirection.Up);

            Assert.That(handled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(only.gameObject));
        }

        [Test]
        public void BeforeMoveRule_OverridesSemanticLayoutAndReceivesContext()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            Button special = CreateButton("Special");
            FixedMoveRule moveRule = new FixedMoveRule(
                AppUIFocusMoveDecision.Focus(special));
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .VerticalGroup(ListGroupId)
                        .BeforeMove(moveRule)
                    .SingleGroup(TargetGroupId)
                    .Build());
            Register(ListGroupId, first, second);
            Register(TargetGroupId, special);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(TargetGroupId);
            navigator.FocusNode(ListGroupId, first);

            bool handled = navigator.MoveFocus(
                ListGroupId,
                first,
                MoveDirection.Down);

            Assert.That(handled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(special.gameObject));
            Assert.That(moveRule.LastContext.Stage, Is.EqualTo(AppUIFocusMoveStage.BeforeMove));
            Assert.That(moveRule.LastContext.CurrentIndex, Is.EqualTo(0));
            Assert.That(moveRule.LastContext.NodeCount, Is.EqualTo(2));
        }

        [Test]
        public void BeforeMoveRule_ContinueDefaultPreservesInternalMove()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            FixedMoveRule moveRule = new FixedMoveRule(
                AppUIFocusMoveDecision.ContinueDefault());
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .VerticalGroup(ListGroupId)
                        .BeforeMove(moveRule)
                    .Build());
            Register(ListGroupId, first, second);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, first);

            navigator.MoveFocus(ListGroupId, first, MoveDirection.Down);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(second.gameObject));
        }

        [Test]
        public void LayoutResolver_CanRouteDirectlyToDeclaredBoundary()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            Button close = CreateButton("Close");
            FixedLayoutResolver layoutResolver = new FixedLayoutResolver(
                AppUIFocusMoveDecision.ReachBoundary());
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .VerticalGroup(ListGroupId)
                        .ResolveLayoutWith(layoutResolver)
                        .AtBoundary(MoveDirection.Down).FocusGroupFirst(CloseGroupId)
                    .SingleGroup(CloseGroupId)
                    .Build());
            Register(ListGroupId, first, second);
            Register(CloseGroupId, close);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(CloseGroupId);
            navigator.FocusNode(ListGroupId, first);

            navigator.MoveFocus(ListGroupId, first, MoveDirection.Down);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
            Assert.That(layoutResolver.LastContext.Stage, Is.EqualTo(AppUIFocusMoveStage.Layout));
        }

        [Test]
        public void BoundaryResolver_OverridesBuiltInBoundaryAction()
        {
            Button only = CreateButton("Only");
            Button resolved = CreateButton("Resolved");
            Button fallback = CreateButton("Fallback");
            FixedBoundaryResolver boundaryResolver = new FixedBoundaryResolver(
                AppUIFocusMoveDecision.Focus(resolved));
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .VerticalGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Up).Resolve(boundaryResolver)
                        .AtBoundary(MoveDirection.Up).FocusGroupFirst(CloseGroupId)
                    .SingleGroup(TargetGroupId)
                    .SingleGroup(CloseGroupId)
                    .Build());
            Register(ListGroupId, only);
            Register(TargetGroupId, resolved);
            Register(CloseGroupId, fallback);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(TargetGroupId);
            navigator.OpenGroup(CloseGroupId);
            navigator.FocusNode(ListGroupId, only);

            navigator.MoveFocus(ListGroupId, only, MoveDirection.Up);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(resolved.gameObject));
            Assert.That(boundaryResolver.LastContext.Stage, Is.EqualTo(AppUIFocusMoveStage.Boundary));
        }

        [Test]
        public void BoundaryResolver_ContinueDefaultRunsBuiltInBoundaryAction()
        {
            Button only = CreateButton("Only");
            Button close = CreateButton("Close");
            FixedBoundaryResolver boundaryResolver = new FixedBoundaryResolver(
                AppUIFocusMoveDecision.ContinueDefault());
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .VerticalGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Up).Resolve(boundaryResolver)
                        .AtBoundary(MoveDirection.Up).FocusGroupFirst(CloseGroupId)
                    .SingleGroup(CloseGroupId)
                    .Build());
            Register(ListGroupId, only);
            Register(CloseGroupId, close);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(CloseGroupId);
            navigator.FocusNode(ListGroupId, only);

            navigator.MoveFocus(ListGroupId, only, MoveDirection.Up);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
        }

        [Test]
        public void EntryResolver_SelectsSpecialTargetThroughConfiguredGroupAction()
        {
            Button source = CreateButton("Source");
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            FixedEntryResolver entryResolver = new FixedEntryResolver(second);
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .SingleGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Right).FocusGroup(TargetGroupId)
                    .VerticalGroup(TargetGroupId)
                        .EnterWith(entryResolver)
                    .Build());
            Register(ListGroupId, source);
            Register(TargetGroupId, first, second);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(TargetGroupId);
            navigator.FocusNode(ListGroupId, source);

            navigator.MoveFocus(ListGroupId, source, MoveDirection.Right);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(second.gameObject));
            Assert.That(entryResolver.LastContext.SourceGroupId, Is.EqualTo(ListGroupId));
            Assert.That(entryResolver.LastContext.TargetGroupId, Is.EqualTo(TargetGroupId));
            Assert.That(entryResolver.LastContext.MoveDirection, Is.EqualTo(MoveDirection.Right));
        }

        [Test]
        public void InvalidSpecialTarget_IsConsumedAndKeepsCurrentFocus()
        {
            Button only = CreateButton("Only");
            Button unregistered = CreateButton("Unregistered");
            FixedBoundaryResolver boundaryResolver = new FixedBoundaryResolver(
                AppUIFocusMoveDecision.Focus(unregistered));
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .VerticalGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Up).Resolve(boundaryResolver)
                    .Build());
            Register(ListGroupId, only);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, only);

            bool handled = navigator.MoveFocus(ListGroupId, only, MoveDirection.Up);

            Assert.That(handled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(only.gameObject));
        }

        [Test]
        public void LegacyGroup_PreservesExistingChainPriority()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            Button close = CreateButton("Close");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .Group(ListGroupId)
                        .On(MoveDirection.Up).FocusGroupFirst(CloseGroupId)
                    .Group(CloseGroupId)
                    .Build());
            Register(ListGroupId, first, second);
            Register(CloseGroupId, close);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(CloseGroupId);
            navigator.FocusNode(ListGroupId, second);

            bool handled = navigator.MoveFocus(ListGroupId, second, MoveDirection.Up);

            Assert.That(handled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
        }

        [Test]
        public void GroupNode_IncomingUsedFlagDoesNotChangeFrameworkNavigation()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(builder.VerticalGroup(ListGroupId).Build());
            Register(ListGroupId, first, second);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, first);
            AxisEventData eventData = CreateMoveEvent(MoveDirection.Down);
            eventData.Use();

            first.GetComponent<AppUIFocusGroupNode>().OnMove(eventData);

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(second.gameObject));
        }

        [Test]
        public void HorizontalSlider_DelegatesSameAxisOnceAndUsesFrameworkForPerpendicularAxis()
        {
            Slider slider = CreateSlider("Slider");
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 10f;
            slider.value = 5f;
            Button second = CreateButton("Second");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(builder.VerticalGroup(ListGroupId).Build());
            navigator.RegisterNode(ListGroupId, slider);
            navigator.RegisterNode(ListGroupId, second);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, slider);
            AppUIFocusGroupNode node = slider.GetComponent<AppUIFocusGroupNode>();

            AxisEventData frameworkFirst = CreateMoveEvent(MoveDirection.Right);
            node.OnMove(frameworkFirst);
            slider.OnMove(frameworkFirst);

            Assert.That(slider.value, Is.GreaterThan(5f));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(slider.gameObject));

            slider.value = 5f;
            AxisEventData nativeFirst = CreateMoveEvent(MoveDirection.Right);
            slider.OnMove(nativeFirst);
            node.OnMove(nativeFirst);

            Assert.That(slider.value, Is.GreaterThan(5f));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(slider.gameObject));

            AxisEventData perpendicular = CreateMoveEvent(MoveDirection.Down);
            node.OnMove(perpendicular);
            slider.OnMove(perpendicular);

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(second.gameObject));
        }

        [Test]
        public void ThrowingControlPolicy_BlocksMoveAndKeepsFocus()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(builder.HorizontalGroup(ListGroupId).Build());
            navigator.RegisterNode(ListGroupId, first, new ThrowingControlPolicy());
            navigator.RegisterNode(ListGroupId, second);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, first);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Focus extension failed.*Stage=ControlPolicy"));

            first.GetComponent<AppUIFocusGroupNode>().OnMove(
                CreateMoveEvent(MoveDirection.Right));

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(first.gameObject));
        }

        [Test]
        public void ThrowingMoveInputPolicy_BlocksMoveAndKeepsFocus()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(builder.HorizontalGroup(ListGroupId).Build());
            navigator.SetMoveInputPolicy(new ThrowingMoveInputPolicy());
            Register(ListGroupId, first, second);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, first);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Focus extension failed.*Stage=MoveInputPolicy"));

            first.GetComponent<AppUIFocusGroupNode>().OnMove(
                CreateMoveEvent(MoveDirection.Right));

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(first.gameObject));
        }

        [Test]
        public void UnvalidatedNativeDelegatePolicy_IsRejectedAndKeepsFocus()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(builder.HorizontalGroup(ListGroupId).Build());
            navigator.RegisterNode(
                ListGroupId,
                first,
                new UnvalidatedNativeDelegatePolicy());
            navigator.RegisterNode(ListGroupId, second);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, first);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Native control delegation rejected.*Stage=ControlPolicy"));

            first.GetComponent<AppUIFocusGroupNode>().OnMove(
                CreateMoveEvent(MoveDirection.Right));

            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(first.gameObject));
        }

        [Test]
        public void ThrowingBeforeMoveRule_BlocksMoveAndKeepsFocus()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .VerticalGroup(ListGroupId)
                        .BeforeMove(new ThrowingMoveRule())
                    .Build());
            Register(ListGroupId, first, second);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, first);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Focus extension failed.*Stage=BeforeMove"));

            bool handled = navigator.MoveFocus(
                ListGroupId,
                first,
                MoveDirection.Down);

            Assert.That(handled, Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(first.gameObject));
        }

        [Test]
        public void ThrowingLayoutResolver_BlocksMoveAndKeepsFocus()
        {
            Button first = CreateButton("First");
            Button second = CreateButton("Second");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .VerticalGroup(ListGroupId)
                        .ResolveLayoutWith(new ThrowingLayoutResolver())
                    .Build());
            Register(ListGroupId, first, second);
            navigator.OpenGroup(ListGroupId);
            navigator.FocusNode(ListGroupId, first);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Focus extension failed.*Stage=Layout"));

            bool handled = navigator.MoveFocus(
                ListGroupId,
                first,
                MoveDirection.Down);

            Assert.That(handled, Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(first.gameObject));
        }

        [Test]
        public void ThrowingBoundaryResolver_BlocksFallbackAndKeepsFocus()
        {
            Button source = CreateButton("Source");
            Button fallback = CreateButton("Fallback");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .SingleGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Right).Resolve(
                            new ThrowingBoundaryResolver())
                        .AtBoundary(MoveDirection.Right).FocusGroupFirst(TargetGroupId)
                    .SingleGroup(TargetGroupId)
                    .Build());
            Register(ListGroupId, source);
            Register(TargetGroupId, fallback);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(TargetGroupId);
            navigator.FocusNode(ListGroupId, source);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Focus extension failed.*Stage=Boundary"));

            bool handled = navigator.MoveFocus(
                ListGroupId,
                source,
                MoveDirection.Right);

            Assert.That(handled, Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(source.gameObject));
        }

        [Test]
        public void ThrowingEntryResolver_BlocksEntryAndKeepsSourceFocus()
        {
            Button source = CreateButton("Source");
            Button target = CreateButton("Target");
            Button fallback = CreateButton("Fallback");
            AppUIFocusChainBuilder builder = new AppUIFocusChainBuilder();
            navigator.SetChain(
                builder
                    .SingleGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Right).Do(
                            AppUIFocusAction.Fallback(
                                AppUIFocusAction.FocusGroup(TargetGroupId),
                                AppUIFocusAction.FocusGroupFirst(CloseGroupId)))
                    .VerticalGroup(TargetGroupId)
                        .EnterWith(new ThrowingEntryResolver())
                    .SingleGroup(CloseGroupId)
                    .Build());
            Register(ListGroupId, source);
            Register(TargetGroupId, target);
            Register(CloseGroupId, fallback);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(TargetGroupId);
            navigator.OpenGroup(CloseGroupId);
            navigator.FocusNode(ListGroupId, source);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Focus extension failed.*Stage=Entry"));

            bool handled = navigator.MoveFocus(
                ListGroupId,
                source,
                MoveDirection.Right);

            Assert.That(handled, Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(source.gameObject));
        }

        [Test]
        public void PreserveOrdinalOrClamp_UsesSourceIndexAndClampsShortTarget()
        {
            Button source0 = CreateButton("Source0");
            Button source1 = CreateButton("Source1");
            Button source2 = CreateButton("Source2");
            Button target0 = CreateButton("Target0");
            Button target1 = CreateButton("Target1");
            navigator.SetChain(
                new AppUIFocusChainBuilder()
                    .VerticalGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Right).FocusGroup(TargetGroupId)
                    .VerticalGroup(TargetGroupId)
                        .EnterWith(AppUIFocusEntryPolicy.PreserveOrdinalOrClamp)
                    .Build());
            Register(ListGroupId, source0, source1, source2);
            Register(TargetGroupId, target0, target1);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(TargetGroupId);
            navigator.FocusNode(ListGroupId, source2);

            bool handled = navigator.MoveFocus(
                ListGroupId,
                source2,
                MoveDirection.Right);

            Assert.That(handled, Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(target1.gameObject));
        }

        [Test]
        public void NearestOnEntryAxis_ChoosesClosestOrthogonalCandidate()
        {
            Button source = CreateButton("Source");
            Button far = CreateButton("Far");
            Button near = CreateButton("Near");
            source.transform.position = new Vector3(0f, 10f, 0f);
            far.transform.position = new Vector3(100f, -20f, 0f);
            near.transform.position = new Vector3(100f, 9f, 0f);
            navigator.SetChain(
                new AppUIFocusChainBuilder()
                    .SingleGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Right).FocusGroup(TargetGroupId)
                    .VerticalGroup(TargetGroupId)
                        .EnterWith(AppUIFocusEntryPolicy.NearestOnEntryAxis)
                    .Build());
            Register(ListGroupId, source);
            Register(TargetGroupId, far, near);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(TargetGroupId);
            navigator.FocusNode(ListGroupId, source);

            bool handled = navigator.MoveFocus(
                ListGroupId,
                source,
                MoveDirection.Right);

            Assert.That(handled, Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(near.gameObject));
        }

        [Test]
        public void AnchorOrFirst_UsesValidAnchorAndFallsBackToFirst()
        {
            Button source = CreateButton("Source");
            Button first = CreateButton("First");
            Button anchored = CreateButton("Anchored");
            navigator.SetAnchorProvider(new FixedAnchorProvider("entry", anchored));
            navigator.SetChain(
                new AppUIFocusChainBuilder()
                    .SingleGroup(ListGroupId)
                        .AtBoundary(MoveDirection.Right).FocusGroup(TargetGroupId)
                    .VerticalGroup(TargetGroupId)
                        .EnterWithAnchor("entry")
                    .Build());
            Register(ListGroupId, source);
            Register(TargetGroupId, first, anchored);
            navigator.OpenGroup(ListGroupId);
            navigator.OpenGroup(TargetGroupId);
            navigator.FocusNode(ListGroupId, source);

            bool handled = navigator.MoveFocus(
                ListGroupId,
                source,
                MoveDirection.Right);

            Assert.That(handled, Is.True);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(anchored.gameObject));
        }

        private void Register(string groupId, params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                navigator.RegisterNode(groupId, buttons[i]);
            }
        }

        private Button CreateButton(string name)
        {
            return CreateObject(name).AddComponent<Button>();
        }

        private Slider CreateSlider(string name)
        {
            return CreateObject(name).AddComponent<Slider>();
        }

        private static AxisEventData CreateMoveEvent(MoveDirection moveDirection)
        {
            return new AxisEventData(EventSystem.current)
            {
                moveDir = moveDirection,
            };
        }

        private GameObject CreateObject(string name)
        {
            GameObject createdObject = new GameObject(name, typeof(RectTransform));
            createdObjects.Add(createdObject);
            return createdObject;
        }

        private sealed class FixedAnchorProvider : IAppUIFocusAnchorProvider
        {
            private readonly string anchorId;
            private readonly Selectable selectable;

            public FixedAnchorProvider(string id, Selectable target)
            {
                anchorId = id;
                selectable = target;
            }

            public bool TryGetFocusAnchor(string id, out Selectable target)
            {
                target = string.Equals(id, anchorId, System.StringComparison.Ordinal)
                    ? selectable
                    : null;
                return target != null;
            }
        }

        private sealed class FixedMoveRule : IAppUIFocusMoveRule
        {
            private readonly AppUIFocusMoveDecision decision;

            public FixedMoveRule(AppUIFocusMoveDecision moveDecision)
            {
                decision = moveDecision;
            }

            public AppUIFocusMoveContext LastContext { get; private set; }

            public AppUIFocusMoveDecision Evaluate(in AppUIFocusMoveContext context)
            {
                LastContext = context;
                return decision;
            }
        }

        private sealed class FixedBoundaryResolver : IAppUIFocusBoundaryResolver
        {
            private readonly AppUIFocusMoveDecision decision;

            public FixedBoundaryResolver(AppUIFocusMoveDecision moveDecision)
            {
                decision = moveDecision;
            }

            public AppUIFocusMoveContext LastContext { get; private set; }

            public AppUIFocusMoveDecision Resolve(in AppUIFocusMoveContext context)
            {
                LastContext = context;
                return decision;
            }
        }

        private sealed class FixedLayoutResolver : IAppUIFocusLayoutResolver
        {
            private readonly AppUIFocusMoveDecision decision;

            public FixedLayoutResolver(AppUIFocusMoveDecision moveDecision)
            {
                decision = moveDecision;
            }

            public AppUIFocusMoveContext LastContext { get; private set; }

            public AppUIFocusMoveDecision Resolve(in AppUIFocusMoveContext context)
            {
                LastContext = context;
                return decision;
            }
        }

        private sealed class FixedEntryResolver : IAppUIFocusEntryResolver
        {
            private readonly Selectable selectable;

            public FixedEntryResolver(Selectable target)
            {
                selectable = target;
            }

            public AppUIFocusEntryContext LastContext { get; private set; }

            public bool TryResolve(
                in AppUIFocusEntryContext context,
                out Selectable target)
            {
                LastContext = context;
                target = selectable;
                return target != null;
            }
        }

        private sealed class ThrowingControlPolicy : IAppUIFocusControlPolicy
        {
            public AppUIFocusControlMoveMode GetMoveMode(
                in AppUIFocusMoveContext context)
            {
                throw new InvalidOperationException("Control policy failure.");
            }

            public AppUIFocusCancelHandlingResult TryHandleCancel(
                in AppUIFocusCancelContext context)
            {
                return AppUIFocusCancelHandlingResult.Continue;
            }
        }

        private sealed class ThrowingMoveInputPolicy : IAppUIFocusMoveInputPolicy
        {
            public bool ShouldConsumeWithoutNavigation(AxisEventData eventData)
            {
                throw new InvalidOperationException("Move input policy failure.");
            }
        }

        private sealed class UnvalidatedNativeDelegatePolicy : IAppUIFocusControlPolicy
        {
            public AppUIFocusControlMoveMode GetMoveMode(
                in AppUIFocusMoveContext context)
            {
                return AppUIFocusControlMoveMode.DelegateToNativeControl;
            }

            public AppUIFocusCancelHandlingResult TryHandleCancel(
                in AppUIFocusCancelContext context)
            {
                return AppUIFocusCancelHandlingResult.Continue;
            }
        }

        private sealed class ThrowingMoveRule : IAppUIFocusMoveRule
        {
            public AppUIFocusMoveDecision Evaluate(
                in AppUIFocusMoveContext context)
            {
                throw new InvalidOperationException("BeforeMove failure.");
            }
        }

        private sealed class ThrowingLayoutResolver : IAppUIFocusLayoutResolver
        {
            public AppUIFocusMoveDecision Resolve(
                in AppUIFocusMoveContext context)
            {
                throw new InvalidOperationException("Layout failure.");
            }
        }

        private sealed class ThrowingBoundaryResolver : IAppUIFocusBoundaryResolver
        {
            public AppUIFocusMoveDecision Resolve(
                in AppUIFocusMoveContext context)
            {
                throw new InvalidOperationException("Boundary failure.");
            }
        }

        private sealed class ThrowingEntryResolver : IAppUIFocusEntryResolver
        {
            public bool TryResolve(
                in AppUIFocusEntryContext context,
                out Selectable target)
            {
                target = null;
                throw new InvalidOperationException("Entry failure.");
            }
        }
    }
}
