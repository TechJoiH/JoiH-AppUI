using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFocusDropdownPolicyTests
    {
        private GameObject dropdownObject;

        [TearDown]
        public void TearDown()
        {
            if (dropdownObject != null)
            {
                UnityEngine.Object.DestroyImmediate(dropdownObject);
                dropdownObject = null;
            }
        }

        [Test]
        public void DropdownPolicy_UGUIConstructor_PreservesChildRegionId()
        {
            Dropdown dropdown = CreateDropdown();

            AppUIFocusDropdownControlPolicy policy =
                new AppUIFocusDropdownControlPolicy(
                    dropdown,
                    "options");

            Assert.That(policy.ChildRegionId, Is.EqualTo("options"));
            Assert.That(policy.IsExpanded, Is.False);
        }

        [Test]
        public void DropdownPolicy_ExpandedState_OpensAndClosesChildRegion()
        {
            AppUIFocusDropdownControlPolicy policy =
                new AppUIFocusDropdownControlPolicy(
                    CreateDropdown(),
                    "options");
            RecordingScope scope = new RecordingScope();

            AppUIFocusRequestResult opened =
                policy.SynchronizeRegion(scope, true);
            AppUIFocusRequestResult closed =
                policy.SynchronizeRegion(scope, false);

            Assert.That(opened, Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(closed, Is.EqualTo(AppUIFocusRequestResult.Focused));
            Assert.That(scope.OpenCount, Is.EqualTo(1));
            Assert.That(scope.CloseCount, Is.EqualTo(1));
            Assert.That(policy.IsExpanded, Is.False);
        }

        [Test]
        public void DropdownPolicy_RegionCancel_CollapsesAndConsumes()
        {
            AppUIFocusDropdownControlPolicy policy =
                new AppUIFocusDropdownControlPolicy(
                    CreateDropdown(),
                    "options");
            RecordingScope scope = new RecordingScope();
            policy.SynchronizeRegion(scope, true);
            AppUIFocusRegionCancelContext context =
                new AppUIFocusRegionCancelContext(
                    "scope",
                    "options",
                    default);

            AppUIFocusCancelHandlingResult result =
                ((IAppUIFocusRegionCancelHandler)policy)
                .TryHandleCancel(in context);

            Assert.That(
                result,
                Is.EqualTo(AppUIFocusCancelHandlingResult.Consumed));
            Assert.That(policy.IsExpanded, Is.False);
        }

        [Test]
        public void DropdownBridge_Dispose_UnsubscribesValueChanged()
        {
            Dropdown dropdown = CreateDropdown();
            AppUIFocusDropdownControlPolicy policy =
                new AppUIFocusDropdownControlPolicy(
                    dropdown,
                    "options");
            RecordingScope scope = new RecordingScope();
            policy.SynchronizeRegion(scope, true);
            IDisposable binding = policy.Bind(scope);
            int closeCountBeforeDispose = scope.CloseCount;

            binding.Dispose();
            dropdown.onValueChanged.Invoke(1);

            Assert.That(scope.CloseCount, Is.EqualTo(closeCountBeforeDispose));
        }

        [Test]
        public void BaseRuntimeAssembly_DoesNotReferenceTextMeshPro()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(
                typeof(AppUIManager).Assembly);
            Assert.That(packageInfo, Is.Not.Null);
            string asmdefPath = Path.Combine(
                packageInfo.resolvedPath,
                "Runtime",
                "Joi.H.AppUI.Runtime.asmdef");
            string json = File.ReadAllText(asmdefPath);

            string forbiddenReference = "Unity.Text" + "MeshPro";
            StringAssert.DoesNotContain(forbiddenReference, json);
        }

        private Dropdown CreateDropdown()
        {
            dropdownObject = new GameObject(
                "Dropdown",
                typeof(RectTransform),
                typeof(Dropdown));
            return dropdownObject.GetComponent<Dropdown>();
        }

        private sealed class RecordingScope : IAppUIFocusScopeHandle
        {
            private AppUIFocusRegionStatus regionStatus =
                AppUIFocusRegionStatus.Closed;

            public string ScopeId => "scope";
            public string ActiveRegionId =>
                regionStatus == AppUIFocusRegionStatus.Active
                    ? "options"
                    : string.Empty;
            public AppUIFocusScopeStatus Status =>
                AppUIFocusScopeStatus.Active;
            public AppUIFocusRegionStatus RootRegionStatus =>
                AppUIFocusRegionStatus.Active;
            public int Revision => 1;
            public int OpenCount { get; private set; }
            public int CloseCount { get; private set; }

            public bool RegisterNode(
                string groupId,
                AppUIFocusNodeKey nodeKey,
                Selectable selectable,
                int order = 0)
            {
                return false;
            }

            public bool RegisterNode(
                string groupId,
                AppUIFocusNodeKey nodeKey,
                Selectable selectable,
                IAppUIFocusControlPolicy controlPolicy,
                int order = 0)
            {
                return false;
            }

            public bool UnregisterNode(
                string groupId,
                AppUIFocusNodeKey nodeKey)
            {
                return false;
            }

            public AppUIFocusGroupUpdateResult BeginGroupUpdate(
                string groupId,
                out AppUIFocusGroupUpdateTransaction transaction)
            {
                transaction = null;
                return AppUIFocusGroupUpdateResult.ValidationFailed;
            }

            public bool ClearGroup(string groupId) { return false; }
            public bool OpenGroup(string groupId) { return false; }
            public bool CloseGroup(string groupId) { return false; }
            public bool IsGroupOpen(string groupId) { return false; }

            public AppUIFocusRegionStatus GetRegionStatus(string regionId)
            {
                return string.Equals(
                    regionId,
                    "options",
                    StringComparison.Ordinal)
                    ? regionStatus
                    : AppUIFocusRegionStatus.Closed;
            }

            public AppUIFocusRequestResult OpenRegion(
                string regionId,
                AppUIFocusRegionEntryPolicy entryPolicy =
                    AppUIFocusRegionEntryPolicy.LastFocusedOrDefault)
            {
                OpenCount++;
                regionStatus = AppUIFocusRegionStatus.Active;
                return AppUIFocusRequestResult.Focused;
            }

            public AppUIFocusRequestResult CloseRegion(string regionId)
            {
                CloseCount++;
                regionStatus = AppUIFocusRegionStatus.Closed;
                return AppUIFocusRequestResult.Focused;
            }

            public AppUIFocusRequestResult FocusNode(
                AppUIFocusNodeAddress nodeAddress,
                AppUIFocusChangeReason reason =
                    AppUIFocusChangeReason.Programmatic)
            {
                return AppUIFocusRequestResult.NodeMissing;
            }

            public AppUIFocusRequestResult FocusGroupFirst(
                string groupId,
                AppUIFocusChangeReason reason =
                    AppUIFocusChangeReason.Programmatic)
            {
                return AppUIFocusRequestResult.NodeMissing;
            }

            public bool TryResolveNode(
                AppUIFocusNodeAddress nodeAddress,
                out Selectable selectable)
            {
                selectable = null;
                return false;
            }

            public bool TryGetNodeAddress(
                Selectable selectable,
                out AppUIFocusNodeAddress nodeAddress)
            {
                nodeAddress = default;
                return false;
            }

            public bool TryGetNodeAddress(
                GameObject selectedObject,
                out AppUIFocusNodeAddress nodeAddress)
            {
                nodeAddress = default;
                return false;
            }
        }
    }
}
