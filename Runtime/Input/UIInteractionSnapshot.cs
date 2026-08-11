using System;
using System.Collections.Generic;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面在一次 Presentation Commit 中的稳定身份。
    /// InstanceId 标识具体运行时实例，OperationVersion 用于拒绝异步操作产生的过期请求。
    /// </summary>
    internal readonly struct UIPageInteractionHandle : IEquatable<UIPageInteractionHandle>
    {
        public UIPageInteractionHandle(string pageId, long instanceId, int operationVersion)
        {
            PageId = pageId ?? string.Empty;
            InstanceId = instanceId;
            OperationVersion = operationVersion;
        }

        public string PageId { get; }

        public long InstanceId { get; }

        public int OperationVersion { get; }

        public bool IsValid
        {
            get { return InstanceId > 0 && !string.IsNullOrEmpty(PageId); }
        }

        public bool Equals(UIPageInteractionHandle other)
        {
            return InstanceId == other.InstanceId &&
                   OperationVersion == other.OperationVersion &&
                   string.Equals(PageId, other.PageId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is UIPageInteractionHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PageId != null ? StringComparer.Ordinal.GetHashCode(PageId) : 0;
                hashCode = (hashCode * 397) ^ InstanceId.GetHashCode();
                hashCode = (hashCode * 397) ^ OperationVersion;
                return hashCode;
            }
        }

        public static bool operator ==(
            UIPageInteractionHandle left,
            UIPageInteractionHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            UIPageInteractionHandle left,
            UIPageInteractionHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>一次 Presentation Commit 中复制出的页面交互状态。</summary>
    internal readonly struct UIPageInteractionState
    {
        public UIPageInteractionState(
            UIPageInteractionHandle page,
            bool stackVisible,
            int pauseDepth,
            int inputBlockDepth)
        {
            Page = page;
            StackVisible = stackVisible;
            PauseDepth = pauseDepth;
            InputBlockDepth = inputBlockDepth;
        }

        public UIPageInteractionHandle Page { get; }

        public bool StackVisible { get; }

        public int PauseDepth { get; }

        public int InputBlockDepth { get; }
    }

    /// <summary>
    /// Presentation 发布的深度不可变交互快照。
    /// 快照只保存值状态，不持有 UIPageInstance、List 或调用方数组引用。
    /// </summary>
    internal sealed class UIInteractionSnapshot
    {
        private static readonly UIPageInteractionState[] EmptyPageStates =
            Array.Empty<UIPageInteractionState>();

        private readonly UIPageInteractionState[] pageStates;

        public UIInteractionSnapshot(
            int stackRevision,
            UIPageInteractionHandle topInteractivePage,
            IReadOnlyList<UIPageInteractionState> states)
        {
            StackRevision = stackRevision;
            TopInteractivePage = topInteractivePage;

            int count = states != null ? states.Count : 0;
            if (count == 0)
            {
                pageStates = EmptyPageStates;
                return;
            }

            pageStates = new UIPageInteractionState[count];
            for (int i = 0; i < count; i++)
            {
                pageStates[i] = states[i];
            }
        }

        public static UIInteractionSnapshot Empty { get; } =
            new UIInteractionSnapshot(0, default, null);

        public int StackRevision { get; }

        public UIPageInteractionHandle TopInteractivePage { get; }

        public int PageStateCount
        {
            get { return pageStates.Length; }
        }

        public UIPageInteractionState GetPageState(int index)
        {
            return pageStates[index];
        }
    }
}
