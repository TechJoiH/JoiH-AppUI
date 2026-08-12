using System;
using System.Collections.Generic;

namespace Joi.H.AppUI
{
    /// <summary>
    /// UI 操作状态协调器。
    /// 该类只管理 active operation、operation version 和单页 pending intent，不直接执行 Open/Close/Refresh 生命周期，避免反向依赖 AppUIManager。
    /// </summary>
    internal sealed class UIOperationCoordinator
    {
        private readonly Dictionary<string, IUIPageOperation> activeOperations =
            new Dictionary<string, IUIPageOperation>(8);
        private readonly Dictionary<string, UIPendingIntent> pendingIntentsByPageId =
            new Dictionary<string, UIPendingIntent>(8);
        private readonly Func<string, UIPageState> knownPageStateProvider;

        private int nextOperationVersion;

        /// <summary>
        /// 创建操作协调器。
        /// knownPageStateProvider 只用于完成 pending 失败结果时带上当前页面状态。
        /// </summary>
        public UIOperationCoordinator(Func<string, UIPageState> pageStateProvider)
        {
            knownPageStateProvider = pageStateProvider;
        }

        public bool TryEnqueueOpenPending(
            string pageId,
            UIOpenArgs args,
            IUIOperationSource<UIOpenResult> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return TryStorePendingIntent(new UIPendingIntent
            {
                PageId = pageId ?? string.Empty,
                Intent = UIPageIntent.Open,
                Priority = GetPendingIntentPriority(
                    UIPageIntent.Open,
                    false),
                OpenArgs = args,
                OpenSource = source,
            });
        }

        public bool TryEnqueueClosePending(
            string pageId,
            UICloseRequest request,
            IUIOperationSource<UICloseResult> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            UIPageIntent intentType = request.ReleaseOnClose
                ? UIPageIntent.Release
                : UIPageIntent.Close;
            return TryStorePendingIntent(new UIPendingIntent
            {
                PageId = pageId ?? string.Empty,
                Intent = intentType,
                Priority = GetPendingIntentPriority(
                    intentType,
                    request.ReleaseOnClose),
                CloseRequest = request,
                CloseSource = source,
            });
        }

        public bool TryEnqueueRefreshPending(
            string pageId,
            UIRefreshArgs args,
            IUIOperationSource<UIRefreshResult> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return TryStorePendingIntent(new UIPendingIntent
            {
                PageId = pageId ?? string.Empty,
                Intent = UIPageIntent.Refresh,
                Priority = GetPendingIntentPriority(
                    UIPageIntent.Refresh,
                    false),
                RefreshArgs = args,
                RefreshSource = source,
            });
        }

        /// <summary>
        /// 尝试取出一个待执行 pending。
        /// 只有页面当前不 busy 时才会移除 pending，真实执行由 AppUIManager 在外层 drain。
        /// </summary>
        public bool TryTakePendingIntent(string pageId, out UIPendingIntent intent)
        {
            intent = null;
            if (string.IsNullOrEmpty(pageId) || IsPageBusy(pageId))
            {
                return false;
            }

            if (!pendingIntentsByPageId.TryGetValue(pageId, out intent) || intent == null)
            {
                return false;
            }

            pendingIntentsByPageId.Remove(pageId);
            return true;
        }

        /// <summary>
        /// 判断指定页面是否存在 pending。
        /// AppUIManager 用它决定 active operation 结束后是否触发 drain。
        /// </summary>
        public bool HasPendingIntent(string pageId)
        {
            return !string.IsNullOrEmpty(pageId) && pendingIntentsByPageId.ContainsKey(pageId);
        }

