using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Joi.H.AppUI.Samples.Basic.Tests
{
    public sealed class CallbackIntegrationTests
    {
        [Test]
        public void CallbackAdapters_CreateValidDependencies_AndLoadRegisteredAsset()
        {
            GameObject asset = new GameObject("SamplePage");
            try
            {
                CallbackUIOperationFactory factory =
                    new CallbackUIOperationFactory();
                InMemoryUIAssetProvider provider =
                    new InMemoryUIAssetProvider(factory);
                provider.Register("sample-page", asset);

                AppUIRuntimeDependencies dependencies =
                    new AppUIRuntimeDependencies(
                        factory,
                        provider,
                        UnityMainThreadExecutionContext.CaptureCurrent());
                bool loaded = provider.TryLoad(
                    "sample-page",
                    out UIAssetLoadResult<GameObject> result);

                Assert.That(dependencies.OperationFactory, Is.SameAs(factory));
                Assert.That(dependencies.AssetProvider, Is.SameAs(provider));
                Assert.That(dependencies.ExecutionContext, Is.Not.Null);
                Assert.That(loaded, Is.True);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Asset, Is.SameAs(asset));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CallbackAdapters_DriveOpenRefreshAndCloseLifecycle()
        {
            if (!Application.isPlaying)
            {
                Assert.Ignore("The real page lifecycle requires PlayMode.");
            }

            GameObject root = new GameObject(
                "SampleIntegrationRoot",
                typeof(RectTransform),
                typeof(Canvas));
            GameObject prefab = new GameObject(
                "SamplePage",
                typeof(RectTransform),
                typeof(SamplePanelController));
            UIPageDefinition definition =
                ScriptableObject.CreateInstance<UIPageDefinition>();
            UIPageDefinitionRegistry registry =
                ScriptableObject.CreateInstance<UIPageDefinitionRegistry>();
            try
            {
                AppUIManager manager = root.AddComponent<AppUIManager>();
                UILayerId[] layerIds =
                    (UILayerId[])System.Enum.GetValues(typeof(UILayerId));
                UILayerRoot[] layers = new UILayerRoot[layerIds.Length];
                for (int i = 0; i < layerIds.Length; i++)
                {
                    UICanvasDomain canvasDomain =
                        ResolveCanvasDomain(layerIds[i]);
                    GameObject layerObject = new GameObject(
                        layerIds[i].ToString(),
                        typeof(RectTransform));
                    layerObject.transform.SetParent(root.transform, false);
                    UILayerRoot layer =
                        layerObject.AddComponent<UILayerRoot>();
                    layer.Configure(
                        layerIds[i],
                        canvasDomain,
                        (RectTransform)layerObject.transform);
                    layers[i] = layer;
                }

                definition.LayerId = UILayerId.PopupLayer;
                definition.CanvasDomain = UICanvasDomain.Overlay;
                definition.Scope = UIPageScope.SceneScope;
                definition.RequiresRaycaster = false;
                SetField(definition, "m_DefinitionId", "sample-page");
                SetField(definition, "m_PrefabAssetId", "ui/sample-page");
                SetField(
                    registry,
                    "m_Pages",
                    new List<UIPageDefinition> { definition });

                CallbackUIOperationFactory factory =
                    new CallbackUIOperationFactory();
                InMemoryUIAssetProvider provider =
                    new InMemoryUIAssetProvider(factory);
                provider.Register("ui/sample-page", prefab);
                manager.Initialize(
                    registry,
                    new AppUIRuntimeDependencies(
                        factory,
                        provider,
                        UnityMainThreadExecutionContext.CaptureCurrent()),
                    layers,
                    null,
                    AppUINoticeSettings.CreateDefault());

                IUIOperation<UIOpenResult> open = manager.Open(
                    "sample-page",
                    UIOpenArgs.FromExplicit("initial"));
                AssertDomainSucceeded(open, out UIOpenResult openResult);
                Assert.That(openResult.Success, Is.True);
                Assert.That(SamplePanelController.LastData, Is.EqualTo("initial"));

                IUIOperation<UIRefreshResult> refresh =
                    manager.Refresh("sample-page", "updated");
                AssertDomainSucceeded(refresh, out UIRefreshResult refreshResult);
                Assert.That(refreshResult.Success, Is.True);
                Assert.That(SamplePanelController.LastData, Is.EqualTo("updated"));

                IUIOperation<UICloseResult> close = manager.Close("sample-page");
                AssertDomainSucceeded(close, out UICloseResult closeResult);
                Assert.That(closeResult.Success, Is.True);
                Assert.That(closeResult.State, Is.EqualTo(UIPageState.Released));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(registry);
            }
        }

        private static void AssertDomainSucceeded<TResult>(
            IUIOperation<TResult> operation,
            out TResult result)
        {
            Assert.That(operation.IsTerminal, Is.True);
            Assert.That(operation.TryGetCompletion(out var completion), Is.True);
            Assert.That(completion.Status, Is.EqualTo(AppUIOperationStatus.Succeeded));
            result = completion.Result;
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            System.Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            field?.SetValue(target, value);
        }

        private static UICanvasDomain ResolveCanvasDomain(UILayerId layerId)
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

        private sealed class SamplePanelController : PanelBaseController
        {
            public static object LastData { get; private set; }

            protected override void OnDataLoadEx(object data)
            {
                LastData = data;
            }
        }
    }
}
