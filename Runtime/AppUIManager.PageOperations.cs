using System;
using UnityEngine;

namespace Joi.H.AppUI
{
    public sealed partial class AppUIManager
    {
        public IUIOperation<UIOpenResult> Open(string pageId)
        {
            return Open(pageId, UIOpenArgs.None);
        }

        public bool IsOpen(string pageId)
        {
            return instanceRegistry.TryGet(
                       pageId,
                       out UIPageInstance instance) &&
                   instance != null &&
                   instance.State == UIPageState.Open;
        }

        public bool IsOpening(string pageId)
        {
            EnsureRuntimeServices();
            return operationCoordinator.IsOpenOperationActive(pageId);
        }

        public bool TryGetPageState(
            string pageId,
            out UIPageState state)
        {
            if (instanceRegistry.TryGet(
                    pageId,
                    out UIPageInstance instance) &&
                instance != null)
            {
                state = instance.State;
                return true;
            }

            state = UIPageState.None;
            return false;
        }

        private UIPageState GetKnownPageState(string pageId)
        {
            return instanceRegistry.TryGet(
                       pageId,
                       out UIPageInstance instance) &&
                   instance != null
                ? instance.State
                : UIPageState.None;
        }

        private UIOperationCheckResult CheckOperation(
            IUIPageOperation operation,
            UIPageInstance instance,
            bool requireVersion)
        {
            UIOperationCheckResult result =
                operationCoordinator.CheckOperation(
                    operation,
                    instance,
                    requireVersion);
            if (result != UIOperationCheckResult.Valid)
            {
                return result;
            }

            UISceneScopeStamp requestedStamp =
                operation is UIOpenOperation openOperation
                    ? openOperation.Args.SceneScopeStamp
                    : operation is UICloseOperation closeOperation
                        ? ResolveCloseSceneScopeStamp(
                            closeOperation.Request)
                        : UISceneScopeStamp.Unstamped(
                            operation.SceneScopeId);
            return instance != null &&
                   !sceneScopeCoordinator.IsSceneScopeCompatible(
                       requestedStamp,
                       instance)
                ? UIOperationCheckResult.SceneScopeInvalid
                : UIOperationCheckResult.Valid;
        }

        private static UISceneScopeStamp ResolveCloseSceneScopeStamp(
            UICloseRequest request)
        {
            string sceneScopeId =
                UISceneScopeCoordinator.NormalizeSceneScopeId(
                    request.SceneScopeId);
            return string.Equals(
                request.SceneScopeStamp.SceneScopeId,
                sceneScopeId,
                StringComparison.Ordinal)
                ? request.SceneScopeStamp
                : UISceneScopeStamp.Unstamped(sceneScopeId);
        }

        public IUIOperation<UIOpenResult> Open(string pageId, object data)
        {
            return Open(
                pageId,
                data == null
                    ? UIOpenArgs.None
                    : UIOpenArgs.FromExplicit(data));
        }

        public IUIOperation<UIOpenResult> Open(
            string pageId,
            UIOpenArgs args)
        {
            RequireInitialized();
            IUIOperationSource<UIOpenResult> source =
                CreateOperationSource<UIOpenResult>(
                    "Open:" + (pageId ?? string.Empty),
                    args.CancellationToken);
            BeginOpen(pageId, args, source, runtimeEpoch);
            return source.Operation;
        }

        public IUIOperation<UICloseResult> Close(string pageId)
        {
            return Close(pageId, UICloseRequest.Default);
        }

        public IUIOperation<UICloseResult> Close(
            string pageId,
            UICloseRequest request)
        {
            RequireInitialized();
            IUIOperationSource<UICloseResult> source =
                CreateOperationSource<UICloseResult>(
                    "Close:" + (pageId ?? string.Empty),
                    request.CancellationToken);
            BeginClose(pageId, request, source, runtimeEpoch);
            return source.Operation;
        }

        public IUIOperation<UIRefreshResult> Refresh(
            string pageId,
            object data)
        {
            return Refresh(pageId, new UIRefreshArgs(data));
        }

        public IUIOperation<UIRefreshResult> Refresh(
            string pageId,
            UIRefreshArgs args)
        {
            RequireInitialized();
            IUIOperationSource<UIRefreshResult> source =
                CreateOperationSource<UIRefreshResult>(
                    "Refresh:" + (pageId ?? string.Empty),
                    args.CancellationToken);
            ExecuteRefresh(pageId, args, source);
            return source.Operation;
        }

