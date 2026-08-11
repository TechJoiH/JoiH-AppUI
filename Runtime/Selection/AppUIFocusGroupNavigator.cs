using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public interface IAppUIFocusMoveInputPolicy
    {
        bool ShouldConsumeWithoutNavigation(AxisEventData eventData);
    }

    public sealed class AppUIFocusGroupNavigator : IDisposable
    {
        public delegate bool FocusMoveHandler(
            AppUIFocusGroupNavigator navigator,
            string groupId,
            Selectable selectable,
            MoveDirection moveDirection);

        private sealed class FocusGroupState
        {
            public readonly List<Selectable> Nodes = new List<Selectable>(16);
            public readonly AppUIFocusSpatialGroupCache SpatialCache =
                new AppUIFocusSpatialGroupCache();
            public FocusMoveHandler MoveHandler;
            public bool IsOpen;
            public int LastIndex = -1;
        }

        private readonly Dictionary<string, FocusGroupState> groups =
            new Dictionary<string, FocusGroupState>(8);
        private readonly List<string> groupStack = new List<string>(8);
        private AppUIFocusChain focusChain;
        private IAppUIFocusAnchorProvider anchorProvider;
        private IAppUIFocusMoveInputPolicy moveInputPolicy;
        private IAppUIFocusCommitGateway commitGateway;
        private IAppUIFocusRegionNavigationGateway regionGateway;
        private IAppUIFocusSelectionObservationSink selectionObservationSink;
        private string diagnosticScopeId = string.Empty;
        private long diagnosticPageInstanceId;

        internal void SetDiagnosticScopeId(string scopeId)
        {
            diagnosticScopeId = scopeId ?? string.Empty;
        }

        internal void SetDiagnosticPageInstanceId(long pageInstanceId)
        {
            diagnosticPageInstanceId = pageInstanceId;
        }

        public void SetChain(AppUIFocusChain chain)
        {
            focusChain = chain;
            if (focusChain == null)
            {
                return;
            }

            foreach (KeyValuePair<string, FocusGroupState> pair in groups)
            {
                FocusGroupState group = pair.Value;
                if (group.MoveHandler != null && focusChain.IsSemanticGroup(pair.Key))
                {
                    group.MoveHandler = null;
                    LogLegacySemanticMix(pair.Key);
                }
            }
        }

        public void SetAnchorProvider(IAppUIFocusAnchorProvider provider)
        {
            anchorProvider = provider;
        }

        public void SetMoveInputPolicy(IAppUIFocusMoveInputPolicy policy)
        {
            moveInputPolicy = policy;
        }

        internal void SetCommitGateway(IAppUIFocusCommitGateway gateway)
        {
            commitGateway = gateway;
        }

        internal void SetRegionGateway(IAppUIFocusRegionNavigationGateway gateway)
        {
            regionGateway = gateway;
        }

        internal void SetSelectionObservationSink(
            IAppUIFocusSelectionObservationSink observationSink)
        {
            selectionObservationSink = observationSink;
        }

        public bool ShouldConsumeMoveWithoutNavigation(AxisEventData eventData)
        {
            return moveInputPolicy != null &&
                moveInputPolicy.ShouldConsumeWithoutNavigation(eventData);
        }

        /// <summary>
        /// Legacy-group fallback only. Semantic groups use BeforeMove, layout resolvers and
        /// boundary resolvers so their internal-first navigation contract remains deterministic.
        /// </summary>
        public void SetMoveHandler(string groupId, FocusMoveHandler handler)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            if (handler != null &&
                focusChain != null &&
                focusChain.IsSemanticGroup(groupId))
            {
                LogLegacySemanticMix(groupId);
                return;
            }

            FocusGroupState group = GetOrCreateGroup(groupId);
            group.MoveHandler = handler;
        }

        public void RegisterNode(string groupId, Selectable selectable)
        {
            RegisterNode(groupId, selectable, null);
        }

        public void RegisterNode(
            string groupId,
            Selectable selectable,
            IAppUIFocusControlPolicy controlPolicy)
        {
            if (string.IsNullOrEmpty(groupId) || selectable == null)
            {
                return;
            }

            FocusGroupState group = GetOrCreateGroup(groupId);
            if (!ContainsSelectable(group, selectable))
            {
                group.Nodes.Add(selectable);
                group.SpatialCache.Invalidate();
            }

            ConfigureNode(groupId, selectable, controlPolicy);
        }

        public bool UnregisterNode(string groupId, Selectable selectable)
        {
            FocusGroupState group = GetGroup(groupId);
            int index = IndexOfSelectable(group, selectable);
            if (index < 0)
            {
                return false;
            }

            group.Nodes.RemoveAt(index);
            group.SpatialCache.Invalidate();
            if (group.LastIndex == index)
            {
                group.LastIndex = -1;
            }
            else if (group.LastIndex > index)
            {
                group.LastIndex--;
            }

            DetachNode(selectable);
            return true;
        }

        /// <summary>
        /// Scope-owned registry replaces a Group snapshot through this entry so explicit Order
        /// is reflected without rebuilding the Group open stack.
        /// </summary>
        internal void ReplaceGroupNodes(
            string groupId,
            IReadOnlyList<Selectable> selectables,
            IReadOnlyList<IAppUIFocusControlPolicy> controlPolicies = null)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            FocusGroupState group = GetOrCreateGroup(groupId);
            Selectable previousLast = group.LastIndex >= 0 && group.LastIndex < group.Nodes.Count
                ? group.Nodes[group.LastIndex]
                : null;

            for (int i = 0; i < group.Nodes.Count; i++)
            {
                Selectable oldSelectable = group.Nodes[i];
                if (!ContainsSelectable(selectables, oldSelectable))
                {
                    DetachNode(oldSelectable);
                }
            }

            group.Nodes.Clear();
            int count = selectables != null ? selectables.Count : 0;
            for (int i = 0; i < count; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null || ContainsSelectable(group, selectable))
                {
                    continue;
                }

                group.Nodes.Add(selectable);
                IAppUIFocusControlPolicy controlPolicy =
                    controlPolicies != null && i < controlPolicies.Count
                        ? controlPolicies[i]
                        : null;
                ConfigureNode(groupId, selectable, controlPolicy);
            }

            group.SpatialCache.Invalidate();

            group.LastIndex = IndexOfSelectable(group, previousLast);
        }

        public void OpenGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            FocusGroupState group = GetOrCreateGroup(groupId);
            group.IsOpen = true;
            RemoveFromStack(groupId);
            groupStack.Add(groupId);
        }

        public void CloseGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) ||
                !groups.TryGetValue(groupId, out FocusGroupState group))
            {
                return;
            }

            group.IsOpen = false;
            RemoveFromStack(groupId);
        }

        public void ClearGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) ||
                !groups.TryGetValue(groupId, out FocusGroupState group))
            {
                return;
            }

            for (int i = 0; i < group.Nodes.Count; i++)
            {
                DetachNode(group.Nodes[i]);
            }

            group.IsOpen = false;
            group.LastIndex = -1;
            group.Nodes.Clear();
            group.SpatialCache.Invalidate();
            RemoveFromStack(groupId);
        }

        public bool TryGetGroupFirst(string groupId, out Selectable selectable)
        {
            return TryGetGroupAt(groupId, 0, 1, out selectable, out _);
        }

        public bool TryGetGroupLastFocused(string groupId, out Selectable selectable)
        {
            return TryGetGroupLastFocused(groupId, out selectable, out _);
        }

        public bool TryGetTopGroupLastFocused(out Selectable selectable)
        {
            return TryGetTopGroupLastFocused(out selectable, out _, out _);
        }

        /// <summary>
        /// Seeds the item used by FocusGroupLastFocused before this group has received real focus.
        /// This only records navigation state; it does not change EventSystem focus or dispatch
        /// selection callbacks.
        /// </summary>
        public bool TrySeedGroupFocus(string groupId, Selectable selectable)
        {
            if (string.IsNullOrEmpty(groupId) || selectable == null)
            {
                return false;
            }

            FocusGroupState group = GetGroup(groupId);
            if (group == null || group.LastIndex >= 0)
            {
                return false;
            }

            int index = IndexOfSelectable(group, selectable);
            if (index < 0)
            {
                return false;
            }

            group.LastIndex = index;
            return true;
        }

        public bool FocusGroupFirst(string groupId)
        {
            return FocusGroupFirst(groupId, AppUIFocusChangeReason.Programmatic);
        }

        internal bool FocusGroupFirst(
            string groupId,
            AppUIFocusChangeReason reason)
        {
            if (!TryGetGroupAt(groupId, 0, 1, out Selectable selectable, out int index))
            {
                return false;
            }

            FocusGroupState group = GetGroup(groupId);
            if (group != null)
            {
                group.LastIndex = index;
            }

            return SetFocus(
                selectable,
                ToInteractionSource(reason),
                reason);
        }

        public bool FocusGroupLast(string groupId)
        {
            return FocusGroupLast(groupId, AppUIFocusChangeReason.Programmatic);
        }

        private bool FocusGroupLast(
            string groupId,
            AppUIFocusChangeReason reason)
        {
            FocusGroupState group = GetGroup(groupId);
            if (group == null)
            {
                return false;
            }

            if (!TryGetGroupAt(groupId, group.Nodes.Count - 1, -1, out Selectable selectable, out int index))
            {
                return false;
            }

            group.LastIndex = index;
            return SetFocus(
                selectable,
                ToInteractionSource(reason),
                reason);
        }

        public bool FocusGroupLastFocused(string groupId)
        {
            return FocusGroupLastFocused(
                groupId,
                AppUIFocusChangeReason.Programmatic);
        }

        internal bool FocusGroupLastFocused(
            string groupId,
            AppUIFocusChangeReason reason)
        {
            if (!TryGetGroupLastFocused(groupId, out Selectable selectable, out int index))
            {
                return false;
            }

            FocusGroupState group = GetGroup(groupId);
            if (group != null)
            {
                group.LastIndex = index;
            }

            return SetFocus(
                selectable,
                ToInteractionSource(reason),
                reason);
        }

        public bool FocusGroup(string groupId)
        {
            return FocusGroup(
                groupId,
                string.Empty,
                null,
                MoveDirection.None);
        }

        private bool FocusGroupPreservingOrdinal(
            string targetGroupId,
            string sourceGroupId,
            Selectable sourceSelectable,
            AppUIFocusChangeReason reason)
        {
            FocusGroupState targetGroup = GetGroup(targetGroupId);
            if (targetGroup == null || !targetGroup.IsOpen || targetGroup.Nodes.Count == 0)
            {
                return false;
            }

            FocusGroupState sourceGroup = GetGroup(sourceGroupId);
            int sourceIndex = IndexOfSelectable(sourceGroup, sourceSelectable);
            int targetIndex = Mathf.Clamp(sourceIndex >= 0 ? sourceIndex : 0, 0, targetGroup.Nodes.Count - 1);
            if (!TryGetGroupAt(
                    targetGroupId,
                    targetIndex,
                    1,
                    out Selectable selectable,
                    out int foundIndex) &&
                !TryGetGroupAt(
                    targetGroupId,
                    targetIndex,
                    -1,
                    out selectable,
                    out foundIndex))
            {
                return false;
            }

            targetGroup.LastIndex = foundIndex;
            return SetFocus(
                selectable,
                ToInteractionSource(reason),
                reason);
        }

        private bool FocusGroupNearestOnEntryAxis(
            string targetGroupId,
            Selectable sourceSelectable,
            MoveDirection moveDirection,
            AppUIFocusChangeReason reason)
        {
            FocusGroupState targetGroup = GetGroup(targetGroupId);
            if (targetGroup == null ||
                !targetGroup.IsOpen ||
                sourceSelectable == null ||
                targetGroup.Nodes.Count == 0)
            {
                return false;
            }

            Vector3 sourcePosition = sourceSelectable.transform.position;
            float bestScore = float.PositiveInfinity;
            int bestIndex = -1;
            for (int i = 0; i < targetGroup.Nodes.Count; i++)
            {
                Selectable candidate = targetGroup.Nodes[i];
                if (!IsUsable(candidate))
                {
                    continue;
                }

                Vector3 delta = candidate.transform.position - sourcePosition;
                float entryAxisDistance;
                float travelDistance;
                switch (moveDirection)
                {
                    case MoveDirection.Left:
                    case MoveDirection.Right:
                        entryAxisDistance = Mathf.Abs(delta.y);
                        travelDistance = Mathf.Abs(delta.x);
                        break;
                    case MoveDirection.Up:
                    case MoveDirection.Down:
                        entryAxisDistance = Mathf.Abs(delta.x);
                        travelDistance = Mathf.Abs(delta.y);
                        break;
                    default:
                        entryAxisDistance = delta.sqrMagnitude;
                        travelDistance = 0f;
                        break;
                }

                float score = entryAxisDistance * entryAxisDistance * 1024f +
                              travelDistance * travelDistance;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            targetGroup.LastIndex = bestIndex;
            return SetFocus(
                targetGroup.Nodes[bestIndex],
                ToInteractionSource(reason),
                reason);
        }

        internal bool FocusGroup(
            string targetGroupId,
            string sourceGroupId,
            Selectable sourceSelectable,
            MoveDirection moveDirection)
        {
            FocusGroupState targetGroup = GetGroup(targetGroupId);
            if (targetGroup == null)
            {
                return false;
            }

            AppUIFocusChangeReason entryReason = moveDirection == MoveDirection.None
                ? AppUIFocusChangeReason.Programmatic
                : AppUIFocusChangeReason.Navigation;

            AppUIFocusEntryPolicy entryPolicy =
                AppUIFocusEntryPolicy.LastFocusedOrFirst;
            IAppUIFocusEntryResolver entryResolver = null;
            string entryAnchorId = string.Empty;
            if (focusChain != null &&
                focusChain.TryGetGroupRules(
                    targetGroupId,
                    out AppUIFocusGroupRules targetRules))
            {
                entryPolicy = targetRules.EntryPolicy;
                entryResolver = targetRules.EntryResolver;
                entryAnchorId = targetRules.EntryAnchorId;
            }

            if (entryResolver != null)
            {
                string sourceRegionId = string.Empty;
                string targetRegionId = string.Empty;
                AppUIFocusNodeAddress sourceAddress = default;
                AppUIFocusNodeAddress targetLastAddress = default;
                regionGateway?.TryGetGroupRegionId(sourceGroupId, out sourceRegionId);
                regionGateway?.TryGetGroupRegionId(targetGroupId, out targetRegionId);
                regionGateway?.TryGetNodeAddress(sourceSelectable, out sourceAddress);
                if (!string.IsNullOrEmpty(targetRegionId))
                {
                    regionGateway?.TryGetRegionLastFocusedAddress(
                        targetRegionId,
                        out targetLastAddress);
                }

                AppUIFocusEntryContext context = new AppUIFocusEntryContext(
                    diagnosticScopeId,
                    sourceRegionId,
                    targetRegionId,
                    sourceGroupId,
                    targetGroupId,
                    sourceAddress,
                    sourceSelectable,
                    moveDirection,
                    targetLastAddress,
                    targetGroup.LastIndex,
                    targetGroup.Nodes.Count);
                try
                {
                    if (entryResolver.TryResolve(in context, out Selectable resolved))
                    {
                        return FocusNode(targetGroupId, resolved, entryReason);
                    }
                }
                catch (Exception exception)
                {
                    LogExtensionException(
                        targetGroupId,
                        sourceSelectable,
                        moveDirection,
                        "Entry",
                        entryResolver,
                        exception);
                    return true;
                }
            }

            switch (entryPolicy)
            {
                case AppUIFocusEntryPolicy.FirstUsable:
                    return FocusGroupFirst(targetGroupId, entryReason);
                case AppUIFocusEntryPolicy.LastUsable:
                    return FocusGroupLast(targetGroupId, entryReason);
                case AppUIFocusEntryPolicy.LastFocusedOrFirst:
                    return FocusGroupLastFocused(targetGroupId, entryReason);
                case AppUIFocusEntryPolicy.PreserveOrdinalOrClamp:
                    return FocusGroupPreservingOrdinal(
                        targetGroupId,
                        sourceGroupId,
                        sourceSelectable,
                        entryReason);
                case AppUIFocusEntryPolicy.NearestOnEntryAxis:
                    return FocusGroupNearestOnEntryAxis(
                        targetGroupId,
                        sourceSelectable,
                        moveDirection,
                        entryReason);
                case AppUIFocusEntryPolicy.AnchorOrFirst:
                    if (!string.IsNullOrEmpty(entryAnchorId) &&
                        FocusAnchor(
                            entryAnchorId,
                            entryReason,
                            targetGroupId))
                    {
                        return true;
                    }

                    return FocusGroupFirst(targetGroupId, entryReason);
                default:
                    return false;
            }
        }

        internal bool FocusAnchor(
            string anchorId,
            AppUIFocusChangeReason reason,
            string expectedGroupId = null)
        {
            if (string.IsNullOrEmpty(anchorId) || anchorProvider == null)
            {
                return false;
            }

            if (anchorProvider is IAppUIFocusTargetAnchorProvider targetProvider &&
                targetProvider.TryGetFocusAnchorTarget(
                    anchorId,
                    out AppUIFocusTarget target))
            {
                if (target.Kind == AppUIFocusTargetKind.NodeAddress)
                {
                    if (!string.IsNullOrEmpty(expectedGroupId) &&
                        !string.Equals(
                            target.NodeAddress.GroupId,
                            expectedGroupId,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }

                    AppUIFocusRequestResult result = commitGateway != null
                        ? commitGateway.CommitFocus(target.NodeAddress, reason)
                        : AppUIFocusRequestResult.NodeMissing;
                    return IsAcceptedFocusResult(result);
                }

                if (target.Kind == AppUIFocusTargetKind.Selectable)
                {
                    return string.IsNullOrEmpty(expectedGroupId)
                        ? FocusSelectable(target.Selectable, reason)
                        : FocusNode(expectedGroupId, target.Selectable, reason);
                }
            }

            if (!anchorProvider.TryGetFocusAnchor(
                    anchorId,
                    out Selectable selectable))
            {
                return false;
            }

            return string.IsNullOrEmpty(expectedGroupId)
                ? FocusSelectable(selectable, reason)
                : FocusNode(expectedGroupId, selectable, reason);
        }

        internal bool IsCurrentAnchor(
            string anchorId,
            Selectable currentSelectable)
        {
            if (string.IsNullOrEmpty(anchorId) ||
                currentSelectable == null ||
                anchorProvider == null)
            {
                return false;
            }

            if (anchorProvider is IAppUIFocusTargetAnchorProvider targetProvider &&
                targetProvider.TryGetFocusAnchorTarget(
                    anchorId,
                    out AppUIFocusTarget target))
            {
                if (target.Kind == AppUIFocusTargetKind.Selectable)
                {
                    return target.Selectable == currentSelectable;
                }

                return target.Kind == AppUIFocusTargetKind.NodeAddress &&
                       regionGateway != null &&
                       regionGateway.TryGetNodeAddress(
                           currentSelectable,
                           out AppUIFocusNodeAddress currentAddress) &&
                       currentAddress == target.NodeAddress;
            }

            return anchorProvider.TryGetFocusAnchor(
                       anchorId,
                       out Selectable selectable) &&
                   selectable == currentSelectable;
        }

        public bool FocusTopGroup()
        {
            if (!TryGetTopGroupLastFocused(out Selectable selectable, out string groupId, out int index))
            {
                return false;
            }

            FocusGroupState group = GetGroup(groupId);
            if (group != null)
            {
                group.LastIndex = index;
            }

            return SetFocus(
                selectable,
                AppUIInteractionSourceKind.Programmatic,
                AppUIFocusChangeReason.Programmatic);
        }

        public bool FocusNode(string groupId, Selectable selectable)
        {
            return FocusNode(
                groupId,
                selectable,
                AppUIFocusChangeReason.Programmatic);
        }

        public bool FocusNode(
            string groupId,
            Selectable selectable,
            AppUIFocusChangeReason reason)
        {
            if (string.IsNullOrEmpty(groupId) || selectable == null)
            {
                return false;
            }

            FocusGroupState group = GetGroup(groupId);
            if (group == null)
            {
                return false;
            }

            int index = IndexOfSelectable(group, selectable);
            if (index < 0 || !IsUsable(selectable))
            {
                return false;
            }

            group.LastIndex = index;
            return SetFocus(
                selectable,
                ToInteractionSource(reason),
                reason);
        }

        public bool FocusSelectable(Selectable selectable)
        {
            return FocusSelectable(
                selectable,
                AppUIInteractionSourceKind.Programmatic);
        }

        private bool FocusSelectable(
            Selectable selectable,
            AppUIInteractionSourceKind sourceKind)
        {
            if (selectable == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, FocusGroupState> pair in groups)
            {
                FocusGroupState group = pair.Value;
                int index = IndexOfSelectable(group, selectable);
                if (index < 0 || !IsUsable(selectable))
                {
                    continue;
                }

                group.LastIndex = index;
                return SetFocus(
                    selectable,
                    sourceKind,
                    ToChangeReason(sourceKind));
            }

            return false;
        }

        internal bool FocusSelectable(
            Selectable selectable,
            AppUIFocusChangeReason reason)
        {
            if (selectable == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, FocusGroupState> pair in groups)
            {
                FocusGroupState group = pair.Value;
                int index = IndexOfSelectable(group, selectable);
                if (index < 0 || !IsUsable(selectable))
                {
                    continue;
                }

                group.LastIndex = index;
                return SetFocus(
                    selectable,
                    ToInteractionSource(reason),
                    reason);
            }

            return false;
        }

        /// <summary>
        /// Unity 会把同一 Move 事件分发给当前 GameObject 上的全部 IMoveHandler。
        /// 此入口不依赖 eventData.used 决定优先级，只根据页面输入策略和节点 ControlPolicy
        /// 选择框架导航或受支持控件的原生语义。
        /// </summary>
        internal bool HandleMoveInput(
            string groupId,
            Selectable selectable,
            AxisEventData eventData,
            IAppUIFocusControlPolicy controlPolicy)
        {
            if (eventData == null || selectable == null)
            {
                return false;
            }

            FocusGroupState activeGroup = GetGroup(groupId);
            if (activeGroup == null ||
                !activeGroup.IsOpen ||
                IndexOfSelectable(activeGroup, selectable) < 0)
            {
                return true;
            }

            try
            {
                if (ShouldConsumeMoveWithoutNavigation(eventData))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                LogExtensionException(
                    groupId,
                    selectable,
                    eventData.moveDir,
                    "MoveInputPolicy",
                    moveInputPolicy,
                    exception);
                return true;
            }

            AppUIFocusGroupRules rules = null;
            focusChain?.TryGetGroupRules(groupId, out rules);
            AppUIFocusMoveContext context = CreateMoveContext(
                groupId,
                selectable,
                eventData.moveDir,
                AppUIFocusMoveStage.ControlPolicy,
                rules);
            IAppUIFocusControlPolicy effectivePolicy =
                AppUIFocusControlPolicies.Resolve(selectable, controlPolicy);

            AppUIFocusControlMoveMode moveMode;
            try
            {
                moveMode = effectivePolicy.GetMoveMode(in context);
            }
            catch (Exception exception)
            {
                LogExtensionException(
                    groupId,
                    selectable,
                    eventData.moveDir,
                    "ControlPolicy",
                    effectivePolicy,
                    exception);
                return true;
            }

            if (moveMode == AppUIFocusControlMoveMode.DelegateToNativeControl)
            {
                if (!(effectivePolicy is IAppUIFocusNativeMoveAdapter))
                {
                    LogRejectedNativeMoveAdapter(
                        groupId,
                        selectable,
                        eventData.moveDir,
                        effectivePolicy);
                    return true;
                }

                AppUIInteractionSourceAuthority.NotifyNavigation();
                return true;
            }

            return MoveFocus(groupId, selectable, eventData.moveDir);
        }

        public bool MoveFocus(string groupId, Selectable selectable, MoveDirection moveDirection)
        {
            bool trace = AppUIFocusTrace.CanTrace(diagnosticPageInstanceId);
            AppUIFocusNodeAddress sourceAddress = default;
            if (trace)
            {
                regionGateway?.TryGetNodeAddress(selectable, out sourceAddress);
                AppUIFocusTrace.Record(
                    diagnosticPageInstanceId,
                    AppUIFocusTraceStage.Move,
                    sourceAddress,
                    default,
                    "Move started. Group=" +
                    groupId +
                    ", Direction=" +
                    moveDirection +
                    ", Selectable=" +
                    (selectable != null ? selectable.name : "null"));
            }

            bool handled = MoveFocusCore(groupId, selectable, moveDirection);
            if (trace)
            {
                AppUIFocusNodeAddress targetAddress = default;
                GameObject selectedObject = EventSystem.current != null
                    ? EventSystem.current.currentSelectedGameObject
                    : null;
                if (selectedObject != null)
                {
                    regionGateway?.TryGetNodeAddress(
                        selectedObject.GetComponent<Selectable>(),
                        out targetAddress);
                }

                AppUIFocusTrace.Record(
                    diagnosticPageInstanceId,
                    AppUIFocusTraceStage.Move,
                    sourceAddress,
                    targetAddress,
                    "Move completed. Handled=" + handled);
            }

            return handled;
        }

        private bool MoveFocusCore(
            string groupId,
            Selectable selectable,
            MoveDirection moveDirection)
        {
            AppUIInteractionSourceAuthority.NotifyNavigation();
            NotifySelected(groupId, selectable);

            AppUIFocusGroupRules rules = null;
            focusChain?.TryGetGroupRules(groupId, out rules);
            if (rules != null && rules.BeforeMoveRule != null)
            {
                AppUIFocusMoveContext context = CreateMoveContext(
                    groupId,
                    selectable,
                    moveDirection,
                    AppUIFocusMoveStage.BeforeMove,
                    rules);
                AppUIFocusMoveDecision decision;
                try
                {
                    decision = rules.BeforeMoveRule.Evaluate(in context);
                }
                catch (Exception exception)
                {
                    LogExtensionException(
                        groupId,
                        selectable,
                        moveDirection,
                        "BeforeMove",
                        rules.BeforeMoveRule,
                        exception);
                    return true;
                }
                if (decision.Result == AppUIFocusMoveResult.BoundaryReached)
                {
                    return ResolveBoundary(groupId, selectable, moveDirection, rules);
                }

                if (TryCommitDecision(decision, out bool handled))
                {
                    return handled;
                }
            }

            if (rules != null && rules.Layout != AppUIFocusGroupLayout.Legacy)
            {
                return MoveWithinSemanticGroup(
                    groupId,
                    selectable,
                    moveDirection,
                    rules);
            }

            if (focusChain != null &&
                focusChain.TryGetAction(groupId, moveDirection, out AppUIFocusAction action))
            {
                bool handled = ExecuteChainAction(
                    action,
                    groupId,
                    selectable,
                    moveDirection);
                AppUIInteractionSourceAuthority.NotifyNavigation();
                return handled;
            }

            FocusGroupState group = GetGroup(groupId);
            if (group != null && group.MoveHandler != null)
            {
                bool handled;
                try
                {
                    handled = group.MoveHandler.Invoke(
                        this,
                        groupId,
                        selectable,
                        moveDirection);
                }
                catch (Exception exception)
                {
                    LogExtensionException(
                        groupId,
                        selectable,
                        moveDirection,
                        "LegacyMoveHandler",
                        group.MoveHandler,
                        exception);
                    return true;
                }
                AppUIInteractionSourceAuthority.NotifyNavigation();
                if (handled)
                {
                    return true;
                }
            }

            switch (moveDirection)
            {
                case MoveDirection.Left:
                case MoveDirection.Up:
                    return MoveWithinGroup(groupId, -1, false);
                case MoveDirection.Right:
                case MoveDirection.Down:
                    return MoveWithinGroup(groupId, 1, false);
            }

            return false;
        }

        private bool MoveWithinSemanticGroup(
            string groupId,
            Selectable selectable,
            MoveDirection moveDirection,
            AppUIFocusGroupRules rules)
        {
            if (!IsDirectionalMove(moveDirection))
            {
                return false;
            }

            if (rules.LayoutResolver != null)
            {
                AppUIFocusMoveContext context = CreateMoveContext(
                    groupId,
                    selectable,
                    moveDirection,
                    AppUIFocusMoveStage.Layout,
                    rules);
                AppUIFocusMoveDecision decision;
                try
                {
                    decision = rules.LayoutResolver.Resolve(in context);
                }
                catch (Exception exception)
                {
                    LogExtensionException(
                        groupId,
                        selectable,
                        moveDirection,
                        "Layout",
                        rules.LayoutResolver,
                        exception);
                    return true;
                }
                if (decision.Result == AppUIFocusMoveResult.BoundaryReached)
                {
                    return ResolveBoundary(groupId, selectable, moveDirection, rules);
                }

                if (TryCommitDecision(decision, out bool handled))
                {
                    return handled;
                }
            }

            bool moved = false;
            bool wrap = rules.WrapPolicy == AppUIFocusWrapPolicy.Cycle;
            switch (rules.Layout)
            {
                case AppUIFocusGroupLayout.Vertical:
                    if (moveDirection == MoveDirection.Up)
                    {
                        moved = MoveWithinGroup(groupId, -1, wrap);
                    }
                    else if (moveDirection == MoveDirection.Down)
                    {
                        moved = MoveWithinGroup(groupId, 1, wrap);
                    }
                    break;
                case AppUIFocusGroupLayout.Horizontal:
                    if (moveDirection == MoveDirection.Left)
                    {
                        moved = MoveWithinGroup(groupId, -1, wrap);
                    }
                    else if (moveDirection == MoveDirection.Right)
                    {
                        moved = MoveWithinGroup(groupId, 1, wrap);
                    }
                    break;
                case AppUIFocusGroupLayout.Grid:
                    moved = MoveWithinSemanticGrid(
                        groupId,
                        moveDirection,
                        rules.GridColumnCount,
                        rules.GridShortRowPolicy);
                    break;
                case AppUIFocusGroupLayout.Spatial:
                    moved = MoveWithinSpatialGroup(
                        groupId,
                        selectable,
                        moveDirection);
                    break;
            }

            if (moved)
            {
                return true;
            }

            return ResolveBoundary(groupId, selectable, moveDirection, rules);
        }

        private bool ResolveBoundary(
            string groupId,
            Selectable selectable,
            MoveDirection moveDirection,
            AppUIFocusGroupRules rules)
        {
            if (rules != null &&
                rules.TryGetBoundaryResolver(
                    moveDirection,
                    out IAppUIFocusBoundaryResolver resolver))
            {
                AppUIFocusMoveContext context = CreateMoveContext(
                    groupId,
                    selectable,
                    moveDirection,
                    AppUIFocusMoveStage.Boundary,
                    rules);
                AppUIFocusMoveDecision decision;
                try
                {
                    decision = resolver.Resolve(in context);
                }
                catch (Exception exception)
                {
                    LogExtensionException(
                        groupId,
                        selectable,
                        moveDirection,
                        "Boundary",
                        resolver,
                        exception);
                    return true;
                }
                if (decision.Result != AppUIFocusMoveResult.BoundaryReached &&
                    TryCommitDecision(decision, out _))
                {
                    return true;
                }
            }

            if (rules != null &&
                rules.TryGetBoundaryAction(
                    moveDirection,
                    out AppUIFocusAction boundaryAction))
            {
                if (ExecuteChainAction(
                    boundaryAction,
                    groupId,
                    selectable,
                    moveDirection))
                {
                    return true;
                }
            }

            if (regionGateway != null &&
                regionGateway.TryRouteRegionBoundary(
                    groupId,
                    selectable,
                    moveDirection))
            {
                return true;
            }

            // Semantic groups own their recognized direction input even when no
            // usable boundary target exists. Focus remains stable instead of
            // leaking into Unity's implicit navigation or another input owner.
            AppUIInteractionSourceAuthority.NotifyNavigation();
            return true;
        }

        private AppUIFocusMoveContext CreateMoveContext(
            string groupId,
            Selectable selectable,
            MoveDirection moveDirection,
            AppUIFocusMoveStage stage,
            AppUIFocusGroupRules rules)
        {
            FocusGroupState group = GetGroup(groupId);
            int currentIndex = group != null
                ? IndexOfSelectable(group, selectable)
                : -1;
            int nodeCount = group != null ? group.Nodes.Count : 0;
            return new AppUIFocusMoveContext(
                groupId,
                selectable,
                moveDirection,
                stage,
                rules != null ? rules.Layout : AppUIFocusGroupLayout.Legacy,
                rules != null ? rules.WrapPolicy : AppUIFocusWrapPolicy.Stop,
                rules != null ? rules.GridColumnCount : 0,
                rules != null
                    ? rules.GridShortRowPolicy
                    : AppUIFocusGridShortRowPolicy.Reject,
                currentIndex,
                nodeCount);
        }

        private bool TryCommitDecision(
            AppUIFocusMoveDecision decision,
            out bool handled)
        {
            switch (decision.Result)
            {
                case AppUIFocusMoveResult.ContinueDefault:
                case AppUIFocusMoveResult.BoundaryReached:
                    handled = false;
                    return false;
                case AppUIFocusMoveResult.FocusTarget:
                    FocusSelectable(
                        decision.Target,
                        AppUIInteractionSourceKind.Navigation);
                    handled = true;
                    return true;
                case AppUIFocusMoveResult.Consumed:
                case AppUIFocusMoveResult.Blocked:
                default:
                    handled = true;
                    return true;
            }
        }

        private bool MoveWithinSemanticGrid(
            string groupId,
            MoveDirection moveDirection,
            int columnCount,
            AppUIFocusGridShortRowPolicy shortRowPolicy)
        {
            switch (moveDirection)
            {
                case MoveDirection.Left:
                    return MoveWithinGrid(groupId, columnCount, -1, 0, shortRowPolicy);
                case MoveDirection.Right:
                    return MoveWithinGrid(groupId, columnCount, 1, 0, shortRowPolicy);
                case MoveDirection.Up:
                    return MoveWithinGrid(groupId, columnCount, 0, -1, shortRowPolicy);
                case MoveDirection.Down:
                    return MoveWithinGrid(groupId, columnCount, 0, 1, shortRowPolicy);
                default:
                    return false;
            }
        }

        private static bool IsDirectionalMove(MoveDirection moveDirection)
        {
            return moveDirection == MoveDirection.Left ||
                moveDirection == MoveDirection.Right ||
                moveDirection == MoveDirection.Up ||
                moveDirection == MoveDirection.Down;
        }

        private bool MoveWithinSpatialGroup(
            string groupId,
            Selectable source,
            MoveDirection moveDirection)
        {
            if (!TryGetSpatialTarget(
                    groupId,
                    source,
                    moveDirection,
                    out Selectable target,
                    out _,
                    out int targetIndex))
            {
                return false;
            }

            FocusGroupState group = GetGroup(groupId);
            if (group == null)
            {
                return false;
            }

            group.LastIndex = targetIndex;
            return SetFocus(
                target,
                AppUIInteractionSourceKind.Navigation,
                AppUIFocusChangeReason.Navigation);
        }

        internal bool TryGetSpatialTarget(
            string groupId,
            Selectable source,
            MoveDirection moveDirection,
            out Selectable target,
            out AppUIFocusSpatialScore score,
            out int targetIndex)
        {
            FocusGroupState group = GetGroup(groupId);
            if (group == null || !group.IsOpen)
            {
                target = null;
                score = default;
                targetIndex = -1;
                return false;
            }

            return group.SpatialCache.TryGetTarget(
                group.Nodes,
                source,
                moveDirection,
                out target,
                out score,
                out targetIndex);
        }

        internal bool TryGetSpatialTarget(
            string groupId,
            in AppUIFocusSpatialRect sourceRect,
            MoveDirection moveDirection,
            out Selectable target,
            out AppUIFocusSpatialScore score,
            out int targetIndex)
        {
            FocusGroupState group = GetGroup(groupId);
            if (group == null || !group.IsOpen)
            {
                target = null;
                score = default;
                targetIndex = -1;
                return false;
            }

            return group.SpatialCache.TryGetTarget(
                group.Nodes,
                in sourceRect,
                moveDirection,
                out target,
                out score,
                out targetIndex);
        }

        public bool MoveWithinGroup(string groupId, int delta, bool wrap)
        {
            AppUIInteractionSourceAuthority.NotifyNavigation();
            FocusGroupState group = GetGroup(groupId);
            if (group == null || group.Nodes.Count == 0 || delta == 0)
            {
                return false;
            }

            int startIndex = group.LastIndex;
            if (startIndex < 0 || startIndex >= group.Nodes.Count)
            {
                startIndex = delta > 0 ? -1 : group.Nodes.Count;
            }

            int index = startIndex;
            int checkedCount = 0;
            while (checkedCount < group.Nodes.Count)
            {
                index += delta;
                if (index < 0 || index >= group.Nodes.Count)
                {
                    if (!wrap)
                    {
                        return false;
                    }

                    index = index < 0 ? group.Nodes.Count - 1 : 0;
                }

                Selectable selectable = group.Nodes[index];
                if (IsUsable(selectable))
                {
                    group.LastIndex = index;
                    return SetFocus(
                        selectable,
                        AppUIInteractionSourceKind.Navigation,
                        AppUIFocusChangeReason.Navigation);
                }

                checkedCount++;
            }

            return false;
        }

        public bool MoveWithinGrid(
            string groupId,
            int columnCount,
            int columnDelta,
            int rowDelta)
        {
            return MoveWithinGrid(
                groupId,
                columnCount,
                columnDelta,
                rowDelta,
                AppUIFocusGridShortRowPolicy.Reject);
        }

        public bool MoveWithinGrid(
            string groupId,
            int columnCount,
            int columnDelta,
            int rowDelta,
            AppUIFocusGridShortRowPolicy shortRowPolicy)
        {
            AppUIInteractionSourceAuthority.NotifyNavigation();
            FocusGroupState group = GetGroup(groupId);
            if (group == null ||
                group.Nodes.Count == 0 ||
                columnCount <= 0 ||
                (columnDelta == 0 && rowDelta == 0))
            {
                return false;
            }

            if (!AppUIFocusGridUtility.TryGetTargetIndex(
                    group.LastIndex,
                    group.Nodes.Count,
                    columnCount,
                    columnDelta,
                    rowDelta,
                    shortRowPolicy,
                    out int targetIndex))
            {
                return false;
            }

            int checkedCount = 0;
            while (checkedCount < group.Nodes.Count)
            {
                Selectable target = group.Nodes[targetIndex];
                if (IsUsable(target))
                {
                    group.LastIndex = targetIndex;
                    return SetFocus(
                        target,
                        AppUIInteractionSourceKind.Navigation,
                        AppUIFocusChangeReason.Navigation);
                }

                checkedCount++;
                if (!AppUIFocusGridUtility.TryGetTargetIndex(
                        targetIndex,
                        group.Nodes.Count,
                        columnCount,
                        columnDelta,
                        rowDelta,
                        shortRowPolicy,
                        out targetIndex))
                {
                    return false;
                }
            }

            return false;
        }

        public void NotifySelected(string groupId, Selectable selectable)
        {
            if (string.IsNullOrEmpty(groupId) || selectable == null)
            {
                return;
            }

            FocusGroupState group = GetGroup(groupId);
            if (group == null)
            {
                return;
            }

            int index = IndexOfSelectable(group, selectable);
            if (index >= 0)
            {
                group.LastIndex = index;
            }
        }

        internal void NotifySelectionObserved(
            string groupId,
            Selectable selectable)
        {
            if (selectionObservationSink != null && selectable != null)
            {
                selectionObservationSink.NotifySelected(selectable.gameObject);
                return;
            }

            NotifySelected(groupId, selectable);
        }

        internal void NotifySelectionDeselected(Selectable selectable)
        {
            if (selectionObservationSink != null && selectable != null)
            {
                selectionObservationSink.NotifyDeselected(selectable.gameObject);
            }
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, FocusGroupState> pair in groups)
            {
                FocusGroupState group = pair.Value;
                for (int i = 0; i < group.Nodes.Count; i++)
                {
                    DetachNode(group.Nodes[i]);
                }
            }

            groups.Clear();
            groupStack.Clear();
            focusChain = null;
            anchorProvider = null;
            moveInputPolicy = null;
            commitGateway = null;
            selectionObservationSink = null;
            regionGateway = null;
            diagnosticScopeId = string.Empty;
        }

        private bool ExecuteChainAction(
            AppUIFocusAction action,
            string groupId,
            Selectable selectable,
            MoveDirection moveDirection)
        {
            if (action == null)
            {
                return false;
            }

            AppUIFocusActionContext context =
                new AppUIFocusActionContext(
                    this,
                    anchorProvider,
                    regionGateway,
                    groupId,
                    selectable,
                    moveDirection);
            try
            {
                return action.Execute(context);
            }
            catch (Exception exception)
            {
                LogExtensionException(
                    groupId,
                    selectable,
                    moveDirection,
                    "Action",
                    action,
                    exception);
                return true;
            }
        }

        private bool TryGetGroupLastFocused(string groupId, out Selectable selectable, out int index)
        {
            FocusGroupState group = GetGroup(groupId);
            if (group == null)
            {
                selectable = null;
                index = -1;
                return false;
            }

            return TryGetGroupAt(groupId, group.LastIndex, 1, out selectable, out index) ||
                TryGetGroupAt(groupId, 0, 1, out selectable, out index);
        }

        private bool TryGetTopGroupLastFocused(
            out Selectable selectable,
            out string groupId,
            out int index)
        {
            for (int i = groupStack.Count - 1; i >= 0; i--)
            {
                string currentGroupId = groupStack[i];
                FocusGroupState group = GetGroup(currentGroupId);
                if (group == null || !group.IsOpen)
                {
                    continue;
                }

                if (TryGetGroupLastFocused(currentGroupId, out selectable, out index))
                {
                    groupId = currentGroupId;
                    return true;
                }
            }

            selectable = null;
            groupId = string.Empty;
            index = -1;
            return false;
        }

        private bool TryGetGroupAt(
            string groupId,
            int startIndex,
            int step,
            out Selectable selectable,
            out int foundIndex)
        {
            FocusGroupState group = GetGroup(groupId);
            if (group == null || !group.IsOpen || group.Nodes.Count == 0)
            {
                selectable = null;
                foundIndex = -1;
                return false;
            }

            int index = Mathf.Clamp(startIndex, 0, group.Nodes.Count - 1);
            int safeStep = step >= 0 ? 1 : -1;
            while (index >= 0 && index < group.Nodes.Count)
            {
                Selectable candidate = group.Nodes[index];
                if (IsUsable(candidate))
                {
                    selectable = candidate;
                    foundIndex = index;
                    return true;
                }

                index += safeStep;
            }

            selectable = null;
            foundIndex = -1;
            return false;
        }

        private FocusGroupState GetGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) ||
                !groups.TryGetValue(groupId, out FocusGroupState group))
            {
                return null;
            }

            return group;
        }

        private FocusGroupState GetOrCreateGroup(string groupId)
        {
            if (!groups.TryGetValue(groupId, out FocusGroupState group))
            {
                group = new FocusGroupState();
                groups.Add(groupId, group);
            }

            return group;
        }

        private static bool ContainsSelectable(FocusGroupState group, Selectable selectable)
        {
            return IndexOfSelectable(group, selectable) >= 0;
        }

        private static bool ContainsSelectable(
            IReadOnlyList<Selectable> selectables,
            Selectable selectable)
        {
            if (selectables == null || selectable == null)
            {
                return false;
            }

            for (int i = 0; i < selectables.Count; i++)
            {
                if (ReferenceEquals(selectables[i], selectable))
                {
                    return true;
                }
            }

            return false;
        }

        private static void LogLegacySemanticMix(string groupId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "<AppUIFocus> Semantic Group cannot register a legacy SetMoveHandler. Group=" +
                (groupId ?? string.Empty));
