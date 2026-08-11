using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public enum AppUIFocusControlMoveMode
    {
        FrameworkOnly = 0,
        DelegateToNativeControl = 1,
    }

    public enum AppUIFocusCancelHandlingResult
    {
        Continue = 0,
        Consumed = 1,
    }

    /// <summary>当前焦点控件处理 Cancel 时使用的只读上下文。</summary>
    public readonly struct AppUIFocusCancelContext
    {
        internal AppUIFocusCancelContext(
            string groupId,
            AppUIFocusNodeAddress nodeAddress,
            Selectable currentSelectable)
        {
            GroupId = groupId ?? string.Empty;
            NodeAddress = nodeAddress;
            CurrentSelectable = currentSelectable;
        }

        public string GroupId { get; }

        public AppUIFocusNodeAddress NodeAddress { get; }

        public Selectable CurrentSelectable { get; }
    }

    /// <summary>
    /// 声明一个焦点节点的原生控件语义。实现只返回决策，不得自行提交 EventSystem 焦点。
    /// </summary>
    public interface IAppUIFocusControlPolicy
    {
        AppUIFocusControlMoveMode GetMoveMode(
            in AppUIFocusMoveContext context);

        AppUIFocusCancelHandlingResult TryHandleCancel(
            in AppUIFocusCancelContext context);
    }

    /// <summary>
    /// 显式声明该自定义策略已验证可把 Move 委托给原生控件，且不会使用 Unity Navigation 改写焦点。
    /// </summary>
    public interface IAppUIFocusNativeMoveAdapter : IAppUIFocusControlPolicy
    {
    }

    /// <summary>框架内建控件白名单；实例在节点注册时解析并缓存。</summary>
    internal static class AppUIFocusControlPolicies
    {
        private sealed class FrameworkOnlyPolicy : IAppUIFocusControlPolicy
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

        private sealed class SliderPolicy : IAppUIFocusNativeMoveAdapter
        {
            public AppUIFocusControlMoveMode GetMoveMode(
                in AppUIFocusMoveContext context)
            {
                Slider slider = context.CurrentSelectable as Slider;
                return slider != null && IsSameAxis(slider.direction, context.MoveDirection)
                    ? AppUIFocusControlMoveMode.DelegateToNativeControl
                    : AppUIFocusControlMoveMode.FrameworkOnly;
            }

            public AppUIFocusCancelHandlingResult TryHandleCancel(
                in AppUIFocusCancelContext context)
            {
                return AppUIFocusCancelHandlingResult.Continue;
            }
        }

        private sealed class InputFieldPolicy : IAppUIFocusControlPolicy
        {
            public AppUIFocusControlMoveMode GetMoveMode(
                in AppUIFocusMoveContext context)
            {
                // 编辑态输入由 InputSystemUIInputModule 的 updateSelected 路径提前消费；
                // Browse 态进入这里时仍作为普通框架 Node。
                return AppUIFocusControlMoveMode.FrameworkOnly;
            }

            public AppUIFocusCancelHandlingResult TryHandleCancel(
                in AppUIFocusCancelContext context)
            {
                if (context.CurrentSelectable is TMP_InputField tmpInputField &&
                    tmpInputField.isFocused)
                {
                    tmpInputField.DeactivateInputField();
                    return AppUIFocusCancelHandlingResult.Consumed;
                }

                if (context.CurrentSelectable is InputField inputField &&
                    inputField.isFocused)
                {
                    inputField.DeactivateInputField();
                    return AppUIFocusCancelHandlingResult.Consumed;
                }

                return AppUIFocusCancelHandlingResult.Continue;
            }
        }

        private sealed class ScrollbarPolicy : IAppUIFocusNativeMoveAdapter
        {
            public AppUIFocusControlMoveMode GetMoveMode(
                in AppUIFocusMoveContext context)
            {
                Scrollbar scrollbar = context.CurrentSelectable as Scrollbar;
                return scrollbar != null && IsSameAxis(scrollbar.direction, context.MoveDirection)
                    ? AppUIFocusControlMoveMode.DelegateToNativeControl
                    : AppUIFocusControlMoveMode.FrameworkOnly;
            }

            public AppUIFocusCancelHandlingResult TryHandleCancel(
                in AppUIFocusCancelContext context)
            {
                return AppUIFocusCancelHandlingResult.Continue;
            }
        }

        private static readonly IAppUIFocusControlPolicy FrameworkOnly =
            new FrameworkOnlyPolicy();
        private static readonly IAppUIFocusControlPolicy SliderPolicyInstance =
            new SliderPolicy();
        private static readonly IAppUIFocusControlPolicy ScrollbarPolicyInstance =
            new ScrollbarPolicy();
        private static readonly IAppUIFocusControlPolicy InputFieldPolicyInstance =
            new InputFieldPolicy();

        internal static IAppUIFocusControlPolicy Resolve(
            Selectable selectable,
            IAppUIFocusControlPolicy explicitPolicy)
        {
            if (explicitPolicy != null)
            {
                return explicitPolicy;
            }

            if (selectable is Slider)
            {
                return SliderPolicyInstance;
            }

            if (selectable is Scrollbar)
            {
                return ScrollbarPolicyInstance;
            }

            if (selectable is TMP_InputField || selectable is InputField)
            {
                return InputFieldPolicyInstance;
            }

            return FrameworkOnly;
        }

        private static bool IsSameAxis(
            Slider.Direction direction,
            MoveDirection moveDirection)
        {
            bool horizontal = direction == Slider.Direction.LeftToRight ||
                              direction == Slider.Direction.RightToLeft;
            return IsSameAxis(horizontal, moveDirection);
        }

        private static bool IsSameAxis(
            Scrollbar.Direction direction,
            MoveDirection moveDirection)
        {
            bool horizontal = direction == Scrollbar.Direction.LeftToRight ||
                              direction == Scrollbar.Direction.RightToLeft;
            return IsSameAxis(horizontal, moveDirection);
        }

        private static bool IsSameAxis(
            bool horizontal,
            MoveDirection moveDirection)
        {
            return horizontal
                ? moveDirection == MoveDirection.Left || moveDirection == MoveDirection.Right
                : moveDirection == MoveDirection.Up || moveDirection == MoveDirection.Down;
        }
    }
}