        private IUIOperation<UICancelResult> BeginCancel()
        {
            IUIOperationSource<UICancelResult> source =
                CreateOperationSource<UICancelResult>(
                    "Cancel",
                    default);
            EnsureRuntimeServices();
            UIPageInstance instance =
                presentationCoordinator.ResolveCancelTarget();
            if (instance == null)
            {
                source.TrySetSucceeded(UICancelResult.NoTarget());
                return source.Operation;
            }

            string pageId = instance.PageId;
            AppUIFocusCancelDispatchResult focusResult =
                focusService.TryHandleCancel(
                    instance,
                    out Exception focusException);
            if (focusResult == AppUIFocusCancelDispatchResult.Failed)
            {
                source.TrySetSucceeded(
                    UICancelResult.HandlerFailed(pageId, focusException));
                return source.Operation;
            }

            if (focusResult == AppUIFocusCancelDispatchResult.Consumed)
            {
                source.TrySetSucceeded(UICancelResult.Handled(pageId));
                return source.Operation;
            }

            try
            {
                if (instance.Controller is IUICancelHandler cancelHandler &&
                    cancelHandler.HandleCancel())
                {
                    source.TrySetSucceeded(
                        UICancelResult.Handled(pageId));
                    return source.Operation;
                }

                if (instance.Controller is
                        IAppUIFocusCancelPolicyProvider policyProvider)
                {
                    AppUIFocusCancelPolicy policy =
                        policyProvider.GetFocusCancelPolicy();
                    if (policy != null && policy.HandleCancel())
                    {
                        source.TrySetSucceeded(
                            UICancelResult.Handled(pageId));
                        return source.Operation;
                    }
                }
            }
            catch (Exception exception)
            {
                source.TrySetSucceeded(
                    UICancelResult.HandlerFailed(pageId, exception));
                return source.Operation;
            }

            if (instance.Definition == null ||
                !instance.Definition.CloseOnCancel)
            {
                source.TrySetSucceeded(
                    UICancelResult.CloseDisabled(pageId));
                return source.Operation;
            }

            IUIOperation<UICloseResult> closeOperation = Close(pageId);
            UIOperationObserver.Observe(
                closeOperation,
                executionContext,
                completion => CompleteCancelAfterClose(
                    pageId,
                    source,
                    completion));
            return source.Operation;
        }

        private static void CompleteCancelAfterClose(
            string pageId,
            IUIOperationSource<UICancelResult> source,
            AppUIOperationCompletion<UICloseResult> completion)
        {
            switch (completion.Status)
            {
                case AppUIOperationStatus.Succeeded:
                    UICloseResult closeResult = completion.Result;
                    source.TrySetSucceeded(closeResult.Success
                        ? UICancelResult.Closed(pageId, closeResult)
                        : closeResult.Error == UICloseError.Rejected
                            ? UICancelResult.CloseRejected(
                                pageId,
                                closeResult)
                            : UICancelResult.CloseFailed(
                                pageId,
                                closeResult));
                    break;
                case AppUIOperationStatus.Cancelled:
                    source.TrySetCancelled();
                    break;
                case AppUIOperationStatus.Expired:
                    source.TrySetExpired();
                    break;
                case AppUIOperationStatus.Failed:
                    source.TrySetFailed(
                        completion.Exception ??
                        new InvalidOperationException(
                            "Failed close operation has no exception."));
                    break;
            }
        }

        private IUIOperation<TResult> CreateCompletedOperation<TResult>(
            string name,
            TResult result)
        {
            IUIOperationSource<TResult> source =
                CreateOperationSource<TResult>(name, default);
            source.TrySetSucceeded(result);
            return source.Operation;
        }

        private void BeginOpen(
            string pageId,
            UIOpenArgs args,
            IUIOperationSource<UIOpenResult> source,
            int epoch)
        {
            EnsureRuntimeServices();
            if (string.IsNullOrEmpty(pageId) || pageRegistry == null ||
                !pageRegistry.TryGet(pageId, out UIPageDefinition definition))
            {
                source.TrySetSucceeded(
                    UIOpenResult.Fail(UIPageOpenError.DefinitionNotFound));
                return;
            }

            if (!ValidateDefinition(definition))
            {
                source.TrySetSucceeded(
                    UIOpenResult.Fail(UIPageOpenError.InvalidDefinition));
                return;
            }

            // Each page keeps at most one pending intent. The coordinator
            // expires a superseded Source before storing the replacement.
            if (operationCoordinator.IsPageBusy(pageId))
            {
                if (definition.OpenPolicy == UIOpenPolicy.QueueIfBusy &&
                    operationCoordinator.TryEnqueueOpenPending(
                        pageId,
                        args,
                        source))
                {
                    return;
                }

                source.TrySetSucceeded(
                    UIOpenResult.Fail(UIPageOpenError.AlreadyOpenRejected));
                return;
            }

            UIOpenOperation operation =
                operationCoordinator.CreateOpenOperation(pageId, args);
            operation.CancellationToken = source.Operation.CancellationToken;
            operation.Source = source;
            if (!operationCoordinator.TryRegisterOperation(operation))
            {
                source.TrySetSucceeded(
                    UIOpenResult.Fail(UIPageOpenError.AlreadyOpenRejected));
                return;
            }

            operation.MarkRunning();
            OpenContext context = new OpenContext(
                operation,
                definition,
                source,
                epoch);
            if (instanceRegistry.TryGet(pageId, out UIPageInstance existing))
            {
                BeginOpenExisting(context, existing);
                return;
            }

            BeginOpenNew(context);
        }

