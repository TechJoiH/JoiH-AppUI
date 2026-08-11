namespace Joi.H.AppUI
{
    /// <summary>
    /// App UI Notice 独立服务接口。
    /// 业务系统通过 GameServiceRegistry 获取该服务，避免把轻量提示能力继续塞进 IUIService 主页面接口。
    /// </summary>
    public interface INoticeService
    {
        /// <summary>显示一条默认全局 Toast，并返回运行时句柄。</summary>
        ToastHandle Toast(string text);

        /// <summary>按完整请求显示 Toast，可携带 Scope、持续时间和颜色。</summary>
        ToastHandle Toast(in ToastNoticeRequest request);

        /// <summary>显示 Tooltip；Tooltip 默认需要调用 HideTooltip 或随 Scope 清理释放。</summary>
        TooltipHandle ShowTooltip(in TooltipNoticeRequest request);

        /// <summary>隐藏指定 Tooltip；无效或已回收句柄会被安全忽略。</summary>
        void HideTooltip(TooltipHandle handle);

        /// <summary>显示一条自动上浮淡出的普通浮动文本。</summary>
        FloatingTextHandle FloatingText(in FloatingTextNoticeRequest request);

        /// <summary>显示一条自动上浮淡出的伤害数字。</summary>
        DamageNumberHandle DamageNumber(in DamageNumberNoticeRequest request);

        /// <summary>清理指定 Scope 下仍然存活的 Notice；GlobalScope 不参与批量清理。</summary>
        void ClearScope(UIPageScope scope, string sceneScopeId);

        /// <summary>清理当前服务内所有 active Notice，通常用于 UI Runtime 关闭兜底。</summary>
        void ClearAll();
    }

    /// <summary>
    /// 提供 NoticeService 的轻量接口。
    /// AppUIRuntimeSystem 通过它从 IUIControllerService 对象上取出独立 Notice 服务并注册到 GameServiceRegistry。
    /// </summary>
    public interface INoticeServiceProvider
    {
        /// <summary>当前 UI Runtime 绑定的 Notice 服务实例。</summary>
        INoticeService Notices { get; }
    }
}
