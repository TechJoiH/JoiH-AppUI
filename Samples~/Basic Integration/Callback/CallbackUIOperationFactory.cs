using System;
using System.Collections.Generic;
using System.Threading;

namespace Joi.H.AppUI.Samples.Basic
{
    /// <summary>
    /// Pure callback implementation supplied only by this optional sample.
    /// Production projects may replace it with any backend that obeys the
    /// IUIOperation contracts.
    /// </summary>
    public sealed class CallbackUIOperationFactory : IUIOperationFactory
    {
        public IUIOperationSource<TResult> Create<TResult>(
            AppUIOperationDescriptor descriptor)
        {
            return new CallbackUIOperation<TResult>(descriptor);
        }

        private sealed class CallbackUIOperation<TResult> :
            IUIOperation<TResult>,
            IUIOperationSource<TResult>
        {
            private readonly object gate = new object();
            private readonly List<Subscription> subscriptions =
                new List<Subscription>(4);
            private readonly CancellationTokenSource cancellation;
            private AppUIOperationCompletion<TResult> completion;
            private AppUIOperationStatus status;
            private bool terminal;

            public CallbackUIOperation(AppUIOperationDescriptor descriptor)
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
                    lock (gate)
                    {
                        return status;
                    }
                }
            }

            public bool IsTerminal
            {
                get
                {
                    lock (gate)
                    {
                        return terminal;
                    }
                }
            }

            public CancellationToken CancellationToken => cancellation.Token;

            public bool RequestCancellation()
            {
                lock (gate)
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

                AppUIOperationCompletion<TResult> completedValue = default;
                lock (gate)
                {
                    if (!terminal)
                    {
                        Subscription subscription =
                            new Subscription(this, continuation);
                        subscriptions.Add(subscription);
                        return subscription;
                    }

                    completedValue = completion;
                }

                continuation.Invoke(completedValue);
                return EmptyDisposable.Instance;
            }

            public bool TryGetCompletion(
                out AppUIOperationCompletion<TResult> value)
            {
                lock (gate)
                {
                    value = completion;
                    return terminal;
                }
            }

            public bool TrySetRunning()
            {
                lock (gate)
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
                lock (gate)
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
                lock (gate)
                {
                    subscriptions.Remove(subscription);
                }
            }

            private sealed class Subscription : IDisposable
            {
                private CallbackUIOperation<TResult> owner;
                private Action<AppUIOperationCompletion<TResult>> continuation;

                public Subscription(
                    CallbackUIOperation<TResult> owner,
                    Action<AppUIOperationCompletion<TResult>> continuation)
                {
                    this.owner = owner;
                    this.continuation = continuation;
                }

                public void Invoke(
                    AppUIOperationCompletion<TResult> value)
                {
                    Action<AppUIOperationCompletion<TResult>> callback =
                        Interlocked.Exchange(ref continuation, null);
                    Interlocked.Exchange(ref owner, null);
                    callback?.Invoke(value);
                }

                public void Dispose()
                {
                    CallbackUIOperation<TResult> current =
                        Interlocked.Exchange(ref owner, null);
                    Interlocked.Exchange(ref continuation, null);
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
}