        private void BeginOpenNew(OpenContext context)
        {
            UIOperationCheckResult check = CheckOpenContext(context, null, false);
            if (check != UIOperationCheckResult.Valid)
            {
                CompleteOpenCheckFailure(context, check);
                return;
            }

            if (!layerController.TryGetRoot(
                    context.Definition.LayerId,
                    out UILayerRoot layerRoot) ||
                layerRoot.ContentRoot == null)
            {
                CompleteOpenDomain(
                    context,
                    UIOpenResult.Fail(UIPageOpenError.LayerNotFound));
                return;
            }

            context.LayerRoot = layerRoot;
            try
            {
                IUILoadStrategy loadStrategy = ResolveLoadStrategy(
                    context.Definition.LoadStrategyId);
                IUIOperation<UILoadResult> loadOperation = loadStrategy.Load(
                    context.Definition,
                    assetProvider,
                    operationFactory,
                    context.Source.Operation.CancellationToken);
                if (loadOperation == null)
                {
                    throw new InvalidOperationException(
                        "IUILoadStrategy.Load returned null.");
                }

                UIOperationObserver.Observe(
                    loadOperation,
                    executionContext,
                    completion => ContinueOpenAfterLoad(context, completion));
            }
            catch (Exception exception)
            {
                FailOpen(context, exception);
            }
        }

        private void ContinueOpenAfterLoad(
            OpenContext context,
            AppUIOperationCompletion<UILoadResult> completion)
        {
            if (completion.Status != AppUIOperationStatus.Succeeded)
            {
                CompleteOpenExternalFailure(context, completion);
                return;
            }

            context.LoadResult = completion.Result;
            UIOperationCheckResult check = CheckOpenContext(context, null, false);
            if (check != UIOperationCheckResult.Valid)
            {
                CompleteOpenCheckFailure(context, check);
                return;
            }

            if (!context.LoadResult.Success ||
                context.LoadResult.Prefab == null)
            {
                ReleaseAssetLeaseSafe(context.LoadResult.AssetLease);
                context.LoadResult = default;
                CompleteOpenDomain(
                    context,
                    UIOpenResult.Fail(UIPageOpenError.ResourceLoadFailed));
                return;
            }

            try
            {
                if (CreateOpenInstance(context))
                {
                    BeginOpenShow(context);
                }
            }
            catch (Exception exception)
            {
                FailOpen(context, exception);
            }
        }

        private bool CreateOpenInstance(OpenContext context)
        {
            GameObject pageObject = Instantiate(
                context.LoadResult.Prefab,
                context.LayerRoot.ContentRoot,
                false);
            pageObject.name = context.LoadResult.Prefab.name;
            pageObject.SetActive(false);

            PanelBaseController[] controllers =
                pageObject.GetComponents<PanelBaseController>();
            if (controllers == null || controllers.Length == 0)
            {
                pageInstanceReleaser.DestroyLoadedObject(
                    pageObject,
                    context.LoadResult.AssetLease);
                context.LoadResult = default;
                CompleteOpenDomain(
                    context,
                    UIOpenResult.Fail(UIPageOpenError.ControllerMissing));
                return false;
            }

            if (controllers.Length > 1)
            {
                pageInstanceReleaser.DestroyLoadedObject(
                    pageObject,
                    context.LoadResult.AssetLease);
                context.LoadResult = default;
                CompleteOpenDomain(
                    context,
                    UIOpenResult.Fail(UIPageOpenError.ControllerInvalid));
                return false;
            }

            PanelBaseController controller = controllers[0];
            UIPageInstance instance = new UIPageInstance
            {
                PageId = context.Operation.PageId,
                Definition = context.Definition,
                LayerId = context.Definition.LayerId,
                SceneScopeId = sceneScopeCoordinator.ResolveInstanceSceneScopeId(
                    context.Definition,
                    context.Operation.SceneScopeId),
                SceneScopeStamp =
                    sceneScopeCoordinator.ResolveInstanceSceneScopeStamp(
                        context.Definition,
                        context.Operation.Args.SceneScopeStamp),
                OperationVersion = context.Operation.Version.Value,
                GameObject = pageObject,
                RectTransform = pageObject.transform as RectTransform,
                Controller = controller,
                AssetLease = context.LoadResult.AssetLease,
                State = UIPageState.Initializing,
            };
            context.LoadResult = default;
            context.Instance = instance;
            instanceRegistry.Register(instance);

            UIPanelContext panelContext = new UIPanelContext(
                this,
                noticeService,
                context.Operation.PageId,
                context.Definition);
            controller.SetContext(panelContext);
            controller.OnCreate(panelContext);
            controller.OnInit();
            AttachFocusScope(instance, controller, pageObject, panelContext);
            ApplyDataAndRefresh(
                controller,
                context.Operation.Args.HasData,
                context.Operation.Args.Data);
            return true;
        }

