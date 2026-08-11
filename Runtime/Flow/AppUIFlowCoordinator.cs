using Cysharp.Threading.Tasks;

namespace Joi.H.AppUI
{
    public sealed class AppUIFlowCoordinator : IUIFlowCoordinator
    {
        public async UniTask<UIFlowApplyResult> ApplyAsync(
            string currentPageId,
            UIFlowContextBase context,
            IUIFlowCommandResult result)
        {
            if (result == null)
            {
                return UIFlowApplyResult.Failed(
                    "MissingFlowResult",
                    "UI flow command result is missing.",
                    UIFlowActionKind.None,
                    currentPageId,
                    string.Empty);
            }

            UIFlowActionKind action = ResolveAction(result);
            string targetPageId = result.NextPageId ?? string.Empty;
            if (!result.Success)
            {
                return UIFlowApplyResult.Noop(
                    action,
                    currentPageId,
                    targetPageId,
                    result.Message);
            }

            if (action == UIFlowActionKind.None ||
                action == UIFlowActionKind.Stay)
            {
                return UIFlowApplyResult.Noop(
                    action,
                    currentPageId,
                    targetPageId,
                    result.Message);
            }

            if (context == null || context.UI == null)
            {
                return UIFlowApplyResult.Failed(
                    "MissingUIFlowContext",
                    "UI flow context or UI service is missing.",
                    action,
                    currentPageId,
                    targetPageId);
            }

            switch (action)
            {
                case UIFlowActionKind.OpenPage:
                case UIFlowActionKind.OpenDialog:
                    return await OpenTargetAsync(
                        currentPageId,
                        context,
                        result,
                        action,
                        false);

                case UIFlowActionKind.ReplacePage:
                    return await OpenTargetAsync(
                        currentPageId,
                        context,
                        result,
                        action,
                        true);

                case UIFlowActionKind.CloseCurrent:
                    return await CloseCurrentAsync(
                        currentPageId,
                        context,
                        result,
                        action);

                case UIFlowActionKind.CloseCurrentAndRefreshTarget:
                    return await CloseCurrentAndRefreshTargetAsync(
                        currentPageId,
                        context,
                        result,
                        action);
            }

            return UIFlowApplyResult.Failed(
                "UnsupportedFlowAction",
                "Unsupported UI flow action: " + action,
                action,
                currentPageId,
                targetPageId);
        }

        private static UIFlowActionKind ResolveAction(IUIFlowCommandResult result)
        {
            if (result == null)
            {
                return UIFlowActionKind.None;
            }

            if (result.FlowAction != UIFlowActionKind.None)
            {
                return result.FlowAction;
            }

            return string.IsNullOrEmpty(result.NextPageId)
                ? UIFlowActionKind.Stay
                : UIFlowActionKind.OpenPage;
        }

        private static async UniTask<UIFlowApplyResult> OpenTargetAsync(
            string currentPageId,
            UIFlowContextBase context,
            IUIFlowCommandResult result,
            UIFlowActionKind action,
            bool closeCurrentAfterOpen)
        {
            string targetPageId = result.NextPageId ?? string.Empty;
            if (string.IsNullOrEmpty(targetPageId))
            {
                return UIFlowApplyResult.Failed(
                    "MissingTargetPageId",
                    "UI flow target page id is missing.",
                    action,
                    currentPageId,
                    targetPageId);
            }

            if (!TryResolveOpenArgs(
                    context,
                    result,
                    targetPageId,
                    out UIOpenArgs openArgs,
                    out string errorCode,
                    out string errorMessage))
            {
                return UIFlowApplyResult.Failed(
                    errorCode,
                    errorMessage,
                    action,
                    currentPageId,
                    targetPageId);
            }

            UIOpenResult openResult = await context.UI.OpenAsync(targetPageId, openArgs);
            if (openResult == null || !openResult.Success)
            {
                return UIFlowApplyResult.Failed(
                    "OpenTargetFailed",
                    "Open target page failed: " +
                    (openResult == null ? "NullResult" : openResult.Error.ToString()),
                    action,
                    currentPageId,
                    targetPageId);
            }

            if (closeCurrentAfterOpen && !string.IsNullOrEmpty(currentPageId) &&
                !string.Equals(currentPageId, targetPageId, System.StringComparison.Ordinal))
            {
                UICloseRequest closeRequest = UICloseRequest.Default;
                closeRequest.ReleaseOnClose =
                    context.ReleaseCurrentPageOnReplace;
                if (!string.IsNullOrEmpty(context.SceneScopeId))
                {
                    closeRequest = closeRequest.WithSceneScopeId(
                        context.SceneScopeId);
                }

                UICloseResult closeResult = await context.UI.CloseAsync(
                    currentPageId,
                    closeRequest);
                if (closeResult == null || !closeResult.Success)
                {
                    return UIFlowApplyResult.Failed(
                        "CloseCurrentFailed",
                        "Close current page failed: " +
                        (closeResult == null ? "NullResult" : closeResult.Error.ToString()),
                        action,
                        currentPageId,
                        targetPageId);
                }
            }

            return UIFlowApplyResult.AppliedOk(
                action,
                currentPageId,
                targetPageId,
                result.Message);
        }

