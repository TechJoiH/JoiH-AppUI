using System.Threading;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Resolves UI assets without exposing a project's resource system to AppUI.
    /// Providers may reject synchronous loads while supporting a host-defined
    /// asynchronous operation.
    /// </summary>
    public interface IUIAssetProvider
    {
        bool TryLoad<T>(string assetId, out UIAssetLoadResult<T> result)
            where T : Object;

        IUIOperation<UIAssetLoadResult<T>> Load<T>(
            string assetId,
            CancellationToken cancellationToken)
            where T : Object;

    }
}