        private void AttachFocusScope(
            UIPageInstance instance,
            PanelBaseController controller,
            GameObject pageObject,
            UIPanelContext panelContext)
        {
            IAppUIFocusDefinitionProvider provider =
                controller as IAppUIFocusDefinitionProvider ??
                pageObject.GetComponent<AppUIFocusAuthoring>();
            if (provider == null)
            {
                return;
            }

            AppUIFocusDefinition focusDefinition =
                provider.BuildFocusDefinition();
            if (focusDefinition == null)
            {
                throw new InvalidOperationException(
                    "IAppUIFocusDefinitionProvider returned null. Page=" +
                    instance.PageId);
            }

            panelContext.SetFocusScope(
                focusService.AttachScope(instance, focusDefinition));
        }

        private void BeginOpenExisting(
            OpenContext context,
            UIPageInstance instance)
        {
            context.Instance = instance;
            if (instance == null)
            {
                CompleteOpenExpired(context);
                return;
            }

            UIOperationCheckResult check =
                CheckOpenContext(context, instance, false);
            if (check != UIOperationCheckResult.Valid)
            {
                CompleteOpenCheckFailure(context, check);
                return;
            }

            if (instance.State == UIPageState.Open)
            {
                if (context.Definition.OpenPolicy ==
                    UIOpenPolicy.RejectIfOpeningOrOpen)
                {
                    CompleteOpenDomain(
                        context,
                        UIOpenResult.Fail(
                            UIPageOpenError.AlreadyOpenRejected));
                    return;
                }

                try
                {
                    instance.OperationVersion = context.Operation.Version.Value;
                    if (context.Definition.OpenPolicy ==
                        UIOpenPolicy.RefreshExisting)
                    {
                        ApplyDataAndRefresh(
                            instance.Controller,
                            context.Operation.Args.HasData,
                            context.Operation.Args.Data);
                    }

                    presentationCoordinator.PushOpened(
                        instance,
                        AppUIFocusChangeReason.RestoreRequested);
                    context.Operation.MarkCompleted();
                    CompleteOpenDomain(
                        context,
                        UIOpenResult.Ok(instance.ToHandle()));
                }
                catch (Exception exception)
                {
                    FailOpen(context, exception);
                }

                return;
            }

            if (instance.State != UIPageState.Hidden ||
                instance.Controller == null)
            {
                CompleteOpenDomain(
                    context,
                    UIOpenResult.Fail(UIPageOpenError.AlreadyOpenRejected));
                return;
            }

            context.IsReopen = true;
            instance.OperationVersion = context.Operation.Version.Value;
            object data = context.Operation.Args.HasData
                ? context.Operation.Args.Data
                : instance.HasPendingRefreshData
                    ? instance.PendingRefreshData
                    : null;
            bool hasData = context.Operation.Args.HasData ||
                           instance.HasPendingRefreshData;
            instance.PendingRefreshData = null;
            instance.HasPendingRefreshData = false;

            try
            {
                ApplyDataAndRefresh(instance.Controller, hasData, data);
                BeginOpenShow(context);
            }
            catch (Exception exception)
            {
                RestoreHiddenInstance(instance);
                FailOpen(context, exception);
            }
        }

        private void BeginOpenShow(OpenContext context)
        {
            UIOperationCheckResult check = CheckOpenContext(
                context,
                context.Instance,
                true);
            if (check != UIOperationCheckResult.Valid)
            {
                CompleteOpenCheckFailure(context, check);
                return;
            }

            UITransition transition = context.Instance.Controller.BeginShow();
            if (transition.IsImmediate)
            {
                context.Instance.Controller.CompleteShow();
                CommitOpenAfterShow(context);
                return;
            }

            UIOperationObserver.Observe(
                transition.Operation,
                executionContext,
                completion => ContinueOpenAfterShow(context, completion));
        }

