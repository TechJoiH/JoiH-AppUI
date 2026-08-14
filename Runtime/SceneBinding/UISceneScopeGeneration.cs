using System;
using System.Collections.Generic;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Internal identity of one active scene-scope lifetime. An unstamped value
    /// preserves compatibility for direct Open/Close calls that only provide a
    /// SceneScopeId.
    /// </summary>
    internal readonly struct UISceneScopeStamp : IEquatable<UISceneScopeStamp>
    {
        public UISceneScopeStamp(string sceneScopeId, long generation)
        {
            SceneScopeId = UISceneScopeCoordinator.NormalizeSceneScopeId(
                sceneScopeId);
            Generation = generation;
        }

        public string SceneScopeId { get; }
        public long Generation { get; }
        public bool HasGeneration => Generation > 0;

        public static UISceneScopeStamp Unstamped(string sceneScopeId)
        {
            return new UISceneScopeStamp(sceneScopeId, 0);
        }

        public bool Equals(UISceneScopeStamp other)
        {
            return Generation == other.Generation &&
                   string.Equals(
                       SceneScopeId,
                       other.SceneScopeId,
                       StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is UISceneScopeStamp other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((SceneScopeId != null
                            ? StringComparer.Ordinal.GetHashCode(SceneScopeId)
                            : 0) * 397) ^ Generation.GetHashCode();
            }
        }

        public static bool operator ==(
            UISceneScopeStamp left,
            UISceneScopeStamp right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            UISceneScopeStamp left,
            UISceneScopeStamp right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Unstamped callers match by scope ID only. Two stamped lifetimes must
        /// match exactly, which prevents retired work from touching a rebound
        /// scene that reused the same public SceneScopeId.
        /// </summary>
        public bool IsCompatibleWith(UISceneScopeStamp other)
        {
            return string.Equals(
                       SceneScopeId,
                       other.SceneScopeId,
                       StringComparison.Ordinal) &&
                   (!HasGeneration || !other.HasGeneration ||
                    Generation == other.Generation);
        }
    }

    /// <summary>
    /// Owns the active generation for every public SceneScopeId. Invalidation
    /// is synchronous, so late asynchronous results can be rejected before a
    /// host begins loading the replacement scene lifetime.
    /// </summary>
    internal sealed class UISceneScopeGenerationRegistry
    {
        private readonly Dictionary<string, UISceneScopeStamp> currentByScope =
            new Dictionary<string, UISceneScopeStamp>(StringComparer.Ordinal);

        private long nextGeneration;

        public UISceneScopeStamp Activate(string sceneScopeId)
        {
            string normalized =
                UISceneScopeCoordinator.NormalizeSceneScopeId(sceneScopeId);
            if (currentByScope.TryGetValue(
                    normalized,
                    out UISceneScopeStamp current))
            {
                return current;
            }

            if (nextGeneration == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "SceneScope generation space has been exhausted.");
            }

            UISceneScopeStamp created = new UISceneScopeStamp(
                normalized,
                ++nextGeneration);
            currentByScope.Add(normalized, created);
            return created;
        }

        public UISceneScopeStamp GetCurrent(string sceneScopeId)
        {
            string normalized =
                UISceneScopeCoordinator.NormalizeSceneScopeId(sceneScopeId);
            return currentByScope.TryGetValue(
                normalized,
                out UISceneScopeStamp current)
                ? current
                : UISceneScopeStamp.Unstamped(normalized);
        }

        public UISceneScopeStamp Invalidate(string sceneScopeId)
        {
            string normalized =
                UISceneScopeCoordinator.NormalizeSceneScopeId(sceneScopeId);
            if (!currentByScope.TryGetValue(
                    normalized,
                    out UISceneScopeStamp retired))
            {
                return UISceneScopeStamp.Unstamped(normalized);
            }

            currentByScope.Remove(normalized);
            return retired;
        }

        public bool IsCurrent(UISceneScopeStamp stamp)
        {
            if (!stamp.HasGeneration)
            {
                return true;
            }

            return currentByScope.TryGetValue(
                       stamp.SceneScopeId,
                       out UISceneScopeStamp current) &&
                   current == stamp;
        }

        public void Clear()
        {
            currentByScope.Clear();
        }
    }
}
