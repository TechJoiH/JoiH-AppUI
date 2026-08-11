using System;
using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI
{
    public enum AppUIFocusTraceStage
    {
        Scope = 0,
        Move = 1,
        Commit = 2,
        Selection = 3,
        Realization = 4,
    }

    public readonly struct AppUIFocusTraceEntry
    {
        internal AppUIFocusTraceEntry(
            long sequence,
            int frame,
            double realtimeSeconds,
            long pageInstanceId,
            string pageId,
            string scopeId,
            AppUIFocusTraceStage stage,
            AppUIFocusNodeAddress source,
            AppUIFocusNodeAddress target,
            string message)
        {
            Sequence = sequence;
            Frame = frame;
            RealtimeSeconds = realtimeSeconds;
            PageInstanceId = pageInstanceId;
            PageId = pageId ?? string.Empty;
            ScopeId = scopeId ?? string.Empty;
            Stage = stage;
            Source = source;
            Target = target;
            Message = message ?? string.Empty;
        }

        public long Sequence { get; }

        public int Frame { get; }

        public double RealtimeSeconds { get; }

        public long PageInstanceId { get; }

        public string PageId { get; }

        public string ScopeId { get; }

        public AppUIFocusTraceStage Stage { get; }

        public AppUIFocusNodeAddress Source { get; }

        public AppUIFocusNodeAddress Target { get; }

        public string Message { get; }

        public override string ToString()
        {
            return "#" +
                   Sequence +
                   " F" +
                   Frame +
                   " [" +
                   Stage +
                   "] " +
                   ScopeId +
                   " " +
                   Source +
                   " -> " +
                   Target +
                   " " +
                   Message;
        }
    }

    public readonly struct AppUIFocusDebugSnapshot
    {
        internal AppUIFocusDebugSnapshot(
            long pageInstanceId,
            string pageId,
            string scopeId,
            AppUIFocusScopeStatus scopeStatus,
            string activeRegionId,
            AppUIFocusNodeAddress current,
            AppUIFocusNodeAddress last,
            int currentOrder,
            string candidates)
        {
            PageInstanceId = pageInstanceId;
            PageId = pageId ?? string.Empty;
            ScopeId = scopeId ?? string.Empty;
            ScopeStatus = scopeStatus;
            ActiveRegionId = activeRegionId ?? string.Empty;
            Current = current;
            Last = last;
            CurrentOrder = currentOrder;
            Candidates = candidates ?? string.Empty;
        }

        public long PageInstanceId { get; }

        public string PageId { get; }

        public string ScopeId { get; }

        public AppUIFocusScopeStatus ScopeStatus { get; }

        public string ActiveRegionId { get; }

        public AppUIFocusNodeAddress Current { get; }

        public AppUIFocusNodeAddress Last { get; }

        public string CurrentGroupId
        {
            get { return Current.GroupId ?? string.Empty; }
        }

        public int CurrentOrder { get; }

        public string Candidates { get; }
    }

    /// <summary>
    /// Editor/Development 专用固定容量 Trace。默认没有启用 Scope，Record 是常量时间 no-op；
    /// 不向 Console 连续输出，也不参与正式导航状态。
    /// </summary>
    public static class AppUIFocusTrace
    {
        public const int Capacity = 128;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly struct ScopeContext
        {
            public ScopeContext(string pageId, string scopeId)
            {
                PageId = pageId ?? string.Empty;
                ScopeId = scopeId ?? string.Empty;
            }

            public string PageId { get; }

            public string ScopeId { get; }
        }

        private static readonly AppUIFocusTraceEntry[] Entries =
            new AppUIFocusTraceEntry[Capacity];
        private static readonly Dictionary<long, ScopeContext> Contexts =
            new Dictionary<long, ScopeContext>(8);
        private static readonly Dictionary<long, AppUIFocusDebugSnapshot> Snapshots =
            new Dictionary<long, AppUIFocusDebugSnapshot>(8);
        private static int writeIndex;
        private static int count;
        private static long nextSequence;
#endif

        public static bool CanTrace(long pageInstanceId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return pageInstanceId > 0 && Contexts.ContainsKey(pageInstanceId);
#else
            return false;
#endif
        }

        internal static void RegisterScope(
            long pageInstanceId,
            string pageId,
            string scopeId,
            bool enabled)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Contexts.Remove(pageInstanceId);
            Snapshots.Remove(pageInstanceId);
            if (enabled && pageInstanceId > 0)
            {
                Contexts.Add(
                    pageInstanceId,
                    new ScopeContext(pageId, scopeId));
            }
#endif
        }

        internal static void UnregisterScope(long pageInstanceId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Contexts.Remove(pageInstanceId);
            Snapshots.Remove(pageInstanceId);
#endif
        }

        internal static void Record(
            long pageInstanceId,
            AppUIFocusTraceStage stage,
            AppUIFocusNodeAddress source,
            AppUIFocusNodeAddress target,
            string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Contexts.TryGetValue(
                    pageInstanceId,
                    out ScopeContext context))
            {
                return;
            }

            if (nextSequence == long.MaxValue)
            {
                nextSequence = 0;
            }

            nextSequence++;
            Entries[writeIndex] = new AppUIFocusTraceEntry(
                nextSequence,
                Time.frameCount,
                Time.realtimeSinceStartupAsDouble,
                pageInstanceId,
                context.PageId,
                context.ScopeId,
                stage,
                source,
                target,
                message);
            writeIndex = (writeIndex + 1) % Capacity;
            if (count < Capacity)
            {
                count++;
            }
#endif
        }

        internal static void UpdateSnapshot(in AppUIFocusDebugSnapshot snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Contexts.ContainsKey(snapshot.PageInstanceId))
            {
                Snapshots[snapshot.PageInstanceId] = snapshot;
            }
