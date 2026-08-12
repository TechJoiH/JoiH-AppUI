using System;
using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI
{
    internal interface IUISceneCommandExecutor
    {
        IUIOperation<UIOpenResult> Open(
            string pageId,
            UIOpenArgs args);

        IUIOperation<UICloseResult> Close(
            string pageId,
            UICloseRequest request);
    }

    internal interface IUIPageInstanceQuery
    {
        List<UIPageInstance> GetSnapshot();
        bool TryGet(string pageId, out UIPageInstance instance);
    }

    /// <summary>
    /// Executes authored scene UI rules sequentially through neutral operations.
    /// </summary>
    internal sealed class UISceneScopeCoordinator
    {
        private readonly IUISceneCommandExecutor commandExecutor;
        private readonly IUIPageInstanceQuery instanceQuery;
        private readonly IUIOperationFactory operationFactory;
        private readonly IAppUIExecutionContext executionContext;

        public UISceneScopeCoordinator(
            IUISceneCommandExecutor executor,
            IUIPageInstanceQuery query,
            IUIOperationFactory factory,
            IAppUIExecutionContext context)
        {
            commandExecutor = executor;
            instanceQuery = query;
            operationFactory = factory;
            executionContext = context;
        }

        public IUIOperation<UISceneBindResult> BindScene(
            SceneUIBindingData bindingData)
        {
            IUIOperationSource<UISceneBindResult> source =
                CreateSource<UISceneBindResult>("BindScene");
            string sceneId = bindingData != null
                ? bindingData.SceneId
                : string.Empty;
            string sceneScopeId = ResolveSceneScopeId(bindingData);
            List<SceneUIOpenRule> rules = bindingData != null &&
                                             bindingData.OpenOnSceneReady != null
                ? new List<SceneUIOpenRule>(bindingData.OpenOnSceneReady)
                : new List<SceneUIOpenRule>(0);
            rules.Sort(CompareSceneOpenRule);
            ContinueBind(
                rules,
                0,
                sceneId,
                sceneScopeId,
                new List<UIOpenResult>(rules.Count),
                source);
            return source.Operation;
        }

        public IUIOperation<UISceneExitResult> UnbindScene(
            SceneUIBindingData bindingData)
        {
            IUIOperationSource<UISceneExitResult> source =
                CreateSource<UISceneExitResult>("UnbindScene");
            string sceneId = bindingData != null
                ? bindingData.SceneId
                : string.Empty;
            string sceneScopeId = ResolveSceneScopeId(bindingData);
            List<SceneCloseWork> work = BuildUnbindWork(
                bindingData,
                sceneScopeId);
            ContinueCloseWork(
                work,
                0,
                new List<UICloseResult>(work.Count),
                source,
                results => UISceneExitResult.FromResults(
                    sceneId,
                    sceneScopeId,
                    results));
            return source.Operation;
        }

        public IUIOperation<UIScopeReleaseResult> ReleaseScope(
            UIPageScope scope,
            string sceneScopeId)
        {
            IUIOperationSource<UIScopeReleaseResult> source =
                CreateSource<UIScopeReleaseResult>("ReleaseScope");
            string normalized = NormalizeSceneScopeId(sceneScopeId);
            if (scope == UIPageScope.GlobalScope)
            {
                Debug.LogWarning(
                    "<Joi.H.AppUI> ReleaseScope does not release " +
                    "GlobalScope pages.");
            }

            List<SceneCloseWork> work = scope == UIPageScope.GlobalScope
                ? new List<SceneCloseWork>(0)
                : BuildScopeReleaseWork(scope, normalized, null);
            ContinueCloseWork(
                work,
                0,
                new List<UICloseResult>(work.Count),
                source,
                results => UIScopeReleaseResult.FromResults(
                    scope,
                    normalized,
                    results));
            return source.Operation;
        }

        public string ResolveInstanceSceneScopeId(
            UIPageDefinition definition,
            string requestedSceneScopeId)
        {
            return definition != null &&
                   definition.Scope == UIPageScope.GlobalScope
                ? string.Empty
                : NormalizeSceneScopeId(requestedSceneScopeId);
        }

        public bool IsSceneScopeCompatible(
            string requestedSceneScopeId,
            UIPageInstance instance)
        {
            if (string.IsNullOrEmpty(requestedSceneScopeId) ||
                instance == null ||
                instance.Definition != null &&
                instance.Definition.Scope == UIPageScope.GlobalScope)
            {
                return true;
            }

            return string.Equals(
                NormalizeSceneScopeId(requestedSceneScopeId),
                NormalizeSceneScopeId(instance.SceneScopeId),
                StringComparison.Ordinal);
        }

        public static string NormalizeSceneScopeId(string sceneScopeId)
        {
            return sceneScopeId ?? string.Empty;
        }

        public static string ResolveSceneScopeId(
            SceneUIBindingData bindingData)
        {
            if (bindingData == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrEmpty(bindingData.SceneScopeId)
                ? NormalizeSceneScopeId(bindingData.SceneScopeId)
                : NormalizeSceneScopeId(bindingData.SceneId);
        }

        private void ContinueBind(
            List<SceneUIOpenRule> rules,
            int index,
            string sceneId,
            string sceneScopeId,
            List<UIOpenResult> results,
            IUIOperationSource<UISceneBindResult> source)
        {
            while (index < rules.Count &&
                   (rules[index] == null ||
                    string.IsNullOrEmpty(rules[index].PageId)))
            {
                index++;
            }

            if (index >= rules.Count)
            {
                source.TrySetSucceeded(UISceneBindResult.FromResults(
                    sceneId,
                    sceneScopeId,
                    results));
                return;
            }

            SceneUIOpenRule rule = rules[index];
            IUIOperation<UIOpenResult> operation = commandExecutor.Open(
                rule.PageId,
                rule.OpenArgs.WithSceneScopeId(sceneScopeId));
            int nextIndex = index + 1;
            Observe(operation, source, completion =>
            {
                results.Add(completion.Result);
                ContinueBind(
                    rules,
                    nextIndex,
                    sceneId,
                    sceneScopeId,
                    results,
                    source);
            });
        }

        private void ContinueCloseWork<TResult>(
            List<SceneCloseWork> work,
            int index,
            List<UICloseResult> results,
            IUIOperationSource<TResult> source,
            Func<List<UICloseResult>, TResult> createResult)
        {
            if (index >= work.Count)
            {
                source.TrySetSucceeded(createResult.Invoke(results));
                return;
            }

            SceneCloseWork item = work[index];
            UICloseRequest request = UICloseRequest.Default;
            request.ReleaseOnClose = item.ReleaseOnClose;
            request.SceneScopeId = ShouldUseScopedCloseRequest(item.PageId)
                ? item.SceneScopeId
                : string.Empty;
            IUIOperation<UICloseResult> operation =
                commandExecutor.Close(item.PageId, request);
            Observe(operation, source, completion =>
            {
                results.Add(completion.Result);
                ContinueCloseWork(
                    work,
                    index + 1,
                    results,
                    source,
                    createResult);
            });
        }

        private void Observe<TExternal, TResult>(
            IUIOperation<TExternal> operation,
            IUIOperationSource<TResult> source,
            Action<AppUIOperationCompletion<TExternal>> onSucceeded)
        {
            if (operation == null)
            {
                source.TrySetFailed(new InvalidOperationException(
                    "Scene command returned a null operation."));
                return;
            }

            UIOperationObserver.Observe(
                operation,
                executionContext,
                completion =>
                {
                    switch (completion.Status)
                    {
                        case AppUIOperationStatus.Succeeded:
                            onSucceeded.Invoke(completion);
                            break;
                        case AppUIOperationStatus.Cancelled:
                            source.TrySetCancelled();
                            break;
                        case AppUIOperationStatus.Expired:
                            source.TrySetExpired();
                            break;
                        case AppUIOperationStatus.Failed:
                            source.TrySetFailed(
                                completion.Exception ??
                                new InvalidOperationException(
                                    "Failed scene command has no exception."));
                            break;
                    }
                });
        }

        private List<SceneCloseWork> BuildUnbindWork(
            SceneUIBindingData bindingData,
            string sceneScopeId)
        {
            List<SceneCloseWork> work = new List<SceneCloseWork>(8);
            HashSet<string> explicitIds = new HashSet<string>(
                StringComparer.Ordinal);
            if (bindingData != null &&
                bindingData.CloseOnSceneExit != null)
            {
                for (int i = 0;
                     i < bindingData.CloseOnSceneExit.Count;
                     i++)
                {
                    SceneUICloseRule rule =
                        bindingData.CloseOnSceneExit[i];
                    if (rule == null || string.IsNullOrEmpty(rule.PageId))
                    {
                        continue;
                    }

                    explicitIds.Add(rule.PageId);
                    if (rule.ExitAction == UISceneExitAction.None)
                    {
                        continue;
                    }

                    work.Add(new SceneCloseWork(
                        rule.PageId,
                        rule.ExitAction == UISceneExitAction.Release,
                        sceneScopeId));
                }
            }

            work.AddRange(BuildScopeReleaseWork(
                UIPageScope.SceneScope,
                sceneScopeId,
                explicitIds));
            work.AddRange(BuildScopeReleaseWork(
                UIPageScope.TemporaryScope,
                sceneScopeId,
                explicitIds));
            return work;
        }

        private List<SceneCloseWork> BuildScopeReleaseWork(
            UIPageScope scope,
            string sceneScopeId,
            HashSet<string> excludedIds)
        {
            List<SceneCloseWork> work = new List<SceneCloseWork>(8);
            if (instanceQuery == null)
            {
                return work;
            }

            List<UIPageInstance> pages = instanceQuery.GetSnapshot();
            for (int i = 0; i < pages.Count; i++)
            {
                UIPageInstance instance = pages[i];
                if (instance == null || instance.Definition == null ||
                    instance.Definition.Scope != scope ||
                    scope == UIPageScope.GlobalScope ||
                    excludedIds != null &&
                    excludedIds.Contains(instance.PageId) ||
                    !string.Equals(
                        NormalizeSceneScopeId(instance.SceneScopeId),
                        sceneScopeId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                work.Add(new SceneCloseWork(
                    instance.PageId,
                    true,
                    sceneScopeId));
            }

            return work;
        }

        private bool ShouldUseScopedCloseRequest(string pageId)
        {
            return string.IsNullOrEmpty(pageId) ||
                   instanceQuery == null ||
                   !instanceQuery.TryGet(
                       pageId,
                       out UIPageInstance instance) ||
                   instance == null ||
                   instance.Definition == null ||
                   instance.Definition.Scope != UIPageScope.GlobalScope;
        }

        private IUIOperationSource<TResult> CreateSource<TResult>(
            string name)
        {
            IUIOperationSource<TResult> source =
                operationFactory.Create<TResult>(
                    AppUIOperationDescriptor.Create(name));
            if (source == null || source.Operation == null)
            {
                throw new InvalidOperationException(
                    "IUIOperationFactory returned a null source or operation.");
            }

            source.TrySetRunning();
            return source;
        }

        private static int CompareSceneOpenRule(
            SceneUIOpenRule left,
            SceneUIOpenRule right)
        {
            return (left != null ? left.Order : int.MaxValue)
                .CompareTo(right != null ? right.Order : int.MaxValue);
        }

        private readonly struct SceneCloseWork
        {
            public SceneCloseWork(
                string pageId,
                bool releaseOnClose,
                string sceneScopeId)
            {
                PageId = pageId;
                ReleaseOnClose = releaseOnClose;
                SceneScopeId = sceneScopeId;
            }

            public string PageId { get; }
            public bool ReleaseOnClose { get; }
            public string SceneScopeId { get; }
        }
    }
}
