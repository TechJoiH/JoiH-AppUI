using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>Group 内节点获得焦点后保证目标可见；实现不得改变业务选择或提交焦点。</summary>
    public interface IAppUIFocusVisibilityAdapter
    {
        bool EnsureVisible(RectTransform target);
    }

    public enum AppUIFocusRealizationStatus
    {
        Realized = 0,
        NotFound = 1,
        Cancelled = 2,
    }

    /// <summary>虚拟列表实现请求；版本由 Scope 捕获并在完成时统一复验。</summary>
    public readonly struct AppUIFocusRealizationRequest
    {
        internal AppUIFocusRealizationRequest(
            string scopeId,
            AppUIFocusNodeAddress nodeAddress)
        {
            ScopeId = scopeId ?? string.Empty;
            NodeAddress = nodeAddress;
        }

        public string ScopeId { get; }

        public AppUIFocusNodeAddress NodeAddress { get; }
    }

    /// <summary>
    /// Adapter 只负责把目标 Item 实例化并返回绑定 Selectable；Scope 在版本复验后执行正式注册。
    /// </summary>
    public readonly struct AppUIFocusRealizationResult
    {
        private AppUIFocusRealizationResult(
            AppUIFocusRealizationStatus status,
            Selectable selectable,
            IAppUIFocusControlPolicy controlPolicy,
            int order)
        {
            Status = status;
            Selectable = selectable;
            ControlPolicy = controlPolicy;
            Order = order;
        }

        public AppUIFocusRealizationStatus Status { get; }

        public Selectable Selectable { get; }

        public IAppUIFocusControlPolicy ControlPolicy { get; }

        public int Order { get; }

        public static AppUIFocusRealizationResult Realized(
            Selectable selectable,
            int order = 0,
            IAppUIFocusControlPolicy controlPolicy = null)
        {
            if (selectable == null)
            {
                throw new ArgumentNullException(nameof(selectable));
            }

            return new AppUIFocusRealizationResult(
                AppUIFocusRealizationStatus.Realized,
                selectable,
                controlPolicy,
                order);
        }

        public static AppUIFocusRealizationResult NotFound()
        {
            return new AppUIFocusRealizationResult(
                AppUIFocusRealizationStatus.NotFound,
                null,
                null,
                0);
        }

        public static AppUIFocusRealizationResult Cancelled()
        {
            return new AppUIFocusRealizationResult(
                AppUIFocusRealizationStatus.Cancelled,
                null,
                null,
                0);
        }
    }

    public interface IAppUIFocusVirtualizationAdapter
    {
        IUIOperation<AppUIFocusRealizationResult> EnsureRealized(
            AppUIFocusRealizationRequest request,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// 普通 ScrollRect 的无分配可见性适配器。只在目标越出 Viewport 时平移 Content，
    /// 并按 Content Bounds 把结果限制在有效滚动范围内。
    /// </summary>
    public sealed class AppUIFocusScrollRectVisibilityAdapter :
        IAppUIFocusVisibilityAdapter
    {
        private readonly ScrollRect scrollRect;
        private readonly Vector3[] viewportCorners = new Vector3[4];

        public AppUIFocusScrollRectVisibilityAdapter(ScrollRect focusScrollRect)
        {
            scrollRect = focusScrollRect != null
                ? focusScrollRect
                : throw new ArgumentNullException(nameof(focusScrollRect));
        }

        public bool EnsureVisible(RectTransform target)
        {
            RectTransform content = scrollRect.content;
            RectTransform viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : scrollRect.transform as RectTransform;
            if (target == null ||
                content == null ||
                viewport == null ||
                !target.IsChildOf(content) ||
                !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            viewport.GetLocalCorners(viewportCorners);
            Rect viewRect = new Rect(
                viewportCorners[0].x,
                viewportCorners[0].y,
                viewportCorners[2].x - viewportCorners[0].x,
                viewportCorners[2].y - viewportCorners[0].y);
            Bounds targetBounds =
                RectTransformUtility.CalculateRelativeRectTransformBounds(
                    viewport,
                    target);
            Vector2 viewportOffset = CalculateVisibilityOffset(
                viewRect,
                targetBounds,
                scrollRect.horizontal,
                scrollRect.vertical);
            if (viewportOffset.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            Vector2 contentOffset = ConvertViewportOffsetToContentParent(
                viewport,
                content,
                viewportOffset);
            scrollRect.StopMovement();
            content.anchoredPosition += contentOffset;
            ClampContentToViewport(content, viewport, viewRect);
            return true;
        }

        private void ClampContentToViewport(
            RectTransform content,
            RectTransform viewport,
            Rect viewRect)
        {
            Bounds contentBounds =
                RectTransformUtility.CalculateRelativeRectTransformBounds(
                    viewport,
                    content);
            Vector2 correction = Vector2.zero;
            if (scrollRect.horizontal && contentBounds.size.x > viewRect.width)
            {
                if (contentBounds.min.x > viewRect.xMin)
                {
                    correction.x = viewRect.xMin - contentBounds.min.x;
                }
                else if (contentBounds.max.x < viewRect.xMax)
                {
                    correction.x = viewRect.xMax - contentBounds.max.x;
                }
            }

            if (scrollRect.vertical && contentBounds.size.y > viewRect.height)
            {
                if (contentBounds.max.y < viewRect.yMax)
                {
                    correction.y = viewRect.yMax - contentBounds.max.y;
                }
                else if (contentBounds.min.y > viewRect.yMin)
                {
                    correction.y = viewRect.yMin - contentBounds.min.y;
                }
            }

            if (correction.sqrMagnitude > 0.000001f)
            {
                content.anchoredPosition += ConvertViewportOffsetToContentParent(
                    viewport,
                    content,
                    correction);
            }
        }

        private static Vector2 CalculateVisibilityOffset(
            Rect viewRect,
            Bounds targetBounds,
            bool horizontal,
            bool vertical)
        {
            Vector2 offset = Vector2.zero;
            if (horizontal)
            {
                if (targetBounds.min.x < viewRect.xMin)
                {
                    offset.x = viewRect.xMin - targetBounds.min.x;
                }
                else if (targetBounds.max.x > viewRect.xMax)
                {
                    offset.x = viewRect.xMax - targetBounds.max.x;
                }
            }

            if (vertical)
            {
                if (targetBounds.max.y > viewRect.yMax)
                {
                    offset.y = viewRect.yMax - targetBounds.max.y;
                }
                else if (targetBounds.min.y < viewRect.yMin)
                {
                    offset.y = viewRect.yMin - targetBounds.min.y;
                }
            }

            return offset;
        }

        private static Vector2 ConvertViewportOffsetToContentParent(
            RectTransform viewport,
            RectTransform content,
            Vector2 viewportOffset)
        {
            Transform contentParent = content.parent;
            if (contentParent == null)
            {
                return viewportOffset;
            }

            Vector3 worldOffset = viewport.TransformVector(viewportOffset);
            return contentParent.InverseTransformVector(worldOffset);
        }
    }
}
