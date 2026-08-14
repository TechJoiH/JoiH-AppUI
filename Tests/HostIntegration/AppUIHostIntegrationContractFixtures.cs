using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

namespace Joi.H.AppUI.TestKit
{
    /// <summary>
    /// Reusable contract tests for a host-provided operation factory.
    /// Derive a test class and return the adapter used by the consumer project.
    /// </summary>
    public abstract class AppUIOperationFactoryContractFixture
    {
        protected abstract IUIOperationFactory CreateOperationFactory();

        [Test]
        public void Factory_CreatesValidSourceAndOperation()
        {
            IUIOperationSource<int> source = CreateSource();

            Assert.That(source, Is.Not.Null);
            Assert.That(source.Operation, Is.Not.Null);
            Assert.That(source.Operation.Status,
                Is.EqualTo(AppUIOperationStatus.Created));
            Assert.That(source.Operation.IsTerminal, Is.False);
        }

        [Test]
        public void TerminalCompletion_IsPublishedOnceToActiveAndLateObservers()
        {
            IUIOperationSource<int> source = CreateSource();
            int activeCalls = 0;
            int lateCalls = 0;
            source.Operation.Register(completion =>
            {
                activeCalls++;
                Assert.That(completion.Status,
                    Is.EqualTo(AppUIOperationStatus.Succeeded));
                Assert.That(completion.Result, Is.EqualTo(17));
            });

            Assert.That(source.TrySetRunning(), Is.True);
            Assert.That(source.TrySetSucceeded(17), Is.True);
            Assert.That(source.TrySetSucceeded(18), Is.False);
            source.Operation.Register(completion =>
            {
                lateCalls++;
                Assert.That(completion.Result, Is.EqualTo(17));
            });

            Assert.That(activeCalls, Is.EqualTo(1));
            Assert.That(lateCalls, Is.EqualTo(1));
            Assert.That(source.Operation.TryGetCompletion(
                    out AppUIOperationCompletion<int> completion),
                Is.True);
            Assert.That(completion.Result, Is.EqualTo(17));
        }

        [Test]
        public void DisposedSubscription_DoesNotReceiveCompletion()
        {
            IUIOperationSource<int> source = CreateSource();
            int disposedCalls = 0;
            int activeCalls = 0;
            IDisposable disposed = source.Operation.Register(
                _ => disposedCalls++);
            source.Operation.Register(_ => activeCalls++);

            disposed.Dispose();
            source.TrySetSucceeded(1);

            Assert.That(disposedCalls, Is.Zero);
            Assert.That(activeCalls, Is.EqualTo(1));
        }

        [Test]
        public void CancellationRequest_DoesNotFabricateTerminalCompletion()
        {
            IUIOperationSource<int> source = CreateSource();

            Assert.That(source.Operation.RequestCancellation(), Is.True);
            Assert.That(
                source.Operation.CancellationToken.IsCancellationRequested,
                Is.True);
            Assert.That(source.Operation.IsTerminal, Is.False);
            Assert.That(source.TrySetCancelled(), Is.True);
            Assert.That(source.Operation.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelled));
        }

