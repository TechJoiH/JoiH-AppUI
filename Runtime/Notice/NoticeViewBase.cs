using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Notice 视图基类。
    /// 负责缓存 RectTransform、CanvasGroup 和 TMP_Text，具体生命周期和池化由 NoticeService 统一驱动。
    /// </summary>
    [DisallowMultipleComponent]
    public class NoticeViewBase : MonoBehaviour
    {
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private TMP_Text label;

        /// <summary>视图根 RectTransform；prefab 非 UI 对象时可能为空，调用方需要安全处理。</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return rectTransform;
            }
        }

        /// <summary>
        /// 初始化并缓存常用组件。
        /// prefab 缺少 CanvasGroup 或 TMP_Text 时会补齐，保证 fallback 和简易 prefab 都可显示文本。
        /// </summary>
        public virtual void EnsureInitialized()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }

                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
                if (label == null && rectTransform != null)
                {
                    label = CreateFallbackText(rectTransform);
                }
            }
        }

        /// <summary>写入显示文本和颜色；颜色使用 NoticeService 已解析好的最终值。</summary>
        public virtual void SetText(string text, Color color)
        {
            EnsureInitialized();
            if (label == null)
            {
                return;
            }

            label.text = text ?? string.Empty;
            label.color = color;
        }

        /// <summary>设置 fallback 文本字号；正式 prefab 可通过自身 TMP 配置覆盖。</summary>
        public virtual void SetFallbackFontSize(int fontSize)
        {
            EnsureInitialized();
            if (label != null)
            {
                label.fontSize = Mathf.Max(8, fontSize);
            }
        }

        /// <summary>设置当前透明度；服务在淡入淡出和回收前统一调用。</summary>
        public virtual void SetAlpha(float alpha)
        {
            EnsureInitialized();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        /// <summary>设置 UI 局部坐标；坐标转换由 NoticeService 完成。</summary>
        public virtual void SetAnchoredPosition(Vector2 position)
        {
            EnsureInitialized();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = position;
            }
        }

        /// <summary>
        /// 准备进入池中待复用。
        /// 这里只还原通用显示状态，不销毁对象，避免下一次播放时重复分配。
        /// </summary>
        public virtual void ResetForPool()
        {
            SetAlpha(1f);
            SetText(string.Empty, Color.white);
        }

        /// <summary>
        /// 为缺少 TMP_Text 的简易 prefab 补一个默认文本节点。
        /// 这样美术 prefab 只要有 RectTransform，也能被 NoticeService 兜底显示。
        /// </summary>
        private static TMP_Text CreateFallbackText(RectTransform parent)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }

    /// <summary>Toast 默认视图组件；具体播放流程由 NoticeService 驱动。</summary>
    public sealed class ToastNoticeView : NoticeViewBase
    {
    }

    /// <summary>Tooltip 默认视图组件；Tooltip 通常不自动消失。</summary>
    public sealed class TooltipNoticeView : NoticeViewBase
    {
    }

    /// <summary>FloatingText 默认视图组件；用于轻量浮动文字。</summary>
    public sealed class FloatingTextNoticeView : NoticeViewBase
    {
    }

    /// <summary>DamageNumber 默认视图组件；用于伤害数字表现。</summary>
    public sealed class DamageNumberNoticeView : NoticeViewBase
    {
    }
}
