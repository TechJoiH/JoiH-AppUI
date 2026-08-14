using System;
using System.Collections.Generic;

namespace Joi.H.AppUI
{
    /// <summary>页面打开失败原因。</summary>
    public enum UIPageOpenError
    {
        None,
        DefinitionNotFound,
        InvalidDefinition,
        LayerNotFound,
        ResourceLoadFailed,
        InstanceCreationFailed,
        ControllerMissing,
        ControllerInvalid,
        Cancelled,
        SceneScopeInvalid,
        OperationExpired,
        AlreadyOpenRejected,
        LifecycleFailed,
        Exception,
    }

    /// <summary>页面关闭失败原因。</summary>
    public enum UICloseError
    {
        None,
        NotOpen,
        Rejected,
        Cancelled,
        SceneScopeInvalid,
        OperationExpired,
        Busy,
        LifecycleFailed,
        Exception,
    }

    /// <summary>页面刷新失败原因。</summary>
    public enum UIRefreshError
    {
        None,
        NotOpen,
        Cancelled,
        SceneScopeInvalid,
        OperationExpired,
        Busy,
        LifecycleFailed,
        Exception,
    }

    /// <summary>统一 Cancel 流程结果类型。</summary>
    public enum UICancelOutcome
    {
        NoTarget,
        Handled,
        CloseDisabled,
        Closed,
        CloseRejected,
        CloseFailed,
        HandlerFailed,
    }

    /// <summary>
    /// 页面打开结果。
    /// 成功时携带 UIPageHandle；失败时携带错误枚举和可选异常。
    /// </summary>
    public sealed class UIOpenResult
    {
        public bool Success { get; private set; }
        public UIPageHandle Handle { get; private set; }
        public UIPageOpenError Error { get; private set; }
        public Exception Exception { get; private set; }

        /// <summary>创建成功打开结果。</summary>
        public static UIOpenResult Ok(UIPageHandle handle)
        {
            return new UIOpenResult
            {
                Success = true,
                Handle = handle,
                Error = UIPageOpenError.None,
            };
        }

        /// <summary>创建打开失败结果。</summary>
        public static UIOpenResult Fail(UIPageOpenError error, Exception exception = null)
        {
            return new UIOpenResult
            {
                Success = false,
                Error = error,
                Exception = exception,
            };
        }
    }

    /// <summary>
    /// 页面关闭结果。
    /// 包含目标 PageId、关闭后的最终状态、错误枚举和可选异常。
    /// </summary>
    public sealed class UICloseResult
    {
        public bool Success { get; private set; }
        public string PageId { get; private set; }
        public UIPageState State { get; private set; }
        public UICloseError Error { get; private set; }
        public Exception Exception { get; private set; }

        /// <summary>创建关闭成功结果。</summary>
        public static UICloseResult Ok(string pageId, UIPageState state)
        {
            return new UICloseResult
            {
                Success = true,
                PageId = pageId ?? string.Empty,
                State = state,
                Error = UICloseError.None,
            };
        }

        /// <summary>创建关闭失败结果。</summary>
        public static UICloseResult Fail(
            string pageId,
            UIPageState state,
            UICloseError error,
            Exception exception = null)
        {
            return new UICloseResult
            {
                Success = false,
                PageId = pageId ?? string.Empty,
                State = state,
                Error = error,
                Exception = exception,
            };
        }
    }

    /// <summary>
    /// 统一 Cancel 流程结果。
    /// Consumed 表示当前焦点页是否消费了本次 Cancel 意图，不等同于一定关闭成功。
    /// </summary>
    public sealed class UICancelResult
    {
        public bool Consumed { get; private set; }
        public string PageId { get; private set; }
        public UICancelOutcome Outcome { get; private set; }
        public UICloseResult CloseResult { get; private set; }
        public Exception Exception { get; private set; }

        /// <summary>没有可处理 Cancel 的目标页面。</summary>
        public static UICancelResult NoTarget()
        {
            return new UICancelResult
            {
                Consumed = false,
                PageId = string.Empty,
                Outcome = UICancelOutcome.NoTarget,
            };
        }

        /// <summary>Cancel Handler 已处理。</summary>
        public static UICancelResult Handled(string pageId)
        {
            return ConsumedResult(pageId, UICancelOutcome.Handled, null, null);
        }

        /// <summary>当前页未启用 CloseOnCancel，本次 Cancel 仍被消费。</summary>
        public static UICancelResult CloseDisabled(string pageId)
        {
            return ConsumedResult(pageId, UICancelOutcome.CloseDisabled, null, null);
        }

        /// <summary>Cancel 触发关闭且关闭成功。</summary>
        public static UICancelResult Closed(string pageId, UICloseResult closeResult)
        {
            return ConsumedResult(pageId, UICancelOutcome.Closed, closeResult, null);
        }

        /// <summary>Cancel 触发关闭但 CanClose 拒绝。</summary>
        public static UICancelResult CloseRejected(string pageId, UICloseResult closeResult)
        {
            return ConsumedResult(pageId, UICancelOutcome.CloseRejected, closeResult, null);
        }

        /// <summary>Cancel 触发关闭但关闭流程失败。</summary>
        public static UICancelResult CloseFailed(string pageId, UICloseResult closeResult)
        {
            return ConsumedResult(pageId, UICancelOutcome.CloseFailed, closeResult, null);
        }

        /// <summary>Cancel Handler 抛异常。</summary>
        public static UICancelResult HandlerFailed(string pageId, Exception exception)
        {
            return ConsumedResult(pageId, UICancelOutcome.HandlerFailed, null, exception);
        }