        private void ContinueOpenAfterShow(
            OpenContext context,
            AppUIOperationCompletion<UITransitionResult> completion)
        {
            if (completion.Status != AppUIOperationStatus.Succeeded)
            {
                CompleteOpenExternalFailure(context, completion);
                return;
            }

            if (!completion.Result.Success)
            {
                CleanupOpenInstance(context);
                CompleteOpenDomain(
                    context,
                    UIOpenResult.Fail(UIPageOpenError.LifecycleFailed));
                return;
            }

            try
            {
                context.Instance.Controller.CompleteShow();
                CommitOpenAfterShow(context);
            }
            catch (Exception exception)
            {
                FailOpen(context, exception);
            }
        }

        private void CommitOpenAfterShow(OpenContext context)
        {
            UIOperationCheckResult check = CheckOpenContext(
                context,
                context.Instance,
                true);
            if (check != UIOperationCheckResult.Valid)
            {
                CompleteOpenCheckFailure(context, check);
                return;
            }

            context.Instance.State = UIPageState.Open;
            presentationCoordinator.PushOpened(
                context.Instance,
                context.IsReopen
                    ? ResolveReopenFocusReason(context.Instance.Controller)
                    : AppUIFocusChangeReason.FirstOpened);
            context.Operation.MarkCompleted();
            CompleteOpenDomain(
                context,
                UIOpenResult.Ok(context.Instance.ToHandle()));
        }

        private void ExecuteRefresh(
            string pageId,
            UIRefreshArgs args,
            IUIOperationSource<UIRefreshResult> source)
        {
            EnsureRuntimeServices();
            if (operationCoordinator.IsPageBusy(pageId))
            {
                if (operationCoordinator.TryEnqueueRefreshPending(
                    pageId,
                    args,
                    source))
                {
                    return;
                }

                source.TrySetSucceeded(UIRefreshResult.Fail(
                    pageId,
                    GetKnownPageState(pageId),
                    UIRefreshError.Busy));
                return;
            }

            if (!instanceRegistry.TryGet(
                    pageId,
                    out UIPageInstance instance) ||
                instance == null)
            {
                source.TrySetSucceeded(UIRefreshResult.Fail(
                    pageId,
                    UIPageState.None,
                    UIRefreshError.NotOpen));
                return;
            }

            UIRefreshOperation operation =
                operationCoordinator.CreateRefreshOperation(pageId, args);
            operation.CancellationToken = source.Operation.CancellationToken;
            operation.Source = source;
            if (!operationCoordinator.TryRegisterOperation(operation))
            {
                source.TrySetSucceeded(UIRefreshResult.Fail(
                    pageId,
                    instance.State,
                    UIRefreshError.Busy));
                return;
            }

            try
            {
                operation.MarkRunning();
                UIOperationCheckResult check =
                    CheckOperation(operation, instance, false);
                if (check != UIOperationCheckResult.Valid)
                {
                    CompleteRefreshCheckFailure(
                        operation,
                        source,
                        instance,
                        check);
                    return;
                }

                instance.OperationVersion = operation.Version.Value;
                if (instance.State == UIPageState.Hidden ||
                    instance.State == UIPageState.Loading ||
                    instance.State == UIPageState.Initializing)
                {
                    instance.PendingRefreshData = args.Data;
                    instance.HasPendingRefreshData = true;
                    operation.MarkCompleted();
                    source.TrySetSucceeded(
                        UIRefreshResult.Ok(pageId, instance.State));
                    return;
                }

                if (instance.State != UIPageState.Open ||
                    instance.Controller == null)
                {
                    operation.MarkFailed();
                    source.TrySetSucceeded(UIRefreshResult.Fail(
                        pageId,
                        instance.State,
                        UIRefreshError.NotOpen));
                    return;
                }

                ApplyDataAndRefresh(instance.Controller, true, args.Data);
                operation.MarkCompleted();
                source.TrySetSucceeded(
                    UIRefreshResult.Ok(pageId, instance.State));
            }
            catch (Exception exception)
            {
                operation.MarkFailed();
                source.TrySetFailed(exception);
            }
            finally
            {
                operationCoordinator.UnregisterOperation(operation);
                TriggerPendingIntentDrain(operation.PageId);
            }
        }

