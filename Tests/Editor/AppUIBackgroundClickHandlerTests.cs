using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIBackgroundClickHandlerTests
    {
        [Test]
        public void OnDisable_DisposesPendingCloseSubscription()
        {
            GameObject root = new GameObject("BackgroundClickHandlerTest");
            UIPageDefinition definition =
                ScriptableObject.CreateInstance<UIPageDefinition>();
            TrackingUIService service = new TrackingUIService();
            try
            {
                definition.LayerId = UILayerId.PopupLayer;
                definition.CloseOnBackgroundClick = true;
                UIBackgroundClickHandler handler =
                    root.AddComponent<UIBackgroundClickHandler>();
                handler.Initialize(service, "popup", definition);

                handler.OnPointerClick(null);
                Assert.That(service.CloseOperation.ActiveSubscriptions, Is.EqualTo(1));

                typeof(UIBackgroundClickHandler)
                    .GetMethod(
                        "OnDisable",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(handler, null);

                Assert.That(service.CloseOperation.ActiveSubscriptions, Is.Zero);
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private sealed class TrackingUIService : IUIControllerService
        {
            public TrackingOperation<UICloseResult> CloseOperation { get; } =
                new TrackingOperation<UICloseResult>();

            public IUIOperation<UICloseResult> Close(string pageId)
            {
                return CloseOperation;
            }

            public IUIOperation<UICloseResult> Close(
                string pageId,
                UICloseRequest request)
            {
                return CloseOperation;
            }

            public IUIOperation<UIOpenResult> Open(string pageId) => throw Unexpected();
            public IUIOperation<UIOpenResult> Open(string pageId, object data) => throw Unexpected();
            public IUIOperation<UIOpenResult> Open(string pageId, UIOpenArgs args) => throw Unexpected();
            public IUIOperation<UISceneBindResult> BindScene(SceneUIBindingData bindingData) => throw Unexpected();
            public IUIOperation<UISceneExitResult> UnbindScene(SceneUIBindingData bindingData) => throw Unexpected();
            public IUIOperation<UIScopeReleaseResult> ReleaseScope(UIPageScope scope, string sceneScopeId) => throw Unexpected();
            public IUIOperation<UIRefreshResult> Refresh(string pageId, object data) => throw Unexpected();
            public IUIOperation<UIRefreshResult> Refresh(string pageId, UIRefreshArgs args) => throw Unexpected();
            public IUIOperation<UICancelResult> Cancel() => throw Unexpected();
            public IUIOperation<UICloseResult> CloseTop() => throw Unexpected();
            public IUIOperation<UICloseResult> CloseTop(UILayerId layerId) => throw Unexpected();
            public bool IsOpen(string pageId) => false;
            public bool IsOpening(string pageId) => false;

            public bool TryGetPageState(string pageId, out UIPageState state)
            {
                state = default;
                return false;
            }

            private static Exception Unexpected()
            {
                return new AssertionException("Unexpected UI service call.");
            }
        }

        private sealed class TrackingOperation<TResult> : IUIOperation<TResult>
        {
            private readonly List<Subscription> subscriptions =
                new List<Subscription>();

            public int ActiveSubscriptions => subscriptions.Count;
            public AppUIOperationStatus Status => AppUIOperationStatus.Running;
            public bool IsTerminal => false;
            public System.Threading.CancellationToken CancellationToken => default;
            public bool RequestCancellation() => true;

            public IDisposable Register(
                Action<AppUIOperationCompletion<TResult>> continuation)
            {
                Subscription subscription = new Subscription(this);
                subscriptions.Add(subscription);
                return subscription;
            }

            public bool TryGetCompletion(
                out AppUIOperationCompletion<TResult> completion)
            {
                completion = default;
                return false;
            }

            private void Remove(Subscription subscription)
            {
                subscriptions.Remove(subscription);
            }

            private sealed class Subscription : IDisposable
            {
                private TrackingOperation<TResult> owner;

                public Subscription(TrackingOperation<TResult> owner)
                {
                    this.owner = owner;
                }

                public void Dispose()
                {
                    TrackingOperation<TResult> current = owner;
                    owner = null;
                    current?.Remove(this);
                }
            }
        }
    }
}
