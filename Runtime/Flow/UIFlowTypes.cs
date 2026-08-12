namespace Joi.H.AppUI
{
    public enum UIFlowActionKind
    {
        None,
        Stay,
        OpenPage,
        ReplacePage,
        OpenDialog,
        CloseCurrent,
        CloseCurrentAndRefreshTarget
    }

    public enum UIFlowPageKind
    {
        FullScreen,
        Dialog,
        Overlay
    }

    public enum UIFlowCancelRule
    {
        Disabled,
        CloseOnCancel,
        FocusThenSubmit,
        SubmitBackCommand,
        CustomHandler
    }

    public interface IUIFlowCommandResult
    {
        bool Success { get; }
        string ErrorCode { get; }
        string Message { get; }
        string NextPageId { get; }
        UIFlowActionKind FlowAction { get; }
        object FlowPayload { get; }
    }

    public interface IUILocalizationService
    {
        IUIOperation<UIUnit> EnsureReady();
        string Localize(string key);
        bool TryLocalize(string key, out string value);
        string Format(string key, object arg0);
    }

    public interface IUIFlowOpenData
    {
        UIFlowContextBase FlowContext { get; }
    }

    public interface IUIFlowCoordinator
    {
        IUIOperation<UIFlowApplyResult> Apply(
            string currentPageId,
            UIFlowContextBase context,
            IUIFlowCommandResult result);
    }

    public struct UIFlowApplyResult
    {
        public bool Success { get; private set; }
        public bool Applied { get; private set; }
        public string ErrorCode { get; private set; }
        public string Message { get; private set; }
        public string CurrentPageId { get; private set; }
        public string TargetPageId { get; private set; }
        public UIFlowActionKind FlowAction { get; private set; }

        public static UIFlowApplyResult AppliedOk(
            UIFlowActionKind flowAction,
            string currentPageId,
            string targetPageId,
            string message = null)
        {
            return new UIFlowApplyResult
            {
                Success = true,
                Applied = true,
                FlowAction = flowAction,
                CurrentPageId = currentPageId ?? string.Empty,
                TargetPageId = targetPageId ?? string.Empty,
                Message = message ?? string.Empty,
                ErrorCode = string.Empty
            };
        }

        public static UIFlowApplyResult Noop(
            UIFlowActionKind flowAction,
            string currentPageId,
            string targetPageId,
            string message = null)
        {
            return new UIFlowApplyResult
            {
                Success = true,
                Applied = false,
                FlowAction = flowAction,
                CurrentPageId = currentPageId ?? string.Empty,
                TargetPageId = targetPageId ?? string.Empty,
                Message = message ?? string.Empty,
                ErrorCode = string.Empty
            };
        }

        public static UIFlowApplyResult Failed(
            string errorCode,
            string message,
            UIFlowActionKind flowAction,
            string currentPageId,
            string targetPageId)
        {
            return new UIFlowApplyResult
            {
                Success = false,
                Applied = false,
                FlowAction = flowAction,
                CurrentPageId = currentPageId ?? string.Empty,
                TargetPageId = targetPageId ?? string.Empty,
                Message = message ?? string.Empty,
                ErrorCode = errorCode ?? string.Empty
            };
        }
    }
}
