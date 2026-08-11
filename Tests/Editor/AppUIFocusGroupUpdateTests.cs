using System;
using System.Collections.Generic;
using System.Reflection;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusGroupUpdateTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>(24);
        private EventSystem testEventSystem;

        [SetUp]
        public void SetUp()
        {
            GameObject eventSystemObject = CreateObject("EventSystem");
            testEventSystem = eventSystemObject.AddComponent<EventSystem>();
            InvokeEventSystemLifecycle(testEventSystem, "OnEnable");
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
        public void DisposeWithoutComplete_AbortsAndPreservesOldSnapshotAndRevision()
        {
            TestRuntime runtime = CreateRuntime("AbortPage", "main");
            Button oldButton = CreateButton(runtime.Page.GameObject, "Old");
            Button stagedButton = CreateButton(runtime.Page.GameObject, "Staged");
            AppUIFocusNodeAddress oldAddress = Register(
                runtime.ScopeHandle,
                "main",
                "old",
                oldButton);

            Assert.That(
                runtime.ScopeHandle.BeginGroupUpdate("main", out AppUIFocusGroupUpdateTransaction update),
                Is.EqualTo(AppUIFocusGroupUpdateResult.Started));
            int capturedRevision = update.CapturedGroupRevision;
            Assert.That(update.Register(new AppUIFocusNodeKey("staged"), stagedButton), Is.True);
            Assert.That(runtime.ScopeHandle.TryResolveNode(oldAddress, out _), Is.True);
            Assert.That(runtime.ScopeHandle.TryGetNodeAddress(stagedButton, out _), Is.False);

            update.Dispose();

            Assert.That(runtime.ScopeHandle.TryResolveNode(oldAddress, out Selectable resolved), Is.True);
            Assert.That(resolved, Is.SameAs(oldButton));
            Assert.That(runtime.ScopeHandle.TryGetNodeAddress(stagedButton, out _), Is.False);
            Assert.That(
                runtime.ScopeHandle.BeginGroupUpdate("main", out AppUIFocusGroupUpdateTransaction next),
                Is.EqualTo(AppUIFocusGroupUpdateResult.Started));
            Assert.That(next.CapturedGroupRevision, Is.EqualTo(capturedRevision));
            Assert.That(next.Abort(), Is.EqualTo(AppUIFocusGroupUpdateResult.Aborted));
        }

        [Test]
        public void Complete_AtomicallyReplacesForwardAndReverseIndexesOnce()
        {
            TestRuntime runtime = CreateRuntime("CompletePage", "main");
            Button oldButton = CreateButton(runtime.Page.GameObject, "Old");
            Button replacement = CreateButton(
                runtime.Page.GameObject,
                "Replacement",
                Navigation.Mode.Automatic);
            AppUIFocusNodeAddress oldAddress = Register(
                runtime.ScopeHandle,
                "main",
                "old",
                oldButton);

            runtime.ScopeHandle.BeginGroupUpdate(
                "main",
                out AppUIFocusGroupUpdateTransaction update);
            int oldRevision = update.CapturedGroupRevision;
            AppUIFocusNodeKey replacementKey = new AppUIFocusNodeKey("replacement");
            update.Register(replacementKey, replacement, 7);

            Assert.That(runtime.ScopeHandle.TryResolveNode(oldAddress, out _), Is.True);
            Assert.That(runtime.ScopeHandle.TryGetNodeAddress(replacement, out _), Is.False);
            LogAssert.Expect(
                LogType.Error,
                "<AppUIFocus> Registered Selectable must use Navigation.Mode.None. " +
                "Scope=CompletePage-scope, Node=main/replacement, Object=Replacement");
            Assert.That(update.Complete(), Is.EqualTo(AppUIFocusGroupUpdateResult.Completed));

            AppUIFocusNodeAddress replacementAddress =
                new AppUIFocusNodeAddress("main", replacementKey);
            Assert.That(runtime.ScopeHandle.TryResolveNode(oldAddress, out _), Is.False);
            Assert.That(runtime.ScopeHandle.TryGetNodeAddress(oldButton, out _), Is.False);
            Assert.That(runtime.ScopeHandle.TryResolveNode(replacementAddress, out Selectable resolved), Is.True);
            Assert.That(resolved, Is.SameAs(replacement));
            Assert.That(runtime.ScopeHandle.TryGetNodeAddress(replacement, out AppUIFocusNodeAddress reverse), Is.True);
            Assert.That(reverse, Is.EqualTo(replacementAddress));
            Assert.That(replacement.navigation.mode, Is.EqualTo(Navigation.Mode.None));

            runtime.ScopeHandle.BeginGroupUpdate(
                "main",
                out AppUIFocusGroupUpdateTransaction next);
            Assert.That(next.CapturedGroupRevision, Is.EqualTo(oldRevision + 1));
            next.Abort();
        }

        [Test]
        public void ActiveTransaction_BlocksSameGroupAndDirectMutation_ButNotOtherGroups()
        {
            TestRuntime runtime = CreateRuntime("ConcurrentPage", "main", "other");
            Button existing = CreateButton(runtime.Page.GameObject, "Existing");
            Button rejected = CreateButton(runtime.Page.GameObject, "Rejected");
            AppUIFocusNodeKey existingKey = new AppUIFocusNodeKey("existing");
            Assert.That(runtime.ScopeHandle.RegisterNode("main", existingKey, existing), Is.True);
            Assert.That(
                runtime.ScopeHandle.BeginGroupUpdate("main", out AppUIFocusGroupUpdateTransaction mainUpdate),
                Is.EqualTo(AppUIFocusGroupUpdateResult.Started));

            Assert.That(
                runtime.ScopeHandle.BeginGroupUpdate("main", out AppUIFocusGroupUpdateTransaction duplicate),
                Is.EqualTo(AppUIFocusGroupUpdateResult.TransactionAlreadyActive));
            Assert.That(duplicate, Is.Null);
            LogAssert.Expect(
                LogType.Error,
                "<AppUIFocus> Node registration rejected. Scope=ConcurrentPage-scope, Group=main, NodeKey=rejected, Object=Rejected");
            Assert.That(
                runtime.ScopeHandle.RegisterNode(
                    "main",
                    new AppUIFocusNodeKey("rejected"),
                    rejected),
                Is.False);
            Assert.That(runtime.ScopeHandle.UnregisterNode("main", existingKey), Is.False);
            Assert.That(runtime.ScopeHandle.ClearGroup("main"), Is.False);

            Assert.That(
                runtime.ScopeHandle.BeginGroupUpdate("other", out AppUIFocusGroupUpdateTransaction otherUpdate),
                Is.EqualTo(AppUIFocusGroupUpdateResult.Started));
            Assert.That(otherUpdate.Abort(), Is.EqualTo(AppUIFocusGroupUpdateResult.Aborted));
            Assert.That(mainUpdate.Abort(), Is.EqualTo(AppUIFocusGroupUpdateResult.Aborted));
        }

        [Test]
        public void OpenOrCloseDuringUpdate_MakesCompleteStaleAndPreservesNodes()
        {
            TestRuntime runtime = CreateRuntime("StaleGroupPage", "main");
            Button oldButton = CreateButton(runtime.Page.GameObject, "Old");
            Button stagedButton = CreateButton(runtime.Page.GameObject, "Staged");
            AppUIFocusNodeAddress oldAddress = Register(
                runtime.ScopeHandle,
                "main",
                "old",
                oldButton);
            runtime.ScopeHandle.BeginGroupUpdate(
                "main",
                out AppUIFocusGroupUpdateTransaction update);
            update.Register(new AppUIFocusNodeKey("staged"), stagedButton);

            Assert.That(runtime.ScopeHandle.CloseGroup("main"), Is.True);
            Assert.That(update.Complete(), Is.EqualTo(AppUIFocusGroupUpdateResult.StaleRevision));
            Assert.That(runtime.ScopeHandle.TryResolveNode(oldAddress, out Selectable resolved), Is.True);
            Assert.That(resolved, Is.SameAs(oldButton));
            Assert.That(runtime.ScopeHandle.TryGetNodeAddress(stagedButton, out _), Is.False);
            Assert.That(runtime.ScopeHandle.IsGroupOpen("main"), Is.False);
        }

        [Test]
        public void CompleteValidationFailure_PreservesOldSnapshotAndRevision()
        {
            TestRuntime runtime = CreateRuntime("ValidationPage", "main");
            Button oldButton = CreateButton(runtime.Page.GameObject, "Old");
            Button first = CreateButton(runtime.Page.GameObject, "First");
            Button duplicate = CreateButton(runtime.Page.GameObject, "Duplicate");
            AppUIFocusNodeAddress oldAddress = Register(
                runtime.ScopeHandle,
                "main",
                "old",
                oldButton);
            runtime.ScopeHandle.BeginGroupUpdate(
                "main",
                out AppUIFocusGroupUpdateTransaction update);
            int oldRevision = update.CapturedGroupRevision;
            AppUIFocusNodeKey duplicateKey = new AppUIFocusNodeKey("duplicate");
            update.Register(duplicateKey, first);
            update.Register(duplicateKey, duplicate);

            Assert.That(
                update.Complete(),
                Is.EqualTo(AppUIFocusGroupUpdateResult.ValidationFailed));
            Assert.That(runtime.ScopeHandle.TryResolveNode(oldAddress, out _), Is.True);
            Assert.That(runtime.ScopeHandle.TryGetNodeAddress(first, out _), Is.False);
            Assert.That(runtime.ScopeHandle.TryGetNodeAddress(duplicate, out _), Is.False);

            runtime.ScopeHandle.BeginGroupUpdate(
                "main",
                out AppUIFocusGroupUpdateTransaction next);
            Assert.That(next.CapturedGroupRevision, Is.EqualTo(oldRevision));
            next.Abort();
        }

        [Test]
        public void ScopeDispose_ForcesActiveTransactionToReturnScopeDisposed()
        {
            TestRuntime runtime = CreateRuntime("DisposedPage", "main");
            runtime.ScopeHandle.BeginGroupUpdate(
                "main",
                out AppUIFocusGroupUpdateTransaction update);

            runtime.FocusService.DetachScope(runtime.Page);

            Assert.That(
                update.Complete(),
                Is.EqualTo(AppUIFocusGroupUpdateResult.ScopeDisposed));
            Assert.Throws<InvalidOperationException>(() => update.Complete());
            Assert.That(runtime.ScopeHandle.Status, Is.EqualTo(AppUIFocusScopeStatus.Disposed));
        }

        [Test]
        public void CompleteRemovingCurrentNode_QueuesOneRepairAgainstFinalSnapshot()
        {
            TestRuntime runtime = CreateRuntime("RepairAfterUpdatePage", "main");
            Button current = CreateButton(runtime.Page.GameObject, "Current");
            Button fallback = CreateButton(runtime.Page.GameObject, "Fallback");
            Register(runtime.ScopeHandle, "main", "current", current);
            Activate(runtime, 1);
            Assert.That(
                runtime.Scope.CommitFocus(current, AppUIFocusChangeReason.Navigation),
                Is.EqualTo(AppUIFocusRequestResult.Focused));

            runtime.ScopeHandle.BeginGroupUpdate(
                "main",
                out AppUIFocusGroupUpdateTransaction update);
            AppUIFocusNodeKey fallbackKey = new AppUIFocusNodeKey("fallback");
            update.Register(fallbackKey, fallback);
            Assert.That(update.Complete(), Is.EqualTo(AppUIFocusGroupUpdateResult.Completed));

            runtime.FocusService.ReconcileSelection();

            AppUIFocusNodeAddress fallbackAddress =
                new AppUIFocusNodeAddress("main", fallbackKey);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(fallback.gameObject));
            Assert.That(runtime.Scope.CurrentFocusedAddress, Is.EqualTo(fallbackAddress));
            Assert.That(runtime.Scope.LastFocusedAddress, Is.EqualTo(fallbackAddress));
            Assert.That(
                ((UIFocusCommitter)runtime.FocusService.Committer).HasPendingRepair,
                Is.False);
        }

        [Test]
        public void ClearGroup_AfterAbortCommitsExplicitEmptyClosedSnapshot()
        {
            TestRuntime runtime = CreateRuntime("ClearPage", "main");
            Button oldButton = CreateButton(runtime.Page.GameObject, "Old");
            AppUIFocusNodeAddress oldAddress = Register(
                runtime.ScopeHandle,
                "main",
                "old",
                oldButton);
            runtime.ScopeHandle.BeginGroupUpdate(
                "main",
                out AppUIFocusGroupUpdateTransaction update);
            int oldRevision = update.CapturedGroupRevision;
            update.Abort();

            Assert.That(runtime.ScopeHandle.ClearGroup("main"), Is.True);
            Assert.That(runtime.ScopeHandle.TryResolveNode(oldAddress, out _), Is.False);
            Assert.That(runtime.ScopeHandle.TryGetNodeAddress(oldButton, out _), Is.False);
            Assert.That(runtime.ScopeHandle.IsGroupOpen("main"), Is.False);

            runtime.ScopeHandle.BeginGroupUpdate(
                "main",
                out AppUIFocusGroupUpdateTransaction next);
            Assert.That(next.CapturedGroupRevision, Is.EqualTo(oldRevision + 1));
            next.Abort();
        }

        private TestRuntime CreateRuntime(string pageId, params string[] groupIds)
        {
            UIPageInstanceRegistry registry = new UIPageInstanceRegistry();
            GameObject pageObject = CreateObject(pageId, typeof(RectTransform));
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

            AppUIFocusDefinitionBuilder definitionBuilder =
                new AppUIFocusDefinitionBuilder(pageId + "-scope");
            for (int i = 0; i < groupIds.Length; i++)
            {
                definitionBuilder.AddGroup(groupIds[i]);
            }

            UIFocusService focusService = new UIFocusService();
            focusService.ConfigureInstanceRegistry(registry);
            IAppUIFocusScopeHandle scopeHandle =
                focusService.AttachScope(page, definitionBuilder.Build());
            return new TestRuntime(
                page,
                focusService,
                scopeHandle,
                (AppUIFocusScope)scopeHandle);
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
            Selectable selectable)
        {
            AppUIFocusNodeKey key = new AppUIFocusNodeKey(nodeKey);
            Assert.That(scope.RegisterNode(groupId, key, selectable), Is.True);
            return new AppUIFocusNodeAddress(groupId, key);
        }

        private Button CreateButton(
            GameObject parent,
            string name,
            Navigation.Mode navigationMode = Navigation.Mode.None)
        {
            GameObject buttonObject = CreateObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent.transform, false);
            Button button = buttonObject.AddComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = navigationMode;
            button.navigation = navigation;
            return button;
        }

        private GameObject CreateObject(string name, params Type[] components)
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
    }
}
