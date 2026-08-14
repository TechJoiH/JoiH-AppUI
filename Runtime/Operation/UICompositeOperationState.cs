using System;
using System.Threading;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Coordinates one outer AppUI operation with one sequential child at a
    /// time. Outer cancellation is terminal, is forwarded to the current
    /// child, and prevents a later child from starting.
    /// </summary>
    internal sealed class UICompositeOperationState<TResult> : IDisposable
    {
        private readonly object sync = new object();
        private readonly IUIOperationSource<TResult> source;
        private readonly IAppUIExecutionContext executionContext;

        private IDisposable cancellationSubscription;
        private IDisposable childSubscription;
        private Action requestChildCancellation;
        private int childVersion;
        private bool cancellationRequested;
        private bool terminal;
        private bool disposed;

        public UICompositeOperationState(
            IUIOperationSource<TResult> operationSource,
            IAppUIExecutionContext context)
        {
            source = operationSource ??
                throw new ArgumentNullException(nameof(operationSource));
            executionContext = context ??
                throw new ArgumentNullException(nameof(context));
            if (source.Operation == null)
            {
                throw new ArgumentException(
                    "Composite operation source has no operation.",
                    nameof(operationSource));
            }

            IDisposable registration = source.Operation.CancellationToken
                .Register(HandleOuterCancellation);
            bool disposeRegistration;
            lock (sync)
            {
                disposeRegistration = disposed || terminal;
                if (!disposeRegistration)
                {
                    cancellationSubscription = registration;
                }
            }

            if (disposeRegistration)
            {
                registration.Dispose();
            }
        }

        public IUIOperation<TResult> Operation => source.Operation;

        public bool ObserveChild<TChild>(
            IUIOperation<TChild> operation,
            Action<AppUIOperationCompletion<TChild>> onSucceeded)
        {
            if (operation == null)
            {
                TrySetFailed(new InvalidOperationException(
                    "Composite child operation is null."));
                return false;
            }

            if (onSucceeded == null)
            {
                TrySetFailed(new ArgumentNullException(nameof(onSucceeded)));
                return false;
            }

            int version;
            bool cancelImmediately;
            lock (sync)
            {
                cancelImmediately = terminal || cancellationRequested;
                if (cancelImmediately)
                {
                    version = 0;
                }
                else
                {
                    version = ++childVersion;
                    requestChildCancellation = () =>
                        operation.RequestCancellation();
                }
            }

            if (cancelImmediately)
            {
                operation.RequestCancellation();
                return false;
            }

            IDisposable subscription;
            try
            {
                subscription = UIOperationObserver.Observe(
                    operation,
                    executionContext,
                    completion => HandleChildCompletion(
                        version,
                        completion,
                        onSucceeded));
            }
            catch (Exception exception)
            {
                ClearChild(version);
                TrySetFailed(exception);
                return false;
            }

            bool disposeSubscription;
            lock (sync)
            {
                disposeSubscription = terminal || cancellationRequested ||
                                      version != childVersion ||
                                      requestChildCancellation == null;
                if (!disposeSubscription)
                {
                    childSubscription = subscription;
                }
            }

            if (disposeSubscription)
            {
                subscription.Dispose();
            }

            return !disposeSubscription;
        }

        public bool TrySetSucceeded(TResult result)
        {
            return TryComplete(() => source.TrySetSucceeded(result));
        }

        public bool TrySetCancelled()
        {
            return TryComplete(source.TrySetCancelled);
        }

        public bool TrySetExpired()
        {
            return TryComplete(source.TrySetExpired);
        }

        public bool TrySetFailed(Exception exception)
        {
            if (exception == null)
            {
                exception = new InvalidOperationException(
                    "Composite operation failed without an exception.");
            }

            Exception captured = exception;
            return TryComplete(() => source.TrySetFailed(captured));
        }

        public void Dispose()
        {
            IDisposable cancellation;
            IDisposable child;
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                cancellation = cancellationSubscription;
                cancellationSubscription = null;
                child = childSubscription;
                childSubscription = null;
                requestChildCancellation = null;
            }

            child?.Dispose();
            cancellation?.Dispose();
        }

        private void HandleOuterCancellation()
        {
            Action cancelChild;
            IDisposable subscription;
            lock (sync)
            {
                if (terminal || cancellationRequested)
                {
                    return;
                }

                cancellationRequested = true;
                cancelChild = requestChildCancellation;
                requestChildCancellation = null;
                subscription = childSubscription;
                childSubscription = null;
                childVersion++;
            }

            subscription?.Dispose();
            try
            {
                cancelChild?.Invoke();
            }
            finally
            {
                CompleteCancellationOnExecutionContext();
            }
        }

        private void CompleteCancellationOnExecutionContext()
        {
            if (executionContext.IsCurrent)
            {
                TrySetCancelled();
                return;
            }

            executionContext.Post(() => TrySetCancelled());
        }

        private void HandleChildCompletion<TChild>(
            int version,
            AppUIOperationCompletion<TChild> completion,
            Action<AppUIOperationCompletion<TChild>> onSucceeded)
        {
            IDisposable subscription;
            lock (sync)
            {
                if (terminal || cancellationRequested ||
                    version != childVersion)
                {
                    return;
                }

                requestChildCancellation = null;
                subscription = childSubscription;
                childSubscription = null;
            }

            subscription?.Dispose();
            switch (completion.Status)
            {
                case AppUIOperationStatus.Succeeded:
                    try
                    {
                        onSucceeded.Invoke(completion);
                    }
                    catch (Exception exception)
                    {
                        TrySetFailed(exception);
                    }

                    break;
                case AppUIOperationStatus.Cancelled:
                    TrySetCancelled();
                    break;
                case AppUIOperationStatus.Expired:
                    TrySetExpired();
                    break;
                case AppUIOperationStatus.Failed:
                    TrySetFailed(completion.Exception);
                    break;
            }
        }

        private void ClearChild(int version)
        {
            IDisposable subscription = null;
            lock (sync)
            {
                if (version != childVersion)
                {
                    return;
                }

                requestChildCancellation = null;
                subscription = childSubscription;
                childSubscription = null;
            }

            subscription?.Dispose();
        }

        private bool TryComplete(Func<bool> complete)
        {
            IDisposable cancellation;
            IDisposable child;
            lock (sync)
            {
                if (terminal)
                {
                    return false;
                }

                terminal = true;
                cancellation = cancellationSubscription;
                cancellationSubscription = null;
                child = childSubscription;
                childSubscription = null;
                requestChildCancellation = null;
            }

            child?.Dispose();
            cancellation?.Dispose();
            try
            {
                return complete.Invoke();
            }
            finally
            {
                lock (sync)
                {
                    disposed = true;
                }
            }
        }
    }
}
