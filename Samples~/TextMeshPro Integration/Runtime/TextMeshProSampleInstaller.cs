using System;
using System.Collections.Generic;
using Joi.H.AppUI;
using Joi.H.AppUI.Integrations.TextMeshPro;
using UnityEngine;

namespace Joi.H.AppUI.Samples.TextMeshPro
{
    [DefaultExecutionOrder(-200)]
    public sealed class TextMeshProSampleInstaller : MonoBehaviour
    {
        public const string PageId = "sample.textmeshpro";
        public const string PageAssetId = "sample.textmeshpro.page";
        public const string NoticeAssetId = "sample.textmeshpro.notice";

        [SerializeField] private AppUIRuntimeHost runtimeHost;
        [SerializeField] private List<TextMeshProSampleAssetEntry> assets =
            new List<TextMeshProSampleAssetEntry>();

        public AppUIRuntimeHost Host => runtimeHost;

        private void Awake()
        {
            Initialize();
        }

        public AppUIInitializationResult Initialize()
        {
            if (runtimeHost == null) runtimeHost = GetComponent<AppUIRuntimeHost>();
            if (runtimeHost == null)
                throw new InvalidOperationException("TextMeshPro sample requires AppUIRuntimeHost.");
            if (runtimeHost.IsInitialized) return AppUIInitializationResult.AlreadyInitialized();

            TextMeshProSampleOperationFactory operationFactory =
                new TextMeshProSampleOperationFactory();
            AppUIRuntimeConfiguration configuration = new AppUIRuntimeConfiguration(
                null,
                null,
                new IAppUIFocusControlPolicyResolver[]
                {
                    new TextMeshProInputFieldPolicyResolver(),
                });
            AppUIInitializationResult result = runtimeHost.Initialize(
                new AppUIRuntimeDependencies(
                    operationFactory,
                    new TextMeshProSampleAssetProvider(operationFactory, assets),
                    TextMeshProSampleExecutionContext.CaptureCurrent()),
                configuration);
            if (!result.Success)
                throw new InvalidOperationException("TMP sample initialization failed: " + result.Status, result.Exception);
            return result;
        }
    }
}
