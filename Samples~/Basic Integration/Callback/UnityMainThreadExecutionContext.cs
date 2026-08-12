using System;
using System.Threading;

namespace Joi.H.AppUI.Samples.Basic
{
    /// <summary>
    /// Captures Unity's current synchronization context at composition time.
    /// </summary>
    public sealed class UnityMainThreadExecutionContext : IAppUIExecutionContext
    {
        private readonly SynchronizationContext synchronizationContext;
        private readonly int ownerThreadId;

        private UnityMainThreadExecutionContext(
            SynchronizationContext context,
            int threadId)
        {
            synchronizationContext = context;
            ownerThreadId = threadId;
        }

        public bool IsCurrent =>
            Thread.CurrentThread.ManagedThreadId == ownerThreadId;

        public static UnityMainThreadExecutionContext CaptureCurrent()
        {
            return new UnityMainThreadExecutionContext(
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

            if (synchronizationContext == null)
            {
                throw new InvalidOperationException(
                    "No Unity SynchronizationContext was captured.");
            }

            synchronizationContext.Post(
                state => ((Action)state).Invoke(),
                continuation);
        }
    }
}
