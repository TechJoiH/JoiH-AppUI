using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Joi.H.AppUI.Samples.Basic
{
    [Serializable]
    public sealed class SampleUIAssetEntry
    {
        public string AssetId = string.Empty;
        public UnityObject Asset;
    }

    /// <summary>
    /// Minimal consumer adapter. Configure AppUIRuntimeHost with
    /// Initialize On Awake disabled, then this installer injects the provider.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class SampleAppUIInstaller : MonoBehaviour
    {
        [SerializeField]
        private AppUIRuntimeHost runtimeHost;

        [SerializeField]
        private List<SampleUIAssetEntry> assets =
            new List<SampleUIAssetEntry>();

        private void Awake()
        {
            if (runtimeHost == null)
            {
                runtimeHost = GetComponent<AppUIRuntimeHost>();
            }

            if (runtimeHost == null)
            {
                Debug.LogError(
                    "<Joi.H.AppUI.Sample> AppUIRuntimeHost is missing.",
                    this);
                return;
            }

            runtimeHost.Initialize(new DirectReferenceUIAssetProvider(assets));
        }
    }

    public sealed class DirectReferenceUIAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, UnityObject> assetById =
            new Dictionary<string, UnityObject>(StringComparer.Ordinal);

        public DirectReferenceUIAssetProvider(
            IReadOnlyList<SampleUIAssetEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SampleUIAssetEntry entry = entries[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.AssetId) ||
                    entry.Asset == null)
                {
                    continue;
                }

                assetById[entry.AssetId] = entry.Asset;
            }
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

        public UniTask<UIAssetLoadResult<T>> LoadAsync<T>(string assetId)
            where T : UnityObject
        {
            TryLoad(assetId, out UIAssetLoadResult<T> result);
            return UniTask.FromResult(result);
        }
    }
}
