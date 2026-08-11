using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Small per-page work queue for spreading non-critical UI work across frames.
    /// </summary>
    public sealed class AppUIFrameBudgetExecutor
    {
        private struct WorkItem
        {
            public int Generation;
            public Action Work;
        }

        private readonly Queue<WorkItem> pending = new Queue<WorkItem>(64);

        public int Generation { get; private set; }

        public int MaxWorkItemsPerFrame { get; set; } = 8;

        public double FrameBudgetMilliseconds { get; set; } = 1.5d;

        public int PendingCount
        {
            get { return pending.Count; }
        }

        public int BeginGeneration()
        {
            AdvanceGeneration();
            pending.Clear();
            return Generation;
        }

        public void CancelAll()
        {
            BeginGeneration();
        }

        public bool IsCurrent(int generation)
        {
            return generation == Generation;
        }

        public void Enqueue(int generation, Action work)
        {
            if (work == null || generation != Generation)
            {
                return;
            }

            pending.Enqueue(new WorkItem
            {
                Generation = generation,
                Work = work,
            });
        }

        public void Tick()
        {
            if (pending.Count == 0)
            {
                return;
            }

            int processed = 0;
            long startTicks = Stopwatch.GetTimestamp();
            double tickBudget = Math.Max(0.0d, FrameBudgetMilliseconds);
            int itemBudget = Math.Max(1, MaxWorkItemsPerFrame);

            while (pending.Count > 0 && processed < itemBudget)
            {
                if (processed > 0 && tickBudget > 0.0d &&
                    GetElapsedMilliseconds(startTicks) >= tickBudget)
                {
                    break;
                }

                WorkItem item = pending.Dequeue();
                if (item.Generation != Generation || item.Work == null)
                {
                    continue;
                }

                try
                {
                    item.Work.Invoke();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogError(exception);
                }

                processed++;
            }
        }

        private void AdvanceGeneration()
        {
            Generation = Generation == int.MaxValue ? 1 : Generation + 1;
        }

        private static double GetElapsedMilliseconds(long startTicks)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            return elapsedTicks * 1000.0d / Stopwatch.Frequency;
        }
    }
}
