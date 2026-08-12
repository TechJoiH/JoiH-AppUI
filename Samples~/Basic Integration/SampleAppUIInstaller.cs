using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Joi.H.AppUI.Samples.Basic
{
    [Serializable]
    public sealed class SampleUIAssetEntry
    {
        public string AssetId = string.Empty;
        public UnityObject Asset;
    }

    /// <summary>
    /// Minimal consumer composition root. The sample keeps the asset provider
    /// local, while the project explicitly supplies its operation factory and
    /// Unity execution context.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class SampleAppUIInstaller : MonoBehaviour
    {
        [SerializeField]
        private AppUIRuntimeHost runtimeHost;

        [SerializeField]
        private List<SampleUIAssetEntry> assets =
            new List<SampleUIAssetEntry>();

        private void Awake()
        {
            if (runtimeHost == null)
            {
                runtimeHost = GetComponent<AppUIRuntimeHost>();
            }

            if (runtimeHost == null)
            {
                Debug.LogError(
                    "<Joi.H.AppUI.Sample> AppUIRuntimeHost is missing.",
                    this);
                return;
            }

            CallbackUIOperationFactory operationFactory =
                new CallbackUIOperationFactory();
            UnityMainThreadExecutionContext executionContext =
                UnityMainThreadExecutionContext.CaptureCurrent();
            InMemoryUIAssetProvider assetProvider =
                new InMemoryUIAssetProvider(operationFactory, assets);
            AppUIInitializationResult result = runtimeHost.Initialize(
                new AppUIRuntimeDependencies(
                    operationFactory,
                    assetProvider,
                    executionContext));
            if (!result.Success)
            {
                Debug.LogError(
                    "<Joi.H.AppUI.Sample> Initialization failed: " +
                    result.Status,
                    this);
            }
        }
    }

}
