using Cysharp.Threading.Tasks;

namespace Joi.H.AppUI
{
    /// <summary>
    /// App UI 对外服务接口。
    /// 业务系统只通过该接口打开、关闭、刷新页面，避免直接依赖 AppUIManager 的内部协调器。
    /// </summary>
    public interface IUIService
    {
        /// <summary>按 PageId 打开页面，不携带数据。</summary>
        UniTask<UIOpenResult> OpenAsync(string pageId);

        /// <summary>按 PageId 打开页面，并把 data 传入页面 OnDataLoadEx。</summary>
        UniTask<UIOpenResult> OpenAsync(string pageId, object data);

        /// <summary>按完整打开参数打开页面，可携带数据、取消 token、SceneScopeId 和打开回调。</summary>
        UniTask<UIOpenResult> OpenAsync(string pageId, UIOpenArgs args);

        /// <summary>场景进入时绑定 UI，按 SceneUIBindingData 中的规则打开页面。</summary>
        UniTask BindSceneAsync(SceneUIBindingData bindingData);

        /// <summary>场景退出时解绑 UI，执行显式退出规则并释放匹配作用域页面。</summary>
        UniTask<UISceneExitResult> UnbindSceneAsync(SceneUIBindingData bindingData);

        /// <summary>释放指定 Scope 和 SceneScopeId 下的页面；GlobalScope 不走批量释放。</summary>
        UniTask<UIScopeReleaseResult> ReleaseScopeAsync(UIPageScope scope, string sceneScopeId);

        /// <summary>关闭页面，默认释放实例和资源。</summary>
        UniTask<UICloseResult> CloseAsync(string pageId);

        /// <summary>刷新页面数据，兼容旧调用入口。</summary>
        UniTask<UIRefreshResult> RefreshAsync(string pageId, object data);

        /// <summary>按完整刷新参数刷新页面，可携带取消 token 和 SceneScopeId。</summary>
        UniTask<UIRefreshResult> RefreshAsync(string pageId, UIRefreshArgs args);

        /// <summary>
        /// 执行唯一取消流程：焦点控件、活动子 Region、页面业务 Handler、
        /// 关闭按钮策略，最后才按 CloseOnCancel 决定是否关闭。
        /// </summary>
        UniTask<UICancelResult> CancelAsync();

        /// <summary>全局关闭当前最高优先级 Layer 的最顶层可见页面。</summary>
        UniTask<UICloseResult> CloseTopAsync();

        /// <summary>关闭指定 Layer 内的最顶层可见页面。</summary>
        UniTask<UICloseResult> CloseTopAsync(UILayerId layerId);

        /// <summary>查询页面当前是否处于 Open 状态。</summary>
        bool IsOpen(string pageId);

        /// <summary>查询页面当前是否有 active Open operation。</summary>
        bool IsOpening(string pageId);

        /// <summary>尝试读取页面当前状态；页面不存在时返回 false。</summary>
        bool TryGetPageState(string pageId, out UIPageState state);
    }

    /// <summary>
    /// Controller 内部使用的 UI 服务接口。
    /// 比 IUIService 多暴露完整 CloseRequest 入口，便于 Controller 或框架内部携带 ReleaseOnClose、SceneScopeId 和取消 token。
    /// </summary>
    public interface IUIControllerService : IUIService
    {
        /// <summary>按完整关闭请求关闭页面。</summary>
        UniTask<UICloseResult> CloseAsync(string pageId, UICloseRequest request);
    }
}
