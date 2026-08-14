using System;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIAssetBoundaryTests
    {
        [Test]
        public void AssetLease_Dispose_ReleasesExactlyOnce()
        {
            int releaseCount = 0;
            UIAssetLease lease = new UIAssetLease(() => releaseCount++);

            lease.Dispose();
            lease.Dispose();

            Assert.That(releaseCount, Is.EqualTo(1));
            Assert.That(lease.IsValid, Is.False);
        }

        [Test]
        public void LeaseTransfer_Claim_MovesOwnershipExactlyOnce()
        {
            int releaseCount = 0;
            UIAssetLease lease = new UIAssetLease(() => releaseCount++);
            UIAssetLeaseTransfer transfer =
                new UIAssetLeaseTransfer(lease);

            UIAssetLeaseClaim claim = transfer.Claim();

            Assert.That(claim.AssetLease, Is.SameAs(lease));
            Assert.Throws<InvalidOperationException>(() => transfer.Claim());
            Assert.That(claim.TryCommit(transfer), Is.True);

            transfer.Dispose();
            Assert.That(releaseCount, Is.Zero);
            claim.AssetLease.Dispose();
            claim.AssetLease.Dispose();
            Assert.That(releaseCount, Is.EqualTo(1));
        }

        [Test]
        public void DefaultInstanceAllocation_Release_DestroysAndDisposesLeaseOnce()
        {
            int releaseCount = 0;
            GameObject prefab = new GameObject(
                "InstanceStrategyPrefab",
                typeof(RectTransform));
            GameObject parent = new GameObject(
                "InstanceStrategyParent",
                typeof(RectTransform));
            UIPageDefinition definition =
                ScriptableObject.CreateInstance<UIPageDefinition>();
            UIAssetLeaseTransfer transfer = new UIAssetLeaseTransfer(
                new UIAssetLease(() => releaseCount++));
            UIPageInstanceAllocation allocation = null;
            try
            {
                allocation = new DefaultUIPageInstanceStrategy().Create(
                    new UIPageInstanceCreationRequest(
                        definition,
                        prefab,
                        (RectTransform)parent.transform,
                        transfer));
                Assert.That(allocation, Is.Not.Null);
                Assert.That(allocation.GameObject, Is.Not.Null);
                Assert.That(allocation.TryAccept(transfer), Is.True);

                allocation.Dispose();
                allocation.Dispose();

                Assert.That(releaseCount, Is.EqualTo(1));
            }
            finally
            {
                allocation?.Dispose();
                transfer.Dispose();
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void CreateFailure_UnclaimedLeaseReturnsToAppUI()
        {
            int releaseCount = 0;
            UIAssetLeaseTransfer transfer = new UIAssetLeaseTransfer(
                new UIAssetLease(() => releaseCount++));

            UIAssetLeaseClaim abandonedClaim = transfer.Claim();
            transfer.Dispose();

            Assert.That(releaseCount, Is.EqualTo(1));
            Assert.That(abandonedClaim.TryCommit(transfer), Is.False);
        }

        [Test]
        public void PoolingStrategy_ReturnKeepsLeaseUntilPoolEviction()
        {
            int releaseCount = 0;
            GameObject instance = new GameObject("PooledInstance");
            UIAssetLeaseTransfer transfer = new UIAssetLeaseTransfer(
                new UIAssetLease(() => releaseCount++));
            UIAssetLeaseClaim claim = transfer.Claim();
            UIAssetLease retainedLease = null;
            UIPageInstanceAllocation allocation =
                new UIPageInstanceAllocation(
                    instance,
                    claim,
                    context =>
                    {
                        retainedLease = context.AssetLease;
                        context.GameObject.SetActive(false);
                        return UIPageInstanceReleaseDisposition.RetainLease;
                    });

            Assert.That(allocation.TryAccept(transfer), Is.True);
            allocation.Dispose();
            transfer.Dispose();

            Assert.That(releaseCount, Is.Zero);
            Assert.That(instance.activeSelf, Is.False);

            retainedLease.Dispose();
            Assert.That(releaseCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(instance);
        }

        [Test]
        public void Provider_UnsupportedSyncLoad_IsExplicit()
        {
            ContractProvider provider = new ContractProvider(
                new ManualUIOperationFactory());

            bool success = provider.TryLoad<GameObject>(
                "page",
                out UIAssetLoadResult<GameObject> result);

            Assert.That(success, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(UIAssetLoadStatus.SynchronousLoadUnsupported));
        }

        [Test]
        public void DefaultLoadStrategy_ForwardsCancellationTokenToProvider()
        {
            ManualUIOperationFactory factory =
                new ManualUIOperationFactory();
            ContractProvider provider = new ContractProvider(factory);
            UIPageDefinition definition =
                ScriptableObject.CreateInstance<UIPageDefinition>();
            CancellationTokenSource cancellation =
                new CancellationTokenSource();
            try
            {
                SetPrefabAssetId(definition, "page");

                IUIOperation<UILoadResult> operation =
                    new DefaultUILoadStrategy().Load(
                        definition,
                        provider,
                        factory,
                        cancellation.Token);

                Assert.That(operation.IsTerminal, Is.True);
                Assert.That(
                    provider.ReceivedCancellationToken,
                    Is.EqualTo(cancellation.Token));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                cancellation.Dispose();
            }
        }

        [Test]
        public void Transition_Immediate_DoesNotOwnOperation()
        {
            UITransition transition = UITransition.Immediate;

            Assert.That(transition.IsImmediate, Is.True);
            Assert.That(transition.Operation, Is.Null);
        }

        [Test]
        public void Transition_WaitForNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                UITransition.WaitFor(null));
        }

        [Test]
        public void LoadResult_Failure_PreservesProviderLeaseForCleanup()
        {
            int releaseCount = 0;
            UIAssetLease lease = new UIAssetLease(() => releaseCount++);
            UIAssetLoadResult<GameObject> result =
                UIAssetLoadResult<GameObject>.Failure(
                    UIAssetLoadStatus.ProviderFailed,
                    "failed",
                    lease);

            result.Lease.Dispose();

            Assert.That(releaseCount, Is.EqualTo(1));
            Assert.That(result.ErrorMessage, Is.EqualTo("failed"));
        }

        private static void SetPrefabAssetId(
            UIPageDefinition definition,
            string value)
        {
            typeof(UIDefinitionAssetBase)
                .GetField(
                    "m_PrefabAssetId",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(definition, value);
        }

        private sealed class ContractProvider : IUIAssetProvider
        {
            private readonly IUIOperationFactory factory;

            public ContractProvider(IUIOperationFactory factory)
            {
                this.factory = factory;
            }

            public CancellationToken ReceivedCancellationToken
            {
                get;
                private set;
            }

            public bool TryLoad<T>(
                string assetId,
                out UIAssetLoadResult<T> result)
                where T : UnityEngine.Object
            {
                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.SynchronousLoadUnsupported,
                    "Synchronous loading is not supported.");
                return false;
            }

            public IUIOperation<UIAssetLoadResult<T>> Load<T>(
                string assetId,
                CancellationToken cancellationToken)
                where T : UnityEngine.Object
            {
                ReceivedCancellationToken = cancellationToken;
                IUIOperationSource<UIAssetLoadResult<T>> source =
                    factory.Create<UIAssetLoadResult<T>>(
                        AppUIOperationDescriptor.Create(
                            "ContractLoad",
                            cancellationToken));
                source.TrySetRunning();
                source.TrySetSucceeded(
                    UIAssetLoadResult<T>.Failure(
                        UIAssetLoadStatus.NotFound,
                        "Not found."));
                return source.Operation;
            }
        }
    }
}
