using System.Collections.Generic;

namespace Joi.H.AppUI
{
    /// <summary>
    /// UI 栈协调器。
    /// 按 Layer 维护 UILayerStack，并提供跨 Layer 的顶层可见页和顶层可交互页查询。
    /// </summary>
    public sealed class UIStackCoordinator
    {
        private static readonly UILayerId[] OrderedLayers =
        {
            UILayerId.SystemLayer,
            UILayerId.HudLayer,
            UILayerId.OverlayLayer,
            UILayerId.PopupLayer,
            UILayerId.ModalLayer,
            UILayerId.NoticeLayer,
            UILayerId.GuideLayer,
            UILayerId.LoadingLayer,
            UILayerId.DebugLayer,
        };

        private readonly Dictionary<UILayerId, UILayerStack> stacks =
            new Dictionary<UILayerId, UILayerStack>(8);

        /// <summary>将页面推入所属 Layer 栈，并立即重建可见性。</summary>
        public void Push(UIPageInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            GetStack(instance.LayerId).Push(instance);
            RebuildVisibility();
        }

        /// <summary>从所属 Layer 栈移除页面，并将 StackVisible 置为 false。</summary>
        public void Remove(UIPageInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            UILayerStack stack;
            if (stacks.TryGetValue(instance.LayerId, out stack))
            {
                stack.Remove(instance);
                instance.StackVisible = false;
                RebuildVisibility();
            }
        }

        /// <summary>查询指定 Layer 的栈顶 Open 页面。</summary>
        public bool TryGetTop(UILayerId layerId, out UIPageInstance instance)
        {
            UILayerStack stack;
            if (stacks.TryGetValue(layerId, out stack))
            {
                instance = stack.Peek();
                return instance != null;
            }

            instance = null;
            return false;
        }

        /// <summary>按 Layer 优先级从高到低查询全局顶层可见页面。</summary>
        public bool TryGetTopVisiblePage(out UIPageInstance instance)
        {
            for (int i = OrderedLayers.Length - 1; i >= 0; i--)
            {
                UILayerId layerId = OrderedLayers[i];
                UILayerStack stack;
                if (!stacks.TryGetValue(layerId, out stack))
                {
                    continue;
                }

                instance = stack.PeekVisible();
                if (instance != null)
                {
                    return true;
                }
            }

            instance = null;
            return false;
        }

        /// <summary>查询指定 Layer 的顶层可见页面。</summary>
        public bool TryGetTopVisiblePage(UILayerId layerId, out UIPageInstance instance)
        {
            UILayerStack stack;
            if (stacks.TryGetValue(layerId, out stack))
            {
                instance = stack.PeekVisible();
                return instance != null;
            }

            instance = null;
            return false;
        }

        /// <summary>按跨 Layer 阻断规则查询当前顶层可交互页面。</summary>
        public bool TryGetTopInteractivePage(out UIPageInstance instance)
        {
            UIPageInstance fallback = null;
            for (int i = OrderedLayers.Length - 1; i >= 0; i--)
            {
                UILayerId layerId = OrderedLayers[i];
                UILayerStack stack;
                if (!stacks.TryGetValue(layerId, out stack))
                {
                    continue;
                }

                UIPageInstance candidate = stack.FindTopInteractiveCandidate(IsBlockingLayer(layerId));
                if (candidate == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }

                if (BlocksLowerLayers(candidate, layerId))
                {
                    instance = candidate;
                    return true;
                }
            }

            instance = fallback;
            return instance != null;
        }

        /// <summary>查询指定 Layer 内的顶层可交互页面。</summary>
        public bool TryGetTopInteractivePage(UILayerId layerId, out UIPageInstance instance)
        {
            UILayerStack stack;
            if (!stacks.TryGetValue(layerId, out stack))
            {
                instance = null;
                return false;
            }

            instance = stack.FindTopInteractiveCandidate(IsBlockingLayer(layerId));
            return instance != null;
        }

        /// <summary>重建所有 Layer 内的 StackVisible。</summary>
        public void RebuildVisibility()
        {
            for (int i = 0; i < OrderedLayers.Length; i++)
            {
                UILayerStack stack;
                if (stacks.TryGetValue(OrderedLayers[i], out stack))
                {
                    stack.RebuildStackVisibility();
                }
            }
        }

        /// <summary>按 Layer 优先级从低到高输出当前栈快照。</summary>
        public void GetSnapshot(List<UIPageInstance> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int i = 0; i < OrderedLayers.Length; i++)
            {
                UILayerStack stack;
                if (!stacks.TryGetValue(OrderedLayers[i], out stack))
                {
                    continue;
                }

                IReadOnlyList<UIPageInstance> pages = stack.GetPages();
                for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
                {
                    results.Add(pages[pageIndex]);
                }
            }
        }

        /// <summary>清空所有 Layer 栈，并把页面 StackVisible 复位为 false。</summary>
        public void Clear()
        {
            foreach (KeyValuePair<UILayerId, UILayerStack> pair in stacks)
            {
                IReadOnlyList<UIPageInstance> pages = pair.Value.GetPages();
                for (int i = 0; i < pages.Count; i++)
                {
                    UIPageInstance instance = pages[i];
                    if (instance != null)
                    {
                        instance.StackVisible = false;
                    }
                }

                pair.Value.Clear();
            }

            stacks.Clear();
        }

        private UILayerStack GetStack(UILayerId layerId)
        {
            UILayerStack stack;
            if (!stacks.TryGetValue(layerId, out stack))
            {
                stack = new UILayerStack();
                stacks.Add(layerId, stack);
            }

            return stack;
        }

        private static bool BlocksLowerLayers(UIPageInstance instance, UILayerId layerId)
        {
            if (instance == null)
            {
                return IsBlockingLayer(layerId);
            }

            UIPageDefinition definition = instance.Definition;
            return IsBlockingLayer(layerId) ||
                   (definition != null &&
                    (definition.BlockLowerLayerInput || definition.IsFullScreen));
        }

        private static bool IsBlockingLayer(UILayerId layerId)
        {
            return layerId == UILayerId.ModalLayer ||
                   layerId == UILayerId.GuideLayer ||
                   layerId == UILayerId.LoadingLayer;
        }
    }
}