#endif
        }

        public static void CopyEntries(List<AppUIFocusTraceEntry> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int start = (writeIndex - count + Capacity) % Capacity;
            for (int i = 0; i < count; i++)
            {
                destination.Add(Entries[(start + i) % Capacity]);
            }
#endif
        }

        public static bool TryGetSnapshot(
            long pageInstanceId,
            out AppUIFocusDebugSnapshot snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Snapshots.TryGetValue(pageInstanceId, out snapshot);
#else
            snapshot = default;
            return false;
#endif
        }

        public static bool TryGetLatestSnapshot(
            out AppUIFocusDebugSnapshot snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long latestSequence = long.MinValue;
            long latestPageInstanceId = 0;
            int start = (writeIndex - count + Capacity) % Capacity;
            for (int i = 0; i < count; i++)
            {
                AppUIFocusTraceEntry entry = Entries[(start + i) % Capacity];
                if (entry.Sequence > latestSequence &&
                    Snapshots.ContainsKey(entry.PageInstanceId))
                {
                    latestSequence = entry.Sequence;
                    latestPageInstanceId = entry.PageInstanceId;
                }
            }

            if (latestPageInstanceId > 0 &&
                Snapshots.TryGetValue(latestPageInstanceId, out snapshot))
            {
                return true;
            }

            foreach (KeyValuePair<long, AppUIFocusDebugSnapshot> pair in Snapshots)
            {
                snapshot = pair.Value;
                return true;
            }
#endif
            snapshot = default;
            return false;
        }

        public static bool TryGetLatestEntry(
            long pageInstanceId,
            out AppUIFocusTraceEntry entry)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int index = (writeIndex - 1 + Capacity) % Capacity;
            for (int i = 0; i < count; i++)
            {
                AppUIFocusTraceEntry candidate =
                    Entries[(index - i + Capacity) % Capacity];
                if (pageInstanceId <= 0 || candidate.PageInstanceId == pageInstanceId)
                {
                    entry = candidate;
                    return true;
                }
            }
#endif
            entry = default;
            return false;
        }

        public static void Clear()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Array.Clear(Entries, 0, Entries.Length);
            writeIndex = 0;
            count = 0;
#endif
        }

        internal static void ResetForTests()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Clear();
            Contexts.Clear();
            Snapshots.Clear();
            nextSequence = 0;
#endif
        }
    }
}
