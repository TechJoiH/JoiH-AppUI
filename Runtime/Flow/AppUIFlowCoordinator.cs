using System;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Applies multi-step UI flow commands through neutral operations.
    /// </summary>
    public sealed class AppUIFlowCoordinator : IUIFlowCoordinator
    {
        private readonly IUIOperationFactory operationFactory;
        private readonly IAppUIExecutionContext executionContext;

        public AppUIFlowCoordinator(
            IUIOperationFactory factory,
            IAppUIExecutionContext context)
        {
            operationFactory = factory ??
                throw new ArgumentNullException(nameof(factory));
            executionContext = context ??
                throw new ArgumentNullException(nameof(context));
        }

        public IUIOperation<UIFlowApplyResult> Apply(
            string currentPageId,
            UIFlowContextBase context,
            IUIFlowCommandResult result)
        {
            IUIOperationSource<UIFlowApplyResult> source =
                operationFactory.Create<UIFlowApplyResult>(
                    AppUIOperationDescriptor.Create("ApplyUIFlow"));
            if (source == null || source.Operation == null)
            {
                throw new InvalidOperationException(
                    "IUIOperationFactory returned a null source or operation.");
            }

            source.TrySetRunning();
            FlowRun run = new FlowRun(
                currentPageId,
                context,
                result,
                source);
            BeginApply(run);
            return source.Operation;
        }

        private void BeginApply(FlowRun run)
        {
            if (run.Result == null)
            {
                Complete(run, UIFlowApplyResult.Failed(
                    "MissingFlowResult",
                    "UI flow command result is missing.",
                    UIFlowActionKind.None,
                    run.CurrentPageId,
                    string.Empty));
                return;
            }

            run.Action = ResolveAction(run.Result);
            run.TargetPageId = run.Result.NextPageId ?? string.Empty;
            if (!run.Result.Success ||
                run.Action == UIFlowActionKind.None ||
                run.Action == UIFlowActionKind.Stay)
            {
                Complete(run, UIFlowApplyResult.Noop(
                    run.Action,
                    run.CurrentPageId,
                    run.TargetPageId,
                    run.Result.Message));
                return;
            }

            if (run.Context == null || run.Context.UI == null)
            {
                Complete(run, UIFlowApplyResult.Failed(
                    "MissingUIFlowContext",
                    "UI flow context or UI service is missing.",
                    run.Action,
                    run.CurrentPageId,
                    run.TargetPageId));
                return;
            }

            switch (run.Action)
            {
                case UIFlowActionKind.OpenPage:
                case UIFlowActionKind.OpenDialog:
                case UIFlowActionKind.ReplacePage:
                    BeginOpen(run);
                    break;
                case UIFlowActionKind.CloseCurrent:
                case UIFlowActionKind.CloseCurrentAndRefreshTarget:
                    BeginClose(run);
                    break;
                default:
                    Complete(run, UIFlowApplyResult.Failed(
                        "UnsupportedFlowAction",
                        "Unsupported UI flow action: " + run.Action,
                        run.Action,
                        run.CurrentPageId,
                        run.TargetPageId));
                    break;
            }
        }

        private void BeginOpen(FlowRun run)
        {
            if (string.IsNullOrEmpty(run.TargetPageId))
            {
                FailDomain(
                    run,
                    "MissingTargetPageId",
                    "UI flow target page id is missing.");
                return;
            }

            if (!TryResolveOpenArgs(
                    run.Context,
                    run.Result,
                    run.TargetPageId,
                    out UIOpenArgs args,
                    out string code,
                    out string message))
            {
                FailDomain(run, code, message);
                return;
            }

            Observe(
                run.Context.UI.Open(run.TargetPageId, args),
                run,
                completion =>
                {
                    UIOpenResult result = completion.Result;
                    if (result == null || !result.Success)
                    {
                        FailDomain(
                            run,
                            "OpenTargetFailed",
                            "Open target page failed: " +
                            (result == null
                                ? "NullResult"
                                : result.Error.ToString()));
                        return;
                    }

                    if (run.Action != UIFlowActionKind.ReplacePage ||
                        string.IsNullOrEmpty(run.CurrentPageId) ||
                        string.Equals(
                            run.CurrentPageId,
                            run.TargetPageId,
                            StringComparison.Ordinal))
                    {
                        CompleteApplied(run);
                        return;
                    }

                    UICloseRequest request = UICloseRequest.Default;
                    request.ReleaseOnClose =
                        run.Context.ReleaseCurrentPageOnReplace;
                    if (!string.IsNullOrEmpty(run.Context.SceneScopeId))
                    {
                        request = request.WithSceneScopeId(
                            run.Context.SceneScopeId);
                    }

                    Observe(
                        run.Context.UI.Close(
                            run.CurrentPageId,
                            request),
                        run,
                        closeCompletion => CompleteAfterClose(
                            run,
                            closeCompletion.Result,
                            false));
                });
        }

        private void BeginClose(FlowRun run)
        {
            if (string.IsNullOrEmpty(run.CurrentPageId))
            {
                FailDomain(
                    run,
                    "MissingCurrentPageId",
                    "Current page id is missing.");
                return;
            }

            Observe(
                run.Context.UI.Close(run.CurrentPageId),
                run,
                completion => CompleteAfterClose(
                    run,
                    completion.Result,
                    run.Action ==
                    UIFlowActionKind.CloseCurrentAndRefreshTarget));
        }

        private void CompleteAfterClose(
            FlowRun run,
            UICloseResult closeResult,
            bool refreshTarget)
        {
            if (closeResult == null || !closeResult.Success)
            {
                FailDomain(
                    run,
                    "CloseCurrentFailed",
                    "Close current page failed: " +
                    (closeResult == null
                        ? "NullResult"
                        : closeResult.Error.ToString()));
                return;
            }

            if (!refreshTarget || string.IsNullOrEmpty(run.TargetPageId))
            {
                CompleteApplied(run);
                return;
            }

            UIRefreshArgs args = new UIRefreshArgs(run.Result.FlowPayload);
            if (!string.IsNullOrEmpty(run.Context.SceneScopeId))
            {
                args = args.WithSceneScopeId(run.Context.SceneScopeId);
            }

            Observe(
                run.Context.UI.Refresh(run.TargetPageId, args),
                run,
                completion =>
                {
                    UIRefreshResult result = completion.Result;
                    if (result == null || !result.Success)
                    {
                        FailDomain(
                            run,
                            "RefreshTargetFailed",
                            "Refresh target page failed: " +
                            (result == null
                                ? "NullResult"
                                : result.Error.ToString()));
                        return;
                    }

                    CompleteApplied(run);
                });
        }

        private void Observe<TResult>(
            IUIOperation<TResult> operation,
            FlowRun run,
            Action<AppUIOperationCompletion<TResult>> onSucceeded)
        {
            if (operation == null)
            {
                run.Source.TrySetFailed(new InvalidOperationException(
                    "UI flow service returned a null operation."));
                return;
            }

            UIOperationObserver.Observe(
                operation,
                executionContext,
                completion =>
                {
                    switch (completion.Status)
                    {
                        case AppUIOperationStatus.Succeeded:
                            onSucceeded.Invoke(completion);
                            break;
                        case AppUIOperationStatus.Cancelled:
                            run.Source.TrySetCancelled();
                            break;
                        case AppUIOperationStatus.Expired:
                            run.Source.TrySetExpired();
                            break;
                        case AppUIOperationStatus.Failed:
                            run.Source.TrySetFailed(
                                completion.Exception ??
                                new InvalidOperationException(
                                    "Failed UI flow operation has no " +
                                    "exception."));
                            break;
                    }
                });
        }

        private static void CompleteApplied(FlowRun run)
        {
            Complete(run, UIFlowApplyResult.AppliedOk(
                run.Action,
                run.CurrentPageId,
                run.TargetPageId,
                run.Result.Message));
        }

        private static void FailDomain(
            FlowRun run,
            string code,
            string message)
        {
            Complete(run, UIFlowApplyResult.Failed(
                code,
                message,
                run.Action,
                run.CurrentPageId,
                run.TargetPageId));
        }

        private static void Complete(
            FlowRun run,
            UIFlowApplyResult result)
        {
            run.Source.TrySetSucceeded(result);
        }

        private static UIFlowActionKind ResolveAction(
            IUIFlowCommandResult result)
        {
            if (result.FlowAction != UIFlowActionKind.None)
            {
                return result.FlowAction;
            }

            return string.IsNullOrEmpty(result.NextPageId)
                ? UIFlowActionKind.Stay
                : UIFlowActionKind.OpenPage;
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
            if (context.Contracts != null &&
                context.Contracts.TryGet(
                    targetPageId,
                    out UIPageFlowContract contract) &&
                contract != null)
            {
                if (!contract.IsImplemented)
                {
                    errorCode = "TargetPageNotImplemented";
                    errorMessage = "Target page is not implemented: " +
                                   targetPageId;
                    return false;
                }

                if (contract.TryCreateOpenData(
                    context,
                    result,
                    out object openData))
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

        private sealed class FlowRun
        {
            public FlowRun(
                string currentPageId,
                UIFlowContextBase context,
                IUIFlowCommandResult result,
                IUIOperationSource<UIFlowApplyResult> source)
            {
                CurrentPageId = currentPageId ?? string.Empty;
                Context = context;
                Result = result;
                Source = source;
            }

            public readonly string CurrentPageId;
            public readonly UIFlowContextBase Context;
            public readonly IUIFlowCommandResult Result;
            public readonly IUIOperationSource<UIFlowApplyResult> Source;
            public string TargetPageId;
            public UIFlowActionKind Action;
        }
    }
}