        /// <summary>
        /// 将 pending 完成为 OperationExpired。
        /// 用于新 pending 覆盖旧 pending，通知旧调用方它已经不会再执行。
        /// </summary>
        public void CompletePendingIntentAsExpired(UIPendingIntent intent)
        {
            if (intent == null)
            {
                return;
            }

            switch (intent.Intent)
            {
                case UIPageIntent.Open:
                    if (intent.OpenSource != null)
                    {
                        intent.OpenSource.TrySetExpired();
                        break;
                    }

                    break;
                case UIPageIntent.Release:
                case UIPageIntent.Close:
                    if (intent.CloseSource != null)
                    {
                        intent.CloseSource.TrySetExpired();
                        break;
                    }

                    break;
                case UIPageIntent.Refresh:
                    if (intent.RefreshSource != null)
                    {
                        intent.RefreshSource.TrySetExpired();
                        break;
                    }

                    break;
            }
        }

        /// <summary>
        /// 将 pending 完成为 Cancelled。
        /// Manager 销毁时会使用该方法，确保没有调用方一直等待完成通知。
        /// </summary>
        public void CompletePendingIntentAsCancelled(UIPendingIntent intent)
        {
            if (intent == null)
            {
                return;
            }

            switch (intent.Intent)
            {
                case UIPageIntent.Open:
                    if (intent.OpenSource != null)
                    {
                        intent.OpenSource.TrySetCancelled();
                        break;
                    }

                    break;
                case UIPageIntent.Release:
                case UIPageIntent.Close:
                    if (intent.CloseSource != null)
                    {
                        intent.CloseSource.TrySetCancelled();
                        break;
                    }

                    break;
                case UIPageIntent.Refresh:
                    if (intent.RefreshSource != null)
                    {
                        intent.RefreshSource.TrySetCancelled();
                        break;
                    }

                    break;
            }
        }

        /// <summary>
        /// 将 pending 执行异常转换为对应失败结果。
        /// AppUIManager 负责记录异常并调用这里完成调用方。
        /// </summary>
        public void CompletePendingIntentAsException(UIPendingIntent intent, Exception exception)
        {
            if (intent == null)
            {
                return;
            }

            switch (intent.Intent)
            {
                case UIPageIntent.Open:
                    if (intent.OpenSource != null)
                    {
                        intent.OpenSource.TrySetFailed(exception);
                        break;
                    }

                    break;
                case UIPageIntent.Release:
                case UIPageIntent.Close:
                    if (intent.CloseSource != null)
                    {
                        intent.CloseSource.TrySetFailed(exception);
                        break;
                    }

                    break;
                case UIPageIntent.Refresh:
                    if (intent.RefreshSource != null)
                    {
                        intent.RefreshSource.TrySetFailed(exception);
                        break;
                    }

                    break;
            }
        }

        /// <summary>
        /// 取消并清空所有 pending。
        /// 该方法用于 Manager 销毁，避免释放 UI 系统后仍有等待中的异步调用。
        /// </summary>
        public void CancelAllPendingIntents()
        {
            List<UIPendingIntent> pendingIntents = new List<UIPendingIntent>(pendingIntentsByPageId.Values);
            pendingIntentsByPageId.Clear();
            for (int i = 0; i < pendingIntents.Count; i++)
            {
                CompletePendingIntentAsCancelled(pendingIntents[i]);
            }
        }

        /// <summary>
        /// 创建 Open operation，并分配新的操作版本号。
        /// SceneScopeId 在这里仅做空值归一化，严格匹配由 SceneScopeCoordinator 负责。
        /// </summary>
        public UIOpenOperation CreateOpenOperation(string pageId, UIOpenArgs args)
        {
            UIPageOperationVersion version = CreateOperationVersion();
            return new UIOpenOperation
            {
                PageId = pageId ?? string.Empty,
                Version = version,
                Args = args,
                SceneScopeId = UISceneScopeCoordinator.NormalizeSceneScopeId(args.SceneScopeId),
                CancellationToken = args.CancellationToken,
            };
        }

        /// <summary>
        /// 创建 Close operation，并分配新的操作版本号。
        /// </summary>
        public UICloseOperation CreateCloseOperation(string pageId, UICloseRequest request)
        {
            UIPageOperationVersion version = CreateOperationVersion();
            return new UICloseOperation
            {
                PageId = pageId ?? string.Empty,
                Version = version,
                Request = request,
                SceneScopeId = UISceneScopeCoordinator.NormalizeSceneScopeId(request.SceneScopeId),
                CancellationToken = request.CancellationToken,
            };
        }

