using System;
using System.Collections.Generic;
using System.Threading;
using Joi.H.AppUI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Joi.H.AppUI.Samples.TextMeshPro
{
    [Serializable]
    public sealed class TextMeshProSampleAssetEntry
    {
        public string AssetId;
        public Object Asset;
    }

    public sealed class TextMeshProSampleOperationFactory : IUIOperationFactory
    {
        public IUIOperationSource<TResult> Create<TResult>(AppUIOperationDescriptor descriptor)
        {
            return new CallbackOperation<TResult>(descriptor);
        }

        private sealed class CallbackOperation<TResult> : IUIOperation<TResult>, IUIOperationSource<TResult>
        {
            private readonly List<Action<AppUIOperationCompletion<TResult>>> continuations =
                new List<Action<AppUIOperationCompletion<TResult>>>(4);
            private readonly CancellationTokenSource cancellation;
            private AppUIOperationCompletion<TResult> completion;
            private AppUIOperationStatus status = AppUIOperationStatus.Created;
            private bool terminal;

            public CallbackOperation(AppUIOperationDescriptor descriptor)
            {
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(descriptor.CancellationToken);
            }

            public IUIOperation<TResult> Operation => this;
            public AppUIOperationStatus Status => status;
            public bool IsTerminal => terminal;
            public CancellationToken CancellationToken => cancellation.Token;
            public bool RequestCancellation()
            {
                if (terminal || cancellation.IsCancellationRequested) return false;
                status = AppUIOperationStatus.Cancelling;
                cancellation.Cancel();
                return true;
            }
            public IDisposable Register(Action<AppUIOperationCompletion<TResult>> continuation)
            {
                if (continuation == null) throw new ArgumentNullException(nameof(continuation));
                if (terminal)
                {
                    continuation(completion);
                    return EmptyDisposable.Instance;
                }

                continuations.Add(continuation);
                return new Subscription(continuations, continuation);
            }
            public bool TryGetCompletion(out AppUIOperationCompletion<TResult> value)
            {
                value = completion;
                return terminal;
            }
            public bool TrySetRunning()
            {
                if (terminal || status != AppUIOperationStatus.Created) return false;
                status = AppUIOperationStatus.Running;
                return true;
            }
            public bool TrySetSucceeded(TResult result) => Complete(AppUIOperationCompletion<TResult>.Succeeded(result));
            public bool TrySetCancelled() => Complete(AppUIOperationCompletion<TResult>.Cancelled());
            public bool TrySetFailed(Exception exception) => Complete(AppUIOperationCompletion<TResult>.Failed(exception));
            public bool TrySetExpired() => Complete(AppUIOperationCompletion<TResult>.Expired());
            private bool Complete(AppUIOperationCompletion<TResult> value)
            {
                if (terminal) return false;
                terminal = true;
                completion = value;
                status = value.Status;
                Action<AppUIOperationCompletion<TResult>>[] callbacks = continuations.ToArray();
                continuations.Clear();
                for (int i = 0; i < callbacks.Length; i++) callbacks[i](value);
                return true;
            }

            private sealed class Subscription : IDisposable
            {
                private List<Action<AppUIOperationCompletion<TResult>>> owner;
                private Action<AppUIOperationCompletion<TResult>> callback;
                public Subscription(List<Action<AppUIOperationCompletion<TResult>>> owner, Action<AppUIOperationCompletion<TResult>> callback)
                {
                    this.owner = owner;
                    this.callback = callback;
                }
                public void Dispose()
                {
                    owner?.Remove(callback);
                    owner = null;
                    callback = null;
                }
            }

            private sealed class EmptyDisposable : IDisposable
            {
                public static readonly EmptyDisposable Instance = new EmptyDisposable();
                public void Dispose() { }
            }
        }
    }

    public sealed class TextMeshProSampleExecutionContext : IAppUIExecutionContext
    {
        private readonly SynchronizationContext context;
        private readonly int threadId;
        private TextMeshProSampleExecutionContext(SynchronizationContext context, int threadId)
        {
            this.context = context;
            this.threadId = threadId;
        }
        public bool IsCurrent => Thread.CurrentThread.ManagedThreadId == threadId;
        public static TextMeshProSampleExecutionContext CaptureCurrent() =>
            new TextMeshProSampleExecutionContext(SynchronizationContext.Current, Thread.CurrentThread.ManagedThreadId);
        public void Post(Action continuation)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));
            if (IsCurrent) continuation();
            else if (context != null) context.Post(state => ((Action)state)(), continuation);
            else throw new InvalidOperationException("No Unity SynchronizationContext was captured.");
        }
    }

    public sealed class TextMeshProSampleAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, Object> assets =
            new Dictionary<string, Object>(StringComparer.Ordinal);
        private readonly IUIOperationFactory factory;
        public TextMeshProSampleAssetProvider(IUIOperationFactory factory, IReadOnlyList<TextMeshProSampleAssetEntry> entries)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && !string.IsNullOrWhiteSpace(entries[i].AssetId) && entries[i].Asset != null)
                    assets[entries[i].AssetId] = entries[i].Asset;
        }
        public bool TryLoad<T>(string assetId, out UIAssetLoadResult<T> result) where T : Object
        {
            if (assets.TryGetValue(assetId ?? string.Empty, out Object value) && value is T typed)
            {
                result = UIAssetLoadResult<T>.Success(typed);
                return true;
            }
            result = UIAssetLoadResult<T>.Failure(UIAssetLoadStatus.NotFound, "Sample asset not registered: " + assetId);
            return false;
        }
        public IUIOperation<UIAssetLoadResult<T>> Load<T>(string assetId, CancellationToken cancellationToken) where T : Object
        {
            IUIOperationSource<UIAssetLoadResult<T>> source = factory.Create<UIAssetLoadResult<T>>(
                AppUIOperationDescriptor.Create("TMP sample load", cancellationToken));
            source.TrySetRunning();
            TryLoad(assetId, out UIAssetLoadResult<T> result);
            source.TrySetSucceeded(result);
            return source.Operation;
        }
    }
}
