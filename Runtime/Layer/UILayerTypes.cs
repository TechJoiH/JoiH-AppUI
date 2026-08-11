namespace Joi.H.AppUI
{
    /// <summary>
    /// UI Layer 枚举。
    /// 枚举顺序同时作为跨 Layer 顶层查询的优先级基础，值越靠后优先级越高。
    /// </summary>
    public enum UILayerId
    {
        SystemLayer,
        HudLayer,
        OverlayLayer,
        PopupLayer,
        ModalLayer,
        NoticeLayer,
        GuideLayer,
        LoadingLayer,
        DebugLayer,
    }

    /// <summary>
    /// Canvas 领域枚举。
    /// 多个 LayerRoot 可共享同一 CanvasDomain，例如 OverlayLayer 和 PopupLayer 共享 Overlay Canvas。
    /// </summary>
    public enum UICanvasDomain
    {
        System,
        Hud,
        Overlay,
        Modal,
        Notice,
        Guide,
        Loading,
        Debug,
    }
}
