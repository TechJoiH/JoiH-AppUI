using System;
using System.Reflection;
using Joi.H.AppUI.TestKit;
using UnityEngine;

namespace Joi.H.AppUI.Samples.CustomHost.Tests
{
    public sealed class CustomHostOperationContractTests :
        AppUIOperationFactoryContractFixture
    {
        protected override IUIOperationFactory CreateOperationFactory()
        {
            return new CustomHostOperationFactory();
        }
    }

    public sealed class CustomHostAssetContractTests :
        AppUIAssetProviderContractFixture
    {
        protected override AppUIAssetProviderContractContext
            CreateAssetContext()
        {
            GameObject asset = new GameObject("CustomHostContractAsset");
            CustomHostAssetProvider provider =
                new CustomHostAssetProvider(
                    new CustomHostOperationFactory());
            provider.Register("contract/custom-page", asset);
            return new AppUIAssetProviderContractContext(
                provider,
                "contract/custom-page",
                asset,
                () =>
                {
                    provider.Dispose();
                    UnityEngine.Object.DestroyImmediate(asset);
                });
        }
    }

    public sealed class CustomHostExecutionContractTests :
        AppUIExecutionContextContractFixture
    {
        protected override IAppUIExecutionContext CreateExecutionContext()
        {
            return CustomHostExecutionContext.CaptureCurrent();
        }
    }

    public sealed class CustomHostInstanceContractTests :
        AppUIInstanceStrategyContractFixture
    {
        protected override AppUIInstanceStrategyContractContext
            CreateInstanceContext()
        {
            GameObject root = new GameObject(
                "CustomHostPoolRoot",
                typeof(RectTransform));
            GameObject prefab = new GameObject(
                "CustomHostPoolPrefab",
                typeof(RectTransform));
            UIPageDefinition definition =
                ScriptableObject.CreateInstance<UIPageDefinition>();
            SetDefinitionField(
                definition,
                "m_DefinitionId",
                "CustomHostPoolPage");
            SetDefinitionField(
                definition,
                "m_PrefabAssetId",
                "contract/custom-pool-page");
            CustomHostPooledInstanceStrategy strategy =
                new CustomHostPooledInstanceStrategy();
            return new AppUIInstanceStrategyContractContext(
                strategy,
                definition,
                prefab,
                (RectTransform)root.transform,
                () =>
                {
                    strategy.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                    UnityEngine.Object.DestroyImmediate(prefab);
                    UnityEngine.Object.DestroyImmediate(definition);
                });
        }

        private static void SetDefinitionField(
            UIPageDefinition definition,
            string fieldName,
            string value)
        {
            FieldInfo field = typeof(UIDefinitionAssetBase).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    typeof(UIDefinitionAssetBase).FullName,
                    fieldName);
            }

            field.SetValue(definition, value);
        }
    }

    public sealed class CustomHostLifecycleContractTests :
        AppUIHostLifecycleContractFixture
    {
        protected override IAppUIHostLifecycleContractDriver CreateDriver()
        {
            return new LifecycleDriver();
        }

        private sealed class LifecycleDriver :
            IAppUIHostLifecycleContractDriver
        {
            private readonly GameObject root;
            private readonly AppUIRuntimeHost host;
            private readonly UIPageDefinitionRegistry registry;
            private CustomHostAssetProvider provider;

            public LifecycleDriver()
            {
                root = new GameObject("CustomHostLifecycleContract");
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
                CustomHostOperationFactory factory =
                    new CustomHostOperationFactory();
                provider = new CustomHostAssetProvider(factory);
                return host.Initialize(
                    new AppUIRuntimeDependencies(
                        factory,
                        provider,
                        CustomHostExecutionContext.CaptureCurrent()),
                    AppUIRuntimeConfiguration.Empty);
            }

            public void Shutdown()
            {
                host.Shutdown();
                provider?.Dispose();
                provider = null;
            }

            public void Dispose()
            {
                host.Shutdown();
                provider?.Dispose();
                provider = null;
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