        private static async UniTask<UIFlowApplyResult> CloseCurrentAsync(
            string currentPageId,
            UIFlowContextBase context,
            IUIFlowCommandResult result,
            UIFlowActionKind action)
        {
            if (string.IsNullOrEmpty(currentPageId))
            {
                return UIFlowApplyResult.Failed(
                    "MissingCurrentPageId",
                    "Current page id is missing.",
                    action,
                    currentPageId,
                    result.NextPageId);
            }

            UICloseResult closeResult = await context.UI.CloseAsync(currentPageId);
            if (closeResult == null || !closeResult.Success)
            {
                return UIFlowApplyResult.Failed(
                    "CloseCurrentFailed",
                    "Close current page failed: " +
                    (closeResult == null ? "NullResult" : closeResult.Error.ToString()),
                    action,
                    currentPageId,
                    result.NextPageId);
            }

            return UIFlowApplyResult.AppliedOk(
                action,
                currentPageId,
                result.NextPageId,
                result.Message);
        }

        private static async UniTask<UIFlowApplyResult> CloseCurrentAndRefreshTargetAsync(
            string currentPageId,
            UIFlowContextBase context,
            IUIFlowCommandResult result,
            UIFlowActionKind action)
        {
            UIFlowApplyResult closeResult = await CloseCurrentAsync(
                currentPageId,
                context,
                result,
                action);
            if (!closeResult.Success)
            {
                return closeResult;
            }

            string targetPageId = result.NextPageId ?? string.Empty;
            if (string.IsNullOrEmpty(targetPageId))
            {
                return closeResult;
            }

            object data = result.FlowPayload;
            UIRefreshArgs refreshArgs = new UIRefreshArgs(data);
            if (!string.IsNullOrEmpty(context.SceneScopeId))
            {
                refreshArgs = refreshArgs.WithSceneScopeId(context.SceneScopeId);
            }

            UIRefreshResult refreshResult = await context.UI.RefreshAsync(
                targetPageId,
                refreshArgs);
            if (refreshResult == null || !refreshResult.Success)
            {
                return UIFlowApplyResult.Failed(
                    "RefreshTargetFailed",
                    "Refresh target page failed: " +
                    (refreshResult == null ? "NullResult" : refreshResult.Error.ToString()),
                    action,
                    currentPageId,
                    targetPageId);
            }

            return UIFlowApplyResult.AppliedOk(
                action,
                currentPageId,
                targetPageId,
                result.Message);
        }

        private static bool TryResolveOpenArgs(
            UIFlowContextBase context,
            IUIFlowCommandResult result,
            string targetPageId,
            out UIOpenArgs args,
            out string errorCode,
            out string errorMessage)
        {
            args = UIOpenArgs.None;
            errorCode = string.Empty;
            errorMessage = string.Empty;

            UIPageFlowContract contract = null;
            if (context.Contracts != null &&
                context.Contracts.TryGet(targetPageId, out contract) &&
                contract != null)
            {
                if (!contract.IsImplemented)
                {
                    errorCode = "TargetPageNotImplemented";
                    errorMessage = "Target page is not implemented: " + targetPageId;
                    return false;
                }

                if (contract.TryCreateOpenData(context, result, out object openData))
                {
                    args = UIOpenArgs.FromExplicit(openData);
                }
            }
            else if (result.FlowPayload != null)
            {
                args = UIOpenArgs.FromExplicit(result.FlowPayload);
            }

            if (!string.IsNullOrEmpty(context.SceneScopeId))
            {
                args = args.WithSceneScopeId(context.SceneScopeId);
            }

            return true;
        }
    }
}
