using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>Region 只表达当前唯一开放栈上的结构状态。</summary>
    public enum AppUIFocusRegionStatus
    {
        Closed = 0,
        Active = 1,
        Suspended = 2,
    }

    /// <summary>打开 Region 时选择默认入口或优先恢复该 Region 的历史。</summary>
    public enum AppUIFocusRegionEntryPolicy
    {
        Default = 0,
        LastFocusedOrDefault = 1,
    }

    /// <summary>活动子 Region 处理统一 Cancel 时使用的只读上下文。</summary>
    public readonly struct AppUIFocusRegionCancelContext
    {
        internal AppUIFocusRegionCancelContext(
            string scopeId,
            string regionId,
            AppUIFocusNodeAddress sourceNodeAddress)
        {
            ScopeId = scopeId ?? string.Empty;
            RegionId = regionId ?? string.Empty;
            SourceNodeAddress = sourceNodeAddress;
        }

        public string ScopeId { get; }

        public string RegionId { get; }

        public AppUIFocusNodeAddress SourceNodeAddress { get; }
    }

    /// <summary>
    /// 活动子 Region 的局部 Cancel 处理器。实现可收起局部控件或弹层，
    /// 但不得直接提交焦点；返回 Consumed 后由 Scope 关闭 Region 并恢复 SourceNode。
    /// </summary>
    public interface IAppUIFocusRegionCancelHandler
    {
        AppUIFocusCancelHandlingResult TryHandleCancel(
            in AppUIFocusRegionCancelContext context);
    }

    internal readonly struct AppUIFocusRegionDefinition
    {
        public AppUIFocusRegionDefinition(
            string regionId,
            string parentRegionId,
            string defaultGroupId,
            IAppUIFocusRegionCancelHandler cancelHandler,
            bool autoAdjacent = false)
        {
            RegionId = regionId ?? string.Empty;
            ParentRegionId = parentRegionId ?? string.Empty;
            DefaultGroupId = defaultGroupId ?? string.Empty;
            CancelHandler = cancelHandler;
            AutoAdjacent = autoAdjacent;
        }

        public string RegionId { get; }

        public string ParentRegionId { get; }

        public string DefaultGroupId { get; }

        public IAppUIFocusRegionCancelHandler CancelHandler { get; }

        public bool AutoAdjacent { get; }

        public AppUIFocusRegionDefinition WithDefaultGroup(string defaultGroupId)
        {
            return new AppUIFocusRegionDefinition(
                RegionId,
                ParentRegionId,
                defaultGroupId,
                CancelHandler,
                AutoAdjacent);
        }

        public AppUIFocusRegionDefinition WithCancelHandler(
            IAppUIFocusRegionCancelHandler cancelHandler)
        {
            return new AppUIFocusRegionDefinition(
                RegionId,
                ParentRegionId,
                DefaultGroupId,
                cancelHandler,
                AutoAdjacent);
        }

        public AppUIFocusRegionDefinition WithAutoAdjacent(bool autoAdjacent)
        {
            return new AppUIFocusRegionDefinition(
                RegionId,
                ParentRegionId,
                DefaultGroupId,
                CancelHandler,
                autoAdjacent);
        }
    }

    internal readonly struct AppUIFocusRegionAdjacencyDefinition
    {
        public AppUIFocusRegionAdjacencyDefinition(
            string regionId,
            string sourceGroupId,
            MoveDirection moveDirection,
            string targetGroupId)
        {
            RegionId = regionId ?? string.Empty;
            SourceGroupId = sourceGroupId ?? string.Empty;
            MoveDirection = moveDirection;
            TargetGroupId = targetGroupId ?? string.Empty;
        }

        public string RegionId { get; }

        public string SourceGroupId { get; }

        public MoveDirection MoveDirection { get; }

        public string TargetGroupId { get; }
    }

    /// <summary>
    /// Navigator 只请求 Region 决策；Scope 仍负责状态转换、版本递增和最终提交。
    /// </summary>
    internal interface IAppUIFocusRegionNavigationGateway
    {
        bool TryGetGroupRegionId(string groupId, out string regionId);

        bool TryGetNodeAddress(Selectable selectable, out AppUIFocusNodeAddress nodeAddress);

        bool TryGetRegionLastFocusedAddress(
            string regionId,
            out AppUIFocusNodeAddress nodeAddress);

        bool TryRouteRegionBoundary(
            string sourceGroupId,
            Selectable sourceSelectable,
            MoveDirection moveDirection);

        bool FocusRegion(
            string regionId,
            AppUIFocusRegionEntryPolicy entryPolicy,
            string sourceGroupId,
            Selectable sourceSelectable,
            MoveDirection moveDirection);

        bool ExitToParentRegion(string sourceGroupId);
    }
}
