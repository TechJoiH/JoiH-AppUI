using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Joi.H.AppUI.Samples.Basic
{
    /// <summary>
    /// Explicit reference table used by the sample. It never calls Resources
    /// and can be replaced by Addressables, AssetBundles, or project services.
    /// </summary>
    public sealed class InMemoryUIAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, UnityObject> assetById =
            new Dictionary<string, UnityObject>(StringComparer.Ordinal);
        private readonly IUIOperationFactory operationFactory;

        public InMemoryUIAssetProvider(IUIOperationFactory factory)
        {
            operationFactory = factory ??
                throw new ArgumentNullException(nameof(factory));
        }

        public InMemoryUIAssetProvider(
            IUIOperationFactory factory,
            IReadOnlyList<SampleUIAssetEntry> entries)
            : this(factory)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SampleUIAssetEntry entry = entries[i];
                if (entry != null)
                {
                    Register(entry.AssetId, entry.Asset);
                }
            }
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
            if (string.IsNullOrWhiteSpace(assetId))
            {
                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.InvalidAssetId,
                    "Asset id is empty.");
                return false;
            }

            if (!assetById.TryGetValue(assetId, out UnityObject asset) ||
                !(asset is T typedAsset))
            {
                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.NotFound,
                    "Asset was not registered. AssetId=" + assetId);
                return false;
            }

            result = UIAssetLoadResult<T>.Success(typedAsset);
            return true;
        }

        public IUIOperation<UIAssetLoadResult<T>> Load<T>(
            string assetId,
            CancellationToken cancellationToken)
            where T : UnityObject
        {
            IUIOperationSource<UIAssetLoadResult<T>> source =
                operationFactory.Create<UIAssetLoadResult<T>>(
                    AppUIOperationDescriptor.Create(
                        "SampleLoad:" + (assetId ?? string.Empty),
                        cancellationToken));
            if (source == null || source.Operation == null)
            {
                throw new InvalidOperationException(
                    "The operation factory returned a null source or operation.");
            }

            source.TrySetRunning();
            TryLoad(assetId, out UIAssetLoadResult<T> result);
            source.TrySetSucceeded(result);
            return source.Operation;
        }
    }
}
