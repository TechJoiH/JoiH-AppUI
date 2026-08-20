using System;
using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// App UI 运行时门面。
    /// 公开 Open/Close/Refresh/SceneScope API 保持集中在这里，具体 Operation、Presentation、Layer 配置和释放协议交给内部 coordinator。
    /// </summary>
    public sealed partial class AppUIManager : MonoBehaviour, IUIControllerService, INoticeServiceProvider, IUISceneCommandExecutor, IUIPageInstanceQuery
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
        private readonly UISceneScopeGenerationRegistry sceneScopeGenerations =
            new UISceneScopeGenerationRegistry();
        private readonly Dictionary<string, IUILoadStrategy> loadStrategies =
            new Dictionary<string, IUILoadStrategy>(4);
        private readonly Dictionary<string, IUIPageInstanceStrategy>
            instanceStrategies =
                new Dictionary<string, IUIPageInstanceStrategy>(4);

        private IUIAssetProvider assetProvider;
        private IUIOperationFactory operationFactory;
        private IAppUIExecutionContext executionContext;
        private int runtimeEpoch;
        private IUILoadStrategy defaultLoadStrategy;
        private IUIPageInstanceStrategy defaultInstanceStrategy;
        private bool initialized;
        private UIOperationCoordinator operationCoordinator;
        private UISceneScopeCoordinator sceneScopeCoordinator;
        private UIPresentationCoordinator presentationCoordinator;
        private UILayerRuntimeConfigurator layerRuntimeConfigurator;
        private UIPageInstanceReleaser pageInstanceReleaser;
        private NoticeService noticeService;

        public IUIService Service
        {
            get
            {
                RequireInitialized();
                return this;
            }
        }

        /// <summary>
        /// 当前 UI Runtime 的 Notice 服务。
        /// 业务侧通过独立 INoticeService 使用轻量提示，ControllerContext 也会注入同一个实例。
        /// </summary>
        public INoticeService Notices
        {
            get
            {
                RequireInitialized();
                EnsureRuntimeServices();
                return noticeService;
            }
        }

        public bool HasAssetProvider
        {
            get { return assetProvider != null; }
        }

        internal IUIOperationFactory OperationFactory
        {
            get { return operationFactory; }
        }

        internal IAppUIExecutionContext ExecutionContext
        {
            get { return executionContext; }
        }

        internal int RuntimeEpoch
        {
            get { return runtimeEpoch; }
        }

        /// <summary>
        /// Initializes the manager from the explicit host dependency set.
        /// </summary>
        public AppUIInitializationResult Initialize(
            UIPageDefinitionRegistry registry,
            AppUIRuntimeDependencies dependencies,
            UILayerRoot[] roots,
            UILayerSettings settings,
            AppUINoticeSettings appNoticeSettings,
            AppUIRuntimeConfiguration configuration)
        {
            if (dependencies == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingDependencies);
            }

            if (dependencies.OperationFactory == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingOperationFactory);
            }

            if (dependencies.AssetProvider == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingAssetProvider);
            }

            if (dependencies.ExecutionContext == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingExecutionContext);
            }

            AppUIRuntimeConfiguration resolvedConfiguration =
                configuration ?? AppUIRuntimeConfiguration.Empty;
            AppUIInitializationResult configurationValidation =
                resolvedConfiguration.Validate(registry);
            if (!configurationValidation.Success)
            {
                return configurationValidation;
            }

            operationFactory = dependencies.OperationFactory;
            executionContext = dependencies.ExecutionContext;
            focusService.ConfigureExecutionContext(executionContext);
            runtimeEpoch++;
            pageRegistry = registry;
            assetProvider = dependencies.AssetProvider;
            layerRoots = roots;
            if (settings != null)
            {
                layerSettings = settings;
            }

            if (appNoticeSettings != null)
            {
                noticeSettings = appNoticeSettings;
            }

            try
            {
                AppUIInitializationResult initialization =
                    InitializeInternal(resolvedConfiguration);
                if (initialization.Success)
                {
                    return initialization;
                }

                ClearPartialInitialization();
                ClearRuntimeDependencies();
                return initialization;
            }
            catch
            {
                ClearPartialInitialization();
                ClearRuntimeDependencies();
                throw;
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
            ShutdownRuntime();
        }

        /// <summary>
        /// Stops this manager without waiting for an external asynchronous backend.
        /// </summary>
        public void Shutdown()
        {
            ShutdownRuntime();
        }

        private void ShutdownRuntime()
        {
            runtimeEpoch++;
            if (!initialized)
            {
                ClearPartialInitialization();
                ClearRuntimeDependencies();
                return;
            }

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
            sceneScopeGenerations.Clear();
            presentationCoordinator.Clear();
            noticeService.Dispose();
            initialized = false;
            loadStrategies.Clear();
            instanceStrategies.Clear();
            defaultLoadStrategy = null;
            defaultInstanceStrategy = null;
            sceneScopeCoordinator = null;
            presentationCoordinator = null;
            layerRuntimeConfigurator = null;
            pageInstanceReleaser = null;
            noticeService = null;
            ClearRuntimeDependencies();
        }

        private void ClearPartialInitialization()
        {
            try
            {
                noticeService?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }

            try
            {
                presentationCoordinator?.Clear();
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }

            instanceRegistry.Clear();
            sceneScopeGenerations.Clear();
            loadStrategies.Clear();
            instanceStrategies.Clear();
            defaultLoadStrategy = null;
            defaultInstanceStrategy = null;
            operationCoordinator = null;
            sceneScopeCoordinator = null;
            presentationCoordinator = null;
            layerRuntimeConfigurator = null;
            pageInstanceReleaser = null;
            noticeService = null;
        }

        private void ClearRuntimeDependencies()
        {
            operationFactory = null;
            executionContext = null;
            assetProvider = null;
        }

        private AppUIInitializationResult InitializeInternal(
            AppUIRuntimeConfiguration configuration)
        {
            EnsureRuntimeServices();
            defaultLoadStrategy = new DefaultUILoadStrategy();
            defaultInstanceStrategy = new DefaultUIPageInstanceStrategy();
            loadStrategies.Add(
                defaultLoadStrategy.StrategyId,
                defaultLoadStrategy);
            instanceStrategies.Add(
                defaultInstanceStrategy.StrategyId,
                defaultInstanceStrategy);
            for (int i = 0; i < configuration.LoadStrategies.Count; i++)
            {
                IUILoadStrategy strategy = configuration.LoadStrategies[i];
                loadStrategies.Add(strategy.StrategyId, strategy);
            }

            for (int i = 0;
                 i < configuration.InstanceStrategies.Count;
                 i++)
            {
                IUIPageInstanceStrategy strategy =
                    configuration.InstanceStrategies[i];
                instanceStrategies.Add(strategy.StrategyId, strategy);
            }

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
            AppUIInitializationResult noticeInitialization =
                ConfigureNoticeService();
            if (!noticeInitialization.Success)
            {
                return noticeInitialization;
            }

            ValidateConfiguration();
            initialized = true;
            return AppUIInitializationResult.Ok();
        }

        public IUIOperation<UISceneBindResult> BindScene(
            SceneUIBindingData bindingData)
        {
            RequireInitialized();
            EnsureRuntimeServices();
            return sceneScopeCoordinator.BindScene(bindingData);
        }

        public IUIOperation<UISceneExitResult> UnbindScene(
            SceneUIBindingData bindingData)
        {
            RequireInitialized();
            EnsureRuntimeServices();
            UISceneScopeStamp retired =
                sceneScopeCoordinator.InvalidateSceneScope(bindingData);
            operationCoordinator.CancelOpenOperations(
                operation => IsOpenOperationInSceneLifetime(
                    operation.PageId,
                    operation.Args.SceneScopeStamp,
                    retired),
                intent => IsOpenOperationInSceneLifetime(
                    intent.PageId,
                    intent.OpenArgs.SceneScopeStamp,
                    retired));
            IUIOperation<UISceneExitResult> operation =
                sceneScopeCoordinator.UnbindScene(bindingData, retired);
            string sceneScopeId =
                UISceneScopeCoordinator.ResolveSceneScopeId(bindingData);
            UIOperationObserver.Observe(
                operation,
                executionContext,
                _ =>
                {
                    noticeService.ClearScope(
                        UIPageScope.SceneScope,
                        sceneScopeId);
                    noticeService.ClearScope(
                        UIPageScope.TemporaryScope,
                        sceneScopeId);
                });
            return operation;
        }

        public IUIOperation<UIScopeReleaseResult> ReleaseScope(
            UIPageScope scope,
            string sceneScopeId)
        {
            RequireInitialized();
            EnsureRuntimeServices();
            string normalized =
                UISceneScopeCoordinator.NormalizeSceneScopeId(sceneScopeId);
            UISceneScopeStamp sceneScopeStamp = scope == UIPageScope.GlobalScope
                ? sceneScopeCoordinator.GetCurrentSceneScopeStamp(normalized)
                : sceneScopeCoordinator.InvalidateSceneScope(normalized);
            if (scope != UIPageScope.GlobalScope)
            {
                operationCoordinator.CancelOpenOperations(
                    operation =>
                        !instanceRegistry.TryGet(
                            operation.PageId,
                            out UIPageInstance _) &&
                        IsOpenOperationInScope(
                            operation.PageId,
                            operation.SceneScopeId,
                            operation.Args.SceneScopeStamp,
                            scope,
                            normalized,
                            sceneScopeStamp),
                    intent => IsOpenOperationInScope(
                        intent.PageId,
                        intent.OpenArgs.SceneScopeId,
                        intent.OpenArgs.SceneScopeStamp,
                        scope,
                        normalized,
                        sceneScopeStamp));
            }

            IUIOperation<UIScopeReleaseResult> operation =
                sceneScopeCoordinator.ReleaseScope(
                    scope,
                    sceneScopeId,
                    sceneScopeStamp);
            if (scope != UIPageScope.GlobalScope)
            {
                UIOperationObserver.Observe(
                    operation,
                    executionContext,
                    _ => noticeService.ClearScope(scope, normalized));
            }

            return operation;
        }

        private bool IsOpenOperationInScope(
            string pageId,
            string operationSceneScopeId,
            UISceneScopeStamp operationStamp,
            UIPageScope scope,
            string normalizedSceneScopeId,
            UISceneScopeStamp targetStamp)
        {
            return pageRegistry != null &&
                   pageRegistry.TryGet(pageId, out UIPageDefinition definition) &&
                   definition != null &&
                   definition.Scope == scope &&
                   string.Equals(
                       UISceneScopeCoordinator.NormalizeSceneScopeId(
                            operationSceneScopeId),
                        normalizedSceneScopeId,
                        StringComparison.Ordinal) &&
                   operationStamp.IsCompatibleWith(targetStamp);
        }

        private bool IsOpenOperationInSceneLifetime(
            string pageId,
            UISceneScopeStamp operationStamp,
            UISceneScopeStamp retired)
        {
            return pageRegistry != null &&
                   pageRegistry.TryGet(
                       pageId,
                       out UIPageDefinition definition) &&
                   definition != null &&
                   definition.Scope != UIPageScope.GlobalScope &&
                   operationStamp.IsCompatibleWith(retired);
        }

        public IUIOperation<UICancelResult> Cancel()
        {
            RequireInitialized();
            return BeginCancel();
        }

        public IUIOperation<UICloseResult> CloseTop()
        {
            RequireInitialized();
            EnsureRuntimeServices();
            if (presentationCoordinator.TryGetTopVisiblePage(
                    out UIPageInstance instance) &&
                instance != null)
            {
                return Close(instance.PageId);
            }

            return CreateCompletedOperation(
                "CloseTop",
                UICloseResult.Fail(
                    string.Empty,
                    UIPageState.None,
                    UICloseError.NotOpen));
        }

        public IUIOperation<UICloseResult> CloseTop(UILayerId layerId)
        {
            RequireInitialized();
            EnsureRuntimeServices();
            if (presentationCoordinator.TryGetTopVisiblePage(
                    layerId,
                    out UIPageInstance instance) &&
                instance != null)
            {
                return Close(instance.PageId);
            }

            return CreateCompletedOperation(
                "CloseTop:" + layerId,
                UICloseResult.Fail(
                    string.Empty,
                    UIPageState.None,
                    UICloseError.NotOpen));
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
                sceneScopeCoordinator = new UISceneScopeCoordinator(
                    this,
                    this,
                    operationFactory,
                    executionContext,
                    sceneScopeGenerations);
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
                    presentationCoordinator.ResetInstancePresentationState);
            }
        }

        /// <summary>
        /// 将 NoticeService 绑定到当前 NoticeLayer。
        /// 该方法在 Manager 初始化和资源服务重新注入时调用，保证池对象挂载到当前 Runtime Root 下。
        /// </summary>
        private AppUIInitializationResult ConfigureNoticeService()
        {
            EnsureRuntimeServices();
            RectTransform noticeRoot = null;
            if (layerController.TryGetRoot(UILayerId.NoticeLayer, out UILayerRoot layerRoot) &&
                layerRoot != null)
            {
                noticeRoot = layerRoot.ContentRoot;
            }

            return noticeService.Initialize(
                noticeRoot,
                assetProvider,
                noticeSettings ?? AppUINoticeSettings.CreateDefault(),
                runtimeEpoch);
        }

        private bool EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException(
                    "<Joi.H.AppUI> Runtime is not initialized. " +
                    "Initialize AppUIRuntimeHost with explicit dependencies first.");
            }

            return pageRegistry != null;
        }

        private void RequireInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException(
                    "<Joi.H.AppUI> Runtime is not initialized. " +
                    "Initialize AppUIRuntimeHost with explicit dependencies first.");
            }
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
            if (string.IsNullOrEmpty(strategyId))
            {
                return defaultLoadStrategy;
            }

            if (loadStrategies.TryGetValue(
                    strategyId,
                    out IUILoadStrategy strategy))
            {
                return strategy;
            }

            throw new InvalidOperationException(
                "<Joi.H.AppUI> Unknown load StrategyId after initialization: " +
                strategyId);
        }

        private IUIPageInstanceStrategy ResolveInstanceStrategy(
            string strategyId)
        {
            if (string.IsNullOrEmpty(strategyId))
            {
                return defaultInstanceStrategy;
            }

            if (instanceStrategies.TryGetValue(
                    strategyId,
                    out IUIPageInstanceStrategy strategy))
            {
                return strategy;
            }

            throw new InvalidOperationException(
                "<Joi.H.AppUI> Unknown instance StrategyId after initialization: " +
                strategyId);
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