        /// <summary>
        /// 创建 Refresh operation，并分配新的操作版本号。
        /// </summary>
        public UIRefreshOperation CreateRefreshOperation(string pageId, UIRefreshArgs args)
        {
            UIPageOperationVersion version = CreateOperationVersion();
            return new UIRefreshOperation
            {
                PageId = pageId ?? string.Empty,
                Version = version,
                Args = args,
                Data = args.Data,
                SceneScopeId = UISceneScopeCoordinator.NormalizeSceneScopeId(args.SceneScopeId),
                CancellationToken = args.CancellationToken,
            };
        }

        /// <summary>
        /// 注册 active operation。
        /// 同一 PageId 同时只允许一个 active operation，旧 operation 若已终态会被清理后替换。
        /// </summary>
        public bool TryRegisterOperation(IUIPageOperation operation)
        {
            if (operation == null || string.IsNullOrEmpty(operation.PageId))
            {
                return false;
            }

            if (activeOperations.TryGetValue(operation.PageId, out IUIPageOperation activeOperation))
            {
                if (activeOperation != null && activeOperation.IsActive)
                {
                    return false;
                }

                activeOperations.Remove(operation.PageId);
            }

            activeOperations.Add(operation.PageId, operation);
            return true;
        }

        /// <summary>
        /// 注销 active operation。
        /// 只移除当前对象，避免旧 operation 的 finally 误删新一轮 operation。
        /// </summary>
        public void UnregisterOperation(IUIPageOperation operation)
        {
            if (operation == null || string.IsNullOrEmpty(operation.PageId))
            {
                return;
            }

            if (activeOperations.TryGetValue(operation.PageId, out IUIPageOperation activeOperation) &&
                ReferenceEquals(activeOperation, operation))
            {
                activeOperations.Remove(operation.PageId);
            }
        }

        /// <summary>
        /// 判断指定页面是否存在 active operation。
        /// </summary>
        public bool IsPageBusy(string pageId)
        {
            return !string.IsNullOrEmpty(pageId) &&
                   activeOperations.TryGetValue(pageId, out IUIPageOperation operation) &&
                   operation != null &&
                   operation.IsActive;
        }

        /// <summary>
        /// 判断指定页面是否正在打开。
        /// IsOpening 只关心 active open operation，不把 pending open 视作正在打开。
        /// </summary>
        public bool IsOpenOperationActive(string pageId)
        {
            return !string.IsNullOrEmpty(pageId) &&
                   activeOperations.TryGetValue(pageId, out IUIPageOperation operation) &&
                   operation is UIOpenOperation &&
                   operation.IsActive;
        }

        /// <summary>
        /// 取消并清空所有 active operation。
        /// Manager 销毁时使用，标记状态后清字典，让 await 返回后的校验进入 Expired/Cancelled。
        /// </summary>
        public void CancelAllActiveOperations()
        {
            List<IUIPageOperation> operations = new List<IUIPageOperation>(activeOperations.Values);
            for (int i = 0; i < operations.Count; i++)
            {
                IUIPageOperation operation = operations[i];
                if (operation == null || !operation.IsActive)
                {
                    continue;
                }

                operation.MarkCancelling();
                operation.MarkCancelled();
                if (operation is UIOpenOperation openOperation)
                {
                    openOperation.Source?.TrySetCancelled();
                }
                else if (operation is UICloseOperation closeOperation)
                {
                    closeOperation.Source?.TrySetCancelled();
                }
                else if (operation is UIRefreshOperation refreshOperation)
                {
                    refreshOperation.Source?.TrySetCancelled();
                }
            }

            activeOperations.Clear();
        }

