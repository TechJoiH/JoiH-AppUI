using System;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Technology-neutral expanded-control contract that owns child Focus
    /// Region lifecycle and Region-stage Cancel behavior.
    /// </summary>
    public abstract class AppUIFocusDropdownControlPolicyBase :
        IAppUIFocusControlPolicy,
        IAppUIFocusRegionCancelHandler
    {
        protected AppUIFocusDropdownControlPolicyBase(
            string childRegionId)
        {
            ChildRegionId = ValidateRegionId(childRegionId);
        }

        public string ChildRegionId { get; }

        protected abstract bool IsControlExpanded { get; }

        protected abstract void CollapseControl();

        public AppUIFocusRequestResult SynchronizeRegion(
            IAppUIFocusScopeHandle scope)
        {
            if (scope == null)
            {
                return AppUIFocusRequestResult.ScopeInactive;
            }

            AppUIFocusRegionStatus regionStatus =
                scope.GetRegionStatus(ChildRegionId);
            if (IsControlExpanded)
            {
                return regionStatus == AppUIFocusRegionStatus.Active
                    ? AppUIFocusRequestResult.Consumed
                    : scope.OpenRegion(
                        ChildRegionId,
                        AppUIFocusRegionEntryPolicy
                            .LastFocusedOrDefault);
            }

            return regionStatus == AppUIFocusRegionStatus.Closed
                ? AppUIFocusRequestResult.Consumed
                : scope.CloseRegion(ChildRegionId);
        }

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

        AppUIFocusCancelHandlingResult
            IAppUIFocusRegionCancelHandler.TryHandleCancel(
                in AppUIFocusRegionCancelContext context)
        {
            if (!string.Equals(
                    context.RegionId,
                    ChildRegionId,
                    StringComparison.Ordinal))
            {
                return AppUIFocusCancelHandlingResult.Continue;
            }

            CollapseControl();
            return AppUIFocusCancelHandlingResult.Consumed;
        }

        private static string ValidateRegionId(string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId) ||
                string.Equals(
                    regionId,
                    AppUIFocusDefinition.RootRegionId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Dropdown requires a non-root ChildRegion id.",
                    nameof(regionId));
            }

            return regionId;
        }
    }
}
