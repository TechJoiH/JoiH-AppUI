using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Default page strategy that resolves PrefabAssetId through IUIAssetProvider.
    /// </summary>
    public sealed class DefaultUILoadStrategy : IUILoadStrategy
    {
        public string StrategyId
        {
            get { return string.Empty; }
        }

        public async UniTask<UILoadResult> LoadAsync(
            UIPageDefinition definition,
            IUIAssetProvider assetProvider)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.PrefabAssetId))
            {
                return new UILoadResult(
                    false,
                    null,
                    null,
                    "UI definition or PrefabAssetId is invalid.");
            }

            if (assetProvider == null)
            {
                return new UILoadResult(
                    false,
                    null,
                    null,
                    "IUIAssetProvider is missing.");
            }

            UIAssetLoadResult<GameObject> result =
                await assetProvider.LoadAsync<GameObject>(definition.PrefabAssetId);
            return new UILoadResult(
                result.IsSuccess,
                result.Asset,
                result.Lease,
                result.ErrorMessage);
        }
    }
}
