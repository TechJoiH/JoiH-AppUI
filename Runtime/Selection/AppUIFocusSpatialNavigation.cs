using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    internal readonly struct AppUIFocusSpatialRect
    {
        public AppUIFocusSpatialRect(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
            Center = (min + max) * 0.5f;
        }

        public Vector2 Min { get; }

        public Vector2 Max { get; }

        public Vector2 Center { get; }
    }

    internal readonly struct AppUIFocusSpatialScore :
        IComparable<AppUIFocusSpatialScore>
    {
        public AppUIFocusSpatialScore(
            float primaryDistance,
            float perpendicularOverlap,
            float perpendicularOffset)
        {
            PrimaryDistance = primaryDistance;
            PerpendicularOverlap = perpendicularOverlap;
            PerpendicularOffset = perpendicularOffset;
        }

        public float PrimaryDistance { get; }

        public float PerpendicularOverlap { get; }

        public float PerpendicularOffset { get; }

        public int CompareTo(AppUIFocusSpatialScore other)
        {
            int comparison = PrimaryDistance.CompareTo(other.PrimaryDistance);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = other.PerpendicularOverlap.CompareTo(
                PerpendicularOverlap);
            return comparison != 0
                ? comparison
                : PerpendicularOffset.CompareTo(other.PerpendicularOffset);
        }
    }

    internal static class AppUIFocusSpatialUtility
    {
        private const float DirectionEpsilon = 0.001f;

        public static bool TryCreateRect(
            Selectable selectable,
            Vector3[] worldCorners,
            out AppUIFocusSpatialRect spatialRect)
        {
            if (selectable == null ||
                !(selectable.transform is RectTransform rectTransform) ||
                worldCorners == null ||
                worldCorners.Length < 4)
            {
                spatialRect = default;
                return false;
            }

            rectTransform.GetWorldCorners(worldCorners);
            Vector2 min = worldCorners[0];
            Vector2 max = worldCorners[0];
            for (int i = 1; i < 4; i++)
            {
                Vector2 corner = worldCorners[i];
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }

            spatialRect = new AppUIFocusSpatialRect(min, max);
            return true;
        }

        public static bool TryScore(
            AppUIFocusSpatialRect source,
            AppUIFocusSpatialRect candidate,
            MoveDirection moveDirection,
            out AppUIFocusSpatialScore score)
        {
            float primaryDistance;
            float perpendicularOverlap;
            float perpendicularOffset;
            switch (moveDirection)
            {
                case MoveDirection.Left:
                    primaryDistance = source.Center.x - candidate.Center.x;
                    if (primaryDistance <= DirectionEpsilon)
                    {
                        score = default;
                        return false;
                    }

                    perpendicularOverlap = CalculateOverlap(
                        source.Min.y,
                        source.Max.y,
                        candidate.Min.y,
                        candidate.Max.y);
                    perpendicularOffset = Mathf.Abs(
                        source.Center.y - candidate.Center.y);
                    break;
                case MoveDirection.Right:
                    primaryDistance = candidate.Center.x - source.Center.x;
                    if (primaryDistance <= DirectionEpsilon)
                    {
                        score = default;
                        return false;
                    }

                    perpendicularOverlap = CalculateOverlap(
                        source.Min.y,
                        source.Max.y,
                        candidate.Min.y,
                        candidate.Max.y);
                    perpendicularOffset = Mathf.Abs(
                        source.Center.y - candidate.Center.y);
                    break;
                case MoveDirection.Up:
                    primaryDistance = candidate.Center.y - source.Center.y;
                    if (primaryDistance <= DirectionEpsilon)
                    {
                        score = default;
                        return false;
                    }

                    perpendicularOverlap = CalculateOverlap(
                        source.Min.x,
                        source.Max.x,
                        candidate.Min.x,
                        candidate.Max.x);
                    perpendicularOffset = Mathf.Abs(
                        source.Center.x - candidate.Center.x);
                    break;
                case MoveDirection.Down:
                    primaryDistance = source.Center.y - candidate.Center.y;
                    if (primaryDistance <= DirectionEpsilon)
                    {
                        score = default;
                        return false;
                    }

                    perpendicularOverlap = CalculateOverlap(
                        source.Min.x,
                        source.Max.x,
                        candidate.Min.x,
                        candidate.Max.x);
                    perpendicularOffset = Mathf.Abs(
                        source.Center.x - candidate.Center.x);
                    break;
                default:
                    score = default;
                    return false;
            }

            score = new AppUIFocusSpatialScore(
                primaryDistance,
                perpendicularOverlap,
                perpendicularOffset);
            return true;
        }

        private static float CalculateOverlap(
            float sourceMin,
            float sourceMax,
            float candidateMin,
            float candidateMax)
        {
            return Mathf.Max(
                0f,
                Mathf.Min(sourceMax, candidateMax) -
                Mathf.Max(sourceMin, candidateMin));
        }
    }

    internal sealed class AppUIFocusSpatialGroupCache
    {
        private readonly struct CachedGeometry
        {
            public CachedGeometry(
                Selectable selectable,
                Rect rect,
                Matrix4x4 localToWorldMatrix,
                AppUIFocusSpatialRect spatialRect)
            {
                Selectable = selectable;
                Rect = rect;
                LocalToWorldMatrix = localToWorldMatrix;
                SpatialRect = spatialRect;
            }

            public Selectable Selectable { get; }

            public Rect Rect { get; }

            public Matrix4x4 LocalToWorldMatrix { get; }

            public AppUIFocusSpatialRect SpatialRect { get; }
        }

        private readonly struct RankedCandidate
        {
            public RankedCandidate(
                int index,
                AppUIFocusSpatialScore score)
            {
                Index = index;
                Score = score;
            }

            public int Index { get; }

            public AppUIFocusSpatialScore Score { get; }
        }

        private sealed class RankedCandidateComparer :
            IComparer<RankedCandidate>
        {
            public static readonly RankedCandidateComparer Instance =
                new RankedCandidateComparer();

            public int Compare(RankedCandidate left, RankedCandidate right)
            {
                int comparison = left.Score.CompareTo(right.Score);
                return comparison != 0
                    ? comparison
                    : left.Index.CompareTo(right.Index);
            }
        }

        private readonly Vector3[] worldCorners = new Vector3[4];
        private CachedGeometry[] geometries = Array.Empty<CachedGeometry>();
        private RankedCandidate[][] rankings = Array.Empty<RankedCandidate[]>();
        private bool dirty = true;

        public void Invalidate()
        {
            dirty = true;
        }

        public bool TryGetTarget(
            IReadOnlyList<Selectable> nodes,
            Selectable source,
            MoveDirection moveDirection,
            out Selectable target,
            out AppUIFocusSpatialScore score,
            out int targetIndex)
        {
            target = null;
            score = default;
            targetIndex = -1;
            EnsureCurrent(nodes);
            int sourceIndex = IndexOf(nodes, source);
            int directionIndex = ToDirectionIndex(moveDirection);
            if (sourceIndex < 0 || directionIndex < 0)
            {
                return false;
            }

            RankedCandidate[] candidates =
                rankings[(sourceIndex * 4) + directionIndex];
            for (int i = 0; i < candidates.Length; i++)
            {
                RankedCandidate candidate = candidates[i];
                Selectable selectable = nodes[candidate.Index];
                if (!IsUsable(selectable))
                {
                    continue;
                }

                target = selectable;
                score = candidate.Score;
                targetIndex = candidate.Index;
                return true;
            }

            return false;
        }

        public bool TryGetTarget(
            IReadOnlyList<Selectable> nodes,
            in AppUIFocusSpatialRect sourceRect,
            MoveDirection moveDirection,
            out Selectable target,
            out AppUIFocusSpatialScore score,
            out int targetIndex)
        {
            target = null;
            score = default;
            targetIndex = -1;
            EnsureCurrent(nodes);
            bool found = false;
            for (int i = 0; i < geometries.Length; i++)
            {
                CachedGeometry geometry = geometries[i];
                if (!IsUsable(geometry.Selectable) ||
                    !AppUIFocusSpatialUtility.TryScore(
                        sourceRect,
                        geometry.SpatialRect,
                        moveDirection,
                        out AppUIFocusSpatialScore candidateScore))
                {
                    continue;
                }

                if (!found ||
                    candidateScore.CompareTo(score) < 0 ||
                    (candidateScore.CompareTo(score) == 0 && i < targetIndex))
                {
                    found = true;
                    target = geometry.Selectable;
                    score = candidateScore;
                    targetIndex = i;
                }
            }

            return found;
        }

        private void EnsureCurrent(IReadOnlyList<Selectable> nodes)
        {
            if (!dirty && IsGeometryCurrent(nodes))
            {
                return;
            }

            Rebuild(nodes);
        }

        private bool IsGeometryCurrent(IReadOnlyList<Selectable> nodes)
        {
            int count = nodes != null ? nodes.Count : 0;
            if (geometries.Length != count)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                Selectable selectable = nodes[i];
                if (selectable == null ||
                    !(selectable.transform is RectTransform rectTransform) ||
                    !ReferenceEquals(geometries[i].Selectable, selectable) ||
                    geometries[i].Rect != rectTransform.rect ||
                    geometries[i].LocalToWorldMatrix !=
                    rectTransform.localToWorldMatrix)
                {
                    return false;
                }
            }

            return true;
        }

        private void Rebuild(IReadOnlyList<Selectable> nodes)
        {
            int count = nodes != null ? nodes.Count : 0;
            geometries = new CachedGeometry[count];
            rankings = new RankedCandidate[count * 4][];
            for (int i = 0; i < count; i++)
            {
                Selectable selectable = nodes[i];
                RectTransform rectTransform = selectable != null
                    ? selectable.transform as RectTransform
                    : null;
                AppUIFocusSpatialUtility.TryCreateRect(
                    selectable,
                    worldCorners,
                    out AppUIFocusSpatialRect spatialRect);
                geometries[i] = new CachedGeometry(
                    selectable,
                    rectTransform != null ? rectTransform.rect : default,
                    rectTransform != null
                        ? rectTransform.localToWorldMatrix
                        : default,
                    spatialRect);
            }

            List<RankedCandidate> candidates = new List<RankedCandidate>(count);
            for (int sourceIndex = 0; sourceIndex < count; sourceIndex++)
            {
                for (int directionIndex = 0; directionIndex < 4; directionIndex++)
                {
                    candidates.Clear();
                    MoveDirection moveDirection = FromDirectionIndex(directionIndex);
                    for (int candidateIndex = 0;
                         candidateIndex < count;
                         candidateIndex++)
                    {
                        if (candidateIndex == sourceIndex ||
                            geometries[candidateIndex].Selectable == null ||
                            !AppUIFocusSpatialUtility.TryScore(
                                geometries[sourceIndex].SpatialRect,
                                geometries[candidateIndex].SpatialRect,
                                moveDirection,
                                out AppUIFocusSpatialScore candidateScore))
                        {
                            continue;
                        }

                        candidates.Add(
                            new RankedCandidate(candidateIndex, candidateScore));
                    }

                    candidates.Sort(RankedCandidateComparer.Instance);
                    rankings[(sourceIndex * 4) + directionIndex] =
                        candidates.ToArray();
                }
            }

            dirty = false;
        }

        private static int IndexOf(
            IReadOnlyList<Selectable> nodes,
            Selectable selectable)
        {
            if (nodes == null || selectable == null)
            {
                return -1;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == selectable)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsUsable(Selectable selectable)
        {
            return selectable != null &&
                   selectable.IsActive() &&
                   selectable.IsInteractable() &&
                   selectable.gameObject.activeInHierarchy;
        }

        private static int ToDirectionIndex(MoveDirection moveDirection)
        {
            switch (moveDirection)
            {
                case MoveDirection.Left:
                    return 0;
                case MoveDirection.Right:
                    return 1;
                case MoveDirection.Up:
                    return 2;
                case MoveDirection.Down:
                    return 3;
                default:
                    return -1;
            }
        }

        private static MoveDirection FromDirectionIndex(int directionIndex)
        {
            switch (directionIndex)
            {
                case 0:
                    return MoveDirection.Left;
                case 1:
                    return MoveDirection.Right;
                case 2:
                    return MoveDirection.Up;
                case 3:
                    return MoveDirection.Down;
                default:
                    return MoveDirection.None;
            }
        }
    }
}
