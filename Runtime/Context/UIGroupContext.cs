namespace Joi.H.AppUI
{
    /// <summary>
    /// Group Controller 上下文。
    /// UIGroupBase 通过它获取 GroupId、Definition 和 UI 服务。
    /// </summary>
    public sealed class UIGroupContext : UIControllerContext
    {
        /// <summary>当前 Group ID。</summary>
        public string GroupId { get; private set; }

        /// <summary>当前 Group 定义。</summary>
        public UIGroupDefinition Definition { get; private set; }

        /// <summary>
        /// 创建 Group 上下文。
        /// 保留旧构造签名用于兼容已有 Group 初始化流程；Notice 服务为空时不影响 Group 生命周期。
        /// </summary>
        public UIGroupContext(
            IUIControllerService ui,
            string groupId,
            UIGroupDefinition definition)
            : this(ui, null, groupId, definition)
        {
        }

        /// <summary>
        /// 创建带 Notice 服务的 Group 上下文。
        /// 后续 Group Controller 如需轻量提示，可直接复用页面同一套 NoticeService。
        /// </summary>
        public UIGroupContext(
            IUIControllerService ui,
            INoticeService notices,
            string groupId,
            UIGroupDefinition definition)
            : base(ui, notices)
        {
            GroupId = groupId ?? string.Empty;
            Definition = definition;
        }
    }
}
