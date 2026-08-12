namespace Joi.H.AppUI
{
    /// <summary>
    /// AppUI service consumed by application code.
    /// All lifecycle commands return backend-neutral operations.
    /// </summary>
    public interface IUIService
    {
        IUIOperation<UIOpenResult> Open(string pageId);

        IUIOperation<UIOpenResult> Open(string pageId, object data);

        IUIOperation<UIOpenResult> Open(string pageId, UIOpenArgs args);

        IUIOperation<UISceneBindResult> BindScene(
            SceneUIBindingData bindingData);

        IUIOperation<UISceneExitResult> UnbindScene(
            SceneUIBindingData bindingData);

        IUIOperation<UIScopeReleaseResult> ReleaseScope(
            UIPageScope scope,
            string sceneScopeId);

        IUIOperation<UICloseResult> Close(string pageId);

        IUIOperation<UIRefreshResult> Refresh(
            string pageId,
            object data);

        IUIOperation<UIRefreshResult> Refresh(
            string pageId,
            UIRefreshArgs args);

        IUIOperation<UICancelResult> Cancel();

        IUIOperation<UICloseResult> CloseTop();

        IUIOperation<UICloseResult> CloseTop(UILayerId layerId);

        bool IsOpen(string pageId);

        bool IsOpening(string pageId);

        bool TryGetPageState(string pageId, out UIPageState state);

    }

    /// <summary>
    /// Controller-facing service with the full close request overload.
    /// </summary>
    public interface IUIControllerService : IUIService
    {
        IUIOperation<UICloseResult> Close(
            string pageId,
            UICloseRequest request);
    }
}
