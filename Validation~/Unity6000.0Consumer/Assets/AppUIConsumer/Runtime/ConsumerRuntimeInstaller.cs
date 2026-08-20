using System;
using UnityEngine;

namespace Joi.H.AppUI.Validation.Consumer
{
    [DefaultExecutionOrder(-200)]
    public sealed class ConsumerRuntimeInstaller : MonoBehaviour
    {
        public const string BasicPageId = "consumer.basic";
        public const string PopupPageId = "consumer.popup";
        public const string BindingPageId = "consumer.binding";
        public const string FocusPageId = "consumer.focus";
        public const string BasicAssetId =
            "Assets/AppUIConsumerGenerated/Prefabs/BasicPage.prefab";
        public const string PopupAssetId =
            "Assets/AppUIConsumerGenerated/Prefabs/Popup.prefab";
        public const string BindingAssetId =
            "Assets/AppUIConsumerGenerated/Prefabs/BindingPage.prefab";
        public const string FocusAssetId =
            "Assets/AppUIConsumerGenerated/Prefabs/FocusList.prefab";
        public const string NoticeAssetId =
            "Assets/AppUIConsumerGenerated/Prefabs/Notice.prefab";

        [SerializeField]
        private AppUIRuntimeHost runtimeHost;

        [SerializeField]
        private GameObject basicPagePrefab;

        [SerializeField]
        private GameObject popupPagePrefab;

        [SerializeField]
        private GameObject bindingPagePrefab;

        [SerializeField]
        private GameObject focusPagePrefab;

        [SerializeField]
        private GameObject noticePrefab;

        private ConsumerAssetProvider assetProvider;

        public AppUIRuntimeHost Host
        {
            get { return runtimeHost; }
        }

        public AppUIManager Manager
        {
            get { return runtimeHost != null ? runtimeHost.Manager : null; }
        }

        public ConsumerAssetProvider AssetProvider
        {
            get { return assetProvider; }
        }

        private void Awake()
        {
            InitializeForValidation();
        }

        public void Configure(
            AppUIRuntimeHost host,
            GameObject basicPrefab,
            GameObject popupPrefab,
            GameObject bindingPrefab,
            GameObject focusPrefab,
            GameObject authoredNoticePrefab)
        {
            runtimeHost = host;
            basicPagePrefab = basicPrefab;
            popupPagePrefab = popupPrefab;
            bindingPagePrefab = bindingPrefab;
            focusPagePrefab = focusPrefab;
            noticePrefab = authoredNoticePrefab;
        }

        public AppUIInitializationResult InitializeForValidation()
        {
            if (runtimeHost == null)
            {
                runtimeHost = GetComponent<AppUIRuntimeHost>();
            }

            if (runtimeHost == null)
            {
                throw new InvalidOperationException(
                    "Consumer AppUIRuntimeHost is missing.");
            }

            if (runtimeHost.IsInitialized)
            {
                return AppUIInitializationResult.AlreadyInitialized();
            }

            ConsumerOperationFactory operationFactory =
                new ConsumerOperationFactory();
            assetProvider = new ConsumerAssetProvider(operationFactory);
            RegisterRequiredAsset(BasicAssetId, basicPagePrefab);
            RegisterRequiredAsset(PopupAssetId, popupPagePrefab);
            RegisterRequiredAsset(BindingAssetId, bindingPagePrefab);
            RegisterRequiredAsset(FocusAssetId, focusPagePrefab);
            RegisterRequiredAsset(NoticeAssetId, noticePrefab);
            AppUIInitializationResult result = runtimeHost.Initialize(
                new AppUIRuntimeDependencies(
                    operationFactory,
                    assetProvider,
                    ConsumerExecutionContext.CaptureCurrent()));
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    "Consumer AppUI initialization failed: " + result.Status,
                    result.Exception);
            }

            return result;
        }

        private void RegisterRequiredAsset(string assetId, GameObject prefab)
        {
            if (!assetProvider.Register(assetId, prefab))
            {
                throw new InvalidOperationException(
                    "Consumer page asset is missing: " + assetId);
            }
        }
    }
}