#endif
        }

        private void ConfigureNode(
            string groupId,
            Selectable selectable,
            IAppUIFocusControlPolicy controlPolicy)
        {
            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;

            AppUIFocusGroupNode node = selectable.GetComponent<AppUIFocusGroupNode>();
            if (node == null)
            {
                node = selectable.gameObject.AddComponent<AppUIFocusGroupNode>();
            }

            node.Initialize(this, groupId, selectable, controlPolicy);
        }

        private void LogExtensionException(
            string groupId,
            Selectable selectable,
            MoveDirection moveDirection,
            string stage,
            object extension,
            Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "<AppUIFocus> Focus extension failed. Scope=" +
                diagnosticScopeId +
                ", Group=" +
                (groupId ?? string.Empty) +
                ", Node=" +
                (selectable != null ? selectable.name : string.Empty) +
                ", Stage=" +
                (stage ?? string.Empty) +
                ", Direction=" +
                moveDirection +
                ", Extension=" +
                (extension != null ? extension.GetType().FullName : string.Empty) +
                ", Exception=" +
                exception);
#endif
        }

        private void LogRejectedNativeMoveAdapter(
            string groupId,
            Selectable selectable,
            MoveDirection moveDirection,
            IAppUIFocusControlPolicy controlPolicy)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "<AppUIFocus> Native control delegation rejected. Scope=" +
                diagnosticScopeId +
                ", Group=" +
                (groupId ?? string.Empty) +
                ", Node=" +
                (selectable != null ? selectable.name : string.Empty) +
                ", Stage=ControlPolicy, Direction=" +
                moveDirection +
                ", Policy=" +
                (controlPolicy != null ? controlPolicy.GetType().FullName : string.Empty) +
                ". DelegateToNativeControl requires IAppUIFocusNativeMoveAdapter.");
