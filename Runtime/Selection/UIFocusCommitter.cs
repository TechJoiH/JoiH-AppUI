using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// AppUI 语义焦点的唯一 EventSystem 写入口。
    /// 提交前后都复验 Snapshot / Scope / Region / Group / Registration 版本，
    /// 同步回调中的二次请求进入单槽 pending，并在外层提交结束后以非递归方式排空。
    /// </summary>
    internal sealed class UIFocusCommitter : IUIFocusCommitter
    {
        private const int MaxPendingCommitDrainCount = 8;

        private readonly UIFocusService focusService;
        private readonly AppUIFocusNodeRegistry nodeRegistry;

        private AppUIFocusCommitRequest pendingCommitRequest;
        private bool hasPendingCommitRequest;
        private AppUIFocusClearRequest pendingClearRequest;
        private bool hasPendingClearRequest;
        private int commitDepth;
        private int nextCommitSerial;
        private int activeCommitSerial;
        private GameObject expectedSelectedObject;

        private bool authorizedClearPending;
        private int authorizedClearStackRevision;

        private bool pendingRepair;
        private long pendingRepairPageInstanceId;
        private int lastRepairSelectedObjectId;
        private int lastRepairStackRevision = -1;
        private int lastRepairScopeRevision = -1;
        private int lastRepairRegistrationGeneration = -1;

        private static bool legacyCommitInProgress;
        private static bool legacyPendingWrite;
        private static GameObject legacyPendingTarget;
        private static AppUIInteractionSourceKind legacyPendingSource;

        public UIFocusCommitter(
            UIFocusService owner,
            AppUIFocusNodeRegistry registry)
        {
            focusService = owner;
            nodeRegistry = registry;
        }

        internal int ActiveCommitSerial
        {
            get { return activeCommitSerial; }
        }

        internal bool HasPendingRepair
        {
            get { return pendingRepair; }
        }

        public AppUIFocusRequestResult Commit(in AppUIFocusCommitRequest request)
        {
            if (commitDepth > 0)
            {
                pendingCommitRequest = request;
                hasPendingCommitRequest = true;
                hasPendingClearRequest = false;
                AppUIFocusRequestResult deferred = AppUIFocusRequestResult.Deferred;
                TraceCommit(in request, deferred);
                return deferred;
            }

            AppUIFocusRequestResult initialResult = CommitCore(in request);
            TraceCommit(in request, initialResult);
            DrainPendingOperations();
            return initialResult;
        }

        public AppUIFocusRequestResult ClearSelection(
            in AppUIFocusClearRequest request)
        {
            if (commitDepth > 0)
            {
                pendingClearRequest = request;
                hasPendingClearRequest = true;
                hasPendingCommitRequest = false;
                AppUIFocusRequestResult deferred = AppUIFocusRequestResult.Deferred;
                TraceClear(in request, deferred);
                return deferred;
            }

            AppUIFocusRequestResult result = ClearSelectionCore(in request);
            TraceClear(in request, result);
            DrainPendingOperations();
            return result;
        }

        private AppUIFocusRequestResult ClearSelectionCore(
            in AppUIFocusClearRequest request)
        {
            UIInteractionSnapshot snapshot = focusService.CurrentInteractionSnapshot;
            if (snapshot == null || request.StackRevision != snapshot.StackRevision)
            {
                return AppUIFocusRequestResult.StaleRevision;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return AppUIFocusRequestResult.Blocked;
            }

            commitDepth++;
            activeCommitSerial = GetNextCommitSerial();
            expectedSelectedObject = null;
            try
            {
                authorizedClearPending = true;
                authorizedClearStackRevision = request.StackRevision;
                AppUIInteractionSourceAuthority.NotifyProgrammatic();
                if (eventSystem.currentSelectedGameObject != null)
                {
                    eventSystem.SetSelectedGameObject(null);
                }

                focusService.NotifySelectionCleared(request.PreviousOwner);
                return AppUIFocusRequestResult.Cleared;
            }
            finally
            {
                expectedSelectedObject = null;
                activeCommitSerial = 0;
                commitDepth--;
            }
        }

        public AppUIFocusSelectionObservationResult ObserveSelection(
            in AppUIFocusSelectionObservation observation)
        {
            AppUIFocusSelectionObservationResult result =
                ObserveSelectionCore(in observation);
            if (focusService.TryGetActiveScope(out AppUIFocusScope activeScope) &&
                AppUIFocusTrace.CanTrace(activeScope.PageInstanceId))
            {
                AppUIFocusNodeAddress target = default;
                if (observation.SelectedObject != null)
                {
                    activeScope.TryGetNodeAddress(
                        observation.SelectedObject,
                        out target);
                }

                AppUIFocusTrace.Record(
                    activeScope.PageInstanceId,
                    AppUIFocusTraceStage.Selection,
                    activeScope.CurrentFocusedAddress,
                    target,
                    "Selection observed. Source=" +
                    observation.Source +
                    ", Result=" +
                    result +
                    ", Object=" +
                    (observation.SelectedObject != null
                        ? observation.SelectedObject.name
                        : "null"));
            }

            return result;
        }

        private AppUIFocusSelectionObservationResult ObserveSelectionCore(
            in AppUIFocusSelectionObservation observation)
        {
            if (observation.Source == AppUIFocusSelectionObservationSource.DeselectCallback)
            {
                return AppUIFocusSelectionObservationResult.DeferredUntilReconcile;
            }

            GameObject selectedObject = observation.SelectedObject;
            if (commitDepth > 0 && ReferenceEquals(selectedObject, expectedSelectedObject))
            {
                return AppUIFocusSelectionObservationResult.OwnCommitCallback;
            }

            UIInteractionSnapshot snapshot = focusService.CurrentInteractionSnapshot;
            if (authorizedClearPending &&
                selectedObject == null &&
                snapshot != null &&
                snapshot.StackRevision == authorizedClearStackRevision)
            {
                authorizedClearPending = false;
                return AppUIFocusSelectionObservationResult.IgnoredAuthorizedClear;
            }

            if (!focusService.TryGetActiveScope(out AppUIFocusScope activeScope))
            {
                focusService.ClearCanonicalFocus();
                return AppUIFocusSelectionObservationResult.IgnoredNoActiveScope;
            }

            if (selectedObject != null &&
                nodeRegistry.TryResolveNode(
                    selectedObject,
                    out AppUIFocusResolvedNode resolvedNode))
            {
                if (focusService.TryAcceptExternalSelection(
                        activeScope,
                        resolvedNode))
                {
                    ResetRepairDeduplication();
                    return AppUIFocusSelectionObservationResult.AcceptedExternal;
                }

                QueueRepair(
                    activeScope,
                    selectedObject,
                    resolvedNode.RegistrationGeneration);
                return AppUIFocusSelectionObservationResult.RejectedIneligible;
            }

            QueueRepair(activeScope, selectedObject, 0);
            return selectedObject == null
                ? AppUIFocusSelectionObservationResult.RepairQueued
                : AppUIFocusSelectionObservationResult.RejectedUnregistered;
        }

        internal void QueueRepairForScope(AppUIFocusScope scope)
        {
            if (scope == null || scope.Status == AppUIFocusScopeStatus.Disposed)
            {
                return;
            }

            if (scope.Status != AppUIFocusScopeStatus.Active)
            {
                scope.MarkPendingRepair();
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            QueueRepair(
                scope,
                eventSystem != null ? eventSystem.currentSelectedGameObject : null,
                0);
        }

        internal void DrainPendingRepair()
        {
            if (!pendingRepair || commitDepth > 0)
            {
                return;
            }

            long pageInstanceId = pendingRepairPageInstanceId;
            pendingRepair = false;
            pendingRepairPageInstanceId = 0;
            focusService.TryRepairActiveScope(pageInstanceId);
        }

        internal void ResetObservationState()
        {
            authorizedClearPending = false;
            pendingRepair = false;
            pendingRepairPageInstanceId = 0;
            hasPendingCommitRequest = false;
            pendingCommitRequest = default;
            hasPendingClearRequest = false;
            pendingClearRequest = default;
            ResetRepairDeduplication();
        }

        private void DrainPendingOperations()
        {
            int drainedCount = 0;
            while ((hasPendingCommitRequest || hasPendingClearRequest) &&
                   drainedCount < MaxPendingCommitDrainCount)
            {
                if (hasPendingClearRequest)
                {
                    AppUIFocusClearRequest nextClear = pendingClearRequest;
                    hasPendingClearRequest = false;
                    pendingClearRequest = default;
                    AppUIFocusRequestResult result =
                        ClearSelectionCore(in nextClear);
                    TraceClear(in nextClear, result);
                }
                else
                {
                    AppUIFocusCommitRequest nextCommit = pendingCommitRequest;
                    hasPendingCommitRequest = false;
                    pendingCommitRequest = default;
                    AppUIFocusRequestResult result = CommitCore(in nextCommit);
                    TraceCommit(in nextCommit, result);
                }

                drainedCount++;
            }

            if (!hasPendingCommitRequest && !hasPendingClearRequest)
            {
                return;
            }

            hasPendingCommitRequest = false;
            pendingCommitRequest = default;
            hasPendingClearRequest = false;
            pendingClearRequest = default;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "<AppUIFocus> Pending focus operation drain limit reached; the last reentrant request was dropped.");
#endif
        }

        /// <summary>
        /// 未接入 AppUI Scope 的旧页面使用该隔离入口；它不读写语义 History，
        /// 但仍保证底层 EventSystem 写入只存在于 Committer 实现中。
        /// </summary>
        internal static AppUIFocusRequestResult CommitLegacySelection(
            Selectable selectable,
            AppUIInteractionSourceKind sourceKind)
        {
            if (!IsSelectableUsable(selectable) || EventSystem.current == null)
            {
                return AppUIFocusRequestResult.NodeUnusable;
            }

            return CommitLegacySelection(
                selectable.gameObject,
                sourceKind);
        }

        internal static AppUIFocusRequestResult ClearLegacySelection()
        {
            return CommitLegacySelection(
                (GameObject)null,
                AppUIInteractionSourceKind.Programmatic);
        }

        private AppUIFocusRequestResult CommitCore(
            in AppUIFocusCommitRequest request)
        {
            if (!focusService.TryValidateCommitRequest(
                    in request,
                    out AppUIFocusScope scope,
                    out AppUIFocusResolvedNode resolvedNode,
                    out AppUIFocusRequestResult failure))
            {
                return failure;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return AppUIFocusRequestResult.Blocked;
            }

            commitDepth++;
            activeCommitSerial = GetNextCommitSerial();
            expectedSelectedObject = resolvedNode.SelectedObject;
            AppUIFocusScope validatedScope = scope;
            try
            {
                NotifyInteractionSource(request.Reason);
                if (!ReferenceEquals(
                        eventSystem.currentSelectedGameObject,
                        resolvedNode.SelectedObject))
                {
                    eventSystem.SetSelectedGameObject(resolvedNode.SelectedObject);
                }

                if (!focusService.TryValidateCommitRequest(
                        in request,
                        out scope,
                        out resolvedNode,
                        out failure))
                {
                    QueueRepairForScope(scope ?? validatedScope);
                    return failure;
                }

                scope.AcceptCommittedFocus(
                    in request,
                    resolvedNode,
                    ResolveHistoryWriteMode(request.Reason, request.HistoryWriteMode));
                scope.EnsureVisible(resolvedNode);
                ResetRepairDeduplication();
                return AppUIFocusRequestResult.Focused;
            }
            finally
            {
                expectedSelectedObject = null;
                activeCommitSerial = 0;
                commitDepth--;
            }
        }

        private static void TraceCommit(
            in AppUIFocusCommitRequest request,
            AppUIFocusRequestResult result)
        {
            long pageInstanceId = request.PageHandle.InstanceId;
            if (!AppUIFocusTrace.CanTrace(pageInstanceId))
            {
                return;
            }

            AppUIFocusTrace.Record(
                pageInstanceId,
                AppUIFocusTraceStage.Commit,
                default,
                request.NodeAddress,
                "Commit. Reason=" +
                request.Reason +
                ", Result=" +
                result +
                ", StackRev=" +
                request.StackRevision +
                ", ScopeRev=" +
                request.ScopeRevision +
                ", RegionRev=" +
                request.RegionRevision +
                ", GroupRev=" +
                request.GroupRevision +
                ", Registration=" +
                request.RegistrationGeneration);
        }

        private static void TraceClear(
            in AppUIFocusClearRequest request,
            AppUIFocusRequestResult result)
        {
            long pageInstanceId = request.PreviousOwner.InstanceId;
            if (!AppUIFocusTrace.CanTrace(pageInstanceId))
            {
                return;
            }

            AppUIFocusTrace.Record(
                pageInstanceId,
                AppUIFocusTraceStage.Commit,
                default,
                default,
                "Clear selection. Reason=" +
                request.Reason +
                ", Result=" +
                result);
        }

        private void QueueRepair(
            AppUIFocusScope activeScope,
            GameObject selectedObject,
            int registrationGeneration)
        {
            if (activeScope == null || activeScope.Status != AppUIFocusScopeStatus.Active)
            {
                return;
            }

            UIInteractionSnapshot snapshot = focusService.CurrentInteractionSnapshot;
            int selectedObjectId = selectedObject != null
                ? selectedObject.GetInstanceID()
                : 0;
            int stackRevision = snapshot != null ? snapshot.StackRevision : 0;
            if (selectedObjectId == lastRepairSelectedObjectId &&
                stackRevision == lastRepairStackRevision &&
                activeScope.SelectionRepairRevision == lastRepairScopeRevision &&
                registrationGeneration == lastRepairRegistrationGeneration)
            {
                return;
            }

            lastRepairSelectedObjectId = selectedObjectId;
            lastRepairStackRevision = stackRevision;
            lastRepairScopeRevision = activeScope.SelectionRepairRevision;
            lastRepairRegistrationGeneration = registrationGeneration;
            pendingRepair = true;
            pendingRepairPageInstanceId = activeScope.PageInstanceId;
        }

        private void ResetRepairDeduplication()
        {
            lastRepairSelectedObjectId = 0;
            lastRepairStackRevision = -1;
            lastRepairScopeRevision = -1;
            lastRepairRegistrationGeneration = -1;
        }

        private static AppUIFocusHistoryWriteMode ResolveHistoryWriteMode(
            AppUIFocusChangeReason reason,
            AppUIFocusHistoryWriteMode requestedMode)
        {
            if (requestedMode != AppUIFocusHistoryWriteMode.UseReasonDefault)
            {
                return requestedMode;
            }

            switch (reason)
            {
                case AppUIFocusChangeReason.FirstOpened:
                    return AppUIFocusHistoryWriteMode.InitializeIfEmpty;
                case AppUIFocusChangeReason.Navigation:
                case AppUIFocusChangeReason.PointerClick:
                case AppUIFocusChangeReason.ExternalSelection:
                    return AppUIFocusHistoryWriteMode.Full;
                case AppUIFocusChangeReason.Reopened:
                case AppUIFocusChangeReason.SelectionRepair:
                    return AppUIFocusHistoryWriteMode.ReplaceInvalidOnly;
                case AppUIFocusChangeReason.PointerHover:
                case AppUIFocusChangeReason.RestoreRequested:
                case AppUIFocusChangeReason.CancelPreview:
                case AppUIFocusChangeReason.Programmatic:
                default:
                    return AppUIFocusHistoryWriteMode.None;
            }
        }

        private int GetNextCommitSerial()
        {
            if (nextCommitSerial == int.MaxValue)
            {
                nextCommitSerial = 0;
            }

            nextCommitSerial++;
            return nextCommitSerial;
        }

        private static void NotifyInteractionSource(AppUIFocusChangeReason reason)
        {
            if (reason == AppUIFocusChangeReason.Navigation)
            {
                AppUIInteractionSourceAuthority.NotifyNavigation();
                return;
            }

            AppUIInteractionSourceAuthority.NotifyProgrammatic();
        }

        private static AppUIFocusRequestResult CommitLegacySelection(
            GameObject target,
            AppUIInteractionSourceKind sourceKind)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return AppUIFocusRequestResult.Blocked;
            }

            if (legacyCommitInProgress)
            {
                legacyPendingTarget = target;
                legacyPendingSource = sourceKind;
                legacyPendingWrite = true;
                return AppUIFocusRequestResult.Deferred;
            }

            legacyCommitInProgress = true;
            try
            {
                GameObject nextTarget = target;
                AppUIInteractionSourceKind nextSource = sourceKind;
                int drainCount = 0;
                do
                {
                    legacyPendingWrite = false;
                    if (nextSource == AppUIInteractionSourceKind.Navigation)
                    {
                        AppUIInteractionSourceAuthority.NotifyNavigation();
                    }
                    else
                    {
                        AppUIInteractionSourceAuthority.NotifyProgrammatic();
                    }

                    if (!ReferenceEquals(
                            eventSystem.currentSelectedGameObject,
                            nextTarget))
                    {
                        eventSystem.SetSelectedGameObject(nextTarget);
                    }

                    if (!legacyPendingWrite)
                    {
                        break;
                    }

                    nextTarget = legacyPendingTarget;
                    nextSource = legacyPendingSource;
                    drainCount++;
                }
                while (drainCount < MaxPendingCommitDrainCount);

                legacyPendingWrite = false;
                legacyPendingTarget = null;
                return target == null
                    ? AppUIFocusRequestResult.Cleared
                    : AppUIFocusRequestResult.Focused;
            }
            finally
            {
                legacyCommitInProgress = false;
            }
        }

        private static bool IsSelectableUsable(Selectable selectable)
        {
            return selectable != null &&
                   selectable.gameObject != null &&
                   selectable.gameObject.activeInHierarchy &&
                   selectable.IsActive() &&
                   selectable.IsInteractable();
        }
    }
}