        private void BeginClose(
            string pageId,
            UICloseRequest request,
            IUIOperationSource<UICloseResult> source,
            int epoch)
        {
            EnsureRuntimeServices();
            if (operationCoordinator.IsPageBusy(pageId))
            {
                if (operationCoordinator.TryEnqueueClosePending(
                    pageId,
                    request,
                    source))
                {
                    return;
                }

                source.TrySetSucceeded(UICloseResult.Fail(
                    pageId,
                    GetKnownPageState(pageId),
                    UICloseError.Busy));
                return;
            }

            if (!instanceRegistry.TryGet(
                    pageId,
                    out UIPageInstance instance) ||
                instance == null)
            {
                source.TrySetSucceeded(UICloseResult.Fail(
                    pageId,
                    UIPageState.None,
                    UICloseError.NotOpen));
                return;
            }

            UICloseOperation operation =
                operationCoordinator.CreateCloseOperation(pageId, request);
            operation.CancellationToken = source.Operation.CancellationToken;
            operation.Source = source;
            if (!operationCoordinator.TryRegisterOperation(operation))
            {
                source.TrySetSucceeded(UICloseResult.Fail(
                    pageId,
                    instance.State,
                    UICloseError.Busy));
                return;
            }

            operation.MarkRunning();
            CloseContext context = new CloseContext(
                operation,
                instance,
                request,
                source,
                epoch);
            try
            {
                UIOperationCheckResult check =
                    CheckCloseContext(context, false);
                if (check != UIOperationCheckResult.Valid)
                {
                    CompleteCloseCheckFailure(context, check);
                    return;
                }

                if (instance.Controller != null &&
                    !instance.Controller.CanClose(ref request))
                {
                    CompleteCloseDomain(context, UICloseResult.Fail(
                        pageId,
                        instance.State,
                        UICloseError.Rejected));
                    return;
                }

                context.Request = request;
                context.Operation.Request = request;
                instance.OperationVersion = operation.Version.Value;
                presentationCoordinator.RemoveFromStack(instance);
                presentationCoordinator.ClearFocusIfOwned(instance);
                presentationCoordinator.Commit();

                if (instance.State != UIPageState.Open ||
                    instance.Controller == null)
                {
                    CommitCloseAfterHide(context);
                    return;
                }

                UITransition transition = instance.Controller.BeginHide();
                if (transition.IsImmediate)
                {
                    instance.Controller.CompleteHide();
                    CommitCloseAfterHide(context);
                    return;
                }

                UIOperationObserver.Observe(
                    transition.Operation,
                    executionContext,
                    completion => ContinueCloseAfterHide(
                        context,
                        completion));
            }
            catch (Exception exception)
            {
                FailClose(context, exception);
            }
        }

        private void ContinueCloseAfterHide(
            CloseContext context,
            AppUIOperationCompletion<UITransitionResult> completion)
        {
            if (completion.Status != AppUIOperationStatus.Succeeded)
            {
                CompleteCloseExternalFailure(context, completion);
                return;
            }

            if (!completion.Result.Success)
            {
                ForceCloseCleanup(context);
                CompleteCloseDomain(context, UICloseResult.Fail(
                    context.Operation.PageId,
                    context.Instance.State,
                    UICloseError.LifecycleFailed));
                return;
            }

            try
            {
                context.Instance.Controller.CompleteHide();
                CommitCloseAfterHide(context);
            }
            catch (Exception exception)
            {
                FailClose(context, exception);
            }
        }

        private void CommitCloseAfterHide(CloseContext context)
        {
            UIOperationCheckResult check =
                CheckCloseContext(context, true);
            if (check != UIOperationCheckResult.Valid)
            {
                ForceCloseCleanup(context);
                CompleteCloseCheckFailure(context, check);
                return;
            }

            if (!context.Request.ReleaseOnClose)
            {
                context.Instance.State = UIPageState.Hidden;
                context.Instance.StackVisible = false;
                presentationCoordinator.SetInstanceActive(
                    context.Instance,
                    false);
                presentationCoordinator.Commit();
                context.Operation.MarkCompleted();
                CompleteCloseDomain(context, UICloseResult.Ok(
                    context.Operation.PageId,
                    UIPageState.Hidden));
                return;
            }

            ReleaseInstance(context.Instance, UIReleaseReason.CloseRelease);
            context.Operation.MarkCompleted();
            CompleteCloseDomain(context, UICloseResult.Ok(
                context.Operation.PageId,
                UIPageState.Released));
        }

        private IUIOperationSource<TResult> CreateOperationSource<TResult>(
            string name,
            System.Threading.CancellationToken cancellationToken)
        {
            IUIOperationSource<TResult> source =
                operationFactory.Create<TResult>(
                    AppUIOperationDescriptor.Create(name, cancellationToken));
            if (source == null || source.Operation == null)
            {
                throw new InvalidOperationException(
                    "IUIOperationFactory returned a null source or operation.");
            }

            if (!source.TrySetRunning())
            {
                throw new InvalidOperationException(
                    "IUIOperationSource rejected the Running transition.");
            }

            return source;
        }

