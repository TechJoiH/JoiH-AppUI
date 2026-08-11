namespace Joi.H.AppUI
{
    /// <summary>
    /// UI Group 控制器基类。
    /// Group 作为可复用或嵌入式子作用域使用，不直接参与页面 Open/Close 流程。
    /// </summary>
    public abstract class UIGroupBase : UIBaseController
    {
        /// <summary>Group 上下文，包含父级 UI 服务和 Group 定义信息。</summary>
        protected UIGroupContext Context { get; private set; }

        internal void SetContext(UIGroupContext context)
        {
            Context = context;
        }
    }
}