#endif
        }

        private void DetachNode(Selectable selectable)
        {
            if (selectable == null)
            {
                return;
            }

            AppUIFocusGroupNode node = selectable.GetComponent<AppUIFocusGroupNode>();
            node?.Detach(this, selectable);
        }

        private static int IndexOfSelectable(FocusGroupState group, Selectable selectable)
        {
            if (group == null || selectable == null)
            {
                return -1;
            }

            for (int i = 0; i < group.Nodes.Count; i++)
            {
                if (group.Nodes[i] == selectable)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsUsable(Selectable selectable)
        {
            return selectable != null &&
                selectable.IsActive() &&
                selectable.IsInteractable() &&
                selectable.gameObject.activeInHierarchy;
        }

        private bool SetFocus(
            Selectable selectable,
            AppUIInteractionSourceKind sourceKind,
            AppUIFocusChangeReason reason)
        {
            if (!IsUsable(selectable))
            {
                return false;
            }

            AppUIFocusRequestResult result = commitGateway != null
                ? commitGateway.CommitFocus(selectable, reason)
                : UIFocusCommitter.CommitLegacySelection(selectable, sourceKind);
            return IsAcceptedFocusResult(result);
        }

        private static bool IsAcceptedFocusResult(AppUIFocusRequestResult result)
        {
            return result == AppUIFocusRequestResult.Focused ||
                   result == AppUIFocusRequestResult.Consumed ||
                   result == AppUIFocusRequestResult.Deferred ||
                   result == AppUIFocusRequestResult.PendingRealization;
        }

        private static AppUIInteractionSourceKind ToInteractionSource(
            AppUIFocusChangeReason reason)
        {
            switch (reason)
            {
                case AppUIFocusChangeReason.Navigation:
                    return AppUIInteractionSourceKind.Navigation;
                case AppUIFocusChangeReason.PointerClick:
                case AppUIFocusChangeReason.PointerHover:
                    return AppUIInteractionSourceKind.Pointer;
                default:
                    return AppUIInteractionSourceKind.Programmatic;
            }
        }

        private static AppUIFocusChangeReason ToChangeReason(
            AppUIInteractionSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case AppUIInteractionSourceKind.Navigation:
                    return AppUIFocusChangeReason.Navigation;
                case AppUIInteractionSourceKind.Pointer:
                    return AppUIFocusChangeReason.PointerHover;
                default:
                    return AppUIFocusChangeReason.Programmatic;
            }
        }

        private void RemoveFromStack(string groupId)
        {
            for (int i = groupStack.Count - 1; i >= 0; i--)
            {
                if (groupStack[i] == groupId)
                {
                    groupStack.RemoveAt(i);
                }
            }
        }
    }

    public enum AppUIFocusGridShortRowPolicy
    {
        Reject = 0,
        ClampToLastItem = 1,
    }

    public static class AppUIFocusGridUtility
    {
        public static bool TryGetTargetIndex(
            int currentIndex,
            int nodeCount,
            int columnCount,
            int columnDelta,
            int rowDelta,
            out int targetIndex)
        {
            return TryGetTargetIndex(
                currentIndex,
                nodeCount,
                columnCount,
                columnDelta,
                rowDelta,
                AppUIFocusGridShortRowPolicy.Reject,
                out targetIndex);
        }

        public static bool TryGetTargetIndex(
            int currentIndex,
            int nodeCount,
            int columnCount,
            int columnDelta,
            int rowDelta,
            AppUIFocusGridShortRowPolicy shortRowPolicy,
            out int targetIndex)
        {
            targetIndex = -1;
            if (currentIndex < 0 ||
                currentIndex >= nodeCount ||
                nodeCount <= 0 ||
                columnCount <= 0 ||
                (columnDelta == 0 && rowDelta == 0))
            {
                return false;
            }

            int currentRow = currentIndex / columnCount;
            int currentColumn = currentIndex % columnCount;
            int targetRow = currentRow + rowDelta;
            int targetColumn = currentColumn + columnDelta;
            if (targetRow < 0 ||
                targetColumn < 0 ||
                targetColumn >= columnCount)
            {
                return false;
            }

            int targetRowStart = targetRow * columnCount;
            if (targetRowStart < 0 || targetRowStart >= nodeCount)
            {
                return false;
            }

            int targetRowEnd = Mathf.Min(targetRowStart + columnCount, nodeCount);
            int candidateIndex = targetRowStart + targetColumn;
            if (candidateIndex >= targetRowEnd)
            {
                if (shortRowPolicy != AppUIFocusGridShortRowPolicy.ClampToLastItem ||
                    columnDelta != 0 ||
                    rowDelta == 0)
                {
                    return false;
                }

                candidateIndex = targetRowEnd - 1;
            }

            targetIndex = candidateIndex;
            return true;
        }
    }
}