        private static UICancelResult ConsumedResult(
            string pageId,
            UICancelOutcome outcome,
            UICloseResult closeResult,
            Exception exception)
        {
            return new UICancelResult
            {
                Consumed = true,
                PageId = pageId ?? string.Empty,
                Outcome = outcome,
                CloseResult = closeResult,
                Exception = exception,
            };
        }
    }

    /// <summary>
    /// 页面刷新结果。
    /// 包含目标 PageId、刷新时页面状态、错误枚举和可选异常。
    /// </summary>
    public sealed class UIRefreshResult
    {
        public bool Success { get; private set; }
        public string PageId { get; private set; }
        public UIPageState State { get; private set; }
        public UIRefreshError Error { get; private set; }
        public Exception Exception { get; private set; }

        /// <summary>创建刷新成功结果。</summary>
        public static UIRefreshResult Ok(string pageId, UIPageState state)
        {
            return new UIRefreshResult
            {
                Success = true,
                PageId = pageId ?? string.Empty,
                State = state,
                Error = UIRefreshError.None,
            };
        }

        /// <summary>创建刷新失败结果。</summary>
        public static UIRefreshResult Fail(
            string pageId,
            UIPageState state,
            UIRefreshError error,
            Exception exception = null)
        {
            return new UIRefreshResult
            {
                Success = false,
                PageId = pageId ?? string.Empty,
                State = state,
                Error = error,
                Exception = exception,
            };
        }
    }

    public sealed class UISceneBindResult
    {
        public bool Success { get; private set; }
        public string SceneId { get; private set; }
        public string SceneScopeId { get; private set; }
        public IReadOnlyList<UIOpenResult> OpenResults { get; private set; }
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }

        public static UISceneBindResult FromResults(
            string sceneId,
            string sceneScopeId,
            List<UIOpenResult> openResults)
        {
            List<UIOpenResult> results = openResults != null
                ? new List<UIOpenResult>(openResults)
                : new List<UIOpenResult>(0);
            int successCount = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i] != null && results[i].Success)
                {
                    successCount++;
                }
            }

            return new UISceneBindResult
            {
                Success = successCount == results.Count,
                SceneId = sceneId ?? string.Empty,
                SceneScopeId = sceneScopeId ?? string.Empty,
                OpenResults = results,
                SuccessCount = successCount,
                FailureCount = results.Count - successCount,
            };
        }
    }

    /// <summary>
    /// 场景退出 UI 清理聚合结果。
    /// 收集显式 CloseOnSceneExit 和默认 Scope 释放产生的所有关闭结果。
    /// </summary>
    public sealed class UISceneExitResult
    {
        public bool Success { get; private set; }
        public string SceneId { get; private set; }
        public string SceneScopeId { get; private set; }
        public IReadOnlyList<UICloseResult> CloseResults { get; private set; }
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }

        /// <summary>根据关闭结果列表创建场景退出聚合结果。</summary>
        public static UISceneExitResult FromResults(
            string sceneId,
            string sceneScopeId,
            List<UICloseResult> closeResults)
        {
            List<UICloseResult> results = closeResults != null
                ? new List<UICloseResult>(closeResults)
                : new List<UICloseResult>(0);
            int successCount = UIAggregateResultUtility.CountSuccessfulResults(results);
            int failureCount = results.Count - successCount;
            return new UISceneExitResult
            {
                Success = failureCount == 0,
                SceneId = sceneId ?? string.Empty,
                SceneScopeId = sceneScopeId ?? string.Empty,
                CloseResults = results,
                SuccessCount = successCount,
                FailureCount = failureCount,
            };
        }
    }

    /// <summary>
    /// Scope 批量释放聚合结果。
    /// 用于 LoadingScope、TemporaryScope 或 SceneScope owner 显式释放边界。
    /// </summary>
    public sealed class UIScopeReleaseResult
    {
        public bool Success { get; private set; }
        public UIPageScope Scope { get; private set; }
        public string SceneScopeId { get; private set; }
        public IReadOnlyList<UICloseResult> CloseResults { get; private set; }
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }

        /// <summary>根据关闭结果列表创建 Scope 释放聚合结果。</summary>
        public static UIScopeReleaseResult FromResults(
            UIPageScope scope,
            string sceneScopeId,
            List<UICloseResult> closeResults)
        {
            List<UICloseResult> results = closeResults != null
                ? new List<UICloseResult>(closeResults)
                : new List<UICloseResult>(0);
            int successCount = UIAggregateResultUtility.CountSuccessfulResults(results);
            int failureCount = results.Count - successCount;
            return new UIScopeReleaseResult
            {
                Success = failureCount == 0,
                Scope = scope,
                SceneScopeId = sceneScopeId ?? string.Empty,
                CloseResults = results,
                SuccessCount = successCount,
                FailureCount = failureCount,
            };
        }
    }

    /// <summary>
    /// UI 聚合结果工具。
    /// </summary>
    internal static class UIAggregateResultUtility
    {
        /// <summary>统计关闭结果列表中的成功数量。</summary>
        public static int CountSuccessfulResults(IReadOnlyList<UICloseResult> closeResults)
        {
            if (closeResults == null)
            {
                return 0;
            }

            int successCount = 0;
            for (int i = 0; i < closeResults.Count; i++)
            {
                UICloseResult result = closeResults[i];
                if (result != null && result.Success)
                {
                    successCount++;
                }
            }

            return successCount;
        }
    }
}
