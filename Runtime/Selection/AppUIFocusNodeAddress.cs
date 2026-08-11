using System;

namespace Joi.H.AppUI
{
    /// <summary>Group 内稳定的焦点节点身份。</summary>
    public readonly struct AppUIFocusNodeKey : IEquatable<AppUIFocusNodeKey>
    {
        public AppUIFocusNodeKey(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(Value); }
        }

        public bool Equals(AppUIFocusNodeKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AppUIFocusNodeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(AppUIFocusNodeKey left, AppUIFocusNodeKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AppUIFocusNodeKey left, AppUIFocusNodeKey right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Scope 内完整的焦点节点身份，由 GroupId 和 Group 内 NodeKey 组成。</summary>
    public readonly struct AppUIFocusNodeAddress : IEquatable<AppUIFocusNodeAddress>
    {
        public AppUIFocusNodeAddress(string groupId, AppUIFocusNodeKey nodeKey)
        {
            GroupId = groupId ?? string.Empty;
            NodeKey = nodeKey;
        }

        public string GroupId { get; }

        public AppUIFocusNodeKey NodeKey { get; }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(GroupId) && NodeKey.IsValid; }
        }

        public bool Equals(AppUIFocusNodeAddress other)
        {
            return NodeKey.Equals(other.NodeKey) &&
                   string.Equals(GroupId, other.GroupId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AppUIFocusNodeAddress other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = GroupId != null
                    ? StringComparer.Ordinal.GetHashCode(GroupId)
                    : 0;
                return (hashCode * 397) ^ NodeKey.GetHashCode();
            }
        }

        public override string ToString()
        {
            return (GroupId ?? string.Empty) + "/" + NodeKey;
        }

        public static bool operator ==(
            AppUIFocusNodeAddress left,
            AppUIFocusNodeAddress right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            AppUIFocusNodeAddress left,
            AppUIFocusNodeAddress right)
        {
            return !left.Equals(right);
        }
    }
}
