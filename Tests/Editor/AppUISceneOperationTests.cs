using System.Collections.Generic;
using NUnit.Framework;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUISceneOperationTests
    {
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

        private sealed class RecordingSceneExecutor : IUISceneCommandExecutor
        {
            private readonly IUIOperationFactory factory;
            private readonly Queue<IUIOperationSource<UICloseResult>> closes =
                new Queue<IUIOperationSource<UICloseResult>>();

            public RecordingSceneExecutor(IUIOperationFactory factory)
            {
                this.factory = factory;
            }

            public List<string> CloseOrder { get; } = new List<string>();

            public IUIOperation<UIOpenResult> Open(
                string pageId,
                UIOpenArgs args)
            {
                throw new AssertionException("Open is not expected.");
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
                return source.Operation;
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
    }
}
