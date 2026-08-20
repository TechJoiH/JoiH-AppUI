using System;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Technology-neutral base for an authored, poolable Notice view.
    /// Concrete integrations own their text or graphic components.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class NoticeViewBase : MonoBehaviour
    {
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private bool initialized;

        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return rectTransform;
            }
        }

        public CanvasGroup CanvasGroup
        {
            get
            {
                EnsureInitialized();
                return canvasGroup;
            }
        }

        /// <summary>
        /// Validates and caches authored base components. Missing components are
        /// configuration errors and are never added at runtime.
        /// </summary>
        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                throw new InvalidOperationException(
                    "Notice view requires an authored RectTransform: " +
                    name);
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                throw new InvalidOperationException(
                    "Notice view requires an authored CanvasGroup: " +
                    name);
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            initialized = true;
        }

        public abstract void ApplyContent(
            in UINoticeContent content);

        public virtual void SetAlpha(float alpha)
        {
            EnsureInitialized();
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public virtual void SetAnchoredPosition(Vector2 position)
        {
            EnsureInitialized();
            rectTransform.anchoredPosition = position;
        }

        public virtual void ResetForPool()
        {
            SetAlpha(1f);
        }
    }
}
