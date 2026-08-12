using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIFlowOperationTests
    {
        [Test]
        public void ReplacePage_WaitsForOpenBeforeClosingCurrentPage()
        {
            FlowFixture fixture = new FlowFixture();

            IUIOperation<UIFlowApplyResult> operation = fixture.Coordinator.Apply(
                "current",
                fixture.Context,
                FlowResult.For(UIFlowActionKind.ReplacePage, "target"));

            CollectionAssert.AreEqual(new[] { "Open:target" }, fixture.UI.Calls);
            fixture.UI.CompleteOpen(UIOpenResult.Ok(
                new UIPageHandle(
                    "target",
                    UIPageState.Open,
                    UILayerId.OverlayLayer)));

            CollectionAssert.AreEqual(
                new[] { "Open:target", "Close:current" },
                fixture.UI.Calls);
            Assert.That(operation.IsTerminal, Is.False);

            fixture.UI.CompleteClose(
                UICloseResult.Ok("current", UIPageState.Released));

            Assert.That(operation.TryGetCompletion(out var completion), Is.True);
            Assert.That(completion.Status, Is.EqualTo(AppUIOperationStatus.Succeeded));
            Assert.That(completion.Result.Success, Is.True);
            Assert.That(completion.Result.Applied, Is.True);
        }

        [Test]
        public void CloseAndRefresh_WaitsForCloseBeforeRefreshingTarget()
        {
            FlowFixture fixture = new FlowFixture();

            IUIOperation<UIFlowApplyResult> operation = fixture.Coordinator.Apply(
                "current",
                fixture.Context,
                FlowResult.For(
                    UIFlowActionKind.CloseCurrentAndRefreshTarget,
                    "target"));

            CollectionAssert.AreEqual(new[] { "Close:current" }, fixture.UI.Calls);
            fixture.UI.CompleteClose(
                UICloseResult.Ok("current", UIPageState.Released));

            CollectionAssert.AreEqual(
                new[] { "Close:current", "Refresh:target" },
                fixture.UI.Calls);
            Assert.That(operation.IsTerminal, Is.False);

            fixture.UI.CompleteRefresh(
                UIRefreshResult.Ok("target", UIPageState.Open));

            Assert.That(operation.TryGetCompletion(out var completion), Is.True);
            Assert.That(completion.Result.Success, Is.True);
        }

        [Test]
        public void DomainOpenFailure_IsSuccessfulOperationWithFailedFlowResult()
        {
            FlowFixture fixture = new FlowFixture();

            IUIOperation<UIFlowApplyResult> operation = fixture.Coordinator.Apply(
                string.Empty,
                fixture.Context,
                FlowResult.For(UIFlowActionKind.OpenPage, "missing"));
            fixture.UI.CompleteOpen(
                UIOpenResult.Fail(UIPageOpenError.DefinitionNotFound));

            Assert.That(operation.TryGetCompletion(out var completion), Is.True);
            Assert.That(completion.Status, Is.EqualTo(AppUIOperationStatus.Succeeded));
            Assert.That(completion.Result.Success, Is.False);
            Assert.That(completion.Result.ErrorCode, Is.EqualTo("OpenTargetFailed"));
        }

        private sealed class FlowFixture
        {
            public FlowFixture()
            {
                Factory = new ManualUIOperationFactory();
                UI = new RecordingUIService(Factory);
                Coordinator = new AppUIFlowCoordinator(
                    Factory,
                    new ImmediateAppUIExecutionContext());
                Context = new TestFlowContext(UI, Coordinator);
            }

            public ManualUIOperationFactory Factory { get; }
            public RecordingUIService UI { get; }
            public AppUIFlowCoordinator Coordinator { get; }
            public TestFlowContext Context { get; }
        }

        private sealed class TestFlowContext : UIFlowContextBase
        {
            public TestFlowContext(
                IUIControllerService ui,
                IUIFlowCoordinator flow)
                : base(ui, flow, null, null, "test-scope")
            {
            }
        }

        private sealed class FlowResult : IUIFlowCommandResult
        {
            public bool Success { get; private set; }
            public string ErrorCode { get; private set; }
            public string Message { get; private set; }
            public string NextPageId { get; private set; }
            public UIFlowActionKind FlowAction { get; private set; }
            public object FlowPayload { get; private set; }

            public static FlowResult For(
                UIFlowActionKind action,
                string targetPageId)
            {
                return new FlowResult
                {
                    Success = true,
                    ErrorCode = string.Empty,
                    Message = string.Empty,
                    NextPageId = targetPageId,
                    FlowAction = action,
                };
            }
        }

        private sealed class RecordingUIService : IUIControllerService
        {
            private readonly IUIOperationFactory factory;
            private IUIOperationSource<UIOpenResult> open;
            private IUIOperationSource<UICloseResult> close;
            private IUIOperationSource<UIRefreshResult> refresh;

            public RecordingUIService(IUIOperationFactory factory)
            {
                this.factory = factory;
            }

            public List<string> Calls { get; } = new List<string>();

            public IUIOperation<UIOpenResult> Open(string pageId)
            {
                return Open(pageId, UIOpenArgs.None);
            }

            public IUIOperation<UIOpenResult> Open(string pageId, object data)
            {
                return Open(pageId, UIOpenArgs.FromExplicit(data));
            }

            public IUIOperation<UIOpenResult> Open(
                string pageId,
                UIOpenArgs args)
            {
                Calls.Add("Open:" + pageId);
                open = CreateSource<UIOpenResult>("Open:" + pageId);
                return open.Operation;
            }

            public IUIOperation<UICloseResult> Close(string pageId)
            {
                return Close(pageId, UICloseRequest.Default);
            }

            public IUIOperation<UICloseResult> Close(
                string pageId,
                UICloseRequest request)
            {
                Calls.Add("Close:" + pageId);
                close = CreateSource<UICloseResult>("Close:" + pageId);
                return close.Operation;
            }

            public IUIOperation<UIRefreshResult> Refresh(
                string pageId,
                object data)
            {
                return Refresh(pageId, new UIRefreshArgs(data));
            }

            public IUIOperation<UIRefreshResult> Refresh(
                string pageId,
                UIRefreshArgs args)
            {
                Calls.Add("Refresh:" + pageId);
                refresh = CreateSource<UIRefreshResult>("Refresh:" + pageId);
                return refresh.Operation;
            }

            public void CompleteOpen(UIOpenResult result)
            {
                open.TrySetSucceeded(result);
            }

            public void CompleteClose(UICloseResult result)
            {
                close.TrySetSucceeded(result);
            }

            public void CompleteRefresh(UIRefreshResult result)
            {
                refresh.TrySetSucceeded(result);
            }

            public IUIOperation<UISceneBindResult> BindScene(
                SceneUIBindingData bindingData)
            {
                throw Unexpected();
            }

            public IUIOperation<UISceneExitResult> UnbindScene(
                SceneUIBindingData bindingData)
            {
                throw Unexpected();
            }

            public IUIOperation<UIScopeReleaseResult> ReleaseScope(
                UIPageScope scope,
                string sceneScopeId)
            {
                throw Unexpected();
            }

            public IUIOperation<UICancelResult> Cancel()
            {
                throw Unexpected();
            }

            public IUIOperation<UICloseResult> CloseTop()
            {
                throw Unexpected();
            }

            public IUIOperation<UICloseResult> CloseTop(UILayerId layerId)
            {
                throw Unexpected();
            }

            public bool IsOpen(string pageId)
            {
                return false;
            }

            public bool IsOpening(string pageId)
            {
                return false;
            }

            public bool TryGetPageState(
                string pageId,
                out UIPageState state)
            {
                state = default;
                return false;
            }

            private IUIOperationSource<TResult> CreateSource<TResult>(
                string name)
            {
                IUIOperationSource<TResult> source =
                    factory.Create<TResult>(
                        AppUIOperationDescriptor.Create(name));
                source.TrySetRunning();
                return source;
            }

            private static Exception Unexpected()
            {
                return new AssertionException(
                    "Unexpected UI service call.");
            }
        }
    }
}