        private IUIOperationSource<int> CreateSource()
        {
            IUIOperationFactory factory = CreateOperationFactory();
            Assert.That(factory, Is.Not.Null);
            return factory.Create<int>(
                AppUIOperationDescriptor.Create("HostContract"));
        }
    }

    /// <summary>
    /// Per-test asset provider state supplied by a consumer adapter test.
    /// </summary>
    public sealed class AppUIAssetProviderContractContext : IDisposable
    {
        private readonly Action dispose;

        public AppUIAssetProviderContractContext(
            IUIAssetProvider provider,
            string existingAssetId,
            UnityObject expectedAsset,
            Action disposeAction = null)
        {
            Provider = provider ?? throw new ArgumentNullException(
                nameof(provider));
            ExistingAssetId = !string.IsNullOrWhiteSpace(existingAssetId)
                ? existingAssetId
                : throw new ArgumentException(
                    "An existing AssetId is required.",
                    nameof(existingAssetId));
            ExpectedAsset = expectedAsset ?? throw new ArgumentNullException(
                nameof(expectedAsset));
            dispose = disposeAction;
        }

        public IUIAssetProvider Provider { get; }
        public string ExistingAssetId { get; }
        public UnityObject ExpectedAsset { get; }

        public void Dispose()
        {
            dispose?.Invoke();
        }
    }

    /// <summary>
    /// Reusable contract tests for a host-provided asset adapter.
    /// </summary>
    public abstract class AppUIAssetProviderContractFixture
    {
        protected virtual int MaxCompletionFrames => 300;

        protected abstract AppUIAssetProviderContractContext CreateAssetContext();

        [Test]
        public void TryLoadExistingAsset_IsSuccessOrExplicitlyUnsupported()
        {
            using (AppUIAssetProviderContractContext context =
                   CreateAssetContext())
            {
                bool loaded = context.Provider.TryLoad(
                    context.ExistingAssetId,
                    out UIAssetLoadResult<UnityObject> result);

                if (result.Status ==
                    UIAssetLoadStatus.SynchronousLoadUnsupported)
                {
                    Assert.That(loaded, Is.False);
                    return;
                }

                Assert.That(loaded, Is.True, result.ErrorMessage);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Asset, Is.SameAs(context.ExpectedAsset));
                result.Lease?.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator LoadExistingAsset_CompletesWithExpectedAsset()
        {
            AppUIAssetProviderContractContext context = CreateAssetContext();
            try
            {
                IUIOperation<UIAssetLoadResult<UnityObject>> operation =
                    context.Provider.Load<UnityObject>(
                        context.ExistingAssetId,
                        CancellationToken.None);
                Assert.That(operation, Is.Not.Null);
                AppUIOperationCompletion<UIAssetLoadResult<UnityObject>>
                    completion = default;
                bool completed = false;
                operation.Register(value =>
                {
                    completion = value;
                    completed = true;
                });

                int frames = 0;
                while (!completed && frames < MaxCompletionFrames)
                {
                    frames++;
                    yield return null;
                }

                Assert.That(completed, Is.True,
                    "Asset operation did not complete within the contract timeout.");
                Assert.That(completion.Status,
                    Is.EqualTo(AppUIOperationStatus.Succeeded));
                Assert.That(completion.Result.IsSuccess,
                    Is.True,
                    completion.Result.ErrorMessage);
                Assert.That(completion.Result.Asset,
                    Is.SameAs(context.ExpectedAsset));
                completion.Result.Lease?.Dispose();
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void EmptyAssetId_NeverReportsDomainSuccess()
        {
            using (AppUIAssetProviderContractContext context =
                   CreateAssetContext())
            {
                bool loaded = context.Provider.TryLoad(
                    string.Empty,
                    out UIAssetLoadResult<UnityObject> result);

                Assert.That(loaded, Is.False);
                Assert.That(result.IsSuccess, Is.False);
            }
        }
    }

    /// <summary>
    /// Reusable contract tests for returning host completions to Unity.
    /// </summary>
    public abstract class AppUIExecutionContextContractFixture
    {
        protected virtual int MaxCompletionFrames => 120;

        protected abstract IAppUIExecutionContext CreateExecutionContext();

        [UnityTest]
        public IEnumerator WorkerPost_RunsOnceOnReportedCurrentContext()
        {
            IAppUIExecutionContext context = CreateExecutionContext();
            Assert.That(context, Is.Not.Null);
            int calls = 0;
            bool callbackWasCurrent = false;
            Exception postException = null;
            Thread worker = new Thread(() =>
            {
                try
                {
                    context.Post(() =>
                    {
                        callbackWasCurrent = context.IsCurrent;
                        Interlocked.Increment(ref calls);
                    });
                }
                catch (Exception exception)
                {
                    postException = exception;
                }
            });
            worker.Start();
            Assert.That(worker.Join(2000), Is.True,
                "Execution-context worker did not return.");

            int frames = 0;
            while (Volatile.Read(ref calls) == 0 &&
                   postException == null &&
                   frames < MaxCompletionFrames)
            {
                frames++;
                yield return null;
            }

            Assert.That(postException, Is.Null);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(callbackWasCurrent, Is.True);
        }
    }

    public interface IAppUIHostLifecycleContractDriver : IDisposable
    {
        bool IsInitialized { get; }
        AppUIInitializationResult Initialize();
        void Shutdown();
    }

    /// <summary>
    /// Verifies initialization, shutdown, and reinitialization through a
    /// consumer-owned lifecycle bridge.
    /// </summary>
    public abstract class AppUIHostLifecycleContractFixture
    {
        protected abstract IAppUIHostLifecycleContractDriver CreateDriver();

        [Test]
        public void InitializeShutdownAndReinitialize_AreSymmetric()
        {
            using (IAppUIHostLifecycleContractDriver driver = CreateDriver())
            {
                AppUIInitializationResult first = driver.Initialize();
                Assert.That(first.Success, Is.True, first.Status.ToString());
                Assert.That(driver.IsInitialized, Is.True);

                driver.Shutdown();
                Assert.That(driver.IsInitialized, Is.False);

                AppUIInitializationResult second = driver.Initialize();
                Assert.That(second.Success, Is.True, second.Status.ToString());
                Assert.That(driver.IsInitialized, Is.True);
                driver.Shutdown();
            }
        }
    }

    public sealed class AppUIInstanceStrategyContractContext : IDisposable
    {
        private readonly Action dispose;

        public AppUIInstanceStrategyContractContext(
            IUIPageInstanceStrategy strategy,
            UIPageDefinition definition,
            GameObject prefab,
            RectTransform parent,
            Action disposeAction = null)
        {
            Strategy = strategy ?? throw new ArgumentNullException(
                nameof(strategy));
            Definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            Prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            dispose = disposeAction;
        }

        public IUIPageInstanceStrategy Strategy { get; }
        public UIPageDefinition Definition { get; }
        public GameObject Prefab { get; }
        public RectTransform Parent { get; }

        public void Dispose()
        {
            dispose?.Invoke();
        }
    }

    /// <summary>
    /// Verifies the public rejection boundary of an instance strategy. The
    /// accepted allocation path remains owned and exercised by AppUI Runtime.
    /// </summary>
    public abstract class AppUIInstanceStrategyContractFixture
    {
        protected abstract AppUIInstanceStrategyContractContext
            CreateInstanceContext();

        [Test]
        public void RejectedAllocation_ReleasesObjectAndLeaseExactlyOnce()
        {
            using (AppUIInstanceStrategyContractContext context =
                   CreateInstanceContext())
            {
                int leaseReleases = 0;
                UIAssetLease lease = new UIAssetLease(
                    () => leaseReleases++);
                using (UIAssetLeaseTransfer transfer =
                       new UIAssetLeaseTransfer(lease))
                {
                    UIPageInstanceAllocation allocation =
                        context.Strategy.Create(
                            new UIPageInstanceCreationRequest(
                                context.Definition,
                                context.Prefab,
                                context.Parent,
                                transfer));

                    Assert.That(allocation, Is.Not.Null);
                    Assert.That(allocation.GameObject, Is.Not.Null);
                    Assert.That(allocation.IsAccepted, Is.False);
                    allocation.Dispose();
                    allocation.Dispose();
                }

                Assert.That(leaseReleases, Is.EqualTo(1));
            }
        }
    }
}
