using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Resolves UI assets without exposing a project's resource system to AppUI.
    /// Providers may reject synchronous loads while still supporting LoadAsync.
    /// </summary>
    public interface IUIAssetProvider
    {
        bool TryLoad<T>(string assetId, out UIAssetLoadResult<T> result)
            where T : Object;

        UniTask<UIAssetLoadResult<T>> LoadAsync<T>(string assetId)
            where T : Object;
    }
}
