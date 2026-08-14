using System;
using System.Collections.Generic;
using System.Reflection;
using Joi.H.AppUI.TestKit;
using UnityEngine;

namespace Joi.H.AppUI.Samples.Basic.Tests
{
    public sealed class CallbackOperationContractTests :
        AppUIOperationFactoryContractFixture
    {
        protected override IUIOperationFactory CreateOperationFactory()
        {
            return new CallbackUIOperationFactory();
        }
    }

    public sealed class InMemoryAssetProviderContractTests :
        AppUIAssetProviderContractFixture
    {
        protected override AppUIAssetProviderContractContext
            CreateAssetContext()
        {
            GameObject asset = new GameObject("HostContractAsset");
            CallbackUIOperationFactory factory =
                new CallbackUIOperationFactory();
            InMemoryUIAssetProvider provider =
                new InMemoryUIAssetProvider(factory);
            provider.Register("contract/page", asset);
            return new AppUIAssetProviderContractContext(
                provider,
                "contract/page",
                asset,
                () => UnityEngine.Object.DestroyImmediate(asset));
        }
    }

    public sealed class UnityExecutionContextContractTests :
        AppUIExecutionContextContractFixture
    {
        protected override IAppUIExecutionContext CreateExecutionContext()
        {
            return UnityMainThreadExecutionContext.CaptureCurrent();
        }
    }

    public sealed class SampleHostLifecycleContractTests :
        AppUIHostLifecycleContractFixture
    {
        protected override IAppUIHostLifecycleContractDriver CreateDriver()
        {
            return new SampleLifecycleDriver();
        }

        private sealed class SampleLifecycleDriver :
            IAppUIHostLifecycleContractDriver
        {
            private readonly GameObject root;
            private readonly AppUIRuntimeHost host;
            private readonly UIPageDefinitionRegistry registry;

            public SampleLifecycleDriver()
            {
                root = new GameObject("SampleHostLifecycleContract");
                root.SetActive(false);
                AppUIManager manager = root.AddComponent<AppUIManager>();
                host = root.AddComponent<AppUIRuntimeHost>();
                registry = ScriptableObject.CreateInstance<
                    UIPageDefinitionRegistry>();
                SetField(host, "uiManager", manager);
                SetField(host, "pageRegistry", registry);
                SetField(host, "layerRoots", CreateLayers(root));
                SetField(
                    host,
                    "noticeSettings",
                    AppUINoticeSettings.CreateDefault());
            }

            public bool IsInitialized => host.IsInitialized;

            public AppUIInitializationResult Initialize()
            {
                CallbackUIOperationFactory factory =
                    new CallbackUIOperationFactory();
                return host.Initialize(
                    new AppUIRuntimeDependencies(
                        factory,
                        new InMemoryUIAssetProvider(factory),
                        UnityMainThreadExecutionContext.CaptureCurrent()));
            }

            public void Shutdown()
            {
                host.Shutdown();
            }

            public void Dispose()
            {
                host.Shutdown();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(registry);
            }

            private static UILayerRoot[] CreateLayers(GameObject owner)
            {
                Array layerIds = Enum.GetValues(typeof(UILayerId));
                UILayerRoot[] roots = new UILayerRoot[layerIds.Length];
                for (int i = 0; i < layerIds.Length; i++)
                {
                    UILayerId layerId = (UILayerId)layerIds.GetValue(i);
                    GameObject layerObject = new GameObject(
                        layerId.ToString(),
                        typeof(RectTransform),
                        typeof(UILayerRoot));
                    layerObject.transform.SetParent(owner.transform, false);
                    UILayerRoot layer =
                        layerObject.GetComponent<UILayerRoot>();
                    layer.Configure(
                        layerId,
                        ResolveCanvasDomain(layerId),
                        (RectTransform)layerObject.transform);
                    roots[i] = layer;
                }

                return roots;
            }

            private static UICanvasDomain ResolveCanvasDomain(
                UILayerId layerId)
            {
                switch (layerId)
                {
                    case UILayerId.HudLayer:
                        return UICanvasDomain.Hud;
                    case UILayerId.OverlayLayer:
                    case UILayerId.PopupLayer:
                        return UICanvasDomain.Overlay;
                    case UILayerId.ModalLayer:
                        return UICanvasDomain.Modal;
                    case UILayerId.NoticeLayer:
                        return UICanvasDomain.Notice;
                    case UILayerId.GuideLayer:
                        return UICanvasDomain.Guide;
                    case UILayerId.LoadingLayer:
                        return UICanvasDomain.Loading;
                    case UILayerId.DebugLayer:
                        return UICanvasDomain.Debug;
                    default:
                        return UICanvasDomain.System;
                }
            }

            private static void SetField(
                object target,
                string fieldName,
                object value)
            {
                FieldInfo field = target.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    throw new MissingFieldException(
                        target.GetType().FullName,
                        fieldName);
                }

                field.SetValue(target, value);
            }
        }
    }
}
