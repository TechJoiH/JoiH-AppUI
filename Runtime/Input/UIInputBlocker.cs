namespace Joi.H.AppUI
{
    /// <summary>
    /// UI 输入阻断器。
    /// 通过 CanvasGroup 禁用被阻断页面输入，并为阻断页面创建透明 Raycast Shield 吃掉空白区域点击。
    /// </summary>
    public sealed class UIInputBlocker
    {
        private readonly System.Collections.Generic.Dictionary<UIPageInstance, InputBlockState> states =
            new System.Collections.Generic.Dictionary<UIPageInstance, InputBlockState>(16);
        private readonly System.Collections.Generic.Dictionary<UIPageInstance, ShieldState> shields =
            new System.Collections.Generic.Dictionary<UIPageInstance, ShieldState>(8);
        private readonly System.Collections.Generic.List<UIPageInstance> staleInputStates =
            new System.Collections.Generic.List<UIPageInstance>(16);
        private readonly System.Collections.Generic.List<UIPageInstance> staleShields =
            new System.Collections.Generic.List<UIPageInstance>(8);

        /// <summary>开始一次输入阻断刷新，先把旧状态标记为未命中。</summary>
        public void BeginRefresh()
        {
            foreach (System.Collections.Generic.KeyValuePair<UIPageInstance, InputBlockState> pair in states)
            {
                pair.Value.Seen = false;
            }

            foreach (System.Collections.Generic.KeyValuePair<UIPageInstance, ShieldState> pair in shields)
            {
                pair.Value.Seen = false;
            }
        }

        /// <summary>设置页面输入阻断深度；depth 小于等于 0 时恢复原 CanvasGroup 状态。</summary>
        public void SetBlockedDepth(UIPageInstance instance, int depth)
        {
            if (instance == null)
            {
                return;
            }

            if (depth <= 0)
            {
                Restore(instance);
                return;
            }

            InputBlockState state = GetOrCreateState(instance);
            if (state == null)
            {
                instance.InputBlockDepth = 0;
                return;
            }

            instance.InputBlockDepth = depth;
            state.Seen = true;
            state.Depth = depth;
            if (state.CanvasGroup != null)
            {
                state.CanvasGroup.interactable = false;
                state.CanvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>为阻断页面设置透明边界 Shield，Shield 位于阻断页之前以避免挡住页面自身。</summary>
        public void SetBoundaryShield(UIPageInstance blocker, UnityEngine.RectTransform contentRoot)
        {
            if (blocker == null || contentRoot == null || blocker.GameObject == null)
            {
                return;
            }

            ShieldState state = GetOrCreateShield(blocker);
            if (state == null)
            {
                return;
            }

            state.Seen = true;
            RefreshShieldTransform(state, contentRoot, blocker.GameObject.transform);
        }

        /// <summary>结束刷新，恢复未命中的页面输入状态并销毁过期 Shield。</summary>
        public void EndRefresh()
        {
            staleInputStates.Clear();
            foreach (System.Collections.Generic.KeyValuePair<UIPageInstance, InputBlockState> pair in states)
            {
                if (!pair.Value.Seen)
                {
                    staleInputStates.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleInputStates.Count; i++)
            {
                Restore(staleInputStates[i]);
            }

            staleInputStates.Clear();

            staleShields.Clear();
            foreach (System.Collections.Generic.KeyValuePair<UIPageInstance, ShieldState> pair in shields)
            {
                if (!pair.Value.Seen)
                {
                    staleShields.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleShields.Count; i++)
            {
                DestroyShield(staleShields[i]);
            }

            staleShields.Clear();
        }

        /// <summary>清空全部输入阻断状态和 Shield，通常在 Manager 销毁时调用。</summary>
        public void Clear()
        {
            foreach (System.Collections.Generic.KeyValuePair<UIPageInstance, InputBlockState> pair in states)
            {
                if (pair.Key != null)
                {
                    pair.Key.InputBlockDepth = 0;
                }

                Restore(pair.Value);
            }

            states.Clear();

            foreach (System.Collections.Generic.KeyValuePair<UIPageInstance, ShieldState> pair in shields)
            {
                DestroyShield(pair.Value);
            }

            shields.Clear();
            staleInputStates.Clear();
            staleShields.Clear();
        }

        private InputBlockState GetOrCreateState(UIPageInstance instance)
        {
            InputBlockState state;
            if (states.TryGetValue(instance, out state))
            {
                return state;
            }

            if (instance.GameObject == null)
            {
                return null;
            }

            UnityEngine.CanvasGroup canvasGroup = instance.GameObject.GetComponent<UnityEngine.CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = instance.GameObject.AddComponent<UnityEngine.CanvasGroup>();
            }

            state = new InputBlockState
            {
                CanvasGroup = canvasGroup,
                OriginalInteractable = canvasGroup.interactable,
                OriginalBlocksRaycasts = canvasGroup.blocksRaycasts,
                Seen = true,
            };
            states.Add(instance, state);
            return state;
        }

        private void Restore(UIPageInstance instance)
        {
            if (instance != null)
            {
                instance.InputBlockDepth = 0;
            }

            InputBlockState state;
            if (instance == null || !states.TryGetValue(instance, out state))
            {
                return;
            }

            Restore(state);
            states.Remove(instance);
        }

        private ShieldState GetOrCreateShield(UIPageInstance blocker)
        {
            ShieldState state;
            if (shields.TryGetValue(blocker, out state) && state.GameObject != null)
            {
                return state;
            }

            if (state != null)
            {
                DestroyShield(state);
                shields.Remove(blocker);
            }

            UnityEngine.GameObject shieldObject = new UnityEngine.GameObject(
                BuildShieldName(blocker),
                typeof(UnityEngine.RectTransform),
                typeof(UnityEngine.CanvasRenderer),
                typeof(UnityEngine.UI.Image),
                typeof(UIInputBlockerShield));
            shieldObject.hideFlags = UnityEngine.HideFlags.DontSave;

            UnityEngine.UI.Image image = shieldObject.GetComponent<UnityEngine.UI.Image>();
            image.color = UnityEngine.Color.clear;
            image.raycastTarget = true;

            state = new ShieldState
            {
                GameObject = shieldObject,
                RectTransform = shieldObject.transform as UnityEngine.RectTransform,
                Image = image,
                Seen = true,
            };
            shields.Add(blocker, state);
            return state;
        }

        private void RefreshShieldTransform(
            ShieldState state,
            UnityEngine.RectTransform contentRoot,
            UnityEngine.Transform blockerTransform)
        {
            if (state == null || state.GameObject == null || state.RectTransform == null)
            {
                return;
            }

            UnityEngine.RectTransform rectTransform = state.RectTransform;
            if (rectTransform.parent != contentRoot)
            {
                rectTransform.SetParent(contentRoot, false);
            }

            rectTransform.anchorMin = UnityEngine.Vector2.zero;
            rectTransform.anchorMax = UnityEngine.Vector2.one;
            rectTransform.offsetMin = UnityEngine.Vector2.zero;
            rectTransform.offsetMax = UnityEngine.Vector2.zero;
            rectTransform.localScale = UnityEngine.Vector3.one;
            rectTransform.localRotation = UnityEngine.Quaternion.identity;

            if (state.Image != null)
            {
                state.Image.color = UnityEngine.Color.clear;
                state.Image.raycastTarget = true;
            }

            state.GameObject.SetActive(true);
            UnityEngine.Transform layerChild = GetDirectChildUnder(contentRoot, blockerTransform);
            if (layerChild == null || layerChild == rectTransform)
            {
                rectTransform.SetAsFirstSibling();
                return;
            }

            int targetIndex = layerChild.GetSiblingIndex();
            int currentIndex = rectTransform.GetSiblingIndex();
            if (currentIndex >= 0 && currentIndex < targetIndex)
            {
                targetIndex--;
            }

            rectTransform.SetSiblingIndex(UnityEngine.Mathf.Max(0, targetIndex));
        }

        private void DestroyShield(UIPageInstance blocker)
        {
            ShieldState state;
            if (!shields.TryGetValue(blocker, out state))
            {
                return;
            }

            DestroyShield(state);
            shields.Remove(blocker);
        }

        private static UnityEngine.Transform GetDirectChildUnder(
            UnityEngine.Transform root,
            UnityEngine.Transform target)
        {
            if (root == null || target == null)
            {
                return null;
            }

            UnityEngine.Transform current = target;
            while (current != null && current.parent != root)
            {
                current = current.parent;
            }

            return current;
        }

        private static string BuildShieldName(UIPageInstance blocker)
        {
            string pageId = blocker != null && !string.IsNullOrEmpty(blocker.PageId)
                ? blocker.PageId
                : "Unknown";
            return "UIInputBlocker.Shield." + pageId;
        }

        private static void DestroyShield(ShieldState state)
        {
            if (state == null || state.GameObject == null)
            {
                return;
            }

            state.GameObject.SetActive(false);
            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(state.GameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(state.GameObject);
            }
        }

        private static void Restore(InputBlockState state)
        {
            if (state == null || state.CanvasGroup == null)
            {
                return;
            }

            state.CanvasGroup.interactable = state.OriginalInteractable;
            state.CanvasGroup.blocksRaycasts = state.OriginalBlocksRaycasts;
            state.Depth = 0;
            state.Seen = false;
        }

        private sealed class InputBlockState
        {
            public UnityEngine.CanvasGroup CanvasGroup;
            public bool OriginalInteractable;
            public bool OriginalBlocksRaycasts;
            public int Depth;
            public bool Seen;
        }

        private sealed class ShieldState
        {
            public UnityEngine.GameObject GameObject;
            public UnityEngine.RectTransform RectTransform;
            public UnityEngine.UI.Image Image;
            public bool Seen;
        }
    }

    [UnityEngine.DisallowMultipleComponent]
    public sealed class UIInputBlockerShield : UnityEngine.MonoBehaviour
    {
    }
}