        /// <summary>
        /// 取消符合条件的 Open operation 与 pending Open intent。
        /// ReleaseScope 在页面实例尚未建立时使用该入口，使晚到的资源结果只能进入过期清理，不能重新提交页面。
        /// </summary>
        public void CancelOpenOperations(
            Predicate<UIOpenOperation> activePredicate,
            Predicate<UIPendingIntent> pendingPredicate)
        {
            if (activePredicate != null)
            {
                List<IUIPageOperation> operations =
                    new List<IUIPageOperation>(activeOperations.Values);
                for (int i = 0; i < operations.Count; i++)
                {
                    UIOpenOperation operation =
                        operations[i] as UIOpenOperation;
                    if (operation == null || !operation.IsActive ||
                        !activePredicate(operation))
                    {
                        continue;
                    }

                    operation.MarkCancelling();
                    operation.MarkCancelled();
                    operation.Source?.TrySetCancelled();
                    UnregisterOperation(operation);
                }
            }

            if (pendingPredicate == null || pendingIntentsByPageId.Count == 0)
            {
                return;
            }

            List<string> pageIds =
                new List<string>(pendingIntentsByPageId.Keys);
            for (int i = 0; i < pageIds.Count; i++)
            {
                string pageId = pageIds[i];
                if (!pendingIntentsByPageId.TryGetValue(
                        pageId,
                        out UIPendingIntent intent) ||
                    intent == null ||
                    intent.Intent != UIPageIntent.Open ||
                    !pendingPredicate(intent))
                {
                    continue;
                }

                pendingIntentsByPageId.Remove(pageId);
                CompletePendingIntentAsCancelled(intent);
            }
        }

        /// <summary>
        /// 校验 operation 是否仍然有效。
        /// 只检查取消、active 对象身份和 OperationVersion；SceneScope 匹配由外层串联 SceneScopeCoordinator。
        /// </summary>
        public UIOperationCheckResult CheckOperation(
            IUIPageOperation operation,
            UIPageInstance instance,
            bool requireVersion)
        {
            if (operation == null)
            {
                return UIOperationCheckResult.Expired;
            }

            if (operation.CancellationToken.IsCancellationRequested)
            {
                return UIOperationCheckResult.Cancelled;
            }

            if (!activeOperations.TryGetValue(operation.PageId, out IUIPageOperation activeOperation) ||
                !ReferenceEquals(activeOperation, operation) ||
                !operation.IsActive)
            {
                return UIOperationCheckResult.Expired;
            }

            if (instance != null && requireVersion && instance.OperationVersion != operation.Version.Value)
            {
                return UIOperationCheckResult.Expired;
            }

            return UIOperationCheckResult.Valid;
        }

        /// <summary>
        /// 把 operation 校验失败映射为 Open 结果并标记 operation 终态。
        /// </summary>
        public static UIOpenResult FailOpenOperation(UIOpenOperation operation, UIPageOpenError error)
        {
            MarkOperationFailed(operation, error);
            return UIOpenResult.Fail(error);
        }

        /// <summary>
        /// 把 operation 校验失败映射为 Close 结果并标记 operation 终态。
        /// </summary>
        public static UICloseResult FailCloseOperation(
            UICloseOperation operation,
            string pageId,
            UIPageState state,
            UICloseError error)
        {
            MarkOperationFailed(operation, error);
            return UICloseResult.Fail(pageId, state, error);
        }

        /// <summary>
        /// 把 operation 校验失败映射为 Refresh 结果并标记 operation 终态。
        /// </summary>
        public static UIRefreshResult FailRefreshOperation(
            UIRefreshOperation operation,
            string pageId,
            UIPageState state,
            UIRefreshError error)
        {
            MarkOperationFailed(operation, error);
            return UIRefreshResult.Fail(pageId, state, error);
        }

        /// <summary>
        /// 将 operation 校验结果映射为 Open 错误枚举。
        /// </summary>
        public static UIPageOpenError ToOpenError(UIOperationCheckResult checkResult)
        {
            switch (checkResult)
            {
                case UIOperationCheckResult.Cancelled:
                    return UIPageOpenError.Cancelled;
                case UIOperationCheckResult.SceneScopeInvalid:
                    return UIPageOpenError.SceneScopeInvalid;
                case UIOperationCheckResult.Expired:
                    return UIPageOpenError.OperationExpired;
                default:
                    return UIPageOpenError.Exception;
            }
        }

