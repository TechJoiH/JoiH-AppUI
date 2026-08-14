using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Joi.H.AppUI.Samples.CustomHost
{
    /// <summary>
    /// Callback-based operation adapter owned entirely by this sample host.
    /// </summary>
    public sealed class CustomHostOperationFactory : IUIOperationFactory
    {
        public IUIOperationSource<TResult> Create<TResult>(
            AppUIOperationDescriptor descriptor)
        {
            return new CallbackOperation<TResult>(descriptor);
        }

        private sealed class CallbackOperation<TResult> :
            IUIOperation<TResult>,
            IUIOperationSource<TResult>
        {
            private readonly object sync = new object();
            private readonly List<Subscription> subscriptions =
                new List<Subscription>(4);
            private readonly CancellationTokenSource cancellation;
            private AppUIOperationCompletion<TResult> completion;
            private AppUIOperationStatus status;
            private bool terminal;

            public CallbackOperation(AppUIOperationDescriptor descriptor)
            {
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    descriptor.CancellationToken);
                status = AppUIOperationStatus.Created;
            }

            public IUIOperation<TResult> Operation => this;

            public AppUIOperationStatus Status
            {
                get
                {
                    lock (sync)
                    {
                        return status;
                    }
                }
            }

            public bool IsTerminal
            {
                get
                {
                    lock (sync)
                    {
                        return terminal;
                    }
                }
            }

            public CancellationToken CancellationToken => cancellation.Token;

            public bool RequestCancellation()
            {
                lock (sync)
                {
                    if (terminal || cancellation.IsCancellationRequested)
                    {
                        return false;
                    }

                    status = AppUIOperationStatus.Cancelling;
                }

                cancellation.Cancel();
                return true;
            }

            public IDisposable Register(
                Action<AppUIOperationCompletion<TResult>> continuation)
            {
                if (continuation == null)
                {
                    throw new ArgumentNullException(nameof(continuation));
                }

                AppUIOperationCompletion<TResult> completed = default;
                lock (sync)
                {
                    if (!terminal)
                    {
                        Subscription subscription =
                            new Subscription(this, continuation);
                        subscriptions.Add(subscription);
                        return subscription;
                    }

                    completed = completion;
                }

                continuation.Invoke(completed);
                return EmptyDisposable.Instance;
            }

            public bool TryGetCompletion(
                out AppUIOperationCompletion<TResult> value)
            {
                lock (sync)
                {
                    value = completion;
                    return terminal;
                }
            }

            public bool TrySetRunning()
            {
                lock (sync)
                {
                    if (terminal || status != AppUIOperationStatus.Created)
                    {
                        return false;
                    }

                    status = AppUIOperationStatus.Running;
                    return true;
                }
            }

            public bool TrySetSucceeded(TResult result)
            {
                return TryComplete(
                    AppUIOperationCompletion<TResult>.Succeeded(result));
            }

            public bool TrySetCancelled()
            {
                return TryComplete(
                    AppUIOperationCompletion<TResult>.Cancelled());
            }

            public bool TrySetFailed(Exception exception)
            {
                return TryComplete(
                    AppUIOperationCompletion<TResult>.Failed(exception));
            }

            public bool TrySetExpired()
            {
                return TryComplete(
                    AppUIOperationCompletion<TResult>.Expired());
            }

            private bool TryComplete(
                AppUIOperationCompletion<TResult> value)
            {
                Subscription[] snapshot;
                lock (sync)
                {
                    if (terminal)
                    {
                        return false;
                    }

                    terminal = true;
                    completion = value;
                    status = value.Status;
                    snapshot = subscriptions.ToArray();
                    subscriptions.Clear();
                }

                for (int i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i].Invoke(value);
                }

                return true;
            }

            private void Remove(Subscription subscription)
            {
                lock (sync)
                {
                    subscriptions.Remove(subscription);
                }
            }

            private sealed class Subscription : IDisposable
            {
                private CallbackOperation<TResult> owner;
                private Action<AppUIOperationCompletion<TResult>> callback;

                public Subscription(
                    CallbackOperation<TResult> owner,
                    Action<AppUIOperationCompletion<TResult>> callback)
                {
                    this.owner = owner;
                    this.callback = callback;
                }

                public void Invoke(
                    AppUIOperationCompletion<TResult> value)
                {
                    Action<AppUIOperationCompletion<TResult>> current =
                        Interlocked.Exchange(ref callback, null);
                    Interlocked.Exchange(ref owner, null);
                    current?.Invoke(value);
                }

                public void Dispose()
                {
                    CallbackOperation<TResult> current =
                        Interlocked.Exchange(ref owner, null);
                    Interlocked.Exchange(ref callback, null);
                    current?.Remove(this);
                }
            }

            private sealed class EmptyDisposable : IDisposable
            {
                public static readonly EmptyDisposable Instance =
                    new EmptyDisposable();

                public void Dispose()
                {
                }
            }
        }
    }

    /// <summary>
    /// Execution adapter captured by the sample composition root on Unity's
    /// main thread.
    /// </summary>
    public sealed class CustomHostExecutionContext : IAppUIExecutionContext
    {
        private readonly SynchronizationContext context;
        private readonly int ownerThreadId;

        private CustomHostExecutionContext(
            SynchronizationContext synchronizationContext,
            int threadId)
        {
            context = synchronizationContext;
            ownerThreadId = threadId;
        }

        public bool IsCurrent =>
            Thread.CurrentThread.ManagedThreadId == ownerThreadId;

        public static CustomHostExecutionContext CaptureCurrent()
        {
            return new CustomHostExecutionContext(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId);
        }

        public void Post(Action continuation)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }

            if (IsCurrent)
            {
                continuation.Invoke();
                return;
            }

            if (context == null)
            {
                throw new InvalidOperationException(
                    "No Unity SynchronizationContext was captured.");
            }

            context.Post(state => ((Action)state).Invoke(), continuation);
        }
    }

    [Serializable]
    public sealed class CustomHostAssetEntry
    {
        public string AssetId = string.Empty;
        public UnityObject Asset;
    }

    /// <summary>
    /// Explicit sample catalog with observable lease ownership. A production
    /// host would wrap its own asset backend behind the same public contract.
    /// </summary>
    public sealed class CustomHostAssetProvider : IUIAssetProvider, IDisposable
    {
        private readonly Dictionary<string, UnityObject> assets =
            new Dictionary<string, UnityObject>(StringComparer.Ordinal);
        private readonly IUIOperationFactory operationFactory;
        private int activeLeaseCount;
        private bool disposed;

        public CustomHostAssetProvider(
            IUIOperationFactory factory,
            IReadOnlyList<CustomHostAssetEntry> entries = null)
        {
            operationFactory = factory ?? throw new ArgumentNullException(
                nameof(factory));
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                CustomHostAssetEntry entry = entries[i];
                if (entry != null)
                {
                    Register(entry.AssetId, entry.Asset);
                }
            }
        }

        public int ActiveLeaseCount => activeLeaseCount;

        public bool Register(string assetId, UnityObject asset)
        {
            if (disposed || string.IsNullOrWhiteSpace(assetId) || asset == null)
            {
                return false;
            }

            assets[assetId] = asset;
            return true;
        }

        public bool TryLoad<T>(
            string assetId,
            out UIAssetLoadResult<T> result)
            where T : UnityObject
        {
            if (disposed)
            {
                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.ProviderFailed,
                    "The sample asset provider is shut down.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(assetId))
            {
                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.InvalidAssetId,
                    "AssetId is empty.");
                return false;
            }

            if (!assets.TryGetValue(assetId, out UnityObject asset) ||
                !(asset is T typedAsset))
            {
                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.NotFound,
                    "Asset was not registered. AssetId=" + assetId);
                return false;
            }

            Interlocked.Increment(ref activeLeaseCount);
            UIAssetLease lease = new UIAssetLease(
                () => Interlocked.Decrement(ref activeLeaseCount));
            result = UIAssetLoadResult<T>.Success(typedAsset, lease);
            return true;
        }

        public IUIOperation<UIAssetLoadResult<T>> Load<T>(
            string assetId,
            CancellationToken cancellationToken)
            where T : UnityObject
        {
            IUIOperationSource<UIAssetLoadResult<T>> source =
                operationFactory.Create<UIAssetLoadResult<T>>(
                    AppUIOperationDescriptor.Create(
                        "CustomHostLoad:" + (assetId ?? string.Empty),
                        cancellationToken));
            if (source == null || source.Operation == null)
            {
                throw new InvalidOperationException(
                    "The operation adapter returned a null source.");
            }

            source.TrySetRunning();
            if (cancellationToken.IsCancellationRequested)
            {
                source.TrySetCancelled();
                return source.Operation;
            }

            TryLoad(assetId, out UIAssetLoadResult<T> result);
            source.TrySetSucceeded(result);
            return source.Operation;
        }

        public void Dispose()
        {
            disposed = true;
            assets.Clear();
            if (activeLeaseCount != 0)
            {
                Debug.LogError(
                    "<Joi.H.AppUI.Sample> Asset provider shut down with active leases: " +
                    activeLeaseCount);
            }
        }
    }
}
