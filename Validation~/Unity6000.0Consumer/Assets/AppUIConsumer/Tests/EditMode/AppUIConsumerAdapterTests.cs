using NUnit.Framework;
using System;
using System.Threading;
using Joi.H.AppUI.TestKit;
using UnityEngine;

namespace Joi.H.AppUI.Validation.Consumer.Tests
{
    public sealed class ConsumerOperationContractTests :
        AppUIOperationFactoryContractFixture
    {
        protected override IUIOperationFactory CreateOperationFactory()
        {
            return new ConsumerOperationFactory();
        }
    }

    public sealed class ConsumerAssetProviderContractTests :
        AppUIAssetProviderContractFixture
    {
        protected override AppUIAssetProviderContractContext
            CreateAssetContext()
        {
            GameObject asset = new GameObject("ConsumerContractAsset");
            ConsumerOperationFactory factory =
                new ConsumerOperationFactory();
            ConsumerAssetProvider provider =
                new ConsumerAssetProvider(factory);
            provider.Register("contract/page", asset);
            return new AppUIAssetProviderContractContext(
                provider,
                "contract/page",
                asset,
                () => UnityEngine.Object.DestroyImmediate(asset));
        }
    }

    public sealed class AppUIConsumerAdapterTests
    {
        [Test]
        public void OperationFactory_CompletesOnce_AndReplaysTerminalValue()
        {
            ConsumerOperationFactory factory =
                new ConsumerOperationFactory();
            IUIOperationSource<int> source = factory.Create<int>(
                AppUIOperationDescriptor.Create("consumer-test"));
            int firstValue = -1;
            int replayValue = -1;

            source.Operation.Register(completion =>
                firstValue = completion.Result);
            Assert.That(source.TrySetRunning(), Is.True);
            Assert.That(source.TrySetSucceeded(42), Is.True);
            Assert.That(source.TrySetSucceeded(99), Is.False);
            source.Operation.Register(completion =>
                replayValue = completion.Result);

            Assert.That(source.Operation.Status,
                Is.EqualTo(AppUIOperationStatus.Succeeded));
            Assert.That(firstValue, Is.EqualTo(42));
            Assert.That(replayValue, Is.EqualTo(42));
        }

        [Test]
        public void OperationFactory_TerminalOperation_KeepsTokenReadable()
        {
            ConsumerOperationFactory factory =
                new ConsumerOperationFactory();
            IUIOperationSource<int> source = factory.Create<int>(
                AppUIOperationDescriptor.Create("consumer-token-test"));

            Assert.That(source.TrySetSucceeded(42), Is.True);
            Assert.DoesNotThrow(() =>
            {
                CancellationToken token = source.Operation.CancellationToken;
                Assert.That(token.CanBeCanceled, Is.True);
            });
        }

        [Test]
        public void ExecutionContext_QueuesForeignThread_ThenDrainsOnOwner()
        {
            ConsumerExecutionContext context =
                ConsumerExecutionContext.CaptureCurrent();
            int invocationCount = 0;
            Exception workerException = null;
            Thread worker = new Thread(() =>
            {
                try
                {
                    context.Post(() => invocationCount++);
                }
                catch (Exception exception)
                {
                    workerException = exception;
                }
            });

            worker.Start();
            Assert.That(worker.Join(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(workerException, Is.Null);
            Assert.That(invocationCount, Is.Zero);
            Assert.That(context.PendingCount, Is.EqualTo(1));

            Assert.That(context.Drain(), Is.EqualTo(1));
            Assert.That(invocationCount, Is.EqualTo(1));
            Assert.That(context.PendingCount, Is.Zero);
        }

        [Test]
        public void AssetProvider_ReturnsOneShotLease_AndTracksRelease()
        {
            GameObject asset = new GameObject("ConsumerAsset");
            try
            {
                ConsumerOperationFactory factory =
                    new ConsumerOperationFactory();
                ConsumerAssetProvider provider =
                    new ConsumerAssetProvider(factory);
                Assert.That(provider.Register("page", asset), Is.True);

                bool loaded = provider.TryLoad(
                    "page",
                    out UIAssetLoadResult<GameObject> result);

                Assert.That(loaded, Is.True);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Asset, Is.SameAs(asset));
                Assert.That(result.Lease, Is.Not.Null);
                Assert.That(provider.LoadCount, Is.EqualTo(1));
                result.Lease.Dispose();
                result.Lease.Dispose();
                Assert.That(provider.ReleaseCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetProvider_DeferredLoad_CompletesExplicitly()
        {
            GameObject asset = new GameObject("DeferredAsset");
            try
            {
                ConsumerOperationFactory factory =
                    new ConsumerOperationFactory();
                ConsumerAssetProvider provider =
                    new ConsumerAssetProvider(factory)
                    {
                        CompleteLoadsImmediately = false,
                    };
                provider.Register("page", asset);

                IUIOperation<UIAssetLoadResult<GameObject>> operation =
                    provider.Load<GameObject>("page", CancellationToken.None);

                Assert.That(operation.IsTerminal, Is.False);
                Assert.That(provider.PendingCount, Is.EqualTo(1));
                Assert.That(provider.CompleteNextPending(), Is.True);
                Assert.That(operation.TryGetCompletion(out var completion),
                    Is.True);
                Assert.That(completion.Status,
                    Is.EqualTo(AppUIOperationStatus.Succeeded));
                Assert.That(completion.Result.IsSuccess, Is.True);
                completion.Result.Lease.Dispose();
                Assert.That(provider.ReleaseCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
