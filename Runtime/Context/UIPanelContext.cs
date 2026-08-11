namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面 Controller 上下文。
    /// PanelBaseController 通过它获取 PageId、Definition 和 UI 服务。
    /// </summary>
    public sealed class UIPanelContext : UIControllerContext
    {
        /// <summary>当前页面 ID。</summary>
        public string PageId { get; private set; }

        /// <summary>当前页面定义。</summary>
        public UIPageDefinition Definition { get; private set; }

        /// <summary>
        /// 当前页面受限的焦点 Scope 句柄。
        /// 仅实现 IAppUIFocusDefinitionProvider 的页面会在 OnInit 返回后获得该句柄。
        /// </summary>
        public IAppUIFocusScopeHandle FocusScope { get; private set; }

        /// <summary>
        /// 创建页面上下文。
        /// 保留旧构造签名用于兼容测试代码和旧调用点；Notice 服务为空时页面仍可正常运行。
        /// </summary>
        public UIPanelContext(
            IUIControllerService ui,
            string pageId,
            UIPageDefinition definition)
            : this(ui, null, pageId, definition)
        {
        }

        /// <summary>
        /// 创建带 Notice 服务的页面上下文。
        /// AppUIManager 在页面实例化后注入，Controller 可通过 Context.Notices 触发 Toast/Tooltip 等轻量提示。
        /// </summary>
        public UIPanelContext(
            IUIControllerService ui,
            INoticeService notices,
            string pageId,
            UIPageDefinition definition)
            : base(ui, notices)
        {
            PageId = pageId ?? string.Empty;
            Definition = definition;
        }

        internal void SetFocusScope(IAppUIFocusScopeHandle focusScope)
        {
            FocusScope = focusScope;
        }
    }
}
