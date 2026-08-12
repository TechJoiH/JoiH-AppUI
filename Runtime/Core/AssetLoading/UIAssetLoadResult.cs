using UnityEngine;

namespace Joi.H.AppUI
{
    public enum UIAssetLoadStatus
    {
        Success = 0,
        InvalidAssetId = 1,
        NotFound = 2,
        SynchronousLoadUnsupported = 3,
        ProviderFailed = 4,
    }

    /// <summary>
    /// Provider-neutral asset result. A successful result can optionally own a lease
    /// that AppUI disposes after the page or notice pool releases the asset.
    /// </summary>
    public readonly struct UIAssetLoadResult<T>
        where T : Object
    {
        public UIAssetLoadResult(
            UIAssetLoadStatus status,
            T asset,
            UIAssetLease lease,
            string errorMessage)
        {
            Status = status;
            Asset = asset;
            Lease = lease;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public UIAssetLoadStatus Status { get; }

        public T Asset { get; }

        public UIAssetLease Lease { get; }

        public string ErrorMessage { get; }

        public bool IsSuccess
        {
            get { return Status == UIAssetLoadStatus.Success && Asset != null; }
        }

        public static UIAssetLoadResult<T> Success(T asset, UIAssetLease lease = null)
        {
            return new UIAssetLoadResult<T>(
                UIAssetLoadStatus.Success,
                asset,
                lease,
                string.Empty);
        }

        public static UIAssetLoadResult<T> Failure(
            UIAssetLoadStatus status,
            string errorMessage,
            UIAssetLease lease = null)
        {
            return new UIAssetLoadResult<T>(status, null, lease, errorMessage);
        }
    }
}
