using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// App UI 运行时门面。
    /// 公开 Open/Close/Refresh/SceneScope API 保持集中在这里，具体 Operation、Presentation、Layer 配置和释放协议交给内部 coordinator。
    /// </summary>
    public sealed class AppUIManager : MonoBehaviour, IUIControllerService, INoticeServiceProvider, IUISceneCommandExecutor, IUIPageInstanceQuery
    {
        [SerializeField]
        private UIPageDefinitionRegistry pageRegistry;

        [SerializeField]
        private UILayerRoot[] layerRoots;

        [SerializeField]
        private UILayerSettings layerSettings;

        [SerializeField]
        private AppUINoticeSettings noticeSettings = AppUINoticeSettings.CreateDefault();

        private readonly UIPageInstanceRegistry instanceRegistry = new UIPageInstanceRegistry();
        private readonly UILayerController layerController = new UILayerController();
        private readonly UIStackCoordinator stackCoordinator = new UIStackCoordinator();
        private readonly UIFocusService focusService = new UIFocusService();
        private readonly UIInputBlocker inputBlocker = new UIInputBlocker();
        private readonly UISelectionInputAuthority selectionAuthority = new UISelectionInputAuthority();
        private readonly Dictionary<string, IUILoadStrategy> loadStrategies =
            new Dictionary<string, IUILoadStrategy>(4);
        private readonly Dictionary<string, IUIDestroyStrategy> destroyStrategies =
            new Dictionary<string, IUIDestroyStrategy>(4);

        private IUIAssetProvider assetProvider;
        private IUILoadStrategy defaultLoadStrategy;
        private IUIDestroyStrategy defaultDestroyStrategy;
        private bool initialized;
        private UIOperationCoordinator operationCoordinator;
        private UISceneScopeCoordinator sceneScopeCoordinator;
        private UIPresentationCoordinator presentationCoordinator;
        private UILayerRuntimeConfigurator layerRuntimeConfigurator;
        private UIPageInstanceReleaser pageInstanceReleaser;
        private NoticeService noticeService;

        public IUIService Service
        {
            get { return this; }
        }

        /// <summary>
        /// 当前 UI Runtime 的 Notice 服务。
        /// 业务侧通过独立 INoticeService 使用轻量提示，ControllerContext 也会注入同一个实例。
        /// </summary>
        public INoticeService Notices
        {
            get
            {
                EnsureRuntimeServices();
                return noticeService;
            }
        }

        public bool HasAssetProvider
        {
            get { return assetProvider != null; }
        }

        /// <summary>
        /// 使用默认 Layer/Notice 配置初始化 UI Manager。
        /// This overload uses the current layer and notice configuration.
        /// </summary>
        public void Initialize(
            UIPageDefinitionRegistry registry,
            IUIAssetProvider assetProvider,
            UILayerRoot[] roots)
        {
            Initialize(registry, assetProvider, roots, null);
        }

        /// <summary>
        /// 使用指定 Layer 配置初始化 UI Manager。
        /// 未传 Notice 配置时会使用内置默认值，保证旧测试场景不需要额外资产也能运行。
        /// </summary>
        public void Initialize(
            UIPageDefinitionRegistry registry,
            IUIAssetProvider assetProvider,
            UILayerRoot[] roots,
            UILayerSettings settings)
        {
            Initialize(registry, assetProvider, roots, settings, null);
        }

        /// <summary>
        /// 使用 Runtime Profile 提供的完整配置初始化 UI Manager。
        /// 该入口会注入页面注册表、资源服务、LayerRoot、LayerSettings 与 NoticeSettings，并重新绑定 NoticeLayer。
        /// </summary>
        public void Initialize(
            UIPageDefinitionRegistry registry,
            IUIAssetProvider assetProvider,
            UILayerRoot[] roots,
            UILayerSettings settings,
            AppUINoticeSettings appNoticeSettings)
        {
            pageRegistry = registry;
            this.assetProvider = assetProvider;
            layerRoots = roots;
            if (settings != null)
            {
                layerSettings = settings;
            }

            if (appNoticeSettings != null)
            {
                noticeSettings = appNoticeSettings;
            }

            InitializeInternal();
        }

        /// <summary>
        /// Replaces the asset provider and rebuilds provider-backed Notice pools.
        /// </summary>
        public void SetAssetProvider(IUIAssetProvider assetProvider)
        {
            if (assetProvider != null)
            {
                this.assetProvider = assetProvider;
                ConfigureNoticeService();
            }
        }

        /// <summary>
        /// Releases provider-backed Notice assets and clears the provider reference.
        /// </summary>
        public void ClearAssetProvider()
        {
            noticeService?.ReleaseLoadedResources();
            assetProvider = null;
        }

        public void RegisterLoadStrategy(IUILoadStrategy strategy)
        {
            if (strategy == null)
            {
                return;
            }

            loadStrategies[strategy.StrategyId ?? string.Empty] = strategy;
        }

        public void RegisterDestroyStrategy(IUIDestroyStrategy strategy)
        {
            if (strategy == null)
            {
                return;
            }

            destroyStrategies[strategy.StrategyId ?? string.Empty] = strategy;
        }

        private void Awake()
        {
            if (!initialized && pageRegistry != null)
            {
                InitializeInternal();
            }
        }

        private void Update()
        {
            TickUpdate(Time.deltaTime, Time.unscaledDeltaTime);
            TickNoticeSafe(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            TickLateUpdate(Time.deltaTime, Time.unscaledDeltaTime);
            try
            {
                focusService.ReconcileSelection();
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        private void OnDestroy()
        {
            EnsureRuntimeServices();
            operationCoordinator.CancelAllActiveOperations();
            operationCoordinator.CancelAllPendingIntents();
            noticeService.ClearAll();

            List<UIPageInstance> pages = instanceRegistry.GetSnapshot();
            presentationCoordinator.ClearOwnedSelection(pages);
            for (int i = 0; i < pages.Count; i++)
            {
                try
                {
                    pageInstanceReleaser.ReleaseInstance(pages[i], UIReleaseReason.ManagerDestroy);
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                }
            }

            instanceRegistry.Clear();
            presentationCoordinator.Clear();
            noticeService.Dispose();
            initialized = false;
        }

        private void InitializeInternal()
        {
            EnsureRuntimeServices();
            defaultLoadStrategy = new DefaultUILoadStrategy();
            defaultDestroyStrategy = new DefaultUIDestroyStrategy();
            RegisterLoadStrategy(defaultLoadStrategy);
            RegisterDestroyStrategy(defaultDestroyStrategy);

            if (pageRegistry != null)
            {
                pageRegistry.RebuildIndex();
            }

            if (layerRoots == null || layerRoots.Length == 0)
            {
                layerRoots = GetComponentsInChildren<UILayerRoot>(true);
            }

            layerController.Initialize(layerRoots);
            layerRuntimeConfigurator = new UILayerRuntimeConfigurator(layerSettings);
            layerRuntimeConfigurator.ApplyLayerSortingSafe(layerRoots);
            ConfigureNoticeService();
            ValidateConfiguration();
            initialized = true;
        }

        public async UniTask BindSceneAsync(SceneUIBindingData bindingData)
        {
            EnsureRuntimeServices();
            await sceneScopeCoordinator.BindSceneAsync(bindingData);
        }

        /// <summary>
        /// 解绑场景 UI，并清理随该 SceneScopeId 存活的 Notice。
        /// 页面释放仍由 SceneScopeCoordinator 负责；Notice 不是页面实例，因此在结果返回前单独按 Scope 回收。
        /// </summary>
        public async UniTask<UISceneExitResult> UnbindSceneAsync(SceneUIBindingData bindingData)
        {
            EnsureRuntimeServices();
            UISceneExitResult result = await sceneScopeCoordinator.UnbindSceneAsync(bindingData);
            string sceneScopeId = UISceneScopeCoordinator.ResolveSceneScopeId(bindingData);
            noticeService.ClearScope(UIPageScope.SceneScope, sceneScopeId);
            noticeService.ClearScope(UIPageScope.TemporaryScope, sceneScopeId);
            return result;
        }

        /// <summary>
        /// 释放指定 Scope 的页面和 Notice。
        /// GlobalScope 不允许批量释放，Scene/Loading/Temporary 会同步清理匹配 SceneScopeId 的轻量提示。
        /// </summary>
        public async UniTask<UIScopeReleaseResult> ReleaseScopeAsync(UIPageScope scope, string sceneScopeId)
        {
            EnsureRuntimeServices();
            UIScopeReleaseResult result = await sceneScopeCoordinator.ReleaseScopeAsync(scope, sceneScopeId);
            if (scope != UIPageScope.GlobalScope)
            {
                noticeService.ClearScope(scope, UISceneScopeCoordinator.NormalizeSceneScopeId(sceneScopeId));
            }

            return result;
        }

        public UniTask<UIOpenResult> OpenAsync(string pageId)
        {
            return OpenAsync(pageId, UIOpenArgs.None);
        }

        public UniTask<UIOpenResult> OpenAsync(string pageId, object data)
        {
            return OpenAsync(pageId, data == null ? UIOpenArgs.None : UIOpenArgs.FromExplicit(data));
        }

        public async UniTask<UIOpenResult> OpenAsync(string pageId, UIOpenArgs args)
        {
            EnsureRuntimeServices();
            if (!EnsureInitialized())
            {
                return UIOpenResult.Fail(UIPageOpenError.InvalidDefinition);
            }

            if (string.IsNullOrEmpty(pageId) || pageRegistry == null ||
                !pageRegistry.TryGet(pageId, out UIPageDefinition definition))
            {
                return UIOpenResult.Fail(UIPageOpenError.DefinitionNotFound);
            }

            if (!ValidateDefinition(definition))
            {
                return UIOpenResult.Fail(UIPageOpenError.InvalidDefinition);
            }

            if (operationCoordinator.IsPageBusy(pageId))
            {
                if (definition.OpenPolicy == UIOpenPolicy.QueueIfBusy)
                {
                    return await operationCoordinator.EnqueueOpenPendingAsync(pageId, args);
                }

                return UIOpenResult.Fail(UIPageOpenError.AlreadyOpenRejected);
            }

            UIOpenOperation operation = operationCoordinator.CreateOpenOperation(pageId, args);
            if (!operationCoordinator.TryRegisterOperation(operation))
            {
                if (definition.OpenPolicy == UIOpenPolicy.QueueIfBusy)
                {
                    return await operationCoordinator.EnqueueOpenPendingAsync(pageId, args);
                }

                return UIOpenResult.Fail(UIPageOpenError.AlreadyOpenRejected);
            }

            try
            {
                operation.MarkRunning();

                UIOpenResult result = instanceRegistry.TryGet(pageId, out UIPageInstance existing)
                    ? await OpenExistingAsync(existing, definition, operation)
                    : await OpenNewAsync(definition, operation);

                InvokeOpenedCallback(args, result);
                return result;
            }
            finally
            {
                operationCoordinator.UnregisterOperation(operation);
                TriggerPendingIntentDrain(operation.PageId);
            }
        }

        private async UniTask<UIOpenResult> OpenNewAsync(
            UIPageDefinition definition,
            UIOpenOperation operation)
        {
            UIOperationCheckResult checkResult = CheckOperation(operation, null, false);
            if (checkResult != UIOperationCheckResult.Valid)
            {
                return UIOperationCoordinator.FailOpenOperation(operation, UIOperationCoordinator.ToOpenError(checkResult));
            }

            UIPageInstance instance = null;
            UILoadResult loadResult = default(UILoadResult);
            try
            {
                if (!layerController.TryGetRoot(definition.LayerId, out UILayerRoot layerRoot) ||
                    layerRoot.ContentRoot == null)
                {
                    return UIOperationCoordinator.FailOpenOperation(operation, UIPageOpenError.LayerNotFound);
                }

                IUILoadStrategy loadStrategy = ResolveLoadStrategy(definition.LoadStrategyId);
                loadResult = await loadStrategy.LoadAsync(definition, assetProvider);
                checkResult = CheckOperation(operation, null, false);
                if (checkResult != UIOperationCheckResult.Valid)
                {
                    ReleaseAssetLeaseSafe(loadResult.AssetLease);
                    return UIOperationCoordinator.FailOpenOperation(operation, UIOperationCoordinator.ToOpenError(checkResult));
                }

                if (!loadResult.Success || loadResult.Prefab == null)
                {
                    ReleaseAssetLeaseSafe(loadResult.AssetLease);
                    return UIOperationCoordinator.FailOpenOperation(operation, UIPageOpenError.ResourceLoadFailed);
                }

                GameObject pageObject = Instantiate(loadResult.Prefab, layerRoot.ContentRoot, false);
                pageObject.name = loadResult.Prefab.name;
                pageObject.SetActive(false);

                PanelBaseController[] controllers = pageObject.GetComponents<PanelBaseController>();
                if (controllers == null || controllers.Length == 0)
                {
                    pageInstanceReleaser.DestroyLoadedObject(pageObject, loadResult.AssetLease);
                    return UIOperationCoordinator.FailOpenOperation(operation, UIPageOpenError.ControllerMissing);
                }

                if (controllers.Length > 1)
                {
                    pageInstanceReleaser.DestroyLoadedObject(pageObject, loadResult.AssetLease);
                    return UIOperationCoordinator.FailOpenOperation(operation, UIPageOpenError.ControllerInvalid);
                }

                PanelBaseController controller = controllers[0];
                instance = new UIPageInstance
                {
                    PageId = operation.PageId,
                    Definition = definition,
                    LayerId = definition.LayerId,
                    SceneScopeId = sceneScopeCoordinator.ResolveInstanceSceneScopeId(definition, operation.SceneScopeId),
                    OperationVersion = operation.Version.Value,
                    GameObject = pageObject,
                    RectTransform = pageObject.transform as RectTransform,
                    Controller = controller,
                    AssetLease = loadResult.AssetLease,
                    State = UIPageState.Initializing,
                };

                instanceRegistry.Register(instance);
                UIPanelContext context = new UIPanelContext(this, noticeService, operation.PageId, definition);
                controller.SetContext(context);
                controller.OnCreate(context);
                controller.OnInit();
                IAppUIFocusDefinitionProvider focusDefinitionProvider =
                    controller as IAppUIFocusDefinitionProvider;
                if (focusDefinitionProvider == null)
                {
                    focusDefinitionProvider =
                        pageObject.GetComponent<AppUIFocusAuthoring>();
                }

                if (focusDefinitionProvider != null)
                {
                    AppUIFocusDefinition focusDefinition =
                        focusDefinitionProvider.BuildFocusDefinition();
                    if (focusDefinition == null)
                    {
                        throw new InvalidOperationException(
                            "IAppUIFocusDefinitionProvider returned null. Page=" +
                            operation.PageId);
                    }

                    context.SetFocusScope(
                        focusService.AttachScope(instance, focusDefinition));
                }

                ApplyDataAndRefresh(controller, operation.Args.HasData, operation.Args.Data);

                checkResult = CheckOperation(operation, instance, true);
                if (checkResult != UIOperationCheckResult.Valid)
                {
                    CleanupFailedInstance(instance);
                    return UIOperationCoordinator.FailOpenOperation(operation, UIOperationCoordinator.ToOpenError(checkResult));
                }

                await controller.ShowAsync();

                checkResult = CheckOperation(operation, instance, true);
                if (checkResult != UIOperationCheckResult.Valid)
                {
                    CleanupFailedInstance(instance);
                    return UIOperationCoordinator.FailOpenOperation(operation, UIOperationCoordinator.ToOpenError(checkResult));
                }

                instance.State = UIPageState.Open;
                presentationCoordinator.PushOpened(instance);

                operation.MarkCompleted();
                UIOpenResult result = UIOpenResult.Ok(instance.ToHandle());
                return result;
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                if (instance != null)
                {
                    CleanupFailedInstance(instance);
                }
                else if (loadResult.AssetLease != null &&
                         loadResult.AssetLease.IsValid)
                {
                    ReleaseAssetLeaseSafe(loadResult.AssetLease);
                }

                operation.MarkFailed();
                return UIOpenResult.Fail(UIPageOpenError.LifecycleFailed, exception);
            }
        }

        public UniTask<UICloseResult> CloseAsync(string pageId)
        {
            return CloseAsync(pageId, UICloseRequest.Default);
        }

        public async UniTask<UICloseResult> CloseAsync(string pageId, UICloseRequest request)
        {
            EnsureRuntimeServices();
            if (operationCoordinator.IsPageBusy(pageId))
            {
                return await operationCoordinator.EnqueueClosePendingAsync(pageId, request);
            }

            if (!instanceRegistry.TryGet(pageId, out UIPageInstance instance) || instance == null)
            {
                return UICloseResult.Fail(pageId, UIPageState.None, UICloseError.NotOpen);
            }

            UICloseOperation operation = operationCoordinator.CreateCloseOperation(pageId, request);
            if (!operationCoordinator.TryRegisterOperation(operation))
            {
                return await operationCoordinator.EnqueueClosePendingAsync(pageId, request);
            }

            UIPageState startState = instance.State;
            try
            {
                operation.MarkRunning();

                UIOperationCheckResult checkResult = CheckOperation(operation, instance, false);
                if (checkResult != UIOperationCheckResult.Valid)
                {
                    return UIOperationCoordinator.FailCloseOperation(operation, pageId, instance.State, UIOperationCoordinator.ToCloseError(checkResult));
                }

                try
                {
                    if (instance.Controller != null && !instance.Controller.CanClose(ref request))
                    {
                        operation.MarkFailed();
                        return UICloseResult.Fail(pageId, instance.State, UICloseError.Rejected);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                    operation.MarkFailed();
                    return UICloseResult.Fail(pageId, startState, UICloseError.LifecycleFailed, exception);
                }

                // 只有关闭获批后才切换实例版本，避免 Rejected 使已发布 Snapshot Handle 失效。
                instance.OperationVersion = operation.Version.Value;
                checkResult = CheckOperation(operation, instance, true);
                if (checkResult != UIOperationCheckResult.Valid)
                {
                    return UIOperationCoordinator.FailCloseOperation(operation, pageId, instance.State, UIOperationCoordinator.ToCloseError(checkResult));
                }

                // Hide 动画开始前先从交互快照撤销页面资格，避免动画期间继续持有焦点。
                presentationCoordinator.RemoveFromStack(instance);
                presentationCoordinator.ClearFocusIfOwned(instance);
                presentationCoordinator.Commit();

                bool hideFailed = false;
                if (instance.State == UIPageState.Open && instance.Controller != null)
                {
                    try
                    {
                        await instance.Controller.HideAsync();
                    }
                    catch (Exception exception)
                    {
                        hideFailed = true;
                        Debug.LogError(exception);
                    }
                }

                if (!request.ReleaseOnClose)
                {
                    if (hideFailed)
                    {
                        presentationCoordinator.SetInstanceActive(instance, false);
                    }

                    instance.State = UIPageState.Hidden;
                    instance.StackVisible = false;
                    presentationCoordinator.Commit();

                    checkResult = CheckOperation(operation, instance, true);
                    if (checkResult != UIOperationCheckResult.Valid)
                    {
                        return UIOperationCoordinator.FailCloseOperation(operation, pageId, instance.State, UIOperationCoordinator.ToCloseError(checkResult));
                    }

                    operation.MarkCompleted();
                    return UICloseResult.Ok(pageId, instance.State);
                }

                ReleaseInstance(instance, UIReleaseReason.CloseRelease);

                UIOperationCheckResult releaseCheckResult = CheckOperation(operation, instance, true);
                if (releaseCheckResult != UIOperationCheckResult.Valid)
                {
                    return UIOperationCoordinator.FailCloseOperation(operation, pageId, UIPageState.Released, UIOperationCoordinator.ToCloseError(releaseCheckResult));
                }

                operation.MarkCompleted();
                return UICloseResult.Ok(pageId, UIPageState.Released);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                operation.MarkFailed();
                return UICloseResult.Fail(pageId, instance.State, UICloseError.Exception, exception);
            }
            finally
            {
                operationCoordinator.UnregisterOperation(operation);
                TriggerPendingIntentDrain(operation.PageId);
            }
        }

        public UniTask<UIRefreshResult> RefreshAsync(string pageId, object data)
        {
            return RefreshAsync(pageId, new UIRefreshArgs(data));
        }

        public async UniTask<UIRefreshResult> RefreshAsync(string pageId, UIRefreshArgs args)
        {
            EnsureRuntimeServices();
            if (operationCoordinator.IsPageBusy(pageId))
            {
                return await operationCoordinator.EnqueueRefreshPendingAsync(pageId, args);
            }

            if (!instanceRegistry.TryGet(pageId, out UIPageInstance instance) || instance == null)
            {
                return UIRefreshResult.Fail(pageId, UIPageState.None, UIRefreshError.NotOpen);
            }

            UIRefreshOperation operation = operationCoordinator.CreateRefreshOperation(pageId, args);
            if (!operationCoordinator.TryRegisterOperation(operation))
            {
                return await operationCoordinator.EnqueueRefreshPendingAsync(pageId, args);
            }

            try
            {
                operation.MarkRunning();

                UIOperationCheckResult checkResult = CheckOperation(operation, instance, false);
                if (checkResult != UIOperationCheckResult.Valid)
                {
                    return UIOperationCoordinator.FailRefreshOperation(operation, pageId, instance.State, UIOperationCoordinator.ToRefreshError(checkResult));
                }

                instance.OperationVersion = operation.Version.Value;

                if (instance.State == UIPageState.Hidden ||
                    instance.State == UIPageState.Loading ||
                    instance.State == UIPageState.Initializing)
                {
                    checkResult = CheckOperation(operation, instance, true);
                    if (checkResult != UIOperationCheckResult.Valid)
                    {
                        return UIOperationCoordinator.FailRefreshOperation(operation, pageId, instance.State, UIOperationCoordinator.ToRefreshError(checkResult));
                    }

                    instance.PendingRefreshData = args.Data;
                    instance.HasPendingRefreshData = true;
                    operation.MarkCompleted();
                    return UIRefreshResult.Ok(pageId, instance.State);
                }

                if (instance.State != UIPageState.Open || instance.Controller == null)
                {
                    operation.MarkFailed();
                    return UIRefreshResult.Fail(pageId, instance.State, UIRefreshError.NotOpen);
                }

                checkResult = CheckOperation(operation, instance, true);
                if (checkResult != UIOperationCheckResult.Valid)
                {
                    return UIOperationCoordinator.FailRefreshOperation(operation, pageId, instance.State, UIOperationCoordinator.ToRefreshError(checkResult));
                }

                ApplyDataAndRefresh(instance.Controller, true, args.Data);
                await UniTask.CompletedTask;

                checkResult = CheckOperation(operation, instance, true);
                if (checkResult != UIOperationCheckResult.Valid)
                {
                    return UIOperationCoordinator.FailRefreshOperation(operation, pageId, instance.State, UIOperationCoordinator.ToRefreshError(checkResult));
                }

                operation.MarkCompleted();
                return UIRefreshResult.Ok(pageId, instance.State);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                operation.MarkFailed();
                return UIRefreshResult.Fail(pageId, instance.State, UIRefreshError.LifecycleFailed, exception);
            }
            finally
            {
                operationCoordinator.UnregisterOperation(operation);
                TriggerPendingIntentDrain(operation.PageId);
            }
        }

        public UniTask<UICloseResult> CloseTopAsync()
        {
            EnsureRuntimeServices();
            if (presentationCoordinator.TryGetTopVisiblePage(out UIPageInstance instance) && instance != null)
            {
                return CloseAsync(instance.PageId, UICloseRequest.Default);
            }

            return UniTask.FromResult(UICloseResult.Fail(string.Empty, UIPageState.None, UICloseError.NotOpen));
        }

        public UniTask<UICloseResult> CloseTopAsync(UILayerId layerId)
        {
            EnsureRuntimeServices();
            if (presentationCoordinator.TryGetTopVisiblePage(layerId, out UIPageInstance instance) && instance != null)
            {
                return CloseAsync(instance.PageId, UICloseRequest.Default);
            }

            return UniTask.FromResult(UICloseResult.Fail(string.Empty, UIPageState.None, UICloseError.NotOpen));
        }

        public async UniTask<UICancelResult> CancelAsync()
        {
            EnsureRuntimeServices();
            UIPageInstance instance = presentationCoordinator.ResolveCancelTarget();
            if (instance == null)
            {
                return UICancelResult.NoTarget();
            }

            string pageId = instance.PageId;
            AppUIFocusCancelDispatchResult focusCancelResult =
                focusService.TryHandleCancel(
                    instance,
                    out Exception focusCancelException);
            if (focusCancelResult == AppUIFocusCancelDispatchResult.Failed)
            {
                Debug.LogError(focusCancelException);
                return UICancelResult.HandlerFailed(pageId, focusCancelException);
            }

            if (focusCancelResult == AppUIFocusCancelDispatchResult.Consumed)
            {
                return UICancelResult.Handled(pageId);
            }

            if (instance.Controller is IUICancelHandler cancelHandler)
            {
                bool handled;
                try
                {
                    handled = cancelHandler.HandleCancel();
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                    return UICancelResult.HandlerFailed(pageId, exception);
                }

                if (handled)
                {
                    return UICancelResult.Handled(pageId);
                }
            }

            if (instance.Controller is IAppUIFocusCancelPolicyProvider policyProvider)
            {
                bool handled;
                try
                {
                    AppUIFocusCancelPolicy cancelPolicy =
                        policyProvider.GetFocusCancelPolicy();
                    handled = cancelPolicy != null && cancelPolicy.HandleCancel();
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                    return UICancelResult.HandlerFailed(pageId, exception);
                }

                if (handled)
                {
                    return UICancelResult.Handled(pageId);
                }
            }

            if (instance.Definition == null || !instance.Definition.CloseOnCancel)
            {
                return UICancelResult.CloseDisabled(pageId);
            }

            UICloseResult closeResult = await CloseAsync(pageId, UICloseRequest.Default);
            if (closeResult.Success)
            {
                return UICancelResult.Closed(pageId, closeResult);
            }

            if (closeResult.Error == UICloseError.Rejected)
            {
                return UICancelResult.CloseRejected(pageId, closeResult);
            }

            return UICancelResult.CloseFailed(pageId, closeResult);
        }

        public bool IsOpen(string pageId)
        {
            return instanceRegistry.TryGet(pageId, out UIPageInstance instance) &&
                   instance != null &&
                   instance.State == UIPageState.Open;
        }

        public bool IsOpening(string pageId)
        {
            EnsureRuntimeServices();
            return operationCoordinator.IsOpenOperationActive(pageId);
        }

        public bool TryGetPageState(string pageId, out UIPageState state)
        {
            if (instanceRegistry.TryGet(pageId, out UIPageInstance instance) && instance != null)
            {
                state = instance.State;
                return true;
            }

            state = UIPageState.None;
            return false;
        }

        private void TriggerPendingIntentDrain(string pageId)
        {
            if (string.IsNullOrEmpty(pageId) ||
                operationCoordinator.IsPageBusy(pageId) ||
                !operationCoordinator.HasPendingIntent(pageId))
            {
                return;
            }

            DrainPendingIntentAsync(pageId).Forget();
        }

        private async UniTaskVoid DrainPendingIntentAsync(string pageId)
        {
            if (string.IsNullOrEmpty(pageId) || operationCoordinator.IsPageBusy(pageId))
            {
                return;
            }

            if (!operationCoordinator.TryTakePendingIntent(pageId, out UIPendingIntent intent))
            {
                return;
            }

            // Drain 只负责把 pending 交回公开 API 执行，OperationCoordinator 不反向调用 Manager。
            try
            {
                switch (intent.Intent)
                {
                    case UIPageIntent.Open:
                    {
                        UIOpenResult result = await OpenAsync(intent.PageId, intent.OpenArgs);
                        intent.OpenCompletion?.TrySetResult(result);
                        break;
                    }
                    case UIPageIntent.Release:
                    case UIPageIntent.Close:
                    {
                        UICloseResult result = await CloseAsync(intent.PageId, intent.CloseRequest);
                        intent.CloseCompletion?.TrySetResult(result);
                        break;
                    }
                    case UIPageIntent.Refresh:
                    {
                        UIRefreshResult result = await RefreshAsync(intent.PageId, intent.RefreshArgs);
                        intent.RefreshCompletion?.TrySetResult(result);
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                operationCoordinator.CompletePendingIntentAsException(intent, exception);
            }

            TriggerPendingIntentDrain(pageId);
        }

        private UIPageState GetKnownPageState(string pageId)
        {
            return instanceRegistry.TryGet(pageId, out UIPageInstance instance) && instance != null
                ? instance.State
                : UIPageState.None;
        }

        private UIOperationCheckResult CheckOperation(
            IUIPageOperation operation,
            UIPageInstance instance,
            bool requireVersion)
        {
            UIOperationCheckResult result = operationCoordinator.CheckOperation(operation, instance, requireVersion);
            if (result != UIOperationCheckResult.Valid)
            {
                return result;
            }

            if (instance != null && !sceneScopeCoordinator.IsSceneScopeCompatible(operation.SceneScopeId, instance))
            {
                return UIOperationCheckResult.SceneScopeInvalid;
            }

            return UIOperationCheckResult.Valid;
        }

        private async UniTask<UIOpenResult> OpenExistingAsync(
            UIPageInstance instance,
            UIPageDefinition definition,
            UIOpenOperation operation)
        {
            if (instance == null)
            {
                return UIOperationCoordinator.FailOpenOperation(operation, UIPageOpenError.OperationExpired);
            }

            UIOperationCheckResult checkResult = CheckOperation(operation, instance, false);
            if (checkResult != UIOperationCheckResult.Valid)
            {
                return UIOperationCoordinator.FailOpenOperation(operation, UIOperationCoordinator.ToOpenError(checkResult));
            }

            if (instance.State == UIPageState.Open)
            {
                if (definition.OpenPolicy == UIOpenPolicy.RejectIfOpeningOrOpen)
                {
                    operation.MarkFailed();
                    return UIOpenResult.Fail(UIPageOpenError.AlreadyOpenRejected);
                }

                instance.OperationVersion = operation.Version.Value;
                checkResult = CheckOperation(operation, instance, true);
                if (checkResult != UIOperationCheckResult.Valid)
                {
                    return UIOperationCoordinator.FailOpenOperation(operation, UIOperationCoordinator.ToOpenError(checkResult));
                }

                try
                {
                    if (definition.OpenPolicy == UIOpenPolicy.RefreshExisting)
                    {
                        ApplyDataAndRefresh(instance.Controller, operation.Args.HasData, operation.Args.Data);
                    }

                    presentationCoordinator.PushOpened(
                        instance,
                        AppUIFocusChangeReason.RestoreRequested);
                    operation.MarkCompleted();
                    return UIOpenResult.Ok(instance.ToHandle());
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                    operation.MarkFailed();
                    return UIOpenResult.Fail(UIPageOpenError.LifecycleFailed, exception);
                }
            }

            if (instance.State == UIPageState.Hidden && instance.Controller != null)
            {
                instance.OperationVersion = operation.Version.Value;
                object data = operation.Args.HasData
                    ? operation.Args.Data
                    : instance.HasPendingRefreshData
                        ? instance.PendingRefreshData
                        : null;
                bool hasData = operation.Args.HasData || instance.HasPendingRefreshData;
                instance.PendingRefreshData = null;
                instance.HasPendingRefreshData = false;

                try
                {
                    checkResult = CheckOperation(operation, instance, true);
                    if (checkResult != UIOperationCheckResult.Valid)
                    {
                        instance.PendingRefreshData = data;
                        instance.HasPendingRefreshData = hasData;
                        return UIOperationCoordinator.FailOpenOperation(operation, UIOperationCoordinator.ToOpenError(checkResult));
                    }

                    ApplyDataAndRefresh(instance.Controller, hasData, data);
                    await instance.Controller.ShowAsync();

                    checkResult = CheckOperation(operation, instance, true);
                    if (checkResult != UIOperationCheckResult.Valid)
                    {
                        presentationCoordinator.ClearFocusIfOwned(instance);
                        presentationCoordinator.SetInstanceActive(instance, false);
                        instance.State = UIPageState.Hidden;
                        instance.StackVisible = false;
                        presentationCoordinator.Commit();
                        return UIOperationCoordinator.FailOpenOperation(operation, UIOperationCoordinator.ToOpenError(checkResult));
                    }

                    instance.State = UIPageState.Open;
                    presentationCoordinator.PushOpened(
                        instance,
                        ResolveReopenFocusReason(instance.Controller));
                    operation.MarkCompleted();
                    return UIOpenResult.Ok(instance.ToHandle());
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                    presentationCoordinator.ClearFocusIfOwned(instance);
                    presentationCoordinator.SetInstanceActive(instance, false);
                    instance.State = UIPageState.Hidden;
                    instance.StackVisible = false;
                    presentationCoordinator.Commit();
                    operation.MarkFailed();
                    return UIOpenResult.Fail(UIPageOpenError.LifecycleFailed, exception);
                }
            }

            operation.MarkFailed();
            return UIOpenResult.Fail(UIPageOpenError.AlreadyOpenRejected);
        }

        internal static AppUIFocusChangeReason ResolveReopenFocusReason(
            UIBaseController controller)
        {
            IAppUIFocusReopenPolicyProvider provider =
                controller as IAppUIFocusReopenPolicyProvider;
            return provider != null &&
                   provider.FocusReopenPolicy ==
                   AppUIFocusReopenPolicy.DefaultFocus
                ? AppUIFocusChangeReason.FirstOpened
                : AppUIFocusChangeReason.Reopened;
        }

        /// <summary>
        /// 确保内部 coordinator 已创建。
        /// 公开 API 可能在 Awake/Initialize 之前被测试代码直接调用，因此这里不依赖 initialized 状态。
        /// </summary>
        private void EnsureRuntimeServices()
        {
            if (operationCoordinator == null)
            {
                operationCoordinator = new UIOperationCoordinator(GetKnownPageState);
            }

            if (sceneScopeCoordinator == null)
            {
                sceneScopeCoordinator = new UISceneScopeCoordinator(this, this);
            }

            if (presentationCoordinator == null)
            {
                presentationCoordinator = new UIPresentationCoordinator(
                    this,
                    instanceRegistry,
                    layerController,
                    stackCoordinator,
                    focusService,
                    inputBlocker,
                    selectionAuthority);
            }

            if (layerRuntimeConfigurator == null)
            {
                layerRuntimeConfigurator = new UILayerRuntimeConfigurator(layerSettings);
            }

            if (noticeService == null)
            {
                noticeService = new NoticeService();
            }

            if (pageInstanceReleaser == null)
            {
                pageInstanceReleaser = new UIPageInstanceReleaser(
                    instanceRegistry,
                    ResolveDestroyStrategy,
                    presentationCoordinator.ResetInstancePresentationState);
            }
        }

        /// <summary>
        /// 将 NoticeService 绑定到当前 NoticeLayer。
        /// 该方法在 Manager 初始化和资源服务重新注入时调用，保证池对象挂载到当前 Runtime Root 下。
        /// </summary>
        private void ConfigureNoticeService()
        {
            EnsureRuntimeServices();
            RectTransform noticeRoot = null;
            if (layerController.TryGetRoot(UILayerId.NoticeLayer, out UILayerRoot layerRoot) &&
                layerRoot != null)
            {
                noticeRoot = layerRoot.ContentRoot;
            }

            noticeService.Initialize(noticeRoot, assetProvider, noticeSettings ?? AppUINoticeSettings.CreateDefault());
        }

        private bool EnsureInitialized()
        {
            if (!initialized)
            {
                InitializeInternal();
            }

            return initialized && pageRegistry != null;
        }

        private static bool ValidateDefinition(UIPageDefinition definition)
        {
            return definition != null &&
                   !string.IsNullOrEmpty(definition.PageId) &&
                   !string.IsNullOrEmpty(definition.PrefabAssetId);
        }

        private void ValidateConfiguration()
        {
            if (pageRegistry == null)
            {
                Debug.LogError("<Joi.H.AppUI> UIPageDefinitionRegistry is missing.");
                return;
            }

            HashSet<UICanvasDomain> canvasDomains = new HashSet<UICanvasDomain>();
            layerRuntimeConfigurator.ValidateLayerRoots(layerRoots, canvasDomains);

            HashSet<string> pageIds = new HashSet<string>();
            List<string> criticalErrors = null;
            for (int i = 0; i < pageRegistry.Pages.Count; i++)
            {
                UIPageDefinition page = pageRegistry.Pages[i];
                if (page == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(page.PageId))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> UIPageDefinition has empty PageId: " + page.name,
                        ref criticalErrors);
                    continue;
                }

                if (!pageIds.Add(page.PageId))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> Duplicate UI PageId: " + page.PageId,
                        ref criticalErrors);
                }

                if (string.IsNullOrEmpty(page.PrefabAssetId))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> PrefabAssetId is empty: " + page.PageId,
                        ref criticalErrors);
                }

                if (!UILayerRuntimeConfigurator.IsValidLayerId(page.LayerId))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> LayerId is invalid for page " + page.PageId + ": " + page.LayerId,
                        ref criticalErrors);
                }

                if (!UILayerRuntimeConfigurator.IsValidCanvasDomain(page.CanvasDomain))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> CanvasDomain is invalid for page " + page.PageId + ": " + page.CanvasDomain,
                        ref criticalErrors);
                }
                else if (!canvasDomains.Contains(page.CanvasDomain))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> CanvasDomain is not registered by any UILayerRoot for page " +
                        page.PageId +
                        ": " +
                        page.CanvasDomain,
                        ref criticalErrors);
                }

                if (!UILayerRuntimeConfigurator.IsValidPageScope(page.Scope))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> Scope is invalid for page " + page.PageId + ": " + page.Scope,
                        ref criticalErrors);
                }

                if (!UILayerRuntimeConfigurator.IsValidOpenPolicy(page.OpenPolicy))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> OpenPolicy is invalid for page " + page.PageId + ": " + page.OpenPolicy,
                        ref criticalErrors);
                }

                UILayerRoot layerRoot = null;
                if (UILayerRuntimeConfigurator.IsValidLayerId(page.LayerId) &&
                    (!layerController.TryGetRoot(page.LayerId, out layerRoot) || layerRoot == null))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> LayerRoot is missing for page " + page.PageId + ": " + page.LayerId,
                        ref criticalErrors);
                }

                if (layerRoot != null)
                {
                    if (layerRoot.CanvasDomain != page.CanvasDomain)
                    {
                        ReportConfigurationError(
                            page,
                            "<Joi.H.AppUI> Page CanvasDomain does not match LayerRoot for page " +
                            page.PageId +
                            ". Page=" +
                            page.CanvasDomain +
                            ", LayerRoot=" +
                            layerRoot.CanvasDomain,
                            ref criticalErrors);
                    }

                    if (page.RequiresRaycaster && !UILayerRuntimeConfigurator.HasEnabledGraphicRaycaster(layerRoot))
                    {
                        ReportConfigurationError(
                            page,
                            "<Joi.H.AppUI> RequiresRaycaster page has no enabled GraphicRaycaster on its UI canvas: " +
                            page.PageId,
                            ref criticalErrors);
                    }
                }

                if (!string.IsNullOrEmpty(page.LoadStrategyId) &&
                    !loadStrategies.ContainsKey(page.LoadStrategyId))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> LoadStrategy is not registered for page " + page.PageId + ": " + page.LoadStrategyId,
                        ref criticalErrors);
                }

                if (!string.IsNullOrEmpty(page.DestroyStrategyId) &&
                    !destroyStrategies.ContainsKey(page.DestroyStrategyId))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> DestroyStrategy is not registered for page " + page.PageId + ": " + page.DestroyStrategyId,
                        ref criticalErrors);
                }

                if (page.IsHighFrequency &&
                    (page.LayerId != UILayerId.HudLayer || page.CanvasDomain != UICanvasDomain.Hud))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> High frequency page must use HudLayer + Hud CanvasDomain: " + page.PageId,
                        ref criticalErrors);
                }

                if (page.IsFullScreen && !UILayerRuntimeConfigurator.CanLayerHostFullScreen(page.LayerId))
                {
                    ReportConfigurationError(
                        page,
                        "<Joi.H.AppUI> FullScreen page uses an unsuitable layer: " + page.PageId + " -> " + page.LayerId,
                        ref criticalErrors);
                }
            }

            ThrowIfCriticalConfigurationErrors(criticalErrors);
        }

        private static void ReportConfigurationError(
            UIPageDefinition page,
            string message,
            ref List<string> criticalErrors)
        {
            Debug.LogError(message, page);
            if (page == null || !page.IsCritical)
            {
                return;
            }

            if (criticalErrors == null)
            {
                criticalErrors = new List<string>(4);
            }

            criticalErrors.Add(message);
        }

        private static void ThrowIfCriticalConfigurationErrors(List<string> criticalErrors)
        {
            if (criticalErrors == null || criticalErrors.Count == 0)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new InvalidOperationException(BuildCriticalConfigurationMessage(criticalErrors));
#endif
        }

        private static string BuildCriticalConfigurationMessage(List<string> criticalErrors)
        {
            string message = "<Joi.H.AppUI> Critical UI configuration errors:";
            for (int i = 0; i < criticalErrors.Count; i++)
            {
                message += "\n" + criticalErrors[i];
            }

            return message;
        }

        private IUILoadStrategy ResolveLoadStrategy(string strategyId)
        {
            IUILoadStrategy strategy;
            if (!string.IsNullOrEmpty(strategyId) && loadStrategies.TryGetValue(strategyId, out strategy))
            {
                return strategy;
            }

            return defaultLoadStrategy;
        }

        private IUIDestroyStrategy ResolveDestroyStrategy(string strategyId)
        {
            IUIDestroyStrategy strategy;
            if (!string.IsNullOrEmpty(strategyId) && destroyStrategies.TryGetValue(strategyId, out strategy))
            {
                return strategy;
            }

            return defaultDestroyStrategy;
        }

        private static void ApplyDataAndRefresh(PanelBaseController controller, bool hasData, object data)
        {
            if (controller == null)
            {
                return;
            }

            controller.OnDataLoad(hasData ? data : null);
            controller.OnRefresh();
        }

        private static void InvokeOpenedCallback(UIOpenArgs args, UIOpenResult result)
        {
            if (!result.Success || args.OnOpened == null)
            {
                return;
            }

            try
            {
                args.OnOpened.Invoke(result);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        private static void ReleaseAssetLeaseSafe(UIAssetLease lease)
        {
            if (lease == null || !lease.IsValid)
            {
                return;
            }

            try
            {
                lease.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        /// <summary>
        /// 打开失败后的清理入口。
        /// 具体释放顺序由 UIPageInstanceReleaser 统一处理，Manager 只负责在需要时提交 Presentation。
        /// </summary>
        private void CleanupFailedInstance(UIPageInstance instance)
        {
            UIReleaseResult releaseResult = pageInstanceReleaser.CleanupFailedInstance(instance);
            if (releaseResult.PresentationDirty)
            {
                presentationCoordinator.Commit();
            }
        }

        /// <summary>
        /// 释放页面并在必要时提交显示状态。
        /// reason 由调用方传入，方便 Releaser 在日志中区分关闭、失败清理和 Manager 销毁等路径。
        /// </summary>
        private void ReleaseInstance(UIPageInstance instance, UIReleaseReason reason)
        {
            UIReleaseResult releaseResult = pageInstanceReleaser.ReleaseInstance(instance, reason);
            if (releaseResult.PresentationDirty)
            {
                presentationCoordinator.Commit();
            }
        }

        /// <summary>
        /// 提供给 UISceneScopeCoordinator 的实例快照。
        /// 该接口保持只读查询语义，SceneScope 批量释放会先收集 PageId 再逐个 Close。
        /// </summary>
        List<UIPageInstance> IUIPageInstanceQuery.GetSnapshot()
        {
            return instanceRegistry.GetSnapshot();
        }

        /// <summary>
        /// 提供给 UISceneScopeCoordinator 的实例查询。
        /// 用于判断 GlobalScope 是否需要携带 SceneScopeId 关闭请求。
        /// </summary>
        bool IUIPageInstanceQuery.TryGet(string pageId, out UIPageInstance instance)
        {
            return instanceRegistry.TryGet(pageId, out instance);
        }

        private void TickUpdate(float deltaTime, float unscaledDeltaTime)
        {
            List<UIPageInstance> pages = instanceRegistry.GetSnapshot();
            for (int i = 0; i < pages.Count; i++)
            {
                UIPageInstance instance = pages[i];
                if (ShouldTick(instance, true))
                {
                    instance.Controller.OnTick(deltaTime, unscaledDeltaTime);
                }
            }
        }

        private void TickLateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            List<UIPageInstance> pages = instanceRegistry.GetSnapshot();
            for (int i = 0; i < pages.Count; i++)
            {
                UIPageInstance instance = pages[i];
                if (ShouldTick(instance, false))
                {
                    instance.Controller.OnLateTick(deltaTime, unscaledDeltaTime);
                }
            }
        }

        /// <summary>
        /// 推进 Notice 生命周期。
        /// Notice 属于轻量表现，不允许单个 prefab 或坐标异常打断页面 Tick，因此这里做 best-effort 保护。
        /// </summary>
        private void TickNoticeSafe(float unscaledDeltaTime)
        {
            if (noticeService == null)
            {
                return;
            }

            try
            {
                noticeService.Tick(unscaledDeltaTime);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        private static bool ShouldTick(UIPageInstance instance, bool update)
        {
            if (instance == null ||
                instance.Controller == null ||
                instance.Definition == null ||
                !instance.IsOpenAndStackVisible)
            {
                return false;
            }

            if (instance.IsPaused && !instance.Definition.UpdateWhenPaused)
            {
                return false;
            }

            return update ? instance.Definition.EnableUpdate : instance.Definition.EnableLateUpdate;
        }

    }
}
