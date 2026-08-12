using System.Threading;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Loads a page prefab through a project-independent asset provider.
    /// </summary>
    public interface IUILoadStrategy
    {
        string StrategyId { get; }

        IUIOperation<UILoadResult> Load(
            UIPageDefinition definition,
            IUIAssetProvider assetProvider,
            IUIOperationFactory operationFactory,
            CancellationToken cancellationToken);
    }
}
