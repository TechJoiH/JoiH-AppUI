using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Dependency-free provider backed by Unity Resources.
    /// Resources owns the loaded asset lifetime, so successful results have no lease.
    /// </summary>
    public sealed class ResourcesUIAssetProvider : IUIAssetProvider
    {
        public bool TryLoad<T>(string assetId, out UIAssetLoadResult<T> result)
            where T : UnityObject
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.InvalidAssetId,
                    "Asset id is empty.");
                return false;
            }

            try
            {
                T asset = Resources.Load<T>(assetId);
                if (asset == null)
                {
                    result = UIAssetLoadResult<T>.Failure(
                        UIAssetLoadStatus.NotFound,
                        "Resource was not found. AssetId=" + assetId);
                    return false;
                }

                result = UIAssetLoadResult<T>.Success(asset);
                return true;
            }
            catch (Exception exception)
            {
                result = UIAssetLoadResult<T>.Failure(
                    UIAssetLoadStatus.ProviderFailed,
                    exception.Message);
                return false;
            }
        }

        public UniTask<UIAssetLoadResult<T>> LoadAsync<T>(string assetId)
            where T : UnityObject
        {
            TryLoad(assetId, out UIAssetLoadResult<T> result);
            return UniTask.FromResult(result);
        }
    }
}
