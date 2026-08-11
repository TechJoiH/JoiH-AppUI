using System.Collections.Generic;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 单个 UILayerId 内的页面栈。
    /// 负责同层页面顺序、同层全屏页面的 StackVisible 计算，以及同层交互候选查找。
    /// </summary>
    public sealed class UILayerStack
    {
        private readonly List<UIPageInstance> pages = new List<UIPageInstance>(8);

        /// <summary>当前层内页面列表，顺序从底到顶。</summary>
        public IReadOnlyList<UIPageInstance> Pages
        {
            get { return pages; }
        }

        /// <summary>把页面推到当前层栈顶；已存在时先移除再加入。</summary>
        public void Push(UIPageInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            pages.Remove(instance);
            pages.Add(instance);
        }

        /// <summary>从当前层栈中移除页面。</summary>
        public bool Remove(UIPageInstance instance)
        {
            return instance != null && pages.Remove(instance);
        }

        /// <summary>查找当前层栈顶 Open 页面，不要求 StackVisible。</summary>
        public UIPageInstance Peek()
        {
            for (int i = pages.Count - 1; i >= 0; i--)
            {
                UIPageInstance page = pages[i];
                if (IsOpen(page))
                {
                    return page;
                }
            }

            return null;
        }

        /// <summary>查找当前层栈顶可见 Open 页面。</summary>
        public UIPageInstance PeekVisible()
        {
            for (int i = pages.Count - 1; i >= 0; i--)
            {
                UIPageInstance page = pages[i];
                if (IsVisibleOpen(page))
                {
                    return page;
                }
            }

            return null;
        }

        /// <summary>从栈顶向下查找当前层可交互候选；非阻断页面可作为 fallback。</summary>
        public UIPageInstance FindTopInteractiveCandidate(bool layerBlocksLowerInput)
        {
            UIPageInstance fallback = null;
            for (int i = pages.Count - 1; i >= 0; i--)
            {
                UIPageInstance page = pages[i];
                if (!IsVisibleOpen(page))
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = page;
                }

                if (BlocksLowerPages(page, layerBlocksLowerInput))
                {
                    return page;
                }
            }

            return fallback;
        }

        /// <summary>重建同层 StackVisible；最高有效全屏页面以下的全屏页面会被栈隐藏。</summary>
        public void RebuildStackVisibility()
        {
            bool fullScreenBarrierFound = false;
            for (int i = pages.Count - 1; i >= 0; i--)
            {
                UIPageInstance page = pages[i];
                if (!IsOpen(page))
                {
                    if (page != null)
                    {
                        page.StackVisible = false;
                    }

                    continue;
                }

                bool isFullScreen = IsFullScreen(page);
                page.StackVisible = !isFullScreen || !fullScreenBarrierFound;
                if (isFullScreen)
                {
                    fullScreenBarrierFound = true;
                }
            }
        }

        /// <summary>获取当前层页面列表；调用方只读使用。</summary>
        public IReadOnlyList<UIPageInstance> GetPages()
        {
            return pages;
        }

        /// <summary>清空当前层栈。</summary>
        public void Clear()
        {
            pages.Clear();
        }

        private static bool IsOpen(UIPageInstance page)
        {
            return page != null && page.State == UIPageState.Open;
        }

        private static bool IsVisibleOpen(UIPageInstance page)
        {
            return page != null && page.IsOpenAndStackVisible;
        }

        private static bool IsFullScreen(UIPageInstance page)
        {
            return page != null && page.Definition != null && page.Definition.IsFullScreen;
        }

        private static bool BlocksLowerPages(UIPageInstance page, bool layerBlocksLowerInput)
        {
            if (page == null || page.Definition == null)
            {
                return layerBlocksLowerInput;
            }

            return layerBlocksLowerInput ||
                   page.Definition.BlockLowerLayerInput ||
                   page.Definition.IsFullScreen;
        }
    }
}
