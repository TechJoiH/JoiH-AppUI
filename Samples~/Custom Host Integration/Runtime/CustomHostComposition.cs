using System;
using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI.Samples.CustomHost
{
    /// <summary>
    /// Complete sample composition root. It owns adapters and optional
    /// strategies, while AppUIRuntimeHost remains the AppUI lifecycle boundary.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CustomHostInstaller : MonoBehaviour
    {
        [SerializeField]
        private AppUIRuntimeHost runtimeHost;

        [SerializeField]
        private List<CustomHostAssetEntry> assets =
            new List<CustomHostAssetEntry>();

        [SerializeField]
        private bool enableSamplePooling;

        private CustomHostOperationFactory operationFactory;
        private CustomHostExecutionContext executionContext;
        private CustomHostAssetProvider assetProvider;
        private CustomHostPooledInstanceStrategy pooledInstanceStrategy;
        private bool shuttingDown;

        public bool IsInitialized =>
            runtimeHost != null && runtimeHost.IsInitialized;

        public IUIService UI
        {
            get
            {
                if (!IsInitialized)
                {
                    throw new InvalidOperationException(
                        "The Custom Host sample is not initialized.");
                }

                return runtimeHost.Manager.Service;
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        public AppUIInitializationResult Initialize()
        {
            if (runtimeHost == null)
            {
                runtimeHost = GetComponent<AppUIRuntimeHost>();
            }

            if (runtimeHost == null)
            {
                return AppUIInitializationResult.Failure(
                    AppUIInitializationStatus.MissingManager,
                    new InvalidOperationException(
                        "CustomHostInstaller requires AppUIRuntimeHost."));
            }

            if (runtimeHost.IsInitialized)
            {
                return AppUIInitializationResult.AlreadyInitialized();
            }

            operationFactory = new CustomHostOperationFactory();
            executionContext =
                CustomHostExecutionContext.CaptureCurrent();
            assetProvider = new CustomHostAssetProvider(
                operationFactory,
                assets);

            AppUIRuntimeConfiguration configuration =
                AppUIRuntimeConfiguration.Empty;
            if (enableSamplePooling)
            {
                pooledInstanceStrategy =
                    new CustomHostPooledInstanceStrategy();
                configuration = new AppUIRuntimeConfiguration(
                    null,
                    new IUIPageInstanceStrategy[]
                    {
                        pooledInstanceStrategy,
                    });
            }

            AppUIInitializationResult result = runtimeHost.Initialize(
                new AppUIRuntimeDependencies(
                    operationFactory,
                    assetProvider,
                    executionContext),
                configuration);
            if (!result.Success)
            {
                DisposeOwnedAdapters();
                Debug.LogError(
                    "<Joi.H.AppUI.Sample> Custom host initialization failed: " +
                    result.Status,
                    this);
            }

            return result;
        }

        public void Shutdown()
        {
            if (shuttingDown)
            {
                return;
            }

            shuttingDown = true;
            try
            {
                // AppUI first returns active page allocations. The pool then
                // evicts retained instances and leases. The provider is last.
                if (runtimeHost != null && runtimeHost.IsInitialized)
                {
                    runtimeHost.Shutdown();
                }

                DisposeOwnedAdapters();
            }
            finally
            {
                shuttingDown = false;
            }
        }

        private void DisposeOwnedAdapters()
        {
            pooledInstanceStrategy?.Dispose();
            pooledInstanceStrategy = null;
            assetProvider?.Dispose();
            assetProvider = null;
            executionContext = null;
            operationFactory = null;
        }
    }

    /// <summary>
    /// Explicit bridge called by the host's own scene/procedure system. It has
    /// no OnEnable/OnDisable discovery and never polls Unity scene state.
    /// </summary>
    public sealed class CustomHostSceneBridge : MonoBehaviour
    {
        [SerializeField]
        private CustomHostInstaller installer;

        [SerializeField]
        private SceneUIBinding sceneBinding;

        public IUIOperation<UISceneBindResult> NotifySceneReady()
        {
            RequireReferences();
            return sceneBinding.Bind(installer.UI);
        }

        public IUIOperation<UISceneExitResult> NotifySceneLeaving()
        {
            RequireReferences();
            return sceneBinding.Unbind(installer.UI);
        }

        private void RequireReferences()
        {
            if (installer == null || sceneBinding == null)
            {
                throw new InvalidOperationException(
                    "CustomHostSceneBridge requires installer and SceneUIBinding references.");
            }
        }
    }

    /// <summary>
    /// Host-side query used before executing world pointer actions.
    /// AppUI reports blocking state; the host remains the input authority.
    /// </summary>
    public static class CustomHostWorldInputGate
    {
        public static bool CanProcessWorldInput(
            Vector2 screenPosition,
            AppUIInputChannel channel)
        {
            return !AppUIInputHitResolver.Shared.IsPointerBlocked(
                screenPosition,
                channel);
        }
    }
}
