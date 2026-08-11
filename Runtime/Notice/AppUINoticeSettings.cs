using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Notice 单类表现配置。
    /// 资源 ID 可为空；为空或加载失败时 NoticeService 会创建内置 fallback uGUI 视图。
    /// </summary>
    [Serializable]
    public sealed class AppUINoticeVisualSettings
    {
        [SerializeField]
        [FormerlySerializedAs("prefabResourceId")]
        private string prefabAssetId;

        [SerializeField]
        private float defaultDuration = 1.5f;

        [SerializeField]
        private float fadeDuration = 0.25f;

        [SerializeField]
        private float riseDistance = 64f;

        [SerializeField]
        private int prewarmCount = 2;

        [SerializeField]
        private int maxActiveCount = 16;

        [SerializeField]
        private int fontSize = 28;

        [SerializeField]
        private Color textColor = Color.white;

        /// <summary>可选 prefab 资源 ID；第一版走 IUIAssetProvider 的同步加载。</summary>
        public string PrefabAssetId
        {
            get { return prefabAssetId ?? string.Empty; }
        }

        /// <summary>默认显示持续时间；Tooltip 会忽略自动关闭时间。</summary>
        public float DefaultDuration
        {
            get { return Mathf.Max(0.05f, defaultDuration); }
        }

        /// <summary>默认淡出时间。</summary>
        public float FadeDuration
        {
            get { return Mathf.Max(0.01f, fadeDuration); }
        }

        /// <summary>自动消失类 Notice 在生命周期内向上移动的距离。</summary>
        public float RiseDistance
        {
            get { return Mathf.Max(0f, riseDistance); }
        }

        /// <summary>初始化时预创建的对象数量。</summary>
        public int PrewarmCount
        {
            get { return Mathf.Max(0, prewarmCount); }
        }

        /// <summary>同类 Notice 最大 active 数量；小于等于 0 表示不限制。</summary>
        public int MaxActiveCount
        {
            get { return maxActiveCount; }
        }

        /// <summary>fallback 视图的默认字号。</summary>
        public int FontSize
        {
            get { return Mathf.Max(8, fontSize); }
        }

        /// <summary>默认文本颜色；请求未显式传颜色时使用。</summary>
        public Color TextColor
        {
            get { return textColor; }
        }

        /// <summary>
        /// 设置默认值。
        /// AppUINoticeSettings 通过工厂方法创建默认配置时调用，避免每个调用点重复散落魔法数字。
        /// </summary>
        public void ConfigureDefaults(
            float duration,
            float fade,
            float rise,
            int prewarm,
            int maxActive,
            int size,
            Color color)
        {
            defaultDuration = duration;
            fadeDuration = fade;
            riseDistance = rise;
            prewarmCount = prewarm;
            maxActiveCount = maxActive;
            fontSize = size;
            textColor = color;
        }
    }

    /// <summary>
    /// App UI Notice 总配置。
    /// 挂在 AppUIRuntimeProfile 上，允许正式项目替换美术 prefab；未配置时使用内置默认视觉。
    /// </summary>
    [Serializable]
    public sealed class AppUINoticeSettings
    {
        [SerializeField]
        private AppUINoticeVisualSettings toast = new AppUINoticeVisualSettings();

        [SerializeField]
        private AppUINoticeVisualSettings tooltip = new AppUINoticeVisualSettings();

        [SerializeField]
        private AppUINoticeVisualSettings floatingText = new AppUINoticeVisualSettings();

        [SerializeField]
        private AppUINoticeVisualSettings damageNumber = new AppUINoticeVisualSettings();

        /// <summary>Toast 的资源和池配置。</summary>
        public AppUINoticeVisualSettings Toast
        {
            get { return toast ?? (toast = CreateToastDefaults()); }
        }

        /// <summary>Tooltip 的资源和池配置。</summary>
        public AppUINoticeVisualSettings Tooltip
        {
            get { return tooltip ?? (tooltip = CreateTooltipDefaults()); }
        }

        /// <summary>FloatingText 的资源和池配置。</summary>
        public AppUINoticeVisualSettings FloatingText
        {
            get { return floatingText ?? (floatingText = CreateFloatingTextDefaults()); }
        }

        /// <summary>DamageNumber 的资源和池配置。</summary>
        public AppUINoticeVisualSettings DamageNumber
        {
            get { return damageNumber ?? (damageNumber = CreateDamageNumberDefaults()); }
        }

        /// <summary>
        /// 创建一份内置默认配置。
        /// 手动测试场景或旧 asset 没有序列化 noticeSettings 字段时使用它兜底。
        /// </summary>
        public static AppUINoticeSettings CreateDefault()
        {
            return new AppUINoticeSettings
            {
                toast = CreateToastDefaults(),
                tooltip = CreateTooltipDefaults(),
                floatingText = CreateFloatingTextDefaults(),
                damageNumber = CreateDamageNumberDefaults(),
            };
        }

        /// <summary>创建 Toast 默认表现；顶中短暂停留，数量较少。</summary>
        private static AppUINoticeVisualSettings CreateToastDefaults()
        {
            AppUINoticeVisualSettings settings = new AppUINoticeVisualSettings();
            settings.ConfigureDefaults(1.8f, 0.25f, 36f, 2, 8, 28, Color.white);
            return settings;
        }

        /// <summary>创建 Tooltip 默认表现；Tooltip 由调用方手动隐藏，因此默认时长只作为兜底值。</summary>
        private static AppUINoticeVisualSettings CreateTooltipDefaults()
        {
            AppUINoticeVisualSettings settings = new AppUINoticeVisualSettings();
            settings.ConfigureDefaults(0.1f, 0.1f, 0f, 1, 4, 24, Color.white);
            return settings;
        }

        /// <summary>创建 FloatingText 默认表现；上浮距离和池容量适合资源变化等频繁提示。</summary>
        private static AppUINoticeVisualSettings CreateFloatingTextDefaults()
        {
            AppUINoticeVisualSettings settings = new AppUINoticeVisualSettings();
            settings.ConfigureDefaults(1.0f, 0.35f, 72f, 4, 32, 26, Color.white);
            return settings;
        }

        /// <summary>创建 DamageNumber 默认表现；池容量较大，适合短时间连续伤害数字。</summary>
        private static AppUINoticeVisualSettings CreateDamageNumberDefaults()
        {
            AppUINoticeVisualSettings settings = new AppUINoticeVisualSettings();
            settings.ConfigureDefaults(0.9f, 0.25f, 88f, 8, 64, 34, new Color(1f, 0.25f, 0.18f, 1f));
            return settings;
        }
    }
}
