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
        public void FocusPolicy_ExplicitPolicy_WinsWithoutCallingResolvers()
        {
            GameObject root = CreateChild(null, "ExplicitRoot");
            Button button = root.AddComponent<Button>();
            StubControlPolicy explicitPolicy = new StubControlPolicy();
            StubFocusPolicyResolver resolver =
                StubFocusPolicyResolver.Matching("external");
            AppUIFocusControlPolicyResolverSet set =
                new AppUIFocusControlPolicyResolverSet(
                    new IAppUIFocusControlPolicyResolver[] { resolver });

            bool success = set.TryResolve(
                button,
                explicitPolicy,
                out IAppUIFocusControlPolicy resolved,
                out string diagnostic);

            Assert.That(success, Is.True, diagnostic);
            Assert.That(resolved, Is.SameAs(explicitPolicy));
            Assert.That(resolver.CallCount, Is.Zero);
        }

        [Test]
        public void FocusPolicy_OneExternalMatch_WinsBeforeBuiltIn()
        {
            GameObject root = CreateChild(null, "ExternalRoot");
            Slider slider = root.AddComponent<Slider>();
            StubControlPolicy externalPolicy = new StubControlPolicy();
            StubFocusPolicyResolver resolver =
                StubFocusPolicyResolver.Matching(
                    "external",
                    externalPolicy);
            AppUIFocusControlPolicyResolverSet set =
                new AppUIFocusControlPolicyResolverSet(
                    new IAppUIFocusControlPolicyResolver[] { resolver });

            bool success = set.TryResolve(
                slider,
                null,
                out IAppUIFocusControlPolicy resolved,
                out string diagnostic);

            Assert.That(success, Is.True, diagnostic);
            Assert.That(resolved, Is.SameAs(externalPolicy));
            Assert.That(resolver.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void FocusPolicy_NoExternalMatch_UsesBuiltInUGUI()
        {
            GameObject root = CreateChild(null, "BuiltInRoot");
            Slider slider = root.AddComponent<Slider>();
            StubFocusPolicyResolver resolver =
                StubFocusPolicyResolver.NotMatching("external");
            AppUIFocusControlPolicyResolverSet set =
                new AppUIFocusControlPolicyResolverSet(
                    new IAppUIFocusControlPolicyResolver[] { resolver });

            bool success = set.TryResolve(
                slider,
                null,
                out IAppUIFocusControlPolicy resolved,
                out string diagnostic);

            Assert.That(success, Is.True, diagnostic);
            Assert.That(resolved, Is.InstanceOf<IAppUIFocusNativeMoveAdapter>());
        }

        [Test]
        public void FocusPolicy_TwoExternalMatches_RejectsWithResolverIds()
        {
            GameObject root = CreateChild(null, "ConflictRoot");
            Button button = root.AddComponent<Button>();
            AppUIFocusControlPolicyResolverSet set =
                new AppUIFocusControlPolicyResolverSet(
                    new IAppUIFocusControlPolicyResolver[]
                    {
                        StubFocusPolicyResolver.Matching("first"),
                        StubFocusPolicyResolver.Matching("second"),
                    });

            bool success = set.TryResolve(
                button,
                null,
                out IAppUIFocusControlPolicy resolved,
                out string diagnostic);

            Assert.That(success, Is.False);
            Assert.That(resolved, Is.Null);
            StringAssert.Contains("first", diagnostic);
            StringAssert.Contains("second", diagnostic);
        }

        [Test]
        public void FocusPolicy_NullOrThrowingResolver_RejectsWithoutPolicy()
        {
            GameObject root = CreateChild(null, "InvalidResolverRoot");
            Button button = root.AddComponent<Button>();
            StubFocusPolicyResolver nullResolver =
                StubFocusPolicyResolver.Matching("null-policy", null);
            nullResolver.ReturnNull = true;
            AppUIFocusControlPolicyResolverSet nullSet =
                new AppUIFocusControlPolicyResolverSet(
                    new IAppUIFocusControlPolicyResolver[] { nullResolver });

            Assert.That(
                nullSet.TryResolve(
                    button,
                    null,
                    out IAppUIFocusControlPolicy nullPolicy,
                    out string nullDiagnostic),
                Is.False);
            Assert.That(nullPolicy, Is.Null);
            StringAssert.Contains("null-policy", nullDiagnostic);

            StubFocusPolicyResolver throwing =
                StubFocusPolicyResolver.NotMatching("throwing");
            throwing.ThrowOnResolve = true;
            AppUIFocusControlPolicyResolverSet throwingSet =
                new AppUIFocusControlPolicyResolverSet(
                    new IAppUIFocusControlPolicyResolver[] { throwing });
            Assert.That(
                throwingSet.TryResolve(
                    button,
                    null,
                    out IAppUIFocusControlPolicy throwingPolicy,
                    out string throwingDiagnostic),
                Is.False);
            Assert.That(throwingPolicy, Is.Null);
            StringAssert.Contains("throwing", throwingDiagnostic);
        }

        [Test]
        public void FocusPolicy_ResolverConflict_RejectsRegistrationWithoutMutation()
        {
            UIPageInstanceRegistry pageRegistry = new UIPageInstanceRegistry();
            UIPageInstance page = CreatePageInstance(
                pageRegistry,
                "ResolverConflict");
            UIFocusService focusService = new UIFocusService();
            focusService.ConfigurePolicyResolvers(
                new IAppUIFocusControlPolicyResolver[]
                {
                    StubFocusPolicyResolver.Matching("first"),
                    StubFocusPolicyResolver.Matching("second"),
                });
            IAppUIFocusScopeHandle scope = focusService.AttachScope(
                page,
                new AppUIFocusDefinitionBuilder("resolver-conflict")
                    .AddGroup("main")
                    .Build());
            Button button = CreateSelectable<Button>(
                page.GameObject,
                "ConflictButton");
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "APPUI_FOCUS_POLICY_RESOLUTION_FAILED.*first.*second"));

            bool registered = scope.RegisterNode(
                "main",
                new AppUIFocusNodeKey("conflict"),
                button);

            Assert.That(registered, Is.False);
            Assert.That(scope.TryGetNodeAddress(button, out _), Is.False);
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Automatic));
            Assert.That(button.GetComponent<AppUIFocusGroupNode>(), Is.Null);
            focusService.DetachScope(page);
        }

        [Test]
        public void FocusPolicy_LegacyNavigatorConflict_LogsAndPreservesGroup()
        {
            GameObject root = CreateChild(null, "LegacyConflictRoot");
            Button button = root.AddComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;
            AppUIFocusControlPolicyResolverSet set =
                new AppUIFocusControlPolicyResolverSet(
                    new IAppUIFocusControlPolicyResolver[]
                    {
                        StubFocusPolicyResolver.Matching("first"),
                        StubFocusPolicyResolver.Matching("second"),
                    });
            AppUIFocusGroupNavigator navigator =
                new AppUIFocusGroupNavigator(set);
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "APPUI_FOCUS_POLICY_RESOLUTION_FAILED.*first.*second"));

            navigator.RegisterNode("main", button);

            Assert.That(
                navigator.TryGetGroupFirst("main", out _),
                Is.False);
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Automatic));
            Assert.That(button.GetComponent<AppUIFocusGroupNode>(), Is.Null);
            navigator.Dispose();
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
            if (pageRoot != null)
            {
                child.transform.SetParent(pageRoot.transform, false);
            }

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

        private sealed class StubControlPolicy : IAppUIFocusControlPolicy
        {
            public AppUIFocusControlMoveMode GetMoveMode(
                in AppUIFocusMoveContext context)
            {
                return AppUIFocusControlMoveMode.FrameworkOnly;
            }

            public AppUIFocusCancelHandlingResult TryHandleCancel(
                in AppUIFocusCancelContext context)
            {
                return AppUIFocusCancelHandlingResult.Continue;
            }
        }

        private sealed class StubFocusPolicyResolver :
            IAppUIFocusControlPolicyResolver
        {
            private readonly bool matches;
            private readonly IAppUIFocusControlPolicy policy;

            private StubFocusPolicyResolver(
                string resolverId,
                bool matches,
                IAppUIFocusControlPolicy policy)
            {
                ResolverId = resolverId;
                this.matches = matches;
                this.policy = policy;
            }

            public string ResolverId { get; }

            public int CallCount { get; private set; }

            public bool ReturnNull { get; set; }

            public bool ThrowOnResolve { get; set; }

            public bool TryResolve(
                Selectable selectable,
                out IAppUIFocusControlPolicy resolvedPolicy)
            {
                CallCount++;
                if (ThrowOnResolve)
                {
                    throw new InvalidOperationException(
                        "intentional resolver failure");
                }

                resolvedPolicy = ReturnNull ? null : policy;
                return matches;
            }

            public static StubFocusPolicyResolver Matching(
                string resolverId,
                IAppUIFocusControlPolicy policy = null)
            {
                return new StubFocusPolicyResolver(
                    resolverId,
                    true,
                    policy ?? new StubControlPolicy());
            }

            public static StubFocusPolicyResolver NotMatching(
                string resolverId)
            {
                return new StubFocusPolicyResolver(
                    resolverId,
                    false,
                    null);
            }
        }
    }
}
