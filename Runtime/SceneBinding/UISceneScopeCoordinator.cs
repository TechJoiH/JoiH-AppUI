using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 场景 UI 命令执行接口。
    /// SceneScope 协调器只通过该接口发起 Open/Close，不直接依赖 AppUIManager 具体类型。
    /// </summary>
    internal interface IUISceneCommandExecutor
    {
        /// <summary>
        /// 按指定参数打开页面。
        /// SceneScope 协调器会在场景进入时注入解析后的 SceneScopeId。
        /// </summary>
        UniTask<UIOpenResult> OpenAsync(string pageId, UIOpenArgs args);

        /// <summary>
        /// 按指定关闭请求关闭页面。
        /// SceneScope 协调器会根据显式退出规则决定 ReleaseOnClose 和 SceneScopeId。
        /// </summary>
        UniTask<UICloseResult> CloseAsync(string pageId, UICloseRequest request);
    }

    /// <summary>
    /// 页面实例查询接口。
    /// SceneScope 批量释放只读取当前实例快照，不创建、不打开、不修改 Definition。
    /// </summary>
    internal interface IUIPageInstanceQuery
    {
        /// <summary>
        /// 获取当前页面实例快照。
        /// 调用方应只读取返回值，避免持有 registry 内部临时列表。
        /// </summary>
        List<UIPageInstance> GetSnapshot();

        /// <summary>
        /// 查询指定页面实例。
        /// </summary>
        bool TryGet(string pageId, out UIPageInstance instance);
    }

    /// <summary>
    /// SceneScope 生命周期协调器。
    /// 负责 SceneScopeId 解析、场景进入打开、场景退出显式规则、Scene/Loading/Temporary Scope 批量释放边界。
    /// </summary>
    internal sealed class UISceneScopeCoordinator
    {
        private readonly IUISceneCommandExecutor commandExecutor;
        private readonly IUIPageInstanceQuery instanceQuery;

        /// <summary>
        /// 创建场景作用域协调器。
        /// commandExecutor 串联真实 Open/Close 流程；instanceQuery 提供当前实例快照用于批量释放。
        /// </summary>
        public UISceneScopeCoordinator(
            IUISceneCommandExecutor executor,
            IUIPageInstanceQuery query)
        {
            commandExecutor = executor;
            instanceQuery = query;
        }

        /// <summary>
        /// 场景进入时按 OpenOnSceneReady 顺序打开页面。
        /// 该方法会统一解析 SceneScopeId，并写入每个 OpenArgs，确保页面实例归属一致。
        /// </summary>
        public async UniTask BindSceneAsync(SceneUIBindingData bindingData)
        {
            if (bindingData == null || bindingData.OpenOnSceneReady == null || commandExecutor == null)
            {
                return;
            }

            string sceneScopeId = ResolveSceneScopeId(bindingData);
            bindingData.OpenOnSceneReady.Sort(CompareSceneOpenRule);
            for (int i = 0; i < bindingData.OpenOnSceneReady.Count; i++)
            {
                SceneUIOpenRule rule = bindingData.OpenOnSceneReady[i];
                if (rule == null || string.IsNullOrEmpty(rule.PageId))
                {
                    continue;
                }

                await commandExecutor.OpenAsync(rule.PageId, rule.OpenArgs.WithSceneScopeId(sceneScopeId));
            }
        }

        /// <summary>
        /// 场景退出时执行 CloseOnSceneExit 显式规则，并兜底释放匹配 SceneScopeId 的 SceneScope 与 TemporaryScope 页面。
        /// 显式 None 规则会从默认清理候选中排除，表示调用者明确希望保留该页面。
        /// </summary>
        public async UniTask<UISceneExitResult> UnbindSceneAsync(SceneUIBindingData bindingData)
        {
            if (bindingData == null)
            {
                return UISceneExitResult.FromResults(string.Empty, string.Empty, null);
            }

            string sceneId = bindingData.SceneId;
            string sceneScopeId = ResolveSceneScopeId(bindingData);
            List<UICloseResult> closeResults = new List<UICloseResult>(8);
            HashSet<string> explicitPageIds = new HashSet<string>(StringComparer.Ordinal);

            if (bindingData.CloseOnSceneExit != null)
            {
                for (int i = 0; i < bindingData.CloseOnSceneExit.Count; i++)
                {
                    SceneUICloseRule rule = bindingData.CloseOnSceneExit[i];
                    if (rule == null || string.IsNullOrEmpty(rule.PageId))
                    {
                        continue;
                    }

                    explicitPageIds.Add(rule.PageId);
                    UICloseResult closeResult;
                    switch (rule.ExitAction)
                    {
                        case UISceneExitAction.None:
                            continue;
                        case UISceneExitAction.Close:
                            closeResult = await CloseForSceneScopeAsync(rule.PageId, false, sceneScopeId);
                            closeResults.Add(closeResult);
                            break;
                        case UISceneExitAction.Release:
                            closeResult = await CloseForSceneScopeAsync(rule.PageId, true, sceneScopeId);
                            closeResults.Add(closeResult);
                            break;
                    }
                }
            }

            await AppendScopeReleaseResultsAsync(
                UIPageScope.SceneScope,
                sceneScopeId,
                explicitPageIds,
                closeResults);
            await AppendScopeReleaseResultsAsync(
                UIPageScope.TemporaryScope,
                sceneScopeId,
                explicitPageIds,
                closeResults);

            return UISceneExitResult.FromResults(sceneId, sceneScopeId, closeResults);
        }

        /// <summary>
        /// 显式释放某个 Scope 下匹配 SceneScopeId 的页面。
        /// GlobalScope 不允许批量释放，避免误清跨场景全局 UI。
        /// </summary>
        public async UniTask<UIScopeReleaseResult> ReleaseScopeAsync(UIPageScope scope, string sceneScopeId)
        {
            string normalizedSceneScopeId = NormalizeSceneScopeId(sceneScopeId);
            List<UICloseResult> closeResults = await ReleaseScopeInternalAsync(
                scope,
                normalizedSceneScopeId,
                null);
            return UIScopeReleaseResult.FromResults(scope, normalizedSceneScopeId, closeResults);
        }

        /// <summary>
        /// 根据 Definition.Scope 解析实例最终保存的 SceneScopeId。
        /// GlobalScope 页面强制保存空 scope，避免被场景批量释放误伤。
        /// </summary>
        public string ResolveInstanceSceneScopeId(UIPageDefinition definition, string requestedSceneScopeId)
        {
            if (definition != null && definition.Scope == UIPageScope.GlobalScope)
            {
                return string.Empty;
            }

            return NormalizeSceneScopeId(requestedSceneScopeId);
        }

        /// <summary>
        /// 判断请求携带的 SceneScopeId 是否允许操作当前实例。
        /// 空请求 scope 兼容旧调用；非空请求必须匹配实例 scope，GlobalScope 页面例外。
        /// </summary>
        public bool IsSceneScopeCompatible(string requestedSceneScopeId, UIPageInstance instance)
        {
            if (string.IsNullOrEmpty(requestedSceneScopeId) || instance == null)
            {
                return true;
            }

            if (instance.Definition != null && instance.Definition.Scope == UIPageScope.GlobalScope)
            {
                return true;
            }

            return string.Equals(
                NormalizeSceneScopeId(requestedSceneScopeId),
                NormalizeSceneScopeId(instance.SceneScopeId),
                StringComparison.Ordinal);
        }

        /// <summary>
        /// 将 null scope 统一成空字符串，便于后续字典和字符串比较保持一致。
        /// </summary>
        public static string NormalizeSceneScopeId(string sceneScopeId)
        {
            return sceneScopeId ?? string.Empty;
        }

        /// <summary>
        /// 从 SceneUIBindingData 解析 SceneScopeId。
        /// 优先使用显式 SceneScopeId；未填写时回退到 SceneId；都为空则保持空字符串兼容旧流程。
        /// </summary>
        public static string ResolveSceneScopeId(SceneUIBindingData bindingData)
        {
            if (bindingData == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(bindingData.SceneScopeId))
            {
                return NormalizeSceneScopeId(bindingData.SceneScopeId);
            }

            return NormalizeSceneScopeId(bindingData.SceneId);
        }

        private async UniTask AppendScopeReleaseResultsAsync(
            UIPageScope scope,
            string sceneScopeId,
            HashSet<string> excludedPageIds,
            List<UICloseResult> closeResults)
        {
            if (closeResults == null)
            {
                return;
            }

            List<UICloseResult> scopeResults = await ReleaseScopeInternalAsync(scope, sceneScopeId, excludedPageIds);
            for (int i = 0; i < scopeResults.Count; i++)
            {
                closeResults.Add(scopeResults[i]);
            }
        }

        private async UniTask<List<UICloseResult>> ReleaseScopeInternalAsync(
            UIPageScope scope,
            string sceneScopeId,
            HashSet<string> excludedPageIds)
        {
            List<UICloseResult> closeResults = new List<UICloseResult>(8);
            if (scope == UIPageScope.GlobalScope)
            {
                Debug.LogWarning("<Joi.H.AppUI> ReleaseScopeAsync does not release GlobalScope pages.");
                return closeResults;
            }

            string normalizedSceneScopeId = NormalizeSceneScopeId(sceneScopeId);
            List<string> pageIds = CollectScopeReleasePageIds(scope, normalizedSceneScopeId, excludedPageIds);
            for (int i = 0; i < pageIds.Count; i++)
            {
                UICloseResult closeResult = await CloseForSceneScopeAsync(pageIds[i], true, normalizedSceneScopeId);
                closeResults.Add(closeResult);
            }

            return closeResults;
        }

        private List<string> CollectScopeReleasePageIds(
            UIPageScope scope,
            string sceneScopeId,
            HashSet<string> excludedPageIds)
        {
            List<string> pageIds = new List<string>(8);
            if (instanceQuery == null)
            {
                return pageIds;
            }

            List<UIPageInstance> pages = instanceQuery.GetSnapshot();
            string normalizedSceneScopeId = NormalizeSceneScopeId(sceneScopeId);
            for (int i = 0; i < pages.Count; i++)
            {
                UIPageInstance instance = pages[i];
                if (!IsScopeReleaseCandidate(instance, scope, normalizedSceneScopeId, excludedPageIds))
                {
                    continue;
                }

                pageIds.Add(instance.PageId);
            }

            return pageIds;
        }

        private static bool IsScopeReleaseCandidate(
            UIPageInstance instance,
            UIPageScope scope,
            string sceneScopeId,
            HashSet<string> excludedPageIds)
        {
            if (instance == null ||
                instance.Definition == null ||
                string.IsNullOrEmpty(instance.PageId) ||
                instance.Definition.Scope != scope ||
                instance.Definition.Scope == UIPageScope.GlobalScope)
            {
                return false;
            }

            if (excludedPageIds != null && excludedPageIds.Contains(instance.PageId))
            {
                return false;
            }

            return string.Equals(
                NormalizeSceneScopeId(instance.SceneScopeId),
                NormalizeSceneScopeId(sceneScopeId),
                StringComparison.Ordinal);
        }

        private async UniTask<UICloseResult> CloseForSceneScopeAsync(
            string pageId,
            bool releaseOnClose,
            string sceneScopeId)
        {
            if (commandExecutor == null)
            {
                return UICloseResult.Fail(pageId, UIPageState.None, UICloseError.Exception);
            }

            UICloseRequest request = UICloseRequest.Default;
            request.ReleaseOnClose = releaseOnClose;
            request.SceneScopeId = ShouldUseScopedCloseRequest(pageId)
                ? NormalizeSceneScopeId(sceneScopeId)
                : string.Empty;
            return await commandExecutor.CloseAsync(pageId, request);
        }

        private bool ShouldUseScopedCloseRequest(string pageId)
        {
            if (string.IsNullOrEmpty(pageId) ||
                instanceQuery == null ||
                !instanceQuery.TryGet(pageId, out UIPageInstance instance) ||
                instance == null ||
                instance.Definition == null)
            {
                return true;
            }

            return instance.Definition.Scope != UIPageScope.GlobalScope;
        }

        private static int CompareSceneOpenRule(SceneUIOpenRule leftRule, SceneUIOpenRule rightRule)
        {
            int left = leftRule != null ? leftRule.Order : int.MaxValue;
            int right = rightRule != null ? rightRule.Order : int.MaxValue;
            return left.CompareTo(right);
        }
    }
}
