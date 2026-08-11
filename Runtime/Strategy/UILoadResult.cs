using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Result returned by a page load strategy.
    /// </summary>
    public readonly struct UILoadResult
    {
        public UILoadResult(
            bool success,
            GameObject prefab,
            UIAssetLease assetLease,
            string errorMessage)
        {
            Success = success;
            Prefab = prefab;
            AssetLease = assetLease;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Success { get; }

        public GameObject Prefab { get; }

        public UIAssetLease AssetLease { get; }

        public string ErrorMessage { get; }
    }
}
