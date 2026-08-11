namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面 Cancel 处理接口。
    /// 当前焦点页实现该接口时，统一 Cancel 流程会先调用它；返回 true 表示已消费，不再执行 CloseOnCancel。
    /// </summary>
    public interface IUICancelHandler
    {
        /// <summary>处理取消意图；返回 true 表示页面已自行消费。</summary>
        bool HandleCancel();
    }
}