        private void TriggerPendingIntentDrain(string pageId)
        {
            if (string.IsNullOrEmpty(pageId) ||
                operationCoordinator.IsPageBusy(pageId) ||
                !operationCoordinator.TryTakePendingIntent(
                    pageId,
                    out UIPendingIntent intent))
            {
                return;
            }

            if (intent.OpenSource != null)
            {
                BeginOpen(
                    intent.PageId,
                    intent.OpenArgs,
                    intent.OpenSource,
                    runtimeEpoch);
            }
            else if (intent.CloseSource != null)
            {
                BeginClose(
                    intent.PageId,
                    intent.CloseRequest,
                    intent.CloseSource,
                    runtimeEpoch);
            }
            else if (intent.RefreshSource != null)
            {
                ExecuteRefresh(
                    intent.PageId,
                    intent.RefreshArgs,
                    intent.RefreshSource);
            }
        }

        private UIOperationCheckResult CheckOpenContext(
            OpenContext context,
            UIPageInstance instance,
            bool requireVersion)
        {
            if (!initialized || context.Epoch != runtimeEpoch)
            {
                return UIOperationCheckResult.Expired;
            }

            if (!sceneScopeCoordinator.IsSceneScopeCurrent(
                    context.Operation.Args.SceneScopeStamp))
            {
                return UIOperationCheckResult.SceneScopeInvalid;
            }

            return CheckOperation(
                context.Operation,
                instance,
                requireVersion);
        }

        private UIOperationCheckResult CheckCloseContext(
            CloseContext context,
            bool requireVersion)
        {
            if (!initialized || context.Epoch != runtimeEpoch)
            {
                return UIOperationCheckResult.Expired;
            }

            return CheckOperation(
                context.Operation,
                context.Instance,
                requireVersion);
        }

        private void CompleteOpenCheckFailure(
            OpenContext context,
            UIOperationCheckResult check)
        {
            CleanupOpenInstance(context);
            if (check == UIOperationCheckResult.Cancelled)
            {
                context.Operation.MarkCancelled();
                FinishOpen(context, source => source.TrySetCancelled());
                return;
            }

            CompleteOpenExpired(context);
        }

        private void CompleteOpenExpired(OpenContext context)
        {
            CleanupOpenInstance(context);
            context.Operation.MarkExpired();
            FinishOpen(context, source => source.TrySetExpired());
        }

        private void CompleteOpenExternalFailure(
            OpenContext context,
            AppUIOperationCompletion<UILoadResult> completion)
        {
            if (completion.Status == AppUIOperationStatus.Cancelled)
            {
                CompleteOpenCheckFailure(
                    context,
                    UIOperationCheckResult.Cancelled);
            }
            else if (completion.Status == AppUIOperationStatus.Expired)
            {
                CompleteOpenExpired(context);
            }
            else
            {
                FailOpen(
                    context,
                    completion.Exception ?? new InvalidOperationException(
                        "Failed load operation has no exception."));
            }
        }

        private void CompleteOpenExternalFailure(
            OpenContext context,
            AppUIOperationCompletion<UITransitionResult> completion)
        {
            if (completion.Status == AppUIOperationStatus.Cancelled)
            {
                CompleteOpenCheckFailure(
                    context,
                    UIOperationCheckResult.Cancelled);
            }
            else if (completion.Status == AppUIOperationStatus.Expired)
            {
                CompleteOpenExpired(context);
            }
            else
            {
                FailOpen(
                    context,
                    completion.Exception ?? new InvalidOperationException(
                        "Failed show transition has no exception."));
            }
        }

        private void CompleteOpenDomain(
            OpenContext context,
            UIOpenResult result)
        {
            if (context.Operation.IsActive)
            {
                context.Operation.MarkCompleted();
            }

            FinishOpen(
                context,
                source => source.TrySetSucceeded(result),
                result);
        }

        private void FailOpen(OpenContext context, Exception exception)
        {
            Debug.LogError(exception);
            CleanupOpenInstance(context);
            context.Operation.MarkFailed();
            FinishOpen(
                context,
                source => source.TrySetFailed(exception));
        }

        private void FinishOpen(
            OpenContext context,
            Action<IUIOperationSource<UIOpenResult>> complete,
            UIOpenResult result = null)
        {
            operationCoordinator.UnregisterOperation(context.Operation);
            if (result != null)
            {
                InvokeOpenedCallback(context.Operation.Args, result);
            }

            complete.Invoke(context.Source);
            TriggerPendingIntentDrain(context.Operation.PageId);
        }

