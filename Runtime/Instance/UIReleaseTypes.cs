namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面实例释放的触发原因。
    /// 该枚举只用于运行时内部记录释放语义，方便后续排查失败清理、场景退出和 Manager 销毁等不同路径。
    /// </summary>
    internal enum UIReleaseReason
    {
        /// <summary>默认关闭并释放页面。</summary>
        CloseRelease,

        /// <summary>打开流程失败后的半成品清理。</summary>
        OpenFailed,

        /// <summary>场景退出规则触发的释放。</summary>
        SceneExit,

        /// <summary>按 Scope 批量释放页面。</summary>
        ScopeRelease,

        /// <summary>AppUIManager 销毁时的兜底释放。</summary>
        ManagerDestroy,
    }

    /// <summary>
    /// 页面释放后的轻量结果。
    /// Releaser 只说明外层是否需要重新提交显示状态，避免释放多个页面时在内部反复刷新 Presentation。
    /// </summary>
    internal readonly struct UIReleaseResult
    {
        /// <summary>
        /// 是否有栈、焦点、暂停或输入状态变化，需要外层调用 Presentation Commit。
        /// </summary>
        public readonly bool PresentationDirty;

        public UIReleaseResult(bool presentationDirty)
        {
            PresentationDirty = presentationDirty;
        }

        /// <summary>
        /// 表示释放流程没有造成显示状态变化。
        /// </summary>
        public static UIReleaseResult Clean
        {
            get { return new UIReleaseResult(false); }
        }

        /// <summary>
        /// 表示释放流程已经改变实例展示状态，外层应在合适时机统一刷新。
        /// </summary>
        public static UIReleaseResult Dirty
        {
            get { return new UIReleaseResult(true); }
        }
    }
}
