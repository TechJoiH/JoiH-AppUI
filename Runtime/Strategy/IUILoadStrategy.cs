using Cysharp.Threading.Tasks;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Loads a page prefab through a project-independent asset provider.
    /// </summary>
    public interface IUILoadStrategy
    {
        string StrategyId { get; }

        UniTask<UILoadResult> LoadAsync(
            UIPageDefinition definition,
            IUIAssetProvider assetProvider);
    }
}
