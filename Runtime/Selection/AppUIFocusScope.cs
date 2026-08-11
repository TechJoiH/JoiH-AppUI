using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public enum AppUIFocusScopeStatus
    {
        Inactive,
        Active,
        Suspended,
        Disposed,
    }

    /// <summary>
    /// 页面可持有的受限 FocusScope 句柄。
    /// 只允许维护页面内节点、Group 和 Region，不暴露框架生命周期与 EventSystem 写入。
    /// </summary>
    public interface IAppUIFocusScopeHandle
    {
        string ScopeId { get; }

        string ActiveRegionId { get; }

        AppUIFocusScopeStatus Status { get; }

        AppUIFocusRegionStatus RootRegionStatus { get; }

        int Revision { get; }

        bool RegisterNode(
            string groupId,
            AppUIFocusNodeKey nodeKey,
            Selectable selectable,
            int order = 0);

        bool RegisterNode(
            string groupId,
            AppUIFocusNodeKey nodeKey,
            Selectable selectable,
            IAppUIFocusControlPolicy controlPolicy,
            int order = 0);

        bool UnregisterNode(string groupId, AppUIFocusNodeKey nodeKey);

        AppUIFocusGroupUpdateResult BeginGroupUpdate(
            string groupId,
            out AppUIFocusGroupUpdateTransaction transaction);

        bool ClearGroup(string groupId);

        bool OpenGroup(string groupId);

        bool CloseGroup(string groupId);

        bool IsGroupOpen(string groupId);

        AppUIFocusRegionStatus GetRegionStatus(string regionId);

        AppUIFocusRequestResult OpenRegion(
            string regionId,
            AppUIFocusRegionEntryPolicy entryPolicy =
                AppUIFocusRegionEntryPolicy.LastFocusedOrDefault);

        AppUIFocusRequestResult CloseRegion(string regionId);

        AppUIFocusRequestResult FocusNode(
            AppUIFocusNodeAddress nodeAddress,
            AppUIFocusChangeReason reason = AppUIFocusChangeReason.Programmatic);

        AppUIFocusRequestResult FocusGroupFirst(
            string groupId,
            AppUIFocusChangeReason reason = AppUIFocusChangeReason.Programmatic);

        bool TryResolveNode(
            AppUIFocusNodeAddress nodeAddress,
            out Selectable selectable);

        bool TryGetNodeAddress(
            Selectable selectable,
            out AppUIFocusNodeAddress nodeAddress);

        bool TryGetNodeAddress(
            GameObject selectedObject,
            out AppUIFocusNodeAddress nodeAddress);
    }

    internal sealed class AppUIFocusScope :
        IAppUIFocusScopeHandle,
        IAppUIFocusMoveInputPolicy,
        IAppUIFocusCommitGateway,
        IAppUIFocusRegionNavigationGateway,
        IDisposable
    {
        internal const string RootRegionId = AppUIFocusDefinition.RootRegionId;

        private sealed class FocusNodeRecord
        {
            public AppUIFocusNodeAddress Address;
            public Selectable Selectable;
            public IAppUIFocusControlPolicy ControlPolicy;
            public int Order;
            public int Sequence;
            public int RegistrationGeneration;
        }

        private sealed class FocusGroupState
        {
            public Dictionary<AppUIFocusNodeKey, FocusNodeRecord> NodesByKey =
                new Dictionary<AppUIFocusNodeKey, FocusNodeRecord>(16);
            public List<FocusNodeRecord> OrderedNodes =
                new List<FocusNodeRecord>(16);

            public string GroupId;
            public string RegionId;
            public int Order;
            public bool IsOpen;
            public IAppUIFocusVisibilityAdapter VisibilityAdapter;
            public IAppUIFocusVirtualizationAdapter VirtualizationAdapter;
            public int Revision = 1;
            public AppUIFocusGroupUpdateTransaction ActiveTransaction;
        }

        private sealed class FocusRegionState
        {
            public readonly List<FocusRegionState> Children =
                new List<FocusRegionState>(2);

            public string RegionId;
            public string ParentRegionId;
            public string DefaultGroupId;
            public AppUIFocusRegionStatus Status = AppUIFocusRegionStatus.Closed;
            public int Revision = 1;
            public int Depth;
            public string ActiveChildRegionId;
            public AppUIFocusNodeAddress SourceNodeAddress;
            public string LastFocusedGroupId;
            public AppUIFocusNodeAddress LastFocusedNodeAddress;
            public AppUIFocusNodeAddress PendingRestoreAddress;
            public IAppUIFocusRegionCancelHandler CancelHandler;
            public bool AutoAdjacent;
            public bool HasPendingRepair;
        }

        private readonly struct RegionAdjacencyKey : IEquatable<RegionAdjacencyKey>
        {
            public RegionAdjacencyKey(
                string regionId,
                string sourceGroupId,
                MoveDirection moveDirection)
            {
                RegionId = regionId ?? string.Empty;
                SourceGroupId = sourceGroupId ?? string.Empty;
                MoveDirection = moveDirection;
            }

            public string RegionId { get; }

            public string SourceGroupId { get; }

            public MoveDirection MoveDirection { get; }

            public bool Equals(RegionAdjacencyKey other)
            {
                return MoveDirection == other.MoveDirection &&
                       string.Equals(RegionId, other.RegionId, StringComparison.Ordinal) &&
                       string.Equals(
                           SourceGroupId,
                           other.SourceGroupId,
                           StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is RegionAdjacencyKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StringComparer.Ordinal.GetHashCode(RegionId);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(SourceGroupId);
                    return (hash * 397) ^ (int)MoveDirection;
                }
            }
        }

        private sealed class ScopeAnchorProvider :
            IAppUIFocusAnchorProvider,
            IAppUIFocusTargetAnchorProvider
        {
            private readonly AppUIFocusScope owner;
            private readonly IAppUIFocusAnchorTargetProvider targetProvider;

            public ScopeAnchorProvider(
                AppUIFocusScope focusScope,
                IAppUIFocusAnchorTargetProvider provider)
            {
                owner = focusScope;
                targetProvider = provider;
            }

            public bool TryGetFocusAnchor(
                string anchorId,
                out Selectable selectable)
            {
                selectable = null;
                if (targetProvider == null ||
                    !targetProvider.TryGetFocusAnchor(
                        anchorId,
                        out AppUIFocusTarget target) ||
                    !target.IsValid)
                {
                    return false;
                }

                if (target.Kind == AppUIFocusTargetKind.NodeAddress)
                {
                    return owner.TryResolveNode(target.NodeAddress, out selectable);
                }

                if (target.Kind == AppUIFocusTargetKind.Selectable &&
                    owner.TryGetNodeAddress(
                        target.Selectable,
                        out AppUIFocusNodeAddress nodeAddress))
                {
                    return owner.TryResolveNode(nodeAddress, out selectable);
                }

                return false;
            }

            public bool TryGetFocusAnchorTarget(
                string anchorId,
                out AppUIFocusTarget target)
            {
                target = default;
                return targetProvider != null &&
                       targetProvider.TryGetFocusAnchor(anchorId, out target) &&
                       target.IsValid;
            }
        }

        private readonly long pageInstanceId;
        private readonly string pageId;
        private readonly Transform pageRoot;
        private readonly AppUIFocusNodeRegistry nodeRegistry;
        private readonly IUIFocusCommitter focusCommitter;
        private readonly AppUIFocusGroupNavigator navigator;
        private readonly ScopeAnchorProvider anchorProvider;
        private readonly IAppUIFocusMoveInputPolicy pageMoveInputPolicy;
        private readonly bool traceEnabled;
        private readonly StringBuilder traceCandidateBuilder;
        private FocusRegionState rootRegion;
        private readonly Dictionary<string, FocusRegionState> regions =
            new Dictionary<string, FocusRegionState>(4, StringComparer.Ordinal);
        private readonly List<FocusRegionState> orderedRegions =
            new List<FocusRegionState>(4);
        private readonly Dictionary<RegionAdjacencyKey, string> regionAdjacencies =
            new Dictionary<RegionAdjacencyKey, string>(8);
        private readonly Dictionary<string, FocusGroupState> groups =
            new Dictionary<string, FocusGroupState>(8, StringComparer.Ordinal);
        private readonly List<FocusGroupState> orderedGroups =
            new List<FocusGroupState>(8);
        private readonly List<Selectable> navigatorNodeBuffer =
            new List<Selectable>(16);
        private readonly List<IAppUIFocusControlPolicy> navigatorControlPolicyBuffer =
            new List<IAppUIFocusControlPolicy>(16);
        private readonly List<AppUIFocusResolvedNode> replacementRecordBuffer =
            new List<AppUIFocusResolvedNode>(16);
        private readonly Dictionary<string, AppUIFocusNodeKey> nodeHistoryByGroup =
            new Dictionary<string, AppUIFocusNodeKey>(8, StringComparer.Ordinal);
        private readonly Vector3[] spatialWorldCorners = new Vector3[4];

        private UIPageInteractionHandle pageHandle;
        private AppUIFocusScopeStatus status = AppUIFocusScopeStatus.Inactive;
        private AppUIFocusNodeAddress currentFocusedAddress;
        private AppUIFocusNodeAddress lastFocusedAddress;
        private bool hasPendingRepair;
        private int currentStackRevision;
        private int revision = 1;
        private int selectionRepairRevision = 1;
        private int nextNodeSequence;
        private int pendingRealizationSerial;
        private CancellationTokenSource pendingRealizationCancellation;
        private AppUIFocusNodeAddress pendingRealizationAddress;
        private string activeLeafRegionId = RootRegionId;

        internal AppUIFocusScope(
            UIPageInstance instance,
            AppUIFocusDefinition definition,
            string scopeId,
            AppUIFocusNodeRegistry registry,
            IUIFocusCommitter committer,
            IAppUIFocusSelectionObservationSink selectionObserver)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(scopeId))
            {
                throw new ArgumentException("Focus scope id cannot be empty.", nameof(scopeId));
            }

            if (instance.GameObject == null || instance.RuntimeInstanceId <= 0)
            {
                throw new InvalidOperationException(
                    "FocusScope requires a registered page instance with a live root GameObject.");
            }

            nodeRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
            focusCommitter = committer ?? throw new ArgumentNullException(nameof(committer));
            ScopeId = scopeId;
            pageId = instance.PageId ?? string.Empty;
            pageInstanceId = instance.RuntimeInstanceId;
            pageHandle = instance.ToInteractionHandle();
            pageRoot = instance.GameObject.transform;
            traceEnabled = definition.DebugTraceEnabled;
            if (traceEnabled)
            {
                traceCandidateBuilder = new StringBuilder(256);
            }

            AppUIFocusTrace.RegisterScope(
                pageInstanceId,
                pageId,
                ScopeId,
                traceEnabled);
            navigator = new AppUIFocusGroupNavigator();
            navigator.SetDiagnosticScopeId(ScopeId);
            navigator.SetDiagnosticPageInstanceId(pageInstanceId);
            pageMoveInputPolicy = definition.MoveInputPolicy;

            try
            {
                navigator.SetChain(definition.FocusChain);
                if (definition.AnchorTargetProvider != null)
                {
                    anchorProvider = new ScopeAnchorProvider(
                        this,
                        definition.AnchorTargetProvider);
                    navigator.SetAnchorProvider(anchorProvider);
                }

                navigator.SetMoveInputPolicy(this);
                navigator.SetCommitGateway(this);
                navigator.SetRegionGateway(this);
                navigator.SetSelectionObservationSink(selectionObserver);
                BuildRegions(definition);
                BuildGroups(definition);
                RegisterDefinitionNodes(definition);
                if (AppUIFocusTrace.CanTrace(pageInstanceId))
                {
                    AppUIFocusTrace.Record(
                        pageInstanceId,
                        AppUIFocusTraceStage.Scope,
                        default,
                        default,
                        "Scope attached.");
                    PublishDebugSnapshot();
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string ScopeId { get; }

        public string ActiveRegionId
        {
            get
            {
                return regions.TryGetValue(
                           activeLeafRegionId,
                           out FocusRegionState activeRegion) &&
                       activeRegion.Status == AppUIFocusRegionStatus.Active
                    ? activeLeafRegionId
                    : string.Empty;
            }
        }

        public AppUIFocusScopeStatus Status
        {
            get { return status; }
        }

        public AppUIFocusRegionStatus RootRegionStatus
        {
            get
            {
                return rootRegion != null
                    ? rootRegion.Status
                    : AppUIFocusRegionStatus.Closed;
            }
        }

        public int Revision
        {
            get { return revision; }
        }

        internal UIPageInteractionHandle PageHandle
        {
            get { return pageHandle; }
        }

        internal long PageInstanceId
        {
            get { return pageInstanceId; }
        }

        internal int RootRegionRevision
        {
            get { return rootRegion != null ? rootRegion.Revision : 0; }
        }

        internal AppUIFocusNodeAddress CurrentFocusedAddress
        {
            get { return currentFocusedAddress; }
        }

        internal AppUIFocusNodeAddress LastFocusedAddress
        {
            get { return lastFocusedAddress; }
        }

        internal bool HasPendingRepair
        {
            get { return hasPendingRepair; }
        }

        internal int SelectionRepairRevision
        {
            get { return selectionRepairRevision; }
        }

        internal bool TryGetFocusedControlPolicy(
            GameObject selectedObject,
            out string groupId,
            out AppUIFocusNodeAddress nodeAddress,
            out Selectable selectable,
            out IAppUIFocusControlPolicy controlPolicy)
        {
            groupId = string.Empty;
            nodeAddress = default;
            selectable = null;
            controlPolicy = null;
            if (status != AppUIFocusScopeStatus.Active ||
                selectedObject == null ||
                !nodeRegistry.TryResolveNode(
                    selectedObject,
                    out AppUIFocusResolvedNode resolvedNode) ||
                !IsOwnedRecord(resolvedNode) ||
                !IsResolvedNodeEligible(resolvedNode) ||
                !groups.TryGetValue(
                    resolvedNode.NodeAddress.GroupId,
                    out FocusGroupState group) ||
                !group.NodesByKey.TryGetValue(
                    resolvedNode.NodeAddress.NodeKey,
                    out FocusNodeRecord record) ||
                record.RegistrationGeneration != resolvedNode.RegistrationGeneration)
            {
                return false;
            }

            groupId = group.GroupId;
            nodeAddress = resolvedNode.NodeAddress;
            selectable = resolvedNode.Selectable;
            controlPolicy = record.ControlPolicy;
            return controlPolicy != null;
        }

        internal AppUIFocusCancelHandlingResult TryHandleActiveRegionCancel()
        {
            if (status != AppUIFocusScopeStatus.Active ||
                string.IsNullOrEmpty(activeLeafRegionId) ||
                string.Equals(
                    activeLeafRegionId,
                    RootRegionId,
                    StringComparison.Ordinal) ||
                !regions.TryGetValue(
                    activeLeafRegionId,
                    out FocusRegionState activeRegion) ||
                activeRegion.Status != AppUIFocusRegionStatus.Active ||
                activeRegion.CancelHandler == null)
            {
                return AppUIFocusCancelHandlingResult.Continue;
            }

            AppUIFocusRegionCancelContext context =
                new AppUIFocusRegionCancelContext(
                    ScopeId,
                    activeRegion.RegionId,
                    activeRegion.SourceNodeAddress);
            AppUIFocusCancelHandlingResult result =
                activeRegion.CancelHandler.TryHandleCancel(in context);
            if (result == AppUIFocusCancelHandlingResult.Consumed)
            {
                CloseRegionInternal(activeRegion.RegionId);
            }

            return result;
        }

        public bool RegisterNode(
            string groupId,
            AppUIFocusNodeKey nodeKey,
            Selectable selectable,
            int order = 0)
        {
            return RegisterNode(groupId, nodeKey, selectable, null, order);
        }

        public bool RegisterNode(
            string groupId,
            AppUIFocusNodeKey nodeKey,
            Selectable selectable,
            IAppUIFocusControlPolicy controlPolicy,
            int order = 0)
        {
            CancelPendingRealization();
            IAppUIFocusControlPolicy effectiveControlPolicy =
                AppUIFocusControlPolicies.Resolve(selectable, controlPolicy);
            if (status == AppUIFocusScopeStatus.Disposed ||
                string.IsNullOrEmpty(groupId) ||
                !nodeKey.IsValid ||
                selectable == null ||
                !groups.TryGetValue(groupId, out FocusGroupState group) ||
                group.ActiveTransaction != null ||
                !IsUnderPageRoot(selectable.transform))
            {
                LogRegistrationRejected(groupId, nodeKey, selectable);
                return false;
            }

            if (group.NodesByKey.TryGetValue(
                    nodeKey,
                    out FocusNodeRecord existingRecord))
            {
                if (existingRecord.Selectable == null)
                {
                    RemoveNodeRecord(group, existingRecord);
                }
                else if (!ReferenceEquals(existingRecord.Selectable, selectable))
                {
                    LogRegistrationRejected(groupId, nodeKey, selectable);
                    return false;
                }
                else
                {
                    if (existingRecord.Order != order ||
                        !ReferenceEquals(
                            existingRecord.ControlPolicy,
                            effectiveControlPolicy))
                    {
                        existingRecord.Order = order;
                        existingRecord.ControlPolicy = effectiveControlPolicy;
                        SortGroupNodes(group);
                        group.Revision++;
                        RebuildNavigatorGroup(group);
                    }

                    return true;
                }
            }

            AppUIFocusNodeAddress address = new AppUIFocusNodeAddress(groupId, nodeKey);
            if (!nodeRegistry.TryRegister(
                    pageHandle,
                    ScopeId,
                    group.RegionId,
                    address,
                    selectable,
                    out AppUIFocusResolvedNode resolvedNode))
            {
                LogRegistrationRejected(groupId, nodeKey, selectable);
                return false;
            }

            FocusNodeRecord record = new FocusNodeRecord
            {
                Address = address,
                Selectable = selectable,
                ControlPolicy = effectiveControlPolicy,
                Order = order,
                Sequence = GetNextNodeSequence(),
                RegistrationGeneration = resolvedNode.RegistrationGeneration,
            };
            group.NodesByKey.Add(nodeKey, record);
            group.OrderedNodes.Add(record);
            SortGroupNodes(group);
            group.Revision++;
            RebuildNavigatorGroup(group);
            ReconcileGroupStructure(group);
            return true;
        }

        public bool UnregisterNode(string groupId, AppUIFocusNodeKey nodeKey)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Disposed ||
                string.IsNullOrEmpty(groupId) ||
                !nodeKey.IsValid ||
                !groups.TryGetValue(groupId, out FocusGroupState group) ||
                group.ActiveTransaction != null ||
                !group.NodesByKey.TryGetValue(nodeKey, out FocusNodeRecord record))
            {
                return false;
            }

            RemoveNodeRecord(group, record);
            group.Revision++;
            RebuildNavigatorGroup(group);
            ReconcileGroupStructure(group);
            return true;
        }

        public AppUIFocusGroupUpdateResult BeginGroupUpdate(
            string groupId,
            out AppUIFocusGroupUpdateTransaction transaction)
        {
            CancelPendingRealization();
            transaction = null;
            if (status == AppUIFocusScopeStatus.Disposed)
            {
                return AppUIFocusGroupUpdateResult.ScopeDisposed;
            }

            if (string.IsNullOrEmpty(groupId) ||
                !groups.TryGetValue(groupId, out FocusGroupState group))
            {
                return AppUIFocusGroupUpdateResult.ValidationFailed;
            }

            if (group.ActiveTransaction != null)
            {
                return AppUIFocusGroupUpdateResult.TransactionAlreadyActive;
            }

            transaction = new AppUIFocusGroupUpdateTransaction(
                this,
                groupId,
                revision,
                group.Revision);
            group.ActiveTransaction = transaction;
            return AppUIFocusGroupUpdateResult.Started;
        }

        public bool ClearGroup(string groupId)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Disposed ||
                string.IsNullOrEmpty(groupId) ||
                !groups.TryGetValue(groupId, out FocusGroupState group) ||
                group.ActiveTransaction != null)
            {
                return false;
            }

            replacementRecordBuffer.Clear();
            if (!nodeRegistry.TryReplaceGroup(
                    pageHandle,
                    ScopeId,
                    group.RegionId,
                    groupId,
                    Array.Empty<AppUIFocusStagedNode>(),
                    replacementRecordBuffer))
            {
                replacementRecordBuffer.Clear();
                return false;
            }

            replacementRecordBuffer.Clear();
            group.NodesByKey = new Dictionary<AppUIFocusNodeKey, FocusNodeRecord>(0);
            group.OrderedNodes = new List<FocusNodeRecord>(0);
            group.IsOpen = false;
            group.Revision++;
            navigator.ClearGroup(groupId);
            ReconcileGroupStructure(group);
            return true;
        }

        internal AppUIFocusGroupUpdateResult CompleteGroupUpdate(
            AppUIFocusGroupUpdateTransaction transaction)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Disposed)
            {
                return AppUIFocusGroupUpdateResult.ScopeDisposed;
            }

            if (transaction == null ||
                !groups.TryGetValue(transaction.GroupId, out FocusGroupState group) ||
                !ReferenceEquals(group.ActiveTransaction, transaction))
            {
                return AppUIFocusGroupUpdateResult.StaleRevision;
            }

            try
            {
                if (transaction.CapturedScopeRevision != revision ||
                    transaction.CapturedGroupRevision != group.Revision)
                {
                    return AppUIFocusGroupUpdateResult.StaleRevision;
                }

                IReadOnlyList<AppUIFocusStagedNode> stagedNodes = transaction.StagedNodes;
                if (transaction.HasValidationFailure ||
                    (long)nextNodeSequence + stagedNodes.Count > int.MaxValue)
                {
                    return AppUIFocusGroupUpdateResult.ValidationFailed;
                }

                HashSet<AppUIFocusNodeKey> nodeKeys =
                    new HashSet<AppUIFocusNodeKey>();
                for (int i = 0; i < stagedNodes.Count; i++)
                {
                    AppUIFocusStagedNode stagedNode = stagedNodes[i];
                    if (!stagedNode.NodeKey.IsValid ||
                        stagedNode.Selectable == null ||
                        stagedNode.Selectable.gameObject == null ||
                        !nodeKeys.Add(stagedNode.NodeKey) ||
                        !IsUnderPageRoot(stagedNode.Selectable.transform))
                    {
                        return AppUIFocusGroupUpdateResult.ValidationFailed;
                    }
                }

                replacementRecordBuffer.Clear();
                if (!nodeRegistry.TryReplaceGroup(
                        pageHandle,
                        ScopeId,
                        group.RegionId,
                        group.GroupId,
                        stagedNodes,
                        replacementRecordBuffer))
                {
                    return AppUIFocusGroupUpdateResult.ValidationFailed;
                }

                Dictionary<AppUIFocusNodeKey, FocusNodeRecord> replacementByKey =
                    new Dictionary<AppUIFocusNodeKey, FocusNodeRecord>(stagedNodes.Count);
                List<FocusNodeRecord> replacementNodes =
                    new List<FocusNodeRecord>(stagedNodes.Count);
                for (int i = 0; i < stagedNodes.Count; i++)
                {
                    AppUIFocusStagedNode stagedNode = stagedNodes[i];
                    AppUIFocusResolvedNode resolvedNode = replacementRecordBuffer[i];
                    FocusNodeRecord record = new FocusNodeRecord
                    {
                        Address = resolvedNode.NodeAddress,
                        Selectable = resolvedNode.Selectable,
                        ControlPolicy = AppUIFocusControlPolicies.Resolve(
                            resolvedNode.Selectable,
                            stagedNode.ControlPolicy),
                        Order = stagedNode.Order,
                        Sequence = GetNextNodeSequence(),
                        RegistrationGeneration = resolvedNode.RegistrationGeneration,
                    };
                    replacementByKey.Add(stagedNode.NodeKey, record);
                    replacementNodes.Add(record);
                }

                replacementNodes.Sort(CompareNodes);
                group.NodesByKey = replacementByKey;
                group.OrderedNodes = replacementNodes;
                group.Revision++;
                RebuildNavigatorGroup(group);
                ReconcileGroupStructure(group);
                return AppUIFocusGroupUpdateResult.Completed;
            }
            finally
            {
                replacementRecordBuffer.Clear();
                if (ReferenceEquals(group.ActiveTransaction, transaction))
                {
                    group.ActiveTransaction = null;
                }
            }
        }

        internal void AbortGroupUpdate(AppUIFocusGroupUpdateTransaction transaction)
        {
            if (transaction == null ||
                !groups.TryGetValue(transaction.GroupId, out FocusGroupState group) ||
                !ReferenceEquals(group.ActiveTransaction, transaction))
            {
                return;
            }

            group.ActiveTransaction = null;
        }

        public bool OpenGroup(string groupId)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Disposed ||
                string.IsNullOrEmpty(groupId) ||
                !groups.TryGetValue(groupId, out FocusGroupState group))
            {
                return false;
            }

            if (!group.IsOpen)
            {
                group.IsOpen = true;
                group.Revision++;
                RefreshNavigatorGroupVisibility(group);
            }

            return true;
        }

        public bool CloseGroup(string groupId)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Disposed ||
                string.IsNullOrEmpty(groupId) ||
                !groups.TryGetValue(groupId, out FocusGroupState group))
            {
                return false;
            }

            if (group.IsOpen)
            {
                group.IsOpen = false;
                group.Revision++;
                RefreshNavigatorGroupVisibility(group);
                if (currentFocusedAddress.IsValid &&
                    string.Equals(
                        currentFocusedAddress.GroupId,
                        groupId,
                        StringComparison.Ordinal))
                {
                    ReconcileGroupStructure(group);
                }
            }

            return true;
        }

        public bool IsGroupOpen(string groupId)
        {
            return status != AppUIFocusScopeStatus.Disposed &&
                   !string.IsNullOrEmpty(groupId) &&
                   groups.TryGetValue(groupId, out FocusGroupState group) &&
                   group.IsOpen;
        }

        public AppUIFocusRegionStatus GetRegionStatus(string regionId)
        {
            return status != AppUIFocusScopeStatus.Disposed &&
                   !string.IsNullOrEmpty(regionId) &&
                   regions.TryGetValue(regionId, out FocusRegionState region)
                ? region.Status
                : AppUIFocusRegionStatus.Closed;
        }

        public AppUIFocusRequestResult OpenRegion(
            string regionId,
            AppUIFocusRegionEntryPolicy entryPolicy =
                AppUIFocusRegionEntryPolicy.LastFocusedOrDefault)
        {
            CancelPendingRealization();
            return OpenRegionInternal(
                regionId,
                entryPolicy,
                AppUIFocusChangeReason.Programmatic);
        }

        public AppUIFocusRequestResult CloseRegion(string regionId)
        {
            CancelPendingRealization();
            return CloseRegionInternal(regionId);
        }

        public AppUIFocusRequestResult FocusNode(
            AppUIFocusNodeAddress nodeAddress,
            AppUIFocusChangeReason reason = AppUIFocusChangeReason.Programmatic)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Suspended)
            {
                hasPendingRepair = true;
                return AppUIFocusRequestResult.DeferredWhileSuspended;
            }

            if (status != AppUIFocusScopeStatus.Active)
            {
                return AppUIFocusRequestResult.ScopeInactive;
            }

            if (!nodeAddress.IsValid ||
                !groups.TryGetValue(
                    nodeAddress.GroupId,
                    out FocusGroupState group))
            {
                return AppUIFocusRequestResult.NodeMissing;
            }

            if (!nodeRegistry.TryResolveNode(
                    pageHandle,
                    nodeAddress,
                    out AppUIFocusResolvedNode resolvedNode) ||
                !IsOwnedRecord(resolvedNode))
            {
                return TryStartPendingRealization(group, nodeAddress, reason);
            }

            if (!TryCreateCommitRequest(
                    resolvedNode,
                    reason,
                    out AppUIFocusCommitRequest request,
                    out AppUIFocusRequestResult failure))
            {
                return failure;
            }

            return focusCommitter.Commit(in request);
        }

        public AppUIFocusRequestResult FocusGroupFirst(
            string groupId,
            AppUIFocusChangeReason reason = AppUIFocusChangeReason.Programmatic)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Suspended)
            {
                hasPendingRepair = true;
                return AppUIFocusRequestResult.DeferredWhileSuspended;
            }

            if (status != AppUIFocusScopeStatus.Active)
            {
                return AppUIFocusRequestResult.ScopeInactive;
            }

            if (string.IsNullOrEmpty(groupId) ||
                !groups.TryGetValue(groupId, out FocusGroupState group))
            {
                return AppUIFocusRequestResult.NodeMissing;
            }

            if (!group.IsOpen)
            {
                return AppUIFocusRequestResult.GroupClosed;
            }

            if (!regions.TryGetValue(
                    group.RegionId,
                    out FocusRegionState region) ||
                region.Status != AppUIFocusRegionStatus.Active)
            {
                return AppUIFocusRequestResult.RegionClosed;
            }

            for (int i = 0; i < group.OrderedNodes.Count; i++)
            {
                FocusNodeRecord record = group.OrderedNodes[i];
                if (record.Selectable == null ||
                    !nodeRegistry.TryResolveNode(
                        pageHandle,
                        record.Address,
                        out AppUIFocusResolvedNode resolvedNode) ||
                    !IsResolvedNodeEligible(resolvedNode))
                {
                    continue;
                }

                if (!TryCreateCommitRequest(
                        resolvedNode,
                        reason,
                        out AppUIFocusCommitRequest request,
                        out AppUIFocusRequestResult failure))
                {
                    return failure;
                }

                return focusCommitter.Commit(in request);
            }

            return group.OrderedNodes.Count > 0
                ? AppUIFocusRequestResult.NodeUnusable
                : AppUIFocusRequestResult.NodeMissing;
        }

        public bool TryResolveNode(
            AppUIFocusNodeAddress nodeAddress,
            out Selectable selectable)
        {
            if (status != AppUIFocusScopeStatus.Disposed &&
                nodeRegistry.TryResolveNode(
                    pageHandle,
                    nodeAddress,
                    out AppUIFocusResolvedNode resolvedNode) &&
                string.Equals(resolvedNode.ScopeId, ScopeId, StringComparison.Ordinal))
            {
                selectable = resolvedNode.Selectable;
                return true;
            }

            selectable = null;
            return false;
        }

        public bool TryGetNodeAddress(
            Selectable selectable,
            out AppUIFocusNodeAddress nodeAddress)
        {
            if (status != AppUIFocusScopeStatus.Disposed &&
                nodeRegistry.TryResolveNode(
                    selectable,
                    out AppUIFocusResolvedNode resolvedNode) &&
                IsOwnedRecord(resolvedNode))
            {
                nodeAddress = resolvedNode.NodeAddress;
                return true;
            }

            nodeAddress = default;
            return false;
        }

        public bool TryGetNodeAddress(
            GameObject selectedObject,
            out AppUIFocusNodeAddress nodeAddress)
        {
            if (status != AppUIFocusScopeStatus.Disposed &&
                nodeRegistry.TryResolveNode(
                    selectedObject,
                    out AppUIFocusResolvedNode resolvedNode) &&
                IsOwnedRecord(resolvedNode))
            {
                nodeAddress = resolvedNode.NodeAddress;
                return true;
            }

            nodeAddress = default;
            return false;
        }

        public bool ShouldConsumeWithoutNavigation(AxisEventData eventData)
        {
            if (status != AppUIFocusScopeStatus.Active)
            {
                return true;
            }

            return pageMoveInputPolicy != null &&
                pageMoveInputPolicy.ShouldConsumeWithoutNavigation(eventData);
        }

        bool IAppUIFocusRegionNavigationGateway.TryGetGroupRegionId(
            string groupId,
            out string regionId)
        {
            if (!string.IsNullOrEmpty(groupId) &&
                groups.TryGetValue(groupId, out FocusGroupState group))
            {
                regionId = group.RegionId;
                return true;
            }

            regionId = string.Empty;
            return false;
        }

        bool IAppUIFocusRegionNavigationGateway.TryGetNodeAddress(
            Selectable selectable,
            out AppUIFocusNodeAddress nodeAddress)
        {
            return TryGetNodeAddress(selectable, out nodeAddress);
        }

        bool IAppUIFocusRegionNavigationGateway.TryGetRegionLastFocusedAddress(
            string regionId,
            out AppUIFocusNodeAddress nodeAddress)
        {
            if (!string.IsNullOrEmpty(regionId) &&
                regions.TryGetValue(regionId, out FocusRegionState region) &&
                region.LastFocusedNodeAddress.IsValid)
            {
                nodeAddress = region.LastFocusedNodeAddress;
                return true;
            }

            nodeAddress = default;
            return false;
        }

        bool IAppUIFocusRegionNavigationGateway.TryRouteRegionBoundary(
            string sourceGroupId,
            Selectable sourceSelectable,
            MoveDirection moveDirection)
        {
            if (!groups.TryGetValue(
                    sourceGroupId,
                    out FocusGroupState sourceGroup) ||
                !regions.TryGetValue(
                    sourceGroup.RegionId,
                    out FocusRegionState sourceRegion) ||
                sourceRegion.Status != AppUIFocusRegionStatus.Active)
            {
                return false;
            }

            if (regionAdjacencies.TryGetValue(
                    new RegionAdjacencyKey(
                        sourceGroup.RegionId,
                        sourceGroupId,
                        moveDirection),
                    out string targetGroupId))
            {
                navigator.FocusGroup(
                    targetGroupId,
                    sourceGroupId,
                    sourceSelectable,
                    moveDirection);
                return true;
            }

            return sourceRegion.AutoAdjacent &&
                   TryRouteAutoAdjacentBoundary(
                       sourceGroup,
                       sourceSelectable,
                       moveDirection);
        }

        private bool TryRouteAutoAdjacentBoundary(
            FocusGroupState sourceGroup,
            Selectable sourceSelectable,
            MoveDirection moveDirection)
        {
            if (sourceGroup == null ||
                sourceSelectable == null ||
                !AppUIFocusSpatialUtility.TryCreateRect(
                    sourceSelectable,
                    spatialWorldCorners,
                    out AppUIFocusSpatialRect sourceRect))
            {
                return false;
            }

            bool found = false;
            AppUIFocusSpatialScore bestScore = default;
            FocusGroupState bestGroup = null;
            Selectable bestSelectable = null;
            int bestNodeOrder = 0;
            int bestNodeSequence = 0;
            for (int i = 0; i < orderedGroups.Count; i++)
            {
                FocusGroupState candidateGroup = orderedGroups[i];
                if (ReferenceEquals(candidateGroup, sourceGroup) ||
                    !candidateGroup.IsOpen ||
                    !string.Equals(
                        candidateGroup.RegionId,
                        sourceGroup.RegionId,
                        StringComparison.Ordinal) ||
                    !navigator.TryGetSpatialTarget(
                        candidateGroup.GroupId,
                        in sourceRect,
                        moveDirection,
                        out Selectable candidate,
                        out AppUIFocusSpatialScore candidateScore,
                        out int candidateIndex) ||
                    candidateIndex < 0 ||
                    candidateIndex >= candidateGroup.OrderedNodes.Count)
                {
                    continue;
                }

                FocusNodeRecord candidateRecord =
                    candidateGroup.OrderedNodes[candidateIndex];
                int scoreComparison = found
                    ? candidateScore.CompareTo(bestScore)
                    : -1;
                bool isBetter = !found || scoreComparison < 0;
                if (!isBetter && scoreComparison == 0)
                {
                    int groupOrderComparison =
                        candidateGroup.Order.CompareTo(bestGroup.Order);
                    if (groupOrderComparison < 0)
                    {
                        isBetter = true;
                    }
                    else if (groupOrderComparison == 0)
                    {
                        int nodeOrderComparison =
                            candidateRecord.Order.CompareTo(bestNodeOrder);
                        isBetter = nodeOrderComparison < 0 ||
                                   (nodeOrderComparison == 0 &&
                                    candidateRecord.Sequence < bestNodeSequence);
                    }
                }

                if (!isBetter)
                {
                    continue;
                }

                found = true;
                bestScore = candidateScore;
                bestGroup = candidateGroup;
                bestSelectable = candidate;
                bestNodeOrder = candidateRecord.Order;
                bestNodeSequence = candidateRecord.Sequence;
            }

            if (!found)
            {
                return false;
            }

            navigator.FocusNode(
                bestGroup.GroupId,
                bestSelectable,
                AppUIFocusChangeReason.Navigation);
            return true;
        }

        bool IAppUIFocusRegionNavigationGateway.FocusRegion(
            string regionId,
            AppUIFocusRegionEntryPolicy entryPolicy,
            string sourceGroupId,
            Selectable sourceSelectable,
            MoveDirection moveDirection)
        {
            AppUIFocusRequestResult result = OpenRegionInternal(
                regionId,
                entryPolicy,
                AppUIFocusChangeReason.Navigation);
            return result != AppUIFocusRequestResult.ScopeInactive &&
                   result != AppUIFocusRequestResult.RegionClosed;
        }

        bool IAppUIFocusRegionNavigationGateway.ExitToParentRegion(
            string sourceGroupId)
        {
            if (!groups.TryGetValue(
                    sourceGroupId,
                    out FocusGroupState sourceGroup) ||
                string.Equals(
                    sourceGroup.RegionId,
                    RootRegionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            AppUIFocusRequestResult result = CloseRegionInternal(sourceGroup.RegionId);
            return result != AppUIFocusRequestResult.ScopeInactive &&
                   result != AppUIFocusRequestResult.RegionClosed;
        }

        public AppUIFocusRequestResult CommitFocus(
            Selectable selectable,
            AppUIFocusChangeReason reason)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Suspended)
            {
                hasPendingRepair = true;
                return AppUIFocusRequestResult.DeferredWhileSuspended;
            }

            if (status != AppUIFocusScopeStatus.Active)
            {
                return AppUIFocusRequestResult.ScopeInactive;
            }

            if (!nodeRegistry.TryResolveNode(
                    selectable,
                    out AppUIFocusResolvedNode resolvedNode) ||
                !IsOwnedRecord(resolvedNode))
            {
                return AppUIFocusRequestResult.ReverseLookupFailed;
            }

            if (!TryCreateCommitRequest(
                    resolvedNode,
                    reason,
                    out AppUIFocusCommitRequest request,
                    out AppUIFocusRequestResult failure))
            {
                return failure;
            }

            return focusCommitter.Commit(in request);
        }

        AppUIFocusRequestResult IAppUIFocusCommitGateway.CommitFocus(
            AppUIFocusNodeAddress nodeAddress,
            AppUIFocusChangeReason reason)
        {
            return FocusNode(nodeAddress, reason);
        }

        internal void EnsureVisible(AppUIFocusResolvedNode resolvedNode)
        {
            if (!IsOwnedRecord(resolvedNode) ||
                !groups.TryGetValue(
                    resolvedNode.NodeAddress.GroupId,
                    out FocusGroupState group) ||
                group.VisibilityAdapter == null ||
                !(resolvedNode.Selectable.transform is RectTransform target))
            {
                return;
            }

            try
            {
                group.VisibilityAdapter.EnsureVisible(target);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "<AppUIFocus> Visibility adapter failed. Scope=" +
                    ScopeId +
                    ", Group=" +
                    group.GroupId +
                    ", Node=" +
                    resolvedNode.NodeAddress +
                    ", Adapter=" +
                    group.VisibilityAdapter.GetType().FullName +
                    ", Exception=" +
                    exception);
#endif
            }
        }

        private AppUIFocusRequestResult TryStartPendingRealization(
            FocusGroupState group,
            AppUIFocusNodeAddress nodeAddress,
            AppUIFocusChangeReason reason)
        {
            if (group == null || group.VirtualizationAdapter == null)
            {
                return AppUIFocusRequestResult.NodeMissing;
            }

            if (!group.IsOpen)
            {
                return AppUIFocusRequestResult.GroupClosed;
            }

            if (!regions.TryGetValue(
                    group.RegionId,
                    out FocusRegionState region) ||
                region.Status != AppUIFocusRegionStatus.Active)
            {
                return AppUIFocusRequestResult.RegionClosed;
            }

            if (pendingRealizationSerial == int.MaxValue)
            {
                pendingRealizationSerial = 0;
            }

            pendingRealizationSerial++;
            int requestSerial = pendingRealizationSerial;
            pendingRealizationCancellation = new CancellationTokenSource();
            pendingRealizationAddress = nodeAddress;
            CancellationToken cancellationToken = pendingRealizationCancellation.Token;
            AppUIFocusRealizationRequest realizationRequest =
                new AppUIFocusRealizationRequest(ScopeId, nodeAddress);
            if (AppUIFocusTrace.CanTrace(pageInstanceId))
            {
                AppUIFocusTrace.Record(
                    pageInstanceId,
                    AppUIFocusTraceStage.Realization,
                    currentFocusedAddress,
                    nodeAddress,
                    "Pending realization started. Reason=" + reason);
            }

            CompletePendingRealizationAsync(
                    requestSerial,
                    pageHandle,
                    currentStackRevision,
                    revision,
                    region.Revision,
                    group.Revision,
                    reason,
                    group,
                    realizationRequest,
                    cancellationToken)
                .Forget();
            return AppUIFocusRequestResult.PendingRealization;
        }

        private async UniTaskVoid CompletePendingRealizationAsync(
            int requestSerial,
            UIPageInteractionHandle capturedPageHandle,
            int capturedStackRevision,
            int capturedScopeRevision,
            int capturedRegionRevision,
            int capturedGroupRevision,
            AppUIFocusChangeReason reason,
            FocusGroupState group,
            AppUIFocusRealizationRequest realizationRequest,
            CancellationToken cancellationToken)
        {
            try
            {
                AppUIFocusRealizationResult result =
                    await group.VirtualizationAdapter.EnsureRealizedAsync(
                        realizationRequest,
                        cancellationToken);
                if (cancellationToken.IsCancellationRequested ||
                    requestSerial != pendingRealizationSerial ||
                    result.Status != AppUIFocusRealizationStatus.Realized ||
                    result.Selectable == null ||
                    status != AppUIFocusScopeStatus.Active ||
                    pageHandle != capturedPageHandle ||
                    currentStackRevision != capturedStackRevision ||
                    revision != capturedScopeRevision ||
                    !groups.TryGetValue(
                        realizationRequest.NodeAddress.GroupId,
                        out FocusGroupState currentGroup) ||
                    !ReferenceEquals(currentGroup, group) ||
                    group.Revision != capturedGroupRevision ||
                    !group.IsOpen ||
                    !regions.TryGetValue(
                        group.RegionId,
                        out FocusRegionState region) ||
                    region.Status != AppUIFocusRegionStatus.Active ||
                    region.Revision != capturedRegionRevision)
                {
                    if (AppUIFocusTrace.CanTrace(pageInstanceId))
                    {
                        AppUIFocusTrace.Record(
                            pageInstanceId,
                            AppUIFocusTraceStage.Realization,
                            currentFocusedAddress,
                            realizationRequest.NodeAddress,
                            "Realization discarded. Status=" + result.Status);
                    }

                    return;
                }

                ClearPendingRealization(requestSerial);
                if (!RegisterNode(
                        group.GroupId,
                        realizationRequest.NodeAddress.NodeKey,
                        result.Selectable,
                        result.ControlPolicy,
                        result.Order) ||
                    !nodeRegistry.TryResolveNode(
                        pageHandle,
                        realizationRequest.NodeAddress,
                        out AppUIFocusResolvedNode resolvedNode) ||
                    !TryCreateCommitRequest(
                        resolvedNode,
                        reason,
                        out AppUIFocusCommitRequest commitRequest,
                        out _))
                {
                    return;
                }

                focusCommitter.Commit(in commitRequest);
                if (AppUIFocusTrace.CanTrace(pageInstanceId))
                {
                    AppUIFocusTrace.Record(
                        pageInstanceId,
                        AppUIFocusTraceStage.Realization,
                        currentFocusedAddress,
                        realizationRequest.NodeAddress,
                        "Realization registered and submitted. Order=" + result.Order);
                }
            }
            catch (OperationCanceledException)
            {
                if (AppUIFocusTrace.CanTrace(pageInstanceId))
                {
                    AppUIFocusTrace.Record(
                        pageInstanceId,
                        AppUIFocusTraceStage.Realization,
                        currentFocusedAddress,
                        realizationRequest.NodeAddress,
                        "Realization canceled.");
                }
            }
            catch (Exception exception)
            {
                if (AppUIFocusTrace.CanTrace(pageInstanceId))
                {
                    AppUIFocusTrace.Record(
                        pageInstanceId,
                        AppUIFocusTraceStage.Realization,
                        currentFocusedAddress,
                        realizationRequest.NodeAddress,
                        "Realization failed. Exception=" +
                        exception.GetType().FullName);
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "<AppUIFocus> Virtualization adapter failed. Scope=" +
                    ScopeId +
                    ", Node=" +
                    realizationRequest.NodeAddress +
                    ", Adapter=" +
                    (group.VirtualizationAdapter != null
                        ? group.VirtualizationAdapter.GetType().FullName
                        : string.Empty) +
                    ", Exception=" +
                    exception);
#endif
            }
            finally
            {
                ClearPendingRealization(requestSerial);
            }
        }

        private void ClearPendingRealization(int requestSerial)
        {
            if (requestSerial != pendingRealizationSerial)
            {
                return;
            }

            pendingRealizationCancellation?.Dispose();
            pendingRealizationCancellation = null;
            pendingRealizationAddress = default;
        }

        private void CancelPendingRealization()
        {
            if (pendingRealizationCancellation == null)
            {
                return;
            }

            if (AppUIFocusTrace.CanTrace(pageInstanceId))
            {
                AppUIFocusTrace.Record(
                    pageInstanceId,
                    AppUIFocusTraceStage.Realization,
                    currentFocusedAddress,
                    pendingRealizationAddress,
                    "Pending realization canceled by state change.");
            }

            pendingRealizationCancellation.Cancel();
            pendingRealizationCancellation.Dispose();
            pendingRealizationCancellation = null;
            pendingRealizationAddress = default;
            if (pendingRealizationSerial == int.MaxValue)
            {
                pendingRealizationSerial = 0;
            }

            pendingRealizationSerial++;
        }

        internal bool TryCreateCommitRequest(
            AppUIFocusResolvedNode resolvedNode,
            AppUIFocusChangeReason reason,
            out AppUIFocusCommitRequest request,
            out AppUIFocusRequestResult failure)
        {
            request = default;
            if (status != AppUIFocusScopeStatus.Active)
            {
                failure = status == AppUIFocusScopeStatus.Suspended
                    ? AppUIFocusRequestResult.DeferredWhileSuspended
                    : AppUIFocusRequestResult.ScopeInactive;
                return false;
            }

            if (!IsOwnedRecord(resolvedNode) ||
                !groups.TryGetValue(
                    resolvedNode.NodeAddress.GroupId,
                    out FocusGroupState group))
            {
                failure = AppUIFocusRequestResult.NodeMissing;
                return false;
            }

            if (!regions.TryGetValue(
                    group.RegionId,
                    out FocusRegionState region) ||
                region.Status != AppUIFocusRegionStatus.Active ||
                !string.Equals(
                    resolvedNode.RegionId,
                    group.RegionId,
                    StringComparison.Ordinal))
            {
                failure = AppUIFocusRequestResult.RegionClosed;
                return false;
            }

            if (!group.IsOpen)
            {
                failure = AppUIFocusRequestResult.GroupClosed;
                return false;
            }

            if (!IsUsable(resolvedNode.Selectable))
            {
                failure = AppUIFocusRequestResult.NodeUnusable;
                return false;
            }

            request = new AppUIFocusCommitRequest(
                pageHandle,
                group.RegionId,
                resolvedNode.NodeAddress,
                resolvedNode.Selectable,
                currentStackRevision,
                revision,
                region.Revision,
                group.Revision,
                resolvedNode.RegistrationGeneration,
                reason);
            failure = AppUIFocusRequestResult.Focused;
            return true;
        }

        internal bool TryValidateCommitRequest(
            in AppUIFocusCommitRequest request,
            AppUIFocusResolvedNode resolvedNode,
            out AppUIFocusRequestResult failure)
        {
            if (request.PageHandle != pageHandle ||
                request.StackRevision != currentStackRevision ||
                request.ScopeRevision != revision)
            {
                failure = AppUIFocusRequestResult.StaleRevision;
                return false;
            }

            if (status != AppUIFocusScopeStatus.Active)
            {
                failure = AppUIFocusRequestResult.ScopeInactive;
                return false;
            }

            if (!groups.TryGetValue(
                    request.NodeAddress.GroupId,
                    out FocusGroupState group))
            {
                failure = AppUIFocusRequestResult.NodeMissing;
                return false;
            }


            if (!regions.TryGetValue(
                    group.RegionId,
                    out FocusRegionState region) ||
                region.Status != AppUIFocusRegionStatus.Active ||
                !string.Equals(
                    request.RegionId,
                    group.RegionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    resolvedNode.RegionId,
                    group.RegionId,
                    StringComparison.Ordinal))
            {
                failure = AppUIFocusRequestResult.RegionClosed;
                return false;
            }

            if (request.RegionRevision != region.Revision)
            {
                failure = AppUIFocusRequestResult.StaleRevision;
                return false;
            }

            if (!group.IsOpen)
            {
                failure = AppUIFocusRequestResult.GroupClosed;
                return false;
            }

            if (group.Revision != request.GroupRevision ||
                resolvedNode.RegistrationGeneration != request.RegistrationGeneration ||
                resolvedNode.NodeAddress != request.NodeAddress ||
                !ReferenceEquals(resolvedNode.Selectable, request.Target))
            {
                failure = AppUIFocusRequestResult.StaleRevision;
                return false;
            }

            if (!IsUsable(resolvedNode.Selectable))
            {
                failure = AppUIFocusRequestResult.NodeUnusable;
                return false;
            }

            failure = AppUIFocusRequestResult.Focused;
            return true;
        }

        internal void AcceptCommittedFocus(
            in AppUIFocusCommitRequest request,
            AppUIFocusResolvedNode resolvedNode,
            AppUIFocusHistoryWriteMode historyWriteMode)
        {
            currentFocusedAddress = resolvedNode.NodeAddress;
            hasPendingRepair = false;
            if (regions.TryGetValue(
                    resolvedNode.RegionId,
                    out FocusRegionState focusedRegion))
            {
                focusedRegion.HasPendingRepair = false;
                focusedRegion.PendingRestoreAddress = default;
            }
            navigator.NotifySelected(
                resolvedNode.NodeAddress.GroupId,
                resolvedNode.Selectable);

            switch (historyWriteMode)
            {
                case AppUIFocusHistoryWriteMode.InitializeIfEmpty:
                    if (!lastFocusedAddress.IsValid)
                    {
                        WriteFullHistory(resolvedNode.NodeAddress);
                    }
                    else if (!nodeHistoryByGroup.ContainsKey(
                                 resolvedNode.NodeAddress.GroupId))
                    {
                        nodeHistoryByGroup.Add(
                            resolvedNode.NodeAddress.GroupId,
                            resolvedNode.NodeAddress.NodeKey);
                    }

                    if (focusedRegion != null &&
                        !focusedRegion.LastFocusedNodeAddress.IsValid)
                    {
                        focusedRegion.LastFocusedGroupId =
                            resolvedNode.NodeAddress.GroupId;
                        focusedRegion.LastFocusedNodeAddress =
                            resolvedNode.NodeAddress;
                    }

                    break;
                case AppUIFocusHistoryWriteMode.NodeOnly:
                    nodeHistoryByGroup[resolvedNode.NodeAddress.GroupId] =
                        resolvedNode.NodeAddress.NodeKey;
                    break;
                case AppUIFocusHistoryWriteMode.Full:
                    WriteFullHistory(resolvedNode.NodeAddress);
                    break;
                case AppUIFocusHistoryWriteMode.ReplaceInvalidOnly:
                    if (!IsAddressEligible(lastFocusedAddress))
                    {
                        lastFocusedAddress = resolvedNode.NodeAddress;
                    }

                    if (!nodeHistoryByGroup.TryGetValue(
                            resolvedNode.NodeAddress.GroupId,
                            out AppUIFocusNodeKey historyKey) ||
                        !IsAddressEligible(
                            new AppUIFocusNodeAddress(
                                resolvedNode.NodeAddress.GroupId,
                                historyKey)))
                    {
                        nodeHistoryByGroup[resolvedNode.NodeAddress.GroupId] =
                            resolvedNode.NodeAddress.NodeKey;
                    }

                    if (focusedRegion != null &&
                        !IsAddressEligible(focusedRegion.LastFocusedNodeAddress))
                    {
                        focusedRegion.LastFocusedGroupId =
                            resolvedNode.NodeAddress.GroupId;
                        focusedRegion.LastFocusedNodeAddress =
                            resolvedNode.NodeAddress;
                    }

                    break;
            }

            PublishDebugSnapshot();
        }

        internal bool TryGetRecoveryNode(
            bool useHistory,
            out AppUIFocusResolvedNode resolvedNode)
        {
            if (!regions.TryGetValue(
                    activeLeafRegionId,
                    out FocusRegionState activeRegion) ||
                activeRegion.Status != AppUIFocusRegionStatus.Active)
            {
                resolvedNode = default;
                return false;
            }

            if (activeRegion.PendingRestoreAddress.IsValid &&
                TryResolveEligibleAddress(
                    activeRegion.PendingRestoreAddress,
                    activeRegion.RegionId,
                    out resolvedNode))
            {
                activeRegion.PendingRestoreAddress = default;
                return true;
            }

            activeRegion.PendingRestoreAddress = default;
            if (useHistory &&
                activeRegion.LastFocusedNodeAddress.IsValid &&
                TryResolveEligibleAddress(
                    activeRegion.LastFocusedNodeAddress,
                    activeRegion.RegionId,
                    out resolvedNode))
            {
                return true;
            }

            if (useHistory &&
                lastFocusedAddress.IsValid &&
                nodeRegistry.TryResolveNode(
                    pageHandle,
                    lastFocusedAddress,
                    out resolvedNode) &&
                IsResolvedNodeEligible(resolvedNode))
            {
                return true;
            }

            for (int groupIndex = 0; groupIndex < orderedGroups.Count; groupIndex++)
            {
                FocusGroupState group = orderedGroups[groupIndex];
                if (!group.IsOpen ||
                    !string.Equals(
                        group.RegionId,
                        activeRegion.RegionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                for (int nodeIndex = 0; nodeIndex < group.OrderedNodes.Count; nodeIndex++)
                {
                    FocusNodeRecord record = group.OrderedNodes[nodeIndex];
                    if (record.Selectable == null || !IsUsable(record.Selectable))
                    {
                        continue;
                    }

                    if (nodeRegistry.TryResolveNode(
                            pageHandle,
                            record.Address,
                            out resolvedNode) &&
                        IsResolvedNodeEligible(resolvedNode))
                    {
                        return true;
                    }
                }
            }

            resolvedNode = default;
            return false;
        }

        internal bool IsResolvedNodeEligible(AppUIFocusResolvedNode resolvedNode)
        {
            return status == AppUIFocusScopeStatus.Active &&
                   IsOwnedRecord(resolvedNode) &&
                   groups.TryGetValue(
                       resolvedNode.NodeAddress.GroupId,
                       out FocusGroupState group) &&
                   regions.TryGetValue(
                       group.RegionId,
                       out FocusRegionState region) &&
                   region.Status == AppUIFocusRegionStatus.Active &&
                   string.Equals(
                       resolvedNode.RegionId,
                       group.RegionId,
                       StringComparison.Ordinal) &&
                   group.IsOpen &&
                   group.NodesByKey.TryGetValue(
                       resolvedNode.NodeAddress.NodeKey,
                       out FocusNodeRecord record) &&
                   record.RegistrationGeneration == resolvedNode.RegistrationGeneration &&
                   ReferenceEquals(record.Selectable, resolvedNode.Selectable) &&
                   IsUsable(resolvedNode.Selectable);
        }

        internal void AcceptExternalSelection(AppUIFocusResolvedNode resolvedNode)
        {
            if (!TryCreateCommitRequest(
                    resolvedNode,
                    AppUIFocusChangeReason.ExternalSelection,
                    out AppUIFocusCommitRequest request,
                    out _))
            {
                return;
            }

            AcceptCommittedFocus(
                in request,
                resolvedNode,
                AppUIFocusHistoryWriteMode.Full);
        }

        internal void NotifySelectionCleared()
        {
            currentFocusedAddress = default;
            PublishDebugSnapshot();
        }

        internal void MarkPendingRepair()
        {
            if (status != AppUIFocusScopeStatus.Disposed)
            {
                hasPendingRepair = true;
                if (regions.TryGetValue(
                        activeLeafRegionId,
                        out FocusRegionState activeRegion) &&
                    activeRegion.Status != AppUIFocusRegionStatus.Closed)
                {
                    activeRegion.HasPendingRepair = true;
                }
            }
        }

        internal void ClearPendingRepair()
        {
            if (regions.TryGetValue(
                    activeLeafRegionId,
                    out FocusRegionState activeRegion))
            {
                activeRegion.HasPendingRepair = false;
                activeRegion.PendingRestoreAddress = default;
            }

            hasPendingRepair = false;
        }

        internal void ApplyInteractionSnapshot(UIInteractionSnapshot snapshot)
        {
            if (status == AppUIFocusScopeStatus.Disposed)
            {
                return;
            }

            currentStackRevision = snapshot != null ? snapshot.StackRevision : 0;
            UIPageInteractionState matchedState = default;
            bool found = false;
            int stateCount = snapshot != null ? snapshot.PageStateCount : 0;
            for (int i = 0; i < stateCount; i++)
            {
                UIPageInteractionState state = snapshot.GetPageState(i);
                if (state.Page.InstanceId == pageInstanceId &&
                    string.Equals(state.Page.PageId, pageId, StringComparison.Ordinal))
                {
                    matchedState = state;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                SetLifecycleStatus(AppUIFocusScopeStatus.Suspended);
                return;
            }

            if (pageHandle != matchedState.Page)
            {
                pageHandle = matchedState.Page;
                nodeRegistry.UpdatePageHandle(pageInstanceId, pageHandle);
            }

            bool active = snapshot.TopInteractivePage == matchedState.Page &&
                          matchedState.StackVisible &&
                          matchedState.PauseDepth == 0 &&
                          matchedState.InputBlockDepth == 0;
            SetLifecycleStatus(
                active
                    ? AppUIFocusScopeStatus.Active
                    : AppUIFocusScopeStatus.Suspended);
        }

        public void Dispose()
        {
            if (status == AppUIFocusScopeStatus.Disposed)
            {
                return;
            }

            CancelPendingRealization();
            for (int i = 0; i < orderedGroups.Count; i++)
            {
                FocusGroupState group = orderedGroups[i];
                group.ActiveTransaction?.NotifyScopeDisposed();
                group.ActiveTransaction = null;
            }

            status = AppUIFocusScopeStatus.Disposed;
            for (int i = 0; i < orderedRegions.Count; i++)
            {
                orderedRegions[i].Status = AppUIFocusRegionStatus.Closed;
                orderedRegions[i].HasPendingRepair = false;
                orderedRegions[i].PendingRestoreAddress = default;
            }
            revision++;
            AppUIFocusNodeAddress disposedAddress = currentFocusedAddress.IsValid
                ? currentFocusedAddress
                : lastFocusedAddress;
            if (AppUIFocusTrace.CanTrace(pageInstanceId))
            {
                AppUIFocusTrace.Record(
                    pageInstanceId,
                    AppUIFocusTraceStage.Scope,
                    disposedAddress,
                    default,
                    "Scope disposed.");
            }

            nodeRegistry.RemoveScope(pageInstanceId, ScopeId);
            navigator.Dispose();
            navigatorNodeBuffer.Clear();
            navigatorControlPolicyBuffer.Clear();
            replacementRecordBuffer.Clear();
            nodeHistoryByGroup.Clear();
            currentFocusedAddress = default;
            lastFocusedAddress = default;
            hasPendingRepair = false;
            currentStackRevision = 0;
            AppUIFocusTrace.UnregisterScope(pageInstanceId);
            orderedGroups.Clear();
            groups.Clear();
            orderedRegions.Clear();
            regions.Clear();
            regionAdjacencies.Clear();
            rootRegion = null;
            activeLeafRegionId = string.Empty;
        }

        private void BuildRegions(AppUIFocusDefinition definition)
        {
            for (int i = 0; i < definition.RegionCount; i++)
            {
                AppUIFocusRegionDefinition regionDefinition = definition.GetRegion(i);
                FocusRegionState region = new FocusRegionState
                {
                    RegionId = regionDefinition.RegionId,
                    ParentRegionId = regionDefinition.ParentRegionId,
                    DefaultGroupId = regionDefinition.DefaultGroupId,
                    CancelHandler = regionDefinition.CancelHandler,
                    AutoAdjacent = regionDefinition.AutoAdjacent,
                };
                regions.Add(region.RegionId, region);
                orderedRegions.Add(region);
            }

            if (!regions.TryGetValue(RootRegionId, out rootRegion))
            {
                throw new InvalidOperationException("Focus definition is missing RootRegion.");
            }

            for (int i = 0; i < orderedRegions.Count; i++)
            {
                FocusRegionState region = orderedRegions[i];
                if (string.Equals(region.RegionId, RootRegionId, StringComparison.Ordinal))
                {
                    region.Depth = 0;
                    continue;
                }

                if (!regions.TryGetValue(
                        region.ParentRegionId,
                        out FocusRegionState parentRegion))
                {
                    throw new InvalidOperationException(
                        "Focus Region parent is missing: " + region.RegionId);
                }

                parentRegion.Children.Add(region);
                region.Depth = ResolveRegionDepth(region);
            }

            for (int i = 0; i < definition.RegionAdjacencyCount; i++)
            {
                AppUIFocusRegionAdjacencyDefinition adjacency =
                    definition.GetRegionAdjacency(i);
                regionAdjacencies.Add(
                    new RegionAdjacencyKey(
                        adjacency.RegionId,
                        adjacency.SourceGroupId,
                        adjacency.MoveDirection),
                    adjacency.TargetGroupId);
            }

            activeLeafRegionId = RootRegionId;
        }

        private int ResolveRegionDepth(FocusRegionState region)
        {
            int depth = 0;
            FocusRegionState current = region;
            while (!string.Equals(
                       current.RegionId,
                       RootRegionId,
                       StringComparison.Ordinal))
            {
                depth++;
                if (depth > 4 ||
                    !regions.TryGetValue(
                        current.ParentRegionId,
                        out current))
                {
                    throw new InvalidOperationException(
                        "Focus Region tree is invalid: " + region.RegionId);
                }
            }

            return depth;
        }

        private AppUIFocusRequestResult OpenRegionInternal(
            string regionId,
            AppUIFocusRegionEntryPolicy entryPolicy,
            AppUIFocusChangeReason reason)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Suspended)
            {
                hasPendingRepair = true;
                return AppUIFocusRequestResult.DeferredWhileSuspended;
            }

            if (status != AppUIFocusScopeStatus.Active)
            {
                return AppUIFocusRequestResult.ScopeInactive;
            }

            if (string.IsNullOrEmpty(regionId) ||
                string.Equals(regionId, RootRegionId, StringComparison.Ordinal) ||
                !regions.TryGetValue(regionId, out FocusRegionState targetRegion))
            {
                return AppUIFocusRequestResult.RegionClosed;
            }

            if (targetRegion.Status == AppUIFocusRegionStatus.Active)
            {
                return FocusRegionEntry(targetRegion, entryPolicy, reason);
            }

            if (targetRegion.Status != AppUIFocusRegionStatus.Closed ||
                !regions.TryGetValue(
                    targetRegion.ParentRegionId,
                    out FocusRegionState parentRegion))
            {
                return AppUIFocusRequestResult.RegionClosed;
            }

            AppUIFocusNodeAddress sourceAddress = default;
            if (parentRegion.Status == AppUIFocusRegionStatus.Suspended)
            {
                if (string.IsNullOrEmpty(parentRegion.ActiveChildRegionId) ||
                    !regions.TryGetValue(
                        parentRegion.ActiveChildRegionId,
                        out FocusRegionState previousChild) ||
                    ReferenceEquals(previousChild, targetRegion))
                {
                    return AppUIFocusRequestResult.RegionClosed;
                }

                sourceAddress = previousChild.SourceNodeAddress;
                CloseRegionBranch(previousChild);
                parentRegion.ActiveChildRegionId = string.Empty;
                parentRegion.Status = AppUIFocusRegionStatus.Active;
                IncrementRegionRevision(parentRegion);
            }
            else if (parentRegion.Status != AppUIFocusRegionStatus.Active)
            {
                return AppUIFocusRequestResult.RegionClosed;
            }

            if (!IsAddressOwnedByRegion(sourceAddress, parentRegion.RegionId))
            {
                if (IsAddressOwnedByRegion(
                        currentFocusedAddress,
                        parentRegion.RegionId))
                {
                    sourceAddress = currentFocusedAddress;
                }
                else
                {
                    sourceAddress = parentRegion.LastFocusedNodeAddress;
                }
            }

            targetRegion.SourceNodeAddress = sourceAddress;
            targetRegion.PendingRestoreAddress = default;
            targetRegion.HasPendingRepair = false;
            targetRegion.Status = AppUIFocusRegionStatus.Active;
            IncrementRegionRevision(targetRegion);
            parentRegion.Status = AppUIFocusRegionStatus.Suspended;
            parentRegion.ActiveChildRegionId = targetRegion.RegionId;
            IncrementRegionRevision(parentRegion);
            activeLeafRegionId = targetRegion.RegionId;
            SyncNavigatorRegionVisibility();

            AppUIFocusRequestResult result =
                FocusRegionEntry(targetRegion, entryPolicy, reason);
            if (result != AppUIFocusRequestResult.Focused &&
                result != AppUIFocusRequestResult.Consumed &&
                result != AppUIFocusRequestResult.Deferred)
            {
                MarkRegionPendingRepair(targetRegion);
            }

            return result;
        }

        private AppUIFocusRequestResult CloseRegionInternal(string regionId)
        {
            CancelPendingRealization();
            if (status == AppUIFocusScopeStatus.Disposed ||
                string.IsNullOrEmpty(regionId) ||
                string.Equals(regionId, RootRegionId, StringComparison.Ordinal) ||
                !regions.TryGetValue(regionId, out FocusRegionState region) ||
                region.Status == AppUIFocusRegionStatus.Closed ||
                !regions.TryGetValue(
                    region.ParentRegionId,
                    out FocusRegionState parentRegion))
            {
                return status == AppUIFocusScopeStatus.Disposed
                    ? AppUIFocusRequestResult.ScopeInactive
                    : AppUIFocusRequestResult.RegionClosed;
            }

            AppUIFocusNodeAddress sourceAddress = region.SourceNodeAddress;
            CloseRegionBranch(region);
            parentRegion.ActiveChildRegionId = string.Empty;
            parentRegion.Status = status == AppUIFocusScopeStatus.Active
                ? AppUIFocusRegionStatus.Active
                : AppUIFocusRegionStatus.Suspended;
            parentRegion.PendingRestoreAddress = sourceAddress;
            IncrementRegionRevision(parentRegion);
            activeLeafRegionId = parentRegion.RegionId;
            SyncNavigatorRegionVisibility();

            if (status == AppUIFocusScopeStatus.Suspended)
            {
                MarkRegionPendingRepair(parentRegion);
                return AppUIFocusRequestResult.DeferredWhileSuspended;
            }

            if (status != AppUIFocusScopeStatus.Active)
            {
                return AppUIFocusRequestResult.ScopeInactive;
            }

            AppUIFocusRequestResult result = FocusRegionEntry(
                parentRegion,
                AppUIFocusRegionEntryPolicy.LastFocusedOrDefault,
                AppUIFocusChangeReason.RestoreRequested);
            if (result != AppUIFocusRequestResult.Focused &&
                result != AppUIFocusRequestResult.Consumed &&
                result != AppUIFocusRequestResult.Deferred)
            {
                MarkRegionPendingRepair(parentRegion);
            }

            return result;
        }

        private void CloseRegionBranch(FocusRegionState region)
        {
            for (int i = 0; i < region.Children.Count; i++)
            {
                FocusRegionState child = region.Children[i];
                if (child.Status != AppUIFocusRegionStatus.Closed)
                {
                    CloseRegionBranch(child);
                }
            }

            region.Status = AppUIFocusRegionStatus.Closed;
            region.ActiveChildRegionId = string.Empty;
            region.SourceNodeAddress = default;
            region.PendingRestoreAddress = default;
            region.HasPendingRepair = false;
            IncrementRegionRevision(region);
        }

        private AppUIFocusRequestResult FocusRegionEntry(
            FocusRegionState region,
            AppUIFocusRegionEntryPolicy entryPolicy,
            AppUIFocusChangeReason reason)
        {
            if (region == null || region.Status != AppUIFocusRegionStatus.Active)
            {
                return AppUIFocusRequestResult.RegionClosed;
            }

            if (region.PendingRestoreAddress.IsValid &&
                TryResolveEligibleAddress(
                    region.PendingRestoreAddress,
                    region.RegionId,
                    out AppUIFocusResolvedNode restoredNode))
            {
                region.PendingRestoreAddress = default;
                return CommitResolvedNode(restoredNode, reason);
            }

            region.PendingRestoreAddress = default;
            if (entryPolicy == AppUIFocusRegionEntryPolicy.LastFocusedOrDefault &&
                region.LastFocusedNodeAddress.IsValid &&
                TryResolveEligibleAddress(
                    region.LastFocusedNodeAddress,
                    region.RegionId,
                    out AppUIFocusResolvedNode historyNode))
            {
                return CommitResolvedNode(historyNode, reason);
            }

            if (!string.IsNullOrEmpty(region.DefaultGroupId) &&
                TryGetFirstEligibleNode(
                    region.DefaultGroupId,
                    out AppUIFocusResolvedNode defaultNode))
            {
                return CommitResolvedNode(defaultNode, reason);
            }

            for (int i = 0; i < orderedGroups.Count; i++)
            {
                FocusGroupState group = orderedGroups[i];
                if (string.Equals(
                        group.RegionId,
                        region.RegionId,
                        StringComparison.Ordinal) &&
                    TryGetFirstEligibleNode(
                        group.GroupId,
                        out AppUIFocusResolvedNode firstNode))
                {
                    return CommitResolvedNode(firstNode, reason);
                }
            }

            return AppUIFocusRequestResult.NodeMissing;
        }

        private AppUIFocusRequestResult CommitResolvedNode(
            AppUIFocusResolvedNode resolvedNode,
            AppUIFocusChangeReason reason)
        {
            if (!TryCreateCommitRequest(
                    resolvedNode,
                    reason,
                    out AppUIFocusCommitRequest request,
                    out AppUIFocusRequestResult failure))
            {
                return failure;
            }

            return focusCommitter.Commit(in request);
        }

        private bool TryGetFirstEligibleNode(
            string groupId,
            out AppUIFocusResolvedNode resolvedNode)
        {
            if (!string.IsNullOrEmpty(groupId) &&
                groups.TryGetValue(groupId, out FocusGroupState group) &&
                group.IsOpen &&
                regions.TryGetValue(
                    group.RegionId,
                    out FocusRegionState region) &&
                region.Status == AppUIFocusRegionStatus.Active)
            {
                for (int i = 0; i < group.OrderedNodes.Count; i++)
                {
                    FocusNodeRecord record = group.OrderedNodes[i];
                    if (nodeRegistry.TryResolveNode(
                            pageHandle,
                            record.Address,
                            out resolvedNode) &&
                        IsResolvedNodeEligible(resolvedNode))
                    {
                        return true;
                    }
                }
            }

            resolvedNode = default;
            return false;
        }

        private bool TryResolveEligibleAddress(
            AppUIFocusNodeAddress address,
            string regionId,
            out AppUIFocusResolvedNode resolvedNode)
        {
            resolvedNode = default;
            return address.IsValid &&
                   nodeRegistry.TryResolveNode(
                       pageHandle,
                       address,
                       out resolvedNode) &&
                   string.Equals(
                       resolvedNode.RegionId,
                       regionId,
                       StringComparison.Ordinal) &&
                   IsResolvedNodeEligible(resolvedNode);
        }

        private bool IsAddressOwnedByRegion(
            AppUIFocusNodeAddress address,
            string regionId)
        {
            return address.IsValid &&
                   groups.TryGetValue(
                       address.GroupId,
                       out FocusGroupState group) &&
                   string.Equals(
                       group.RegionId,
                       regionId,
                       StringComparison.Ordinal);
        }

        private void MarkRegionPendingRepair(FocusRegionState region)
        {
            if (region != null && region.Status != AppUIFocusRegionStatus.Closed)
            {
                region.HasPendingRepair = true;
                if (status == AppUIFocusScopeStatus.Active &&
                    region.Status == AppUIFocusRegionStatus.Active)
                {
                    hasPendingRepair = true;
                    if (focusCommitter is UIFocusCommitter concreteCommitter)
                    {
                        concreteCommitter.QueueRepairForScope(this);
                    }
                }
            }
        }

        private void IncrementRegionRevision(FocusRegionState region)
        {
            if (region.Revision == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "AppUI focus Region revision exhausted: " + region.RegionId);
            }

            region.Revision++;
        }

        private void BuildGroups(AppUIFocusDefinition definition)
        {
            for (int i = 0; i < definition.GroupCount; i++)
            {
                AppUIFocusGroupDefinition groupDefinition = definition.GetGroup(i);
                if (string.IsNullOrWhiteSpace(groupDefinition.GroupId) ||
                    groups.ContainsKey(groupDefinition.GroupId))
                {
                    throw new InvalidOperationException(
                        "Focus definition contains an invalid or duplicate group id: " +
                        groupDefinition.GroupId);
                }

                FocusGroupState group = new FocusGroupState
                {
                    GroupId = groupDefinition.GroupId,
                    RegionId = groupDefinition.RegionId,
                    Order = groupDefinition.Order,
                    IsOpen = groupDefinition.OpenByDefault,
                    VisibilityAdapter = groupDefinition.VisibilityAdapter,
                    VirtualizationAdapter = groupDefinition.VirtualizationAdapter,
                };
                groups.Add(group.GroupId, group);
                orderedGroups.Add(group);
            }

            orderedGroups.Sort(CompareGroups);
            SyncNavigatorRegionVisibility();
        }

        private void RegisterDefinitionNodes(AppUIFocusDefinition definition)
        {
            for (int i = 0; i < definition.NodeCount; i++)
            {
                AppUIFocusNodeDefinition node = definition.GetNode(i);
                if (!RegisterNode(
                        node.Address.GroupId,
                        node.Address.NodeKey,
                        node.Selectable,
                        node.ControlPolicy,
                        node.Order))
                {
                    throw new InvalidOperationException(
                        "Focus definition node registration failed. Scope=" +
                        ScopeId +
                        ", Node=" +
                        node.Address);
                }
            }
        }

        private void SetLifecycleStatus(AppUIFocusScopeStatus nextStatus)
        {
            if (status == nextStatus || status == AppUIFocusScopeStatus.Disposed)
            {
                return;
            }

            if (nextStatus != AppUIFocusScopeStatus.Active)
            {
                CancelPendingRealization();
            }

            status = nextStatus;
            switch (nextStatus)
            {
                case AppUIFocusScopeStatus.Active:
                    RestoreOpenRegionStackStatus();
                    break;
                case AppUIFocusScopeStatus.Suspended:
                    SuspendOpenRegionStack();
                    break;
                default:
                    CloseAllRegionStates();
                    break;
            }

            SyncNavigatorRegionVisibility();
            if (AppUIFocusTrace.CanTrace(pageInstanceId))
            {
                AppUIFocusTrace.Record(
                    pageInstanceId,
                    AppUIFocusTraceStage.Scope,
                    currentFocusedAddress,
                    currentFocusedAddress,
                    "Scope status changed to " + nextStatus + ".");
                PublishDebugSnapshot();
            }
        }

        private void SuspendOpenRegionStack()
        {
            if (rootRegion != null &&
                rootRegion.Status == AppUIFocusRegionStatus.Closed)
            {
                rootRegion.Status = AppUIFocusRegionStatus.Suspended;
            }

            for (int i = 0; i < orderedRegions.Count; i++)
            {
                FocusRegionState region = orderedRegions[i];
                if (region.Status != AppUIFocusRegionStatus.Closed)
                {
                    region.Status = AppUIFocusRegionStatus.Suspended;
                }
            }
        }

        private void RestoreOpenRegionStackStatus()
        {
            if (string.IsNullOrEmpty(activeLeafRegionId) ||
                !regions.TryGetValue(
                    activeLeafRegionId,
                    out FocusRegionState activeLeaf) ||
                (activeLeaf.Status == AppUIFocusRegionStatus.Closed &&
                 !ReferenceEquals(activeLeaf, rootRegion)))
            {
                activeLeafRegionId = RootRegionId;
                activeLeaf = rootRegion;
            }

            FocusRegionState current = activeLeaf;
            while (current != null)
            {
                current.Status = ReferenceEquals(current, activeLeaf)
                    ? AppUIFocusRegionStatus.Active
                    : AppUIFocusRegionStatus.Suspended;
                if (string.IsNullOrEmpty(current.ParentRegionId) ||
                    !regions.TryGetValue(
                        current.ParentRegionId,
                        out current))
                {
                    current = null;
                }
            }

            hasPendingRepair = activeLeaf.HasPendingRepair ||
                               activeLeaf.PendingRestoreAddress.IsValid;
        }

        private void CloseAllRegionStates()
        {
            for (int i = 0; i < orderedRegions.Count; i++)
            {
                FocusRegionState region = orderedRegions[i];
                region.Status = AppUIFocusRegionStatus.Closed;
                region.ActiveChildRegionId = string.Empty;
                region.SourceNodeAddress = default;
                region.PendingRestoreAddress = default;
                region.HasPendingRepair = false;
            }

            activeLeafRegionId = RootRegionId;
        }

        private void SyncNavigatorRegionVisibility()
        {
            for (int i = 0; i < orderedGroups.Count; i++)
            {
                RefreshNavigatorGroupVisibility(orderedGroups[i]);
            }
        }

        private void RefreshNavigatorGroupVisibility(FocusGroupState group)
        {
            if (group != null &&
                status == AppUIFocusScopeStatus.Active &&
                group.IsOpen &&
                regions.TryGetValue(
                    group.RegionId,
                    out FocusRegionState region) &&
                region.Status == AppUIFocusRegionStatus.Active)
            {
                navigator.OpenGroup(group.GroupId);
            }
            else if (group != null)
            {
                navigator.CloseGroup(group.GroupId);
            }
        }

        private void RequestSelectionRepair()
        {
            if (selectionRepairRevision == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "AppUI focus selection repair revision exhausted.");
            }

            selectionRepairRevision++;
            hasPendingRepair = true;
            if (regions.TryGetValue(
                    activeLeafRegionId,
                    out FocusRegionState activeRegion) &&
                activeRegion.Status != AppUIFocusRegionStatus.Closed)
            {
                activeRegion.HasPendingRepair = true;
            }
            if (status == AppUIFocusScopeStatus.Active &&
                focusCommitter is UIFocusCommitter concreteCommitter)
            {
                concreteCommitter.QueueRepairForScope(this);
            }
        }

        private void WriteFullHistory(AppUIFocusNodeAddress address)
        {
            nodeHistoryByGroup[address.GroupId] = address.NodeKey;
            lastFocusedAddress = address;
            if (groups.TryGetValue(
                    address.GroupId,
                    out FocusGroupState group) &&
                regions.TryGetValue(
                    group.RegionId,
                    out FocusRegionState region))
            {
                region.LastFocusedGroupId = address.GroupId;
                region.LastFocusedNodeAddress = address;
            }
        }

        private bool IsAddressEligible(AppUIFocusNodeAddress address)
        {
            return address.IsValid &&
                   nodeRegistry.TryResolveNode(
                       pageHandle,
                       address,
                       out AppUIFocusResolvedNode resolvedNode) &&
                   IsResolvedNodeEligible(resolvedNode);
        }

        private static bool IsUsable(Selectable selectable)
        {
            return selectable != null &&
                   selectable.gameObject != null &&
                   selectable.gameObject.activeInHierarchy &&
                   selectable.IsActive() &&
                   selectable.IsInteractable();
        }

        private void RemoveNodeRecord(FocusGroupState group, FocusNodeRecord record)
        {
            group.NodesByKey.Remove(record.Address.NodeKey);
            group.OrderedNodes.Remove(record);
            nodeRegistry.Unregister(
                pageInstanceId,
                ScopeId,
                record.Address,
                record.RegistrationGeneration);
        }

        private void ReconcileGroupStructure(FocusGroupState group)
        {
            if (group == null)
            {
                return;
            }

            if (nodeHistoryByGroup.TryGetValue(
                    group.GroupId,
                    out AppUIFocusNodeKey historyKey) &&
                !group.NodesByKey.ContainsKey(historyKey))
            {
                nodeHistoryByGroup.Remove(group.GroupId);
            }


            if (regions.TryGetValue(
                    group.RegionId,
                    out FocusRegionState region) &&
                region.LastFocusedNodeAddress.IsValid &&
                string.Equals(
                    region.LastFocusedNodeAddress.GroupId,
                    group.GroupId,
                    StringComparison.Ordinal) &&
                !group.NodesByKey.ContainsKey(
                    region.LastFocusedNodeAddress.NodeKey))
            {
                region.LastFocusedGroupId = string.Empty;
                region.LastFocusedNodeAddress = default;
                region.HasPendingRepair = true;
                if (region.Status == AppUIFocusRegionStatus.Active)
                {
                    hasPendingRepair = true;
                }
            }

            if (!currentFocusedAddress.IsValid ||
                !string.Equals(
                    currentFocusedAddress.GroupId,
                    group.GroupId,
                    StringComparison.Ordinal))
            {
                return;
            }

            bool currentIsValid = group.IsOpen &&
                region != null &&
                region.Status == AppUIFocusRegionStatus.Active &&
                group.NodesByKey.TryGetValue(
                    currentFocusedAddress.NodeKey,
                    out FocusNodeRecord record) &&
                record.Selectable != null &&
                nodeRegistry.TryResolveNode(
                    pageHandle,
                    currentFocusedAddress,
                    out AppUIFocusResolvedNode resolvedNode) &&
                resolvedNode.RegistrationGeneration == record.RegistrationGeneration &&
                ReferenceEquals(resolvedNode.Selectable, record.Selectable) &&
                string.Equals(
                    resolvedNode.RegionId,
                    group.RegionId,
                    StringComparison.Ordinal) &&
                IsUsable(record.Selectable);
            if (currentIsValid)
            {
                return;
            }

            currentFocusedAddress = default;
            RequestSelectionRepair();
        }

        private void RebuildNavigatorGroup(FocusGroupState group)
        {
            navigatorNodeBuffer.Clear();
            navigatorControlPolicyBuffer.Clear();
            for (int i = 0; i < group.OrderedNodes.Count; i++)
            {
                FocusNodeRecord record = group.OrderedNodes[i];
                Selectable selectable = record.Selectable;
                if (selectable != null)
                {
                    navigatorNodeBuffer.Add(selectable);
                    navigatorControlPolicyBuffer.Add(record.ControlPolicy);
                }
            }

            navigator.ReplaceGroupNodes(
                group.GroupId,
                navigatorNodeBuffer,
                navigatorControlPolicyBuffer);
            RefreshNavigatorGroupVisibility(group);
            navigatorNodeBuffer.Clear();
            navigatorControlPolicyBuffer.Clear();
            PublishDebugSnapshot();
        }

        private void PublishDebugSnapshot()
        {
            if (!traceEnabled ||
                traceCandidateBuilder == null ||
                !AppUIFocusTrace.CanTrace(pageInstanceId))
            {
                return;
            }

            int currentOrder = 0;
            traceCandidateBuilder.Clear();
            if (currentFocusedAddress.IsValid &&
                groups.TryGetValue(
                    currentFocusedAddress.GroupId,
                    out FocusGroupState currentGroup))
            {
                if (currentGroup.NodesByKey.TryGetValue(
                        currentFocusedAddress.NodeKey,
                        out FocusNodeRecord currentRecord))
                {
                    currentOrder = currentRecord.Order;
                }

                for (int i = 0; i < currentGroup.OrderedNodes.Count; i++)
                {
                    if (i > 0)
                    {
                        traceCandidateBuilder.Append(", ");
                    }

                    FocusNodeRecord record = currentGroup.OrderedNodes[i];
                    traceCandidateBuilder
                        .Append(record.Address.NodeKey.Value)
                        .Append('#')
                        .Append(record.Order);
                    if (!IsUsable(record.Selectable))
                    {
                        traceCandidateBuilder.Append("[unusable]");
                    }
                }
            }

            AppUIFocusDebugSnapshot snapshot = new AppUIFocusDebugSnapshot(
                pageInstanceId,
                pageId,
                ScopeId,
                status,
                ActiveRegionId,
                currentFocusedAddress,
                lastFocusedAddress,
                currentOrder,
                traceCandidateBuilder.ToString());
            AppUIFocusTrace.UpdateSnapshot(in snapshot);
        }

        private bool IsOwnedRecord(AppUIFocusResolvedNode resolvedNode)
        {
            return resolvedNode.PageHandle == pageHandle &&
                   string.Equals(resolvedNode.ScopeId, ScopeId, StringComparison.Ordinal);
        }

        private bool IsUnderPageRoot(Transform target)
        {
            return pageRoot != null && target != null && target.IsChildOf(pageRoot);
        }

        private int GetNextNodeSequence()
        {
            if (nextNodeSequence == int.MaxValue)
            {
                throw new InvalidOperationException("Focus node sequence exhausted.");
            }

            nextNodeSequence++;
            return nextNodeSequence;
        }

        private static void SortGroupNodes(FocusGroupState group)
        {
            group.OrderedNodes.Sort(CompareNodes);
        }

        private static int CompareGroups(FocusGroupState left, FocusGroupState right)
        {
            int orderComparison = left.Order.CompareTo(right.Order);
            return orderComparison != 0
                ? orderComparison
                : StringComparer.Ordinal.Compare(left.GroupId, right.GroupId);
        }

        private static int CompareNodes(FocusNodeRecord left, FocusNodeRecord right)
        {
            int orderComparison = left.Order.CompareTo(right.Order);
            return orderComparison != 0
                ? orderComparison
                : left.Sequence.CompareTo(right.Sequence);
        }

        private void LogRegistrationRejected(
            string groupId,
            AppUIFocusNodeKey nodeKey,
            Selectable selectable)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "<AppUIFocus> Node registration rejected. Scope=" +
                ScopeId +
                ", Group=" +
                (groupId ?? string.Empty) +
                ", NodeKey=" +
                nodeKey +
                ", Object=" +
                (selectable != null ? selectable.name : "null"));
#endif
        }
    }
}
