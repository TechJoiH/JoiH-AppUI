using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Joi.H.AppUI.Validation.Consumer
{
    public sealed class ConsumerAssetProvider : IUIAssetProvider
    {
        private interface IPendingLoad
        {
            void Complete();
        }

        private sealed class PendingLoad<T> : IPendingLoad
            where T : UnityObject
        {
            private readonly ConsumerAssetProvider owner;
            private readonly string assetId;
            private readonly IUIOperationSource<UIAssetLoadResult<T>> source;

            public PendingLoad(
                ConsumerAssetProvider owner,
                string assetId,
                IUIOperationSource<UIAssetLoadResult<T>> source)
            {
                this.owner = owner;
                this.assetId = assetId;
                this.source = source;
            }

            public void Complete()
            {
                if (source.Operation.CancellationToken.IsCancellationRequested)
                {
                    source.TrySetCancelled();
                    return;
                }

                source.TrySetSucceeded(owner.Resolve<T>(assetId));
            }
        }

        private readonly Dictionary<string, UnityObject> assetById =
            new Dictionary<string, UnityObject>(StringComparer.Ordinal);
        private readonly Queue<IPendingLoad> pending =
            new Queue<IPendingLoad>();
        private readonly IUIOperationFactory operationFactory;
        private int loadCount;
        private int releaseCount;

        public ConsumerAssetProvider(IUIOperationFactory factory)
        {
            operationFactory = factory ??
                throw new ArgumentNullException(nameof(factory));
            CompleteLoadsImmediately = true;
        }

        public bool CompleteLoadsImmediately { get; set; }

        public int LoadCount
        {
            get { return Volatile.Read(ref loadCount); }
        }

        public int ReleaseCount
        {
            get { return Volatile.Read(ref releaseCount); }
        }

        public int PendingCount
        {
            get { return pending.Count; }
        }

        public bool Register(string assetId, UnityObject asset)
        {
            if (string.IsNullOrWhiteSpace(assetId) || asset == null)
            {
                return false;
            }

            assetById[assetId] = asset;
            return true;
        }

        public bool TryLoad<T>(
            string assetId,
            out UIAssetLoadResult<T> result)
            where T : UnityObject
        {
            Interlocked.Increment(ref loadCount);
            result = Resolve<T>(assetId);
            return result.IsSuccess;
        }

        public IUIOperation<UIAssetLoadResult<T>> Load<T>(
            string assetId,
            CancellationToken cancellationToken)
            where T : UnityObject
        {
            Interlocked.Increment(ref loadCount);
            IUIOperationSource<UIAssetLoadResult<T>> source =
                operationFactory.Create<UIAssetLoadResult<T>>(
                    AppUIOperationDescriptor.Create(
                        "ConsumerLoad:" + (assetId ?? string.Empty),
                        cancellationToken));
            if (source == null || source.Operation == null)
            {
                throw new InvalidOperationException(
                    "Consumer operation factory returned a null source or operation.");
            }

            source.TrySetRunning();
            if (cancellationToken.IsCancellationRequested)
            {
                source.TrySetCancelled();
            }
            else if (CompleteLoadsImmediately)
            {
                source.TrySetSucceeded(Resolve<T>(assetId));
            }
            else
            {
                pending.Enqueue(new PendingLoad<T>(this, assetId, source));
            }

            return source.Operation;
        }

        public bool CompleteNextPending()
        {
            if (pending.Count == 0)
            {
                return false;
            }

            pending.Dequeue().Complete();
            return true;
        }

        private UIAssetLoadResult<T> Resolve<T>(string assetId)
            where T : UnityObject
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                return UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.InvalidAssetId,
                    "Asset id is empty.");
            }

            if (!assetById.TryGetValue(assetId, out UnityObject asset) ||
                !(asset is T typedAsset))
            {
                return UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.NotFound,
                    "Asset was not registered. AssetId=" + assetId);
            }

            UIAssetLease lease = new UIAssetLease(() =>
                Interlocked.Increment(ref releaseCount));
            return UIAssetLoadResult<T>.Success(typedAsset, lease);
        }
    }
}
