using System;
using System.Collections.Generic;
using System.Threading;

namespace Joi.H.AppUI.Tests
{
    public sealed class ManualUIOperationFactory : IUIOperationFactory
    {
        public IUIOperationSource<TResult> Create<TResult>(
            AppUIOperationDescriptor descriptor)
        {
            return new ManualUIOperation<TResult>(descriptor);
        }

        private sealed class ManualUIOperation<TResult> :
            IUIOperation<TResult>,
            IUIOperationSource<TResult>
        {
            private readonly List<Subscription> subscriptions =
                new List<Subscription>(4);
            private readonly CancellationTokenSource cancellation;
            private AppUIOperationCompletion<TResult> completion;
            private bool isTerminal;

            public ManualUIOperation(AppUIOperationDescriptor descriptor)
            {
                cancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        descriptor.CancellationToken);
                Status = AppUIOperationStatus.Created;
            }

            public IUIOperation<TResult> Operation
            {
                get { return this; }
            }

            public AppUIOperationStatus Status { get; private set; }

            public bool IsTerminal
            {
                get { return isTerminal; }
            }

            public CancellationToken CancellationToken
            {
                get { return cancellation.Token; }
            }

            public bool RequestCancellation()
            {
                if (isTerminal || cancellation.IsCancellationRequested)
                {
                    return false;
                }

                Status = AppUIOperationStatus.Cancelling;
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

                if (isTerminal)
                {
                    continuation.Invoke(completion);
                    return EmptyDisposable.Instance;
                }

                Subscription subscription =
                    new Subscription(this, continuation);
                subscriptions.Add(subscription);
                return subscription;
            }

            public bool TryGetCompletion(
                out AppUIOperationCompletion<TResult> value)
            {
                value = completion;
                return isTerminal;
            }

            public bool TrySetRunning()
            {
                if (isTerminal || Status != AppUIOperationStatus.Created)
                {
                    return false;
                }

                Status = AppUIOperationStatus.Running;
                return true;
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
                if (isTerminal)
                {
                    return false;
                }

                completion = value;
                Status = value.Status;
                isTerminal = true;
                Subscription[] snapshot = subscriptions.ToArray();
                subscriptions.Clear();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i].Invoke(value);
                }

                return true;
            }

            private void Remove(Subscription subscription)
            {
                subscriptions.Remove(subscription);
            }

            private sealed class Subscription : IDisposable
            {
                private ManualUIOperation<TResult> owner;
                private Action<AppUIOperationCompletion<TResult>> continuation;

                public Subscription(
                    ManualUIOperation<TResult> owner,
                    Action<AppUIOperationCompletion<TResult>> continuation)
                {
                    this.owner = owner;
                    this.continuation = continuation;
                }

                public void Invoke(
                    AppUIOperationCompletion<TResult> value)
                {
                    Action<AppUIOperationCompletion<TResult>> callback =
                        continuation;
                    owner = null;
                    continuation = null;
                    callback?.Invoke(value);
                }

                public void Dispose()
                {
                    ManualUIOperation<TResult> currentOwner = owner;
                    owner = null;
                    continuation = null;
                    currentOwner?.Remove(this);
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
