namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面实例状态。
    /// State 表示生命周期状态；StackVisible 表示栈遮挡可见性，两者共同决定页面是否实际显示。
    /// </summary>
    public enum UIPageState
    {
        None,
        Loading,
        Initializing,
        Open,
        Hidden,
        Disposed,
        Released,
    }
}
