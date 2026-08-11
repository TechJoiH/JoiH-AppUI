using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public enum AppUIFocusTargetKind
    {
        None,
        NodeAddress,
        Selectable,
    }

    /// <summary>
    /// 页面扩展只返回语义目标，不直接提交焦点。新页面优先返回 NodeAddress；
    /// Selectable 只用于旧页面迁移，并仍须经过 Scope Registry 反向解析。
    /// </summary>
    public readonly struct AppUIFocusTarget
    {
        private AppUIFocusTarget(
            AppUIFocusTargetKind kind,
            AppUIFocusNodeAddress nodeAddress,
            Selectable selectable)
        {
            Kind = kind;
            NodeAddress = nodeAddress;
            Selectable = selectable;
        }

        public AppUIFocusTargetKind Kind { get; }

        public AppUIFocusNodeAddress NodeAddress { get; }

        public Selectable Selectable { get; }

        public bool IsValid
        {
            get
            {
                return (Kind == AppUIFocusTargetKind.NodeAddress && NodeAddress.IsValid) ||
                       (Kind == AppUIFocusTargetKind.Selectable && Selectable != null);
            }
        }

        public static AppUIFocusTarget FromNodeAddress(
            AppUIFocusNodeAddress nodeAddress)
        {
            return nodeAddress.IsValid
                ? new AppUIFocusTarget(
                    AppUIFocusTargetKind.NodeAddress,
                    nodeAddress,
                    null)
                : default;
        }

        public static AppUIFocusTarget FromSelectable(Selectable selectable)
        {
            return selectable != null
                ? new AppUIFocusTarget(
                    AppUIFocusTargetKind.Selectable,
                    default,
                    selectable)
                : default;
        }
    }

    public interface IAppUIDefaultFocusTargetProvider
    {
        bool TryGetDefaultFocus(
            UIDefaultFocusReason reason,
            out AppUIFocusTarget target);
    }

    public enum AppUIFocusReopenPolicy
    {
        RestoreHistory = 0,
        DefaultFocus = 1,
    }

    /// <summary>
    /// Hidden 页面实例重新显示时的焦点策略。默认恢复历史；只有页面语义要求
    /// 每次进入都重新解析默认焦点时才实现该接口并返回 DefaultFocus。
    /// </summary>
    public interface IAppUIFocusReopenPolicyProvider
    {
        AppUIFocusReopenPolicy FocusReopenPolicy { get; }
    }

    public interface IAppUIFocusAnchorTargetProvider
    {
        bool TryGetFocusAnchor(
            string anchorId,
            out AppUIFocusTarget target);
    }
}
