using System;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Scene composition root for an AppUI runtime.
    /// The integrating project must explicitly provide every runtime dependency.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class AppUIRuntimeHost : MonoBehaviour
    {
        [SerializeField]
        private AppUIManager uiManager;

        [SerializeField]
        private GlobalUIRoot globalRoot;

        [SerializeField]
        private AppUIRuntimeProfile profile;

        [SerializeField]
        private UIPageDefinitionRegistry pageRegistry;

        [SerializeField]
        private UILayerRoot[] layerRoots;

        [SerializeField]
        private UILayerSettings layerSettings;

        [SerializeField]
        private AppUINoticeSettings noticeSettings;

        private AppUIRuntimeDependencies dependencies;
        private AppUIRuntimeConfiguration configuration;
        private bool initialized;

        public AppUIManager Manager
        {
            get { return uiManager; }
        }

        public AppUIRuntimeDependencies Dependencies
        {
            get { return dependencies; }
        }

        public AppUIRuntimeConfiguration Configuration
        {
            get { return initialized ? configuration : null; }
        }

        public IUIAssetProvider AssetProvider
        {
            get
            {
                return dependencies != null
                    ? dependencies.AssetProvider
                    : null;
            }
        }

        public bool IsInitialized
        {
            get { return initialized; }
        }

        private void Awake()
        {
            ResolveSceneReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveSceneReferences();
        }
#endif

        /// <summary>
        /// Initializes AppUI exactly once with project-owned dependencies.
        /// Missing dependencies return a structured failure and never trigger fallback.
        /// </summary>
        public AppUIInitializationResult Initialize(
            AppUIRuntimeDependencies runtimeDependencies)
        {
            return Initialize(
                runtimeDependencies,
                AppUIRuntimeConfiguration.Empty);
        }

        /// <summary>
        /// Initializes AppUI with required ports and an immutable optional
        /// strategy snapshot. Configuration validation completes before the
        /// manager receives any runtime dependency.
        /// </summary>
        public AppUIInitializationResult Initialize(
            AppUIRuntimeDependencies runtimeDependencies,
            AppUIRuntimeConfiguration runtimeConfiguration)
        {
            AppUIRuntimeConfiguration resolvedConfiguration =
                runtimeConfiguration ?? AppUIRuntimeConfiguration.Empty;
            if (initialized)
            {
                if (!ReferenceEquals(dependencies, runtimeDependencies))
                {
                    return AppUIInitializationResult.Failure(
                        AppUIInitializationStatus
                            .AlreadyInitializedWithDifferentDependencies);
                }

                return ReferenceEquals(configuration, resolvedConfiguration)
                    ? AppUIInitializationResult.AlreadyInitialized()
                    : AppUIInitializationResult.Failure(
                        AppUIInitializationStatus
                            .AlreadyInitializedWithDifferentConfiguration);
            }

            ResolveSceneReferences();
            AppUIInitializationResult validation =
                ValidateInitialization(runtimeDependencies);
            if (!validation.Success)
            {
                return validation;
            }

            UIPageDefinitionRegistry resolvedRegistry = ResolveRegistry();
            UILayerSettings resolvedLayerSettings =
                profile != null && profile.LayerSettings != null
                    ? profile.LayerSettings
                    : layerSettings;
            AppUINoticeSettings resolvedNoticeSettings =
                profile != null
                    ? profile.NoticeSettings
                    : noticeSettings;

            try
            {
                AppUIInitializationResult managerResult =
                    uiManager.Initialize(
                    resolvedRegistry,
                    runtimeDependencies,
                    layerRoots,
                    resolvedLayerSettings,
                    resolvedNoticeSettings,
                    resolvedConfiguration);
                if (!managerResult.Success)
                {
                    return managerResult;
                }

                dependencies = runtimeDependencies;
                configuration = resolvedConfiguration;
                initialized = true;
                return AppUIInitializationResult.Ok();
            }
            catch (Exception exception)
            {
                uiManager.Shutdown();
                dependencies = null;
                configuration = null;
                initialized = false;
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.DependencyContractFailed,
                    exception);
            }
        }

        /// <summary>
        /// Stops the runtime and releases the injected dependency references.
        /// A later explicit Initialize call may provide a new dependency set.
        /// </summary>
        public void Shutdown()
        {
            if (!initialized)
            {
                dependencies = null;
                return;
            }

            uiManager?.Shutdown();
            dependencies = null;
            configuration = null;
            initialized = false;
        }

        private AppUIInitializationResult ValidateInitialization(
            AppUIRuntimeDependencies runtimeDependencies)
        {
            if (runtimeDependencies == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingDependencies);
            }

            if (runtimeDependencies.OperationFactory == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingOperationFactory);
            }

            if (runtimeDependencies.AssetProvider == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingAssetProvider);
            }

            if (runtimeDependencies.ExecutionContext == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingExecutionContext);
            }

            if (uiManager == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingManager);
            }

            if (ResolveRegistry() == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingRegistry);
            }

            return AppUIInitializationResult.Ok();
        }

        private UIPageDefinitionRegistry ResolveRegistry()
        {
            return profile != null && profile.PageRegistry != null
                ? profile.PageRegistry
                : pageRegistry;
        }

        private void ResolveSceneReferences()
        {
            if (globalRoot == null)
            {
                globalRoot = GetComponent<GlobalUIRoot>();
            }

            if (uiManager == null)
            {
                uiManager = globalRoot != null
                    ? globalRoot.UIManager
                    : GetComponent<AppUIManager>();
            }

            if (layerRoots == null || layerRoots.Length == 0)
            {
                layerRoots = globalRoot != null
                    ? globalRoot.LayerRoots
                    : GetComponentsInChildren<UILayerRoot>(true);
            }
        }
    }
}
