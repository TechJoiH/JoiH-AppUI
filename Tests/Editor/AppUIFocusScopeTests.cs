using System;
using System.Collections.Generic;
using Joi.H.AppUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusScopeTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>(16);

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void NodeAddress_UsesGroupAndLocalNodeKeyForIdentity()
        {
            AppUIFocusNodeKey key = new AppUIFocusNodeKey("item:42");
            AppUIFocusNodeAddress first = new AppUIFocusNodeAddress("inventory", key);
            AppUIFocusNodeAddress same = new AppUIFocusNodeAddress(
                "inventory",
                new AppUIFocusNodeKey("item:42"));
            AppUIFocusNodeAddress otherGroup = new AppUIFocusNodeAddress("actions", key);

            Assert.That(key.IsValid, Is.True);
            Assert.That(first.IsValid, Is.True);
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(otherGroup));
        }

        [Test]
        public void ScopeRegistration_MaintainsUniqueForwardAndReverseMappings()
        {
            UIPageInstanceRegistry pageRegistry = new UIPageInstanceRegistry();
            UIPageInstance page = CreatePageInstance(pageRegistry, "Inventory");
            UIFocusService focusService = new UIFocusService();
            AppUIFocusDefinition definition = new AppUIFocusDefinitionBuilder("inventory-scope")
                .AddGroup("items")
                .AddGroup("actions")
                .Build();
            IAppUIFocusScopeHandle scope = focusService.AttachScope(page, definition);

            Button item = CreateSelectable<Button>(page.GameObject, "Item");
            Button action = CreateSelectable<Button>(page.GameObject, "Action");

            AppUIFocusNodeKey commonKey = new AppUIFocusNodeKey("common");
            Assert.That(scope.RegisterNode("items", commonKey, item, 20), Is.True);
            Assert.That(scope.RegisterNode("items", commonKey, item, 10), Is.True);
            Assert.That(scope.RegisterNode("actions", commonKey, action), Is.True);
            LogAssert.Expect(
                LogType.Error,
                "<AppUIFocus> Node registration rejected. Scope=inventory-scope, Group=actions, NodeKey=duplicate, Object=Item");
            Assert.That(
                scope.RegisterNode("actions", new AppUIFocusNodeKey("duplicate"), item),
                Is.False,
                "The same Selectable cannot belong to multiple addresses.");

            Assert.That(item.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(
                scope.TryGetNodeAddress(item, out AppUIFocusNodeAddress itemAddress),
                Is.True);
            Assert.That(itemAddress, Is.EqualTo(new AppUIFocusNodeAddress("items", commonKey)));
            Assert.That(scope.TryGetNodeAddress(item.gameObject, out AppUIFocusNodeAddress objectAddress), Is.True);
            Assert.That(objectAddress, Is.EqualTo(itemAddress));
            Assert.That(scope.TryResolveNode(itemAddress, out Selectable resolved), Is.True);
            Assert.That(resolved, Is.SameAs(item));

            Assert.That(scope.UnregisterNode("items", commonKey), Is.True);
            Assert.That(scope.TryGetNodeAddress(item, out _), Is.False);

            focusService.DetachScope(page);
        }

        [Test]
        public void Snapshot_DrivesScopeAndRootRegion_AndRebindsOperationHandle()
        {
            UIPageInstanceRegistry pageRegistry = new UIPageInstanceRegistry();
            UIPageInstance page = CreatePageInstance(pageRegistry, "Settings");
            UIFocusService focusService = new UIFocusService();
            IAppUIFocusScopeHandle scope = focusService.AttachScope(
                page,
                new AppUIFocusDefinitionBuilder().AddGroup("main").Build());
            Button button = CreateSelectable<Button>(page.GameObject, "Apply");
            AppUIFocusNodeAddress address = new AppUIFocusNodeAddress(
                "main",
                new AppUIFocusNodeKey("apply"));
            Assert.That(
                scope.RegisterNode(address.GroupId, address.NodeKey, button),
                Is.True);
            Assert.That(scope.Status, Is.EqualTo(AppUIFocusScopeStatus.Inactive));
            Assert.That(
                ((IAppUIFocusMoveInputPolicy)scope).ShouldConsumeWithoutNavigation(null),
                Is.True);

            UIPageInteractionHandle firstHandle = page.ToInteractionHandle();
            focusService.ApplyInteractionSnapshot(CreateSnapshot(1, firstHandle, true));
            Assert.That(scope.Status, Is.EqualTo(AppUIFocusScopeStatus.Active));
            Assert.That(scope.RootRegionStatus, Is.EqualTo(AppUIFocusRegionStatus.Active));
            Assert.That(scope.ActiveRegionId, Is.EqualTo(AppUIFocusDefinition.RootRegionId));
            Assert.That(
                ((IAppUIFocusMoveInputPolicy)scope).ShouldConsumeWithoutNavigation(null),
                Is.False);

            page.OperationVersion++;
            UIPageInteractionHandle reopenedHandle = page.ToInteractionHandle();
            focusService.ApplyInteractionSnapshot(CreateSnapshot(2, reopenedHandle, true));
            Assert.That(
                focusService.NodeRegistry.TryResolveNode(firstHandle, address, out _),
                Is.False);
            Assert.That(
                focusService.NodeRegistry.TryResolveNode(
                    reopenedHandle,
                    address,
                    out AppUIFocusResolvedNode reopenedNode),
                Is.True);
            Assert.That(reopenedNode.Selectable, Is.SameAs(button));

            focusService.ApplyInteractionSnapshot(UIInteractionSnapshot.Empty);
            Assert.That(scope.Status, Is.EqualTo(AppUIFocusScopeStatus.Suspended));
            Assert.That(scope.RootRegionStatus, Is.EqualTo(AppUIFocusRegionStatus.Suspended));
            Assert.That(
                ((IAppUIFocusMoveInputPolicy)scope).ShouldConsumeWithoutNavigation(null),
                Is.True);

            focusService.DetachScope(page);
            Assert.That(scope.Status, Is.EqualTo(AppUIFocusScopeStatus.Disposed));
            Assert.That(scope.RootRegionStatus, Is.EqualTo(AppUIFocusRegionStatus.Closed));
            Assert.That(scope.TryGetNodeAddress(button, out _), Is.False);
        }

        [Test]
        public void AttachScope_StaticRegistrationFailure_RollsBackAllMappings()
        {
            UIPageInstanceRegistry pageRegistry = new UIPageInstanceRegistry();
            UIPageInstance page = CreatePageInstance(pageRegistry, "ConflictPage");
            UIFocusService focusService = new UIFocusService();
            Button shared = CreateSelectable<Button>(page.GameObject, "Shared");
            AppUIFocusDefinition invalidDefinition =
                new AppUIFocusDefinitionBuilder("conflict-scope")
                    .AddGroup("left")
                    .AddGroup("right")
                    .AddNode("left", new AppUIFocusNodeKey("shared"), shared)
                    .AddNode("right", new AppUIFocusNodeKey("shared"), shared)
                    .Build();

            Assert.Throws<InvalidOperationException>(
                () => focusService.AttachScope(page, invalidDefinition));
            Assert.That(
                focusService.NodeRegistry.TryResolveNode(shared, out _),
                Is.False,
                "A failed attach must not leak a forward or reverse registration.");
            Assert.That(focusService.TryGetScope(page, out _), Is.False);
        }

        private UIPageInstance CreatePageInstance(
            UIPageInstanceRegistry registry,
            string pageId)
        {
            GameObject root = new GameObject(pageId, typeof(RectTransform));
            createdObjects.Add(root);
            UIPageInstance page = new UIPageInstance
            {
                PageId = pageId,
                GameObject = root,
                RectTransform = root.transform as RectTransform,
                OperationVersion = 1,
                State = UIPageState.Open,
                StackVisible = true,
            };
            registry.Register(page);
            return page;
        }

        private T CreateSelectable<T>(GameObject pageRoot, string name)
            where T : Selectable
        {
            GameObject child = CreateChild(pageRoot, name);
            T selectable = child.AddComponent<T>();
            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
            return selectable;
        }

        private GameObject CreateChild(GameObject pageRoot, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(pageRoot.transform, false);
            createdObjects.Add(child);
            return child;
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
    }
}
