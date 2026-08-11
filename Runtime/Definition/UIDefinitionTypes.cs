namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面生命周期作用域。
    /// 决定页面在场景进入、场景退出、Loading 流程和临时 owner 释放时是否被自动清理。
    /// </summary>
    public enum UIPageScope
    {
        GlobalScope,
        SceneScope,
        LoadingScope,
        TemporaryScope,
    }

    /// <summary>
    /// Group 定义作用域。
    /// 用于区分嵌入式子界面、可复用组件和列表项模板。
    /// </summary>
    public enum UIGroupScope
    {
        Embedded,
        Reusable,
        ItemTemplate,
    }

    /// <summary>
    /// 页面重复打开策略。
    /// 决定页面已经 Open/Opening 时，新 Open 请求是拒绝、聚焦、刷新还是排队。
    /// </summary>
    public enum UIOpenPolicy
    {
        RejectIfOpeningOrOpen,
        FocusExisting,
        RefreshExisting,
        QueueIfBusy,
    }
}