        private void CleanupOpenInstance(OpenContext context)
        {
            if (context.Instance != null)
            {
                if (context.IsReopen)
                {
                    RestoreHiddenInstance(context.Instance);
                }
                else
                {
                    CleanupFailedInstance(context.Instance);
                }

                context.Instance = null;
            }
            else if (context.LoadResult.AssetLease != null)
            {
                ReleaseAssetLeaseSafe(context.LoadResult.AssetLease);
                context.LoadResult = default;
            }
        }

        private void RestoreHiddenInstance(UIPageInstance instance)
        {
            presentationCoordinator.ClearFocusIfOwned(instance);
            presentationCoordinator.SetInstanceActive(instance, false);
            instance.State = UIPageState.Hidden;
            instance.StackVisible = false;
            presentationCoordinator.Commit();
        }

        private void CompleteRefreshCheckFailure(
            UIRefreshOperation operation,
            IUIOperationSource<UIRefreshResult> source,
            UIPageInstance instance,
            UIOperationCheckResult check)
        {
            if (check == UIOperationCheckResult.Cancelled)
            {
                operation.MarkCancelled();
                source.TrySetCancelled();
            }
            else
            {
                operation.MarkExpired();
                source.TrySetExpired();
            }
        }

        private void CompleteCloseCheckFailure(
            CloseContext context,
            UIOperationCheckResult check)
        {
            if (check == UIOperationCheckResult.Cancelled)
            {
                context.Operation.MarkCancelled();
                FinishClose(context, source => source.TrySetCancelled());
            }
            else
            {
                context.Operation.MarkExpired();
                FinishClose(context, source => source.TrySetExpired());
            }
        }

        private void CompleteCloseExternalFailure(
            CloseContext context,
            AppUIOperationCompletion<UITransitionResult> completion)
        {
            ForceCloseCleanup(context);
            if (completion.Status == AppUIOperationStatus.Cancelled)
            {
                CompleteCloseCheckFailure(
                    context,
                    UIOperationCheckResult.Cancelled);
            }
            else if (completion.Status == AppUIOperationStatus.Expired)
            {
                CompleteCloseCheckFailure(
                    context,
                    UIOperationCheckResult.Expired);
            }
            else
            {
                FailClose(
                    context,
                    completion.Exception ?? new InvalidOperationException(
                        "Failed hide transition has no exception."));
            }
        }

        private void CompleteCloseDomain(
            CloseContext context,
            UICloseResult result)
        {
            if (context.Operation.IsActive)
            {
                context.Operation.MarkCompleted();
            }

            FinishClose(
                context,
                source => source.TrySetSucceeded(result));
        }

        private void FailClose(CloseContext context, Exception exception)
        {
            Debug.LogError(exception);
            ForceCloseCleanup(context);
            context.Operation.MarkFailed();
            FinishClose(
                context,
                source => source.TrySetFailed(exception));
        }

        private void ForceCloseCleanup(CloseContext context)
        {
            if (context.Instance == null ||
                context.Instance.State == UIPageState.Released)
            {
                return;
            }

            if (context.Request.ReleaseOnClose)
            {
                ReleaseInstance(
                    context.Instance,
                    UIReleaseReason.CloseRelease);
                return;
            }

            RestoreHiddenInstance(context.Instance);
        }

        private void FinishClose(
            CloseContext context,
            Action<IUIOperationSource<UICloseResult>> complete)
        {
            operationCoordinator.UnregisterOperation(context.Operation);
            complete.Invoke(context.Source);
            TriggerPendingIntentDrain(context.Operation.PageId);
        }

        private sealed class OpenContext
        {
            public OpenContext(
                UIOpenOperation operation,
                UIPageDefinition definition,
                IUIOperationSource<UIOpenResult> source,
                int epoch)
            {
                Operation = operation;
                Definition = definition;
                Source = source;
                Epoch = epoch;
            }

            public readonly UIOpenOperation Operation;
            public readonly UIPageDefinition Definition;
            public readonly IUIOperationSource<UIOpenResult> Source;
            public readonly int Epoch;
            public UILayerRoot LayerRoot;
            public UIPageInstance Instance;
            public UILoadResult LoadResult;
            public bool IsReopen;
        }

        private sealed class CloseContext
        {
            public CloseContext(
                UICloseOperation operation,
                UIPageInstance instance,
                UICloseRequest request,
                IUIOperationSource<UICloseResult> source,
                int epoch)
            {
                Operation = operation;
                Instance = instance;
                Request = request;
                Source = source;
                Epoch = epoch;
            }

            public readonly UICloseOperation Operation;
            public readonly UIPageInstance Instance;
            public readonly IUIOperationSource<UICloseResult> Source;
            public readonly int Epoch;
            public UICloseRequest Request;
        }
    }
}
