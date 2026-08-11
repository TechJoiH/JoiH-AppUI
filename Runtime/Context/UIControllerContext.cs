namespace Joi.H.AppUI
{
    /// <summary>
    /// UI Controller 上下文基类。
    /// 统一暴露 UI 服务，具体页面或 Group 上下文再追加自身 ID 与 Definition。
    /// </summary>
    public abstract class UIControllerContext
    {
        /// <summary>框架 UI 服务入口。</summary>
        public IUIControllerService UI { get; private set; }

        /// <summary>
        /// 轻量提示服务入口。
        /// 它独立于 IUIService 注册，页面 Controller 通过上下文使用 Toast/Tooltip 等表现能力。
        /// </summary>
        public INoticeService Notices { get; private set; }

        /// <summary>
        /// 创建基础 Controller 上下文。
        /// 该构造保留给旧调用点；未注入 Notice 时，Controller 应自行判空再使用提示能力。
        /// </summary>
        protected UIControllerContext(IUIControllerService ui)
            : this(ui, null)
        {
        }

        /// <summary>
        /// 创建带 Notice 服务的基础 Controller 上下文。
        /// Notice 独立于 IUIService，避免把轻量提示方法继续堆到页面服务门面上。
        /// </summary>
        protected UIControllerContext(IUIControllerService ui, INoticeService notices)
        {
            UI = ui;
            Notices = notices;
        }
    }
}
