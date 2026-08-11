using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Notice 运行时作用域。
    /// Notice 不属于页面实例，但需要跟随 Scene/Loading/Temporary 边界清理，因此单独保存 Scope 与 SceneScopeId。
    /// </summary>
    public readonly struct UINoticeScope
    {
        /// <summary>Notice 归属的生命周期作用域。</summary>
        public readonly UIPageScope Scope;

        /// <summary>Notice 归属的场景或临时 owner id；GlobalScope 固定为空字符串。</summary>
        public readonly string SceneScopeId;

        public UINoticeScope(UIPageScope scope, string sceneScopeId)
        {
            Scope = scope;
            SceneScopeId = scope == UIPageScope.GlobalScope ? string.Empty : sceneScopeId ?? string.Empty;
        }

        /// <summary>创建一个全局 Notice scope；不会被普通场景退出批量清理。</summary>
        public static UINoticeScope Global()
        {
            return new UINoticeScope(UIPageScope.GlobalScope, string.Empty);
        }

        /// <summary>创建一个随场景释放的 Notice scope。</summary>
        public static UINoticeScope Scene(string sceneScopeId)
        {
            return new UINoticeScope(UIPageScope.SceneScope, sceneScopeId);
        }

        /// <summary>创建一个随 Loading 流程显式释放的 Notice scope。</summary>
        public static UINoticeScope Loading(string loadingScopeId)
        {
            return new UINoticeScope(UIPageScope.LoadingScope, loadingScopeId);
        }

        /// <summary>创建一个随临时 owner 或场景兜底释放的 Notice scope。</summary>
        public static UINoticeScope Temporary(string temporaryScopeId)
        {
            return new UINoticeScope(UIPageScope.TemporaryScope, temporaryScopeId);
        }

        /// <summary>
        /// 判断当前 Notice 是否属于指定批量清理范围。
        /// GlobalScope 不走批量释放，避免误清跨场景提示或后续 App-wide 常驻提示。
        /// </summary>
        public bool Matches(UIPageScope scope, string sceneScopeId)
        {
            if (Scope == UIPageScope.GlobalScope || scope == UIPageScope.GlobalScope)
            {
                return false;
            }

            return Scope == scope && string.Equals(SceneScopeId, sceneScopeId ?? string.Empty);
        }
    }

    /// <summary>
    /// Notice 坐标语义。
    /// Screen 直接使用屏幕坐标，World 会通过 Camera 转换，Target 会跟随 Transform。
    /// </summary>
    public enum UINoticeCoordinateMode
    {
        Screen,
        World,
        Target,
    }

    /// <summary>Toast 显示句柄，可用于后续扩展更新或主动关闭指定 Toast。</summary>
    public readonly struct ToastHandle
    {
        /// <summary>Toast 实例 ID；0 表示无效句柄。</summary>
        public readonly int Id;

        public bool IsValid
        {
            get { return Id > 0; }
        }

        public ToastHandle(int id)
        {
            Id = id;
        }
    }

    /// <summary>Tooltip 显示句柄，用于隐藏指定 Tooltip。</summary>
    public readonly struct TooltipHandle
    {
        /// <summary>Tooltip 实例 ID；0 表示无效句柄。</summary>
        public readonly int Id;

        public bool IsValid
        {
            get { return Id > 0; }
        }

        public TooltipHandle(int id)
        {
            Id = id;
        }
    }

    /// <summary>FloatingText 显示句柄，第一版主要用于调试和后续主动关闭扩展。</summary>
    public readonly struct FloatingTextHandle
    {
        /// <summary>FloatingText 实例 ID；0 表示无效句柄。</summary>
        public readonly int Id;

        public bool IsValid
        {
            get { return Id > 0; }
        }

        public FloatingTextHandle(int id)
        {
            Id = id;
        }
    }

    /// <summary>DamageNumber 显示句柄，第一版主要用于调试和后续主动关闭扩展。</summary>
    public readonly struct DamageNumberHandle
    {
        /// <summary>DamageNumber 实例 ID；0 表示无效句柄。</summary>
        public readonly int Id;

        public bool IsValid
        {
            get { return Id > 0; }
        }

        public DamageNumberHandle(int id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// Toast 请求参数。
    /// 文本第一版直接显示调用方传入内容，不在 NoticeService 内做多语言查表。
    /// </summary>
    public struct ToastNoticeRequest
    {
        public string Text;
        public UINoticeScope Scope;
        public float Duration;
        public float FadeDuration;
        public Color TextColor;

        public static ToastNoticeRequest Create(string text)
        {
            return new ToastNoticeRequest
            {
                Text = text,
                Scope = UINoticeScope.Global(),
            };
        }
    }

    /// <summary>
    /// Tooltip 请求参数。
    /// Tooltip 默认不自动消失，调用方通过 HideTooltip 或 Scope 清理释放。
    /// </summary>
    public struct TooltipNoticeRequest
    {
        public string Text;
        public UINoticeScope Scope;
        public UINoticeCoordinateMode CoordinateMode;
        public Vector2 ScreenPosition;
        public Vector3 WorldPosition;
        public Transform Target;
        public Camera Camera;
        public Vector2 Offset;
        public bool FollowTarget;
        public Color TextColor;
    }

    /// <summary>
    /// FloatingText 请求参数。
    /// 适合资源变化、状态提示等轻量浮动文本，自动上浮并淡出。
    /// </summary>
    public struct FloatingTextNoticeRequest
    {
        public string Text;
        public UINoticeScope Scope;
        public UINoticeCoordinateMode CoordinateMode;
        public Vector2 ScreenPosition;
        public Vector3 WorldPosition;
        public Transform Target;
        public Camera Camera;
        public Vector2 Offset;
        public float Duration;
        public float FadeDuration;
        public float RiseDistance;
        public Color TextColor;
    }

    /// <summary>
    /// DamageNumber 请求参数。
    /// 第一版只负责 UI 表现，不直接订阅战斗或 ECS 伤害事件。
    /// </summary>
    public struct DamageNumberNoticeRequest
    {
        public string Text;
        public int Amount;
        public bool IsCritical;
        public UINoticeScope Scope;
        public UINoticeCoordinateMode CoordinateMode;
        public Vector2 ScreenPosition;
        public Vector3 WorldPosition;
        public Transform Target;
        public Camera Camera;
        public Vector2 Offset;
        public float Duration;
        public float FadeDuration;
        public float RiseDistance;
        public Color TextColor;
    }
}
