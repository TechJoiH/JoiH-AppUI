using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Scene composition root for an AppUI runtime.
    /// Projects may inject any IUIAssetProvider before initialization; otherwise the
    /// built-in Resources provider can be used as a dependency-free default.
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

        [SerializeField]
        private bool initializeOnAwake = true;

        [SerializeField]
        private bool useResourcesProviderWhenMissing = true;

        private IUIAssetProvider assetProvider;
        private bool initialized;

        public AppUIManager Manager
        {
            get { return uiManager; }
        }

        public IUIAssetProvider AssetProvider
        {
            get { return assetProvider; }
        }

        public bool IsInitialized
        {
            get { return initialized; }
        }

        private void Awake()
        {
            ResolveSceneReferences();
            if (initializeOnAwake)
            {
                Initialize(null);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveSceneReferences();
        }
#endif

        /// <summary>
        /// Initializes the runtime once. Pass a project provider explicitly, or null to
        /// use ResourcesUIAssetProvider when the serialized fallback is enabled.
        /// </summary>
        public bool Initialize(IUIAssetProvider provider)
        {
            if (initialized)
            {
                if (provider != null && !ReferenceEquals(provider, assetProvider))
                {
                    SetAssetProvider(provider);
                }

                return true;
            }

            ResolveSceneReferences();
            if (uiManager == null)
            {
                Debug.LogError(
                    "<Joi.H.AppUI> AppUIRuntimeHost requires AppUIManager.",
                    this);
                return false;
            }

            UIPageDefinitionRegistry resolvedRegistry =
                profile != null && profile.PageRegistry != null
                    ? profile.PageRegistry
                    : pageRegistry;
            if (resolvedRegistry == null)
            {
                Debug.LogError(
                    "<Joi.H.AppUI> AppUIRuntimeHost requires a page registry.",
                    this);
                return false;
            }

            assetProvider = provider ??
                (useResourcesProviderWhenMissing
                    ? new ResourcesUIAssetProvider()
                    : null);
            UILayerSettings resolvedLayerSettings =
                profile != null && profile.LayerSettings != null
                    ? profile.LayerSettings
                    : layerSettings;
            AppUINoticeSettings resolvedNoticeSettings =
                profile != null
                    ? profile.NoticeSettings
                    : noticeSettings;

            uiManager.Initialize(
                resolvedRegistry,
                assetProvider,
                layerRoots,
                resolvedLayerSettings,
                resolvedNoticeSettings);
            initialized = true;
            return true;
        }

        public void SetAssetProvider(IUIAssetProvider provider)
        {
            if (provider == null)
            {
                Debug.LogError(
                    "<Joi.H.AppUI> Asset provider cannot be null.",
                    this);
                return;
            }

            assetProvider = provider;
            uiManager?.SetAssetProvider(provider);
        }

        public void Shutdown()
        {
            if (!initialized)
            {
                return;
            }

            uiManager?.ClearAssetProvider();
            assetProvider = null;
            initialized = false;
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