        /// <summary>
        /// 将 operation 校验结果映射为 Close 错误枚举。
        /// </summary>
        public static UICloseError ToCloseError(UIOperationCheckResult checkResult)
        {
            switch (checkResult)
            {
                case UIOperationCheckResult.Cancelled:
                    return UICloseError.Cancelled;
                case UIOperationCheckResult.SceneScopeInvalid:
                    return UICloseError.SceneScopeInvalid;
                case UIOperationCheckResult.Expired:
                    return UICloseError.OperationExpired;
                default:
                    return UICloseError.Exception;
            }
        }

        /// <summary>
        /// 将 operation 校验结果映射为 Refresh 错误枚举。
        /// </summary>
        public static UIRefreshError ToRefreshError(UIOperationCheckResult checkResult)
        {
            switch (checkResult)
            {
                case UIOperationCheckResult.Cancelled:
                    return UIRefreshError.Cancelled;
                case UIOperationCheckResult.SceneScopeInvalid:
                    return UIRefreshError.SceneScopeInvalid;
                case UIOperationCheckResult.Expired:
                    return UIRefreshError.OperationExpired;
                default:
                    return UIRefreshError.Exception;
            }
        }

        private bool TryStorePendingIntent(UIPendingIntent intent)
        {
            if (intent == null || string.IsNullOrEmpty(intent.PageId))
            {
                return false;
            }

            // 同 PageId 只保留一个 pending；新意图优先级大于或等于旧意图时覆盖旧调用方。
            if (pendingIntentsByPageId.TryGetValue(intent.PageId, out UIPendingIntent existingIntent))
            {
                if (existingIntent != null && intent.Priority < existingIntent.Priority)
                {
                    return false;
                }

                CompletePendingIntentAsExpired(existingIntent);
            }

            pendingIntentsByPageId[intent.PageId] = intent;
            return true;
        }

        private UIPageOperationVersion CreateOperationVersion()
        {
            nextOperationVersion++;
            return new UIPageOperationVersion(nextOperationVersion);
        }

        private UIPageState GetKnownPageState(string pageId)
        {
            return knownPageStateProvider != null
                ? knownPageStateProvider(pageId)
                : UIPageState.None;
        }

        private static int GetPendingIntentPriority(UIPageIntent intent, bool releaseOnClose)
        {
            switch (intent)
            {
                case UIPageIntent.Release:
                    return 4;
                case UIPageIntent.Close:
                    return releaseOnClose ? 4 : 3;
                case UIPageIntent.Open:
                    return 2;
                case UIPageIntent.Refresh:
                    return 1;
                default:
                    return 0;
            }
        }

        private static void MarkOperationFailed(UIPageOperationBase operation, UIPageOpenError error)
        {
            if (operation == null)
            {
                return;
            }

            if (error == UIPageOpenError.Cancelled)
            {
                operation.MarkCancelled();
            }
            else if (error == UIPageOpenError.OperationExpired)
            {
                operation.MarkExpired();
            }
            else
            {
                operation.MarkFailed();
            }
        }

        private static void MarkOperationFailed(UIPageOperationBase operation, UICloseError error)
        {
            if (operation == null)
            {
                return;
            }

            if (error == UICloseError.Cancelled)
            {
                operation.MarkCancelled();
            }
            else if (error == UICloseError.OperationExpired)
            {
                operation.MarkExpired();
            }
            else
            {
                operation.MarkFailed();
            }
        }

        private static void MarkOperationFailed(UIPageOperationBase operation, UIRefreshError error)
        {
            if (operation == null)
            {
                return;
            }

            if (error == UIRefreshError.Cancelled)
            {
                operation.MarkCancelled();
            }
            else if (error == UIRefreshError.OperationExpired)
            {
                operation.MarkExpired();
            }
            else
            {
                operation.MarkFailed();
            }
        }
    }
}
