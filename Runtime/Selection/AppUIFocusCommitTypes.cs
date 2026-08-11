using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>焦点变化的业务语义；History 策略只由该原因和框架内部覆盖共同决定。</summary>
    public enum AppUIFocusChangeReason
    {
        FirstOpened,
        Reopened,
        Navigation,
        PointerClick,
        PointerHover,
        ExternalSelection,
        RestoreRequested,
        SelectionRepair,
        CancelPreview,
        Programmatic,
    }

    internal enum AppUIFocusHistoryWriteMode
    {
        UseReasonDefault,
        None,
        InitializeIfEmpty,
        NodeOnly,
        Full,
        ReplaceInvalidOnly,
    }

    public enum AppUIFocusRequestResult
    {
        Focused,
        Cleared,
        Consumed,
        Blocked,
        Deferred,
        DeferredWhileSuspended,
        StaleRevision,
        ScopeInactive,
        RegionClosed,
        GroupClosed,
        NodeMissing,
        NodeUnusable,
        ReverseLookupFailed,
        PendingRealization,
    }

    internal enum AppUIFocusClearReason
    {
        PageSuspended,
        PageHidden,
        NoInteractivePage,
        FrameworkExplicitClear,
    }

    internal enum AppUIFocusSelectionObservationSource
    {
        SelectCallback,
        DeselectCallback,
        LateUpdate,
    }

    internal enum AppUIFocusSelectionObservationResult
    {
        OwnCommitCallback,
        AcceptedExternal,
        IgnoredNoActiveScope,
        IgnoredAuthorizedClear,
        DeferredUntilReconcile,
        RejectedUnregistered,
        RejectedIneligible,
        RepairQueued,
        RepairDeferredWhileSuspended,
    }

    /// <summary>
    /// Resolve 阶段生成的不可变焦点请求。NodeAddress 是身份，Target 只用于提交前后的引用复验。
    /// </summary>
    internal readonly struct AppUIFocusCommitRequest
    {
        public AppUIFocusCommitRequest(
            UIPageInteractionHandle pageHandle,
            string regionId,
            AppUIFocusNodeAddress nodeAddress,
            Selectable target,
            int stackRevision,
            int scopeRevision,
            int regionRevision,
            int groupRevision,
            int registrationGeneration,
            AppUIFocusChangeReason reason,
            AppUIFocusHistoryWriteMode historyWriteMode = AppUIFocusHistoryWriteMode.UseReasonDefault)
        {
            PageHandle = pageHandle;
            RegionId = regionId ?? string.Empty;
            NodeAddress = nodeAddress;
            Target = target;
            StackRevision = stackRevision;
            ScopeRevision = scopeRevision;
            RegionRevision = regionRevision;
            GroupRevision = groupRevision;
            RegistrationGeneration = registrationGeneration;
            Reason = reason;
            HistoryWriteMode = historyWriteMode;
        }

        public UIPageInteractionHandle PageHandle { get; }

        public string RegionId { get; }

        public AppUIFocusNodeAddress NodeAddress { get; }

        public Selectable Target { get; }

        public int StackRevision { get; }

        public int ScopeRevision { get; }

        public int RegionRevision { get; }

        public int GroupRevision { get; }

        public int RegistrationGeneration { get; }

        public AppUIFocusChangeReason Reason { get; }

        public AppUIFocusHistoryWriteMode HistoryWriteMode { get; }

        public bool IsValid
        {
            get
            {
                return PageHandle.IsValid &&
                       !string.IsNullOrEmpty(RegionId) &&
                       NodeAddress.IsValid &&
                       Target != null &&
                       StackRevision > 0 &&
                       ScopeRevision > 0 &&
                       RegionRevision > 0 &&
                       GroupRevision > 0 &&
                       RegistrationGeneration > 0;
            }
        }
    }

    internal readonly struct AppUIFocusClearRequest
    {
        public AppUIFocusClearRequest(
            UIPageInteractionHandle previousOwner,
            int stackRevision,
            AppUIFocusClearReason reason)
        {
            PreviousOwner = previousOwner;
            StackRevision = stackRevision;
            Reason = reason;
        }

        public UIPageInteractionHandle PreviousOwner { get; }

        public int StackRevision { get; }

        public AppUIFocusClearReason Reason { get; }
    }

    internal readonly struct AppUIFocusSelectionObservation
    {
        public AppUIFocusSelectionObservation(
            GameObject selectedObject,
            AppUIFocusSelectionObservationSource source)
        {
            SelectedObject = selectedObject;
            Source = source;
        }

        public GameObject SelectedObject { get; }

        public AppUIFocusSelectionObservationSource Source { get; }
    }

    internal interface IUIFocusCommitter
    {
        AppUIFocusRequestResult Commit(in AppUIFocusCommitRequest request);

        AppUIFocusRequestResult ClearSelection(in AppUIFocusClearRequest request);

        AppUIFocusSelectionObservationResult ObserveSelection(
            in AppUIFocusSelectionObservation observation);
    }

    internal interface IAppUIFocusCommitGateway
    {
        AppUIFocusRequestResult CommitFocus(
            Selectable selectable,
            AppUIFocusChangeReason reason);

        AppUIFocusRequestResult CommitFocus(
            AppUIFocusNodeAddress nodeAddress,
            AppUIFocusChangeReason reason);
    }

    internal interface IAppUIFocusSelectionObservationSink
    {
        void NotifySelected(GameObject selectedObject);

        void NotifyDeselected(GameObject deselectedObject);
    }
}
