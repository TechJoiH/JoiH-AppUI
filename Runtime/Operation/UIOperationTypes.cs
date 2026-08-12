using System.Threading;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面意图类型。
    /// Active operation 表示正在执行的意图，pending intent 表示同 PageId 忙碌期间等待下一轮执行的意图。
    /// </summary>
    public enum UIPageIntent
    {
        Open,
        Refresh,
        Close,
        Release,
        Pause,
        Resume,
    }

    /// <summary>
    /// Operation 生命周期状态。
    /// Created/Running/Cancelling 属于 active 状态；Completed/Cancelled/Failed/Expired 属于终态。
    /// </summary>
    public enum UIOperationState
    {
        Created,
        Running,
        Cancelling,
        Completed,
        Cancelled,
        Failed,
        Expired,
    }

    /// <summary>
    /// Operation 校验结果。
    /// OperationCoordinator 只产出 Cancelled/Expired；SceneScopeInvalid 由 AppUIManager 串联 SceneScopeCoordinator 后补充。
    /// </summary>
    internal enum UIOperationCheckResult
    {
        Valid,
        Cancelled,
        SceneScopeInvalid,
        Expired,
    }

    /// <summary>
    /// 页面操作版本号。
    /// 每次 Open/Close/Refresh 开始时递增，用于 await 返回后确认实例仍属于当前操作。
    /// </summary>
    public readonly struct UIPageOperationVersion
    {
        public readonly int Value;

        public UIPageOperationVersion(int value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// 单页 pending 意图。
    /// 同一 PageId 只保留一个实例，新的高优先级或同优先级意图会覆盖旧意图，并完成旧 completion 为 OperationExpired。
    /// </summary>
    public sealed class UIPendingIntent
    {
        public string PageId;
        public UIPageIntent Intent;
        public int Priority;
        public UIOpenArgs OpenArgs;
        public UIRefreshArgs RefreshArgs;
        public UICloseRequest CloseRequest;
        internal IUIOperationSource<UIOpenResult> OpenSource;
        internal IUIOperationSource<UICloseResult> CloseSource;
        internal IUIOperationSource<UIRefreshResult> RefreshSource;
    }

    /// <summary>
    /// 页面操作的只读运行信息。
    /// AppUIManager 持有具体生命周期，OperationCoordinator 只根据该接口判断 busy、取消和版本过期。
    /// </summary>
    public interface IUIPageOperation
    {
        string PageId { get; }
        UIPageOperationVersion Version { get; }
        string SceneScopeId { get; }
        CancellationToken CancellationToken { get; }
        UIOperationState State { get; }
        bool IsActive { get; }
        void MarkRunning();
        void MarkCancelling();
        void MarkCompleted();
        void MarkCancelled();
        void MarkFailed();
        void MarkExpired();
    }

    /// <summary>
    /// 页面操作基类。
    /// 统一保存 PageId、版本、SceneScopeId、CancellationToken 和状态迁移方法。
    /// </summary>
    public abstract class UIPageOperationBase : IUIPageOperation
    {
        public string PageId { get; set; }
        public UIPageOperationVersion Version { get; set; }
        public string SceneScopeId { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public UIOperationState State { get; private set; }

        internal bool IsActive
        {
            get
            {
                return State == UIOperationState.Created ||
                       State == UIOperationState.Running ||
                       State == UIOperationState.Cancelling;
            }
        }

        bool IUIPageOperation.IsActive
        {
            get { return IsActive; }
        }

        internal void MarkRunning()
        {
            State = UIOperationState.Running;
        }

        internal void MarkCancelling()
        {
            State = UIOperationState.Cancelling;
        }

        internal void MarkCompleted()
        {
            State = UIOperationState.Completed;
        }

        internal void MarkCancelled()
        {
            State = UIOperationState.Cancelled;
        }

        internal void MarkFailed()
        {
            State = UIOperationState.Failed;
        }

        internal void MarkExpired()
        {
            State = UIOperationState.Expired;
        }

        void IUIPageOperation.MarkRunning()
        {
            MarkRunning();
        }

        void IUIPageOperation.MarkCancelling()
        {
            MarkCancelling();
        }

        void IUIPageOperation.MarkCompleted()
        {
            MarkCompleted();
        }

        void IUIPageOperation.MarkCancelled()
        {
            MarkCancelled();
        }

        void IUIPageOperation.MarkFailed()
        {
            MarkFailed();
        }

        void IUIPageOperation.MarkExpired()
        {
            MarkExpired();
        }
    }

    /// <summary>
    /// 打开页面操作。
    /// Args 保存本次 Open 的数据、取消 token、SceneScopeId 和打开回调。
    /// </summary>
    public sealed class UIOpenOperation : UIPageOperationBase
    {
        public UIOpenArgs Args { get; set; }
        internal IUIOperationSource<UIOpenResult> Source { get; set; }
    }

    /// <summary>
    /// 关闭页面操作。
    /// Request 保存 ReleaseOnClose、取消 token 和 SceneScopeId。
    /// </summary>
    public sealed class UICloseOperation : UIPageOperationBase
    {
        public UICloseRequest Request { get; set; }
        internal IUIOperationSource<UICloseResult> Source { get; set; }
    }

    /// <summary>
    /// 刷新页面操作。
    /// Args 保存完整刷新参数；Data 是旧调用路径的便捷缓存。
    /// </summary>
    public sealed class UIRefreshOperation : UIPageOperationBase
    {
        public UIRefreshArgs Args { get; set; }
        public object Data { get; set; }
        internal IUIOperationSource<UIRefreshResult> Source { get; set; }
    }
}
