using System;
using System.Collections.Generic;
using System.Threading;

namespace Joi.H.AppUI.Validation.Consumer
{
    public sealed class ConsumerExecutionContext : IAppUIExecutionContext
    {
        private readonly object gate = new object();
        private readonly Queue<Action> pending = new Queue<Action>();
        private readonly int ownerThreadId;

        private ConsumerExecutionContext(int threadId)
        {
            ownerThreadId = threadId;
        }

        public bool IsCurrent
        {
            get
            {
                return Thread.CurrentThread.ManagedThreadId == ownerThreadId;
            }
        }

        public int PendingCount
        {
            get
            {
                lock (gate)
                {
                    return pending.Count;
                }
            }
        }

        public static ConsumerExecutionContext CaptureCurrent()
        {
            return new ConsumerExecutionContext(
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

            lock (gate)
            {
                pending.Enqueue(continuation);
            }
        }

        public int Drain()
        {
            if (!IsCurrent)
            {
                throw new InvalidOperationException(
                    "Consumer execution context can only drain on its owner thread.");
            }

            int count = 0;
            while (true)
            {
                Action continuation;
                lock (gate)
                {
                    if (pending.Count == 0)
                    {
                        return count;
                    }

                    continuation = pending.Dequeue();
                }

                continuation.Invoke();
                count++;
            }
        }
    }
}
