using System;
using System.Collections.Generic;
using System.Threading;

namespace Joi.H.AppUI.Validation.Consumer
{
    public sealed class ConsumerOperationFactory : IUIOperationFactory
    {
        public IUIOperationSource<TResult> Create<TResult>(
            AppUIOperationDescriptor descriptor)
        {
            return new ConsumerOperation<TResult>(descriptor);
        }

        private sealed class ConsumerOperation<TResult> :
            IUIOperation<TResult>,
            IUIOperationSource<TResult>
        {
            private readonly object gate = new object();
            private readonly List<Subscription> subscriptions =
                new List<Subscription>(4);
            private readonly CancellationTokenSource cancellation;
            private readonly CancellationToken cancellationToken;
            private AppUIOperationCompletion<TResult> completion;
            private AppUIOperationStatus status;
            private bool terminal;

            public ConsumerOperation(AppUIOperationDescriptor descriptor)
            {
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    descriptor.CancellationToken);
                cancellationToken = cancellation.Token;
                status = AppUIOperationStatus.Created;
            }

            public IUIOperation<TResult> Operation
            {
                get { return this; }
            }

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

            public CancellationToken CancellationToken
            {
                get { return cancellationToken; }
            }

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

                AppUIOperationCompletion<TResult> terminalValue = default;
                lock (gate)
                {
                    if (!terminal)
                    {
                        Subscription subscription =
                            new Subscription(this, continuation);
                        subscriptions.Add(subscription);
                        return subscription;
                    }

                    terminalValue = completion;
                }

                continuation.Invoke(terminalValue);
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

                    completion = value;
                    status = value.Status;
                    terminal = true;
                    snapshot = subscriptions.ToArray();
                    subscriptions.Clear();
                }

                for (int index = 0; index < snapshot.Length; index++)
                {
                    snapshot[index].Invoke(value);
                }

                cancellation.Dispose();
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
                private ConsumerOperation<TResult> owner;
                private Action<AppUIOperationCompletion<TResult>> continuation;

                public Subscription(
                    ConsumerOperation<TResult> owner,
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
                    ConsumerOperation<TResult> current =
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
