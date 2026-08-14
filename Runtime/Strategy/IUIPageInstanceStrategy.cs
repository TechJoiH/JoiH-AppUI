using UnityEngine;

namespace Joi.H.AppUI
{
    public readonly struct UIPageInstanceCreationRequest
    {
        public UIPageInstanceCreationRequest(
            UIPageDefinition definition,
            GameObject prefab,
            RectTransform parent,
            UIAssetLeaseTransfer assetLeaseTransfer)
        {
            Definition = definition;
            Prefab = prefab;
            Parent = parent;
            AssetLeaseTransfer = assetLeaseTransfer;
        }

        public UIPageDefinition Definition { get; }
        public GameObject Prefab { get; }
        public RectTransform Parent { get; }
        public UIAssetLeaseTransfer AssetLeaseTransfer { get; }
    }

    /// <summary>
    /// Creates and releases one page instance through a symmetric allocation.
    /// Pooling strategies may retain the lease only while they retain the
    /// living pooled object, and must return it on pool eviction or shutdown.
    /// </summary>
    public interface IUIPageInstanceStrategy
    {
        string StrategyId { get; }

        UIPageInstanceAllocation Create(
            UIPageInstanceCreationRequest request);
    }
}
