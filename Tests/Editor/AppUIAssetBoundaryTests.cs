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
        public void ResourcesProvider_EmptyAssetId_ReturnsInvalidId()
        {
            ResourcesUIAssetProvider provider = new ResourcesUIAssetProvider();

            bool success = provider.TryLoad<GameObject>(
                string.Empty,
                out UIAssetLoadResult<GameObject> result);

            Assert.That(success, Is.False);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Status, Is.EqualTo(UIAssetLoadStatus.InvalidAssetId));
            Assert.That(result.Lease, Is.Null);
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
    }
}
