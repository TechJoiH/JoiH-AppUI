namespace Joi.H.AppUI
{
    /// <summary>
    /// UI 页面销毁策略接口。
    /// ReleaseInstance 会按 UIPageDefinition.DestroyStrategyId 选择策略，并在策略异常时继续释放资源句柄。
    /// </summary>
    public interface IUIDestroyStrategy
    {
        /// <summary>策略 ID；空字符串表示默认策略。</summary>
        string StrategyId { get; }

        /// <summary>销毁页面实例对应的 GameObject 或执行自定义回收。</summary>
        void Destroy(UIPageInstance instance);
    }
}
