using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUISceneOperationTests
    {
        [Test]
        public void SceneScopeGeneration_InvalidateThenActivate_ReturnsNewStamp()
        {
            UISceneScopeGenerationRegistry registry =
                new UISceneScopeGenerationRegistry();

            UISceneScopeStamp first = registry.Activate("shared-scope");
            UISceneScopeStamp repeated = registry.Activate("shared-scope");
            UISceneScopeStamp retired = registry.Invalidate("shared-scope");
            UISceneScopeStamp second = registry.Activate("shared-scope");

            Assert.That(first.HasGeneration, Is.True);
            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(retired, Is.EqualTo(first));
            Assert.That(registry.IsCurrent(first), Is.False);
            Assert.That(second.HasGeneration, Is.True);
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(registry.IsCurrent(second), Is.True);
        }

        [Test]
        public void SceneScopeGeneration_UnstampedCompatibility_RemainsValid()
        {
            UISceneScopeGenerationRegistry registry =
                new UISceneScopeGenerationRegistry();

            Assert.That(
                registry.IsCurrent(UISceneScopeStamp.Unstamped("legacy-scope")),
                Is.True);
        }

        [Test]
        public void SceneScopeGeneration_RetiredStampCannotTargetReboundInstance()
        {
            UISceneScopeGenerationRegistry registry =
                new UISceneScopeGenerationRegistry();
            UISceneScopeStamp retired = registry.Activate("shared-scope");
            registry.Invalidate("shared-scope");
            UISceneScopeStamp current = registry.Activate("shared-scope");
            UISceneScopeCoordinator coordinator =
                new UISceneScopeCoordinator(
                    new RecordingSceneExecutor(
                        new ManualUIOperationFactory()),
                    new EmptyPageQuery(),
                    new ManualUIOperationFactory(),
                    new ImmediateAppUIExecutionContext(),
                    registry);
            UIPageInstance rebound = new UIPageInstance
            {
                SceneScopeId = "shared-scope",
                SceneScopeStamp = current,
            };

            Assert.That(
                coordinator.IsSceneScopeCompatible(retired, rebound),
                Is.False);
            Assert.That(
                coordinator.IsSceneScopeCompatible(current, rebound),
                Is.True);
            Assert.That(
                coordinator.IsSceneScopeCompatible("shared-scope", rebound),
                Is.True,
                "Unstamped direct API calls retain SceneScopeId compatibility.");
        }

        [Test]
        public void UnbindScene_WaitsForEachRule_AndContinuesAfterDomainFailure()
        {
            ManualUIOperationFactory factory = new ManualUIOperationFactory();
            RecordingSceneExecutor executor =
                new RecordingSceneExecutor(factory);
            UISceneScopeCoordinator coordinator =
                new UISceneScopeCoordinator(
                    executor,
                    new EmptyPageQuery(),
                    factory,
                    new ImmediateAppUIExecutionContext());
            SceneUIBindingData binding = CreateBinding("first", "second");

            IUIOperation<UISceneExitResult> operation =
                coordinator.UnbindScene(binding);

            CollectionAssert.AreEqual(
                new[] { "first" },
                executor.CloseOrder);
            Assert.That(operation.IsTerminal, Is.False);

            executor.CompleteNextClose(
                UICloseResult.Fail(
                    "first",
                    UIPageState.Open,
                    UICloseError.Rejected));

            CollectionAssert.AreEqual(
                new[] { "first", "second" },
                executor.CloseOrder);
            Assert.That(operation.IsTerminal, Is.False);

            executor.CompleteNextClose(
                UICloseResult.Ok("second", UIPageState.Released));

            Assert.That(operation.TryGetCompletion(out var completion), Is.True);
            Assert.That(completion.Status, Is.EqualTo(AppUIOperationStatus.Succeeded));
            Assert.That(completion.Result.Success, Is.False);
            Assert.That(completion.Result.FailureCount, Is.EqualTo(1));
            Assert.That(completion.Result.CloseResults.Count, Is.EqualTo(2));
        }

        [Test]
        public void UnbindScene_CancelledRule_DoesNotStartLaterRules()
        {
            ManualUIOperationFactory factory = new ManualUIOperationFactory();
            RecordingSceneExecutor executor =
                new RecordingSceneExecutor(factory);
            UISceneScopeCoordinator coordinator =
                new UISceneScopeCoordinator(
                    executor,
                    new EmptyPageQuery(),
                    factory,
                    new ImmediateAppUIExecutionContext());

            IUIOperation<UISceneExitResult> operation =
                coordinator.UnbindScene(CreateBinding("first", "second"));
            executor.CancelNextClose();

            CollectionAssert.AreEqual(
                new[] { "first" },
                executor.CloseOrder);
            Assert.That(operation.TryGetCompletion(out var completion), Is.True);
            Assert.That(completion.Status, Is.EqualTo(AppUIOperationStatus.Cancelled));
        }

        [Test]
        public void BindScene_OuterCancellation_CancelsCurrentOpenAndSkipsRemainingRules()
        {
            ManualUIOperationFactory factory = new ManualUIOperationFactory();
            RecordingSceneExecutor executor =
                new RecordingSceneExecutor(factory);
            UISceneScopeCoordinator coordinator =
                new UISceneScopeCoordinator(
                    executor,
                    new EmptyPageQuery(),
                    factory,
                    new ImmediateAppUIExecutionContext());

            IUIOperation<UISceneBindResult> operation =
                coordinator.BindScene(CreateOpenBinding("first", "second"));
            Assert.That(operation.RequestCancellation(), Is.True);

            Assert.That(operation.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelled));
            Assert.That(
                executor.CurrentOpen.CancellationToken.IsCancellationRequested,
                Is.True);
            CollectionAssert.AreEqual(new[] { "first" }, executor.OpenOrder);

            executor.CompleteNextOpen(UIOpenResult.Ok(
                new UIPageHandle(
                    "first",
                    UIPageState.Open,
                    UILayerId.PopupLayer)));
            CollectionAssert.AreEqual(new[] { "first" }, executor.OpenOrder);
            Assert.That(operation.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelled));
        }

        [Test]
        public void UnbindScene_OuterCancellation_CancelsCurrentCloseAndSkipsRemainingRules()
        {
            ManualUIOperationFactory factory = new ManualUIOperationFactory();
            RecordingSceneExecutor executor =
                new RecordingSceneExecutor(factory);
            UISceneScopeCoordinator coordinator =
                new UISceneScopeCoordinator(
                    executor,
                    new EmptyPageQuery(),
                    factory,
                    new ImmediateAppUIExecutionContext());

            IUIOperation<UISceneExitResult> operation =
                coordinator.UnbindScene(CreateBinding("first", "second"));
            Assert.That(operation.RequestCancellation(), Is.True);

            Assert.That(operation.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelled));
            Assert.That(
                executor.CurrentClose.CancellationToken.IsCancellationRequested,
                Is.True);
            CollectionAssert.AreEqual(new[] { "first" }, executor.CloseOrder);

            executor.CompleteNextClose(
                UICloseResult.Ok("first", UIPageState.Released));
            CollectionAssert.AreEqual(new[] { "first" }, executor.CloseOrder);
            Assert.That(operation.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelled));
        }

        [Test]
        public void ReleaseScope_LateChildCompletionAfterCancellation_DoesNotChangeOuterTerminal()
        {
            ManualUIOperationFactory factory = new ManualUIOperationFactory();
            RecordingSceneExecutor executor =
                new RecordingSceneExecutor(factory);
            UIPageDefinition definition =
                ScriptableObject.CreateInstance<UIPageDefinition>();
            definition.Scope = UIPageScope.SceneScope;
            try
            {
                UISceneScopeCoordinator coordinator =
                    new UISceneScopeCoordinator(
                        executor,
                        new StaticPageQuery(
                            definition,
                            "test-scope",
                            "first",
                            "second"),
                        factory,
                        new ImmediateAppUIExecutionContext());

                IUIOperation<UIScopeReleaseResult> operation =
                    coordinator.ReleaseScope(
                        UIPageScope.SceneScope,
                        "test-scope");
                Assert.That(operation.RequestCancellation(), Is.True);

                Assert.That(operation.Status,
                    Is.EqualTo(AppUIOperationStatus.Cancelled));
                Assert.That(
                    executor.CurrentClose.CancellationToken
                        .IsCancellationRequested,
                    Is.True);

                executor.CompleteNextClose(
                    UICloseResult.Ok("first", UIPageState.Released));
                CollectionAssert.AreEqual(
                    new[] { "first" },
                    executor.CloseOrder);
                Assert.That(operation.Status,
                    Is.EqualTo(AppUIOperationStatus.Cancelled));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static SceneUIBindingData CreateBinding(
            string first,
            string second)
        {
            return new SceneUIBindingData
            {
                SceneId = "TestScene",
                SceneScopeId = "test-scope",
                CloseOnSceneExit = new List<SceneUICloseRule>
                {
                    new SceneUICloseRule
                    {
                        PageId = first,
                        ExitAction = UISceneExitAction.Release,
                    },
                    new SceneUICloseRule
                    {
                        PageId = second,
                        ExitAction = UISceneExitAction.Release,
                    },
                },
            };
        }

        private static SceneUIBindingData CreateOpenBinding(
            string first,
            string second)
        {
            return new SceneUIBindingData
            {
                SceneId = "TestScene",
                SceneScopeId = "test-scope",
                OpenOnSceneReady = new List<SceneUIOpenRule>
                {
                    new SceneUIOpenRule { PageId = first },
                    new SceneUIOpenRule { PageId = second, Order = 1 },
                },
            };
        }

        private sealed class RecordingSceneExecutor : IUISceneCommandExecutor
        {
            private readonly IUIOperationFactory factory;
            private readonly Queue<IUIOperationSource<UICloseResult>> closes =
                new Queue<IUIOperationSource<UICloseResult>>();
            private readonly Queue<IUIOperationSource<UIOpenResult>> opens =
                new Queue<IUIOperationSource<UIOpenResult>>();

            public RecordingSceneExecutor(IUIOperationFactory factory)
            {
                this.factory = factory;
            }

            public List<string> CloseOrder { get; } = new List<string>();
            public List<string> OpenOrder { get; } = new List<string>();
            public IUIOperation<UIOpenResult> CurrentOpen { get; private set; }
            public IUIOperation<UICloseResult> CurrentClose { get; private set; }

            public IUIOperation<UIOpenResult> Open(
                string pageId,
                UIOpenArgs args)
            {
                OpenOrder.Add(pageId);
                IUIOperationSource<UIOpenResult> source =
                    factory.Create<UIOpenResult>(
                        AppUIOperationDescriptor.Create("Open:" + pageId));
                source.TrySetRunning();
                opens.Enqueue(source);
                CurrentOpen = source.Operation;
                return source.Operation;
            }

            public IUIOperation<UICloseResult> Close(
                string pageId,
                UICloseRequest request)
            {
                CloseOrder.Add(pageId);
                IUIOperationSource<UICloseResult> source =
                    factory.Create<UICloseResult>(
                        AppUIOperationDescriptor.Create("Close:" + pageId));
                source.TrySetRunning();
                closes.Enqueue(source);
                CurrentClose = source.Operation;
                return source.Operation;
            }

            public void CompleteNextOpen(UIOpenResult result)
            {
                opens.Dequeue().TrySetSucceeded(result);
            }

            public void CompleteNextClose(UICloseResult result)
            {
                closes.Dequeue().TrySetSucceeded(result);
            }

            public void CancelNextClose()
            {
                closes.Dequeue().TrySetCancelled();
            }
        }

        private sealed class EmptyPageQuery : IUIPageInstanceQuery
        {
            public List<UIPageInstance> GetSnapshot()
            {
                return new List<UIPageInstance>();
            }

            public bool TryGet(string pageId, out UIPageInstance instance)
            {
                instance = null;
                return false;
            }
        }

        private sealed class StaticPageQuery : IUIPageInstanceQuery
        {
            private readonly List<UIPageInstance> instances;

            public StaticPageQuery(
                UIPageDefinition definition,
                string sceneScopeId,
                params string[] pageIds)
            {
                instances = new List<UIPageInstance>(pageIds.Length);
                for (int i = 0; i < pageIds.Length; i++)
                {
                    instances.Add(new UIPageInstance
                    {
                        PageId = pageIds[i],
                        Definition = definition,
                        SceneScopeId = sceneScopeId,
                    });
                }
            }

            public List<UIPageInstance> GetSnapshot()
            {
                return new List<UIPageInstance>(instances);
            }

            public bool TryGet(string pageId, out UIPageInstance instance)
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    if (instances[i].PageId == pageId)
                    {
                        instance = instances[i];
                        return true;
                    }
                }

                instance = null;
                return false;
            }
        }
    }
}
