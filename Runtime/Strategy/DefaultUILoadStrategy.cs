using System;
using System.Threading;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Maps the project-owned provider contract into a page load result.
    /// </summary>
    public sealed class DefaultUILoadStrategy : IUILoadStrategy
    {
        public string StrategyId
        {
            get { return string.Empty; }
        }

        public IUIOperation<UILoadResult> Load(
            UIPageDefinition definition,
            IUIAssetProvider assetProvider,
            IUIOperationFactory operationFactory,
            CancellationToken cancellationToken)
        {
            IUIOperationSource<UILoadResult> source = CreateSource(
                operationFactory,
                cancellationToken);

            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.PrefabAssetId))
            {
                source.TrySetSucceeded(new UILoadResult(
                    false,
                    null,
                    null,
                    "UI definition or PrefabAssetId is invalid."));
                return source.Operation;
            }

            if (assetProvider == null)
            {
                source.TrySetSucceeded(new UILoadResult(
                    false,
                    null,
                    null,
                    "IUIAssetProvider is missing."));
                return source.Operation;
            }

            try
            {
                assetProvider.TryLoad(
                    definition.PrefabAssetId,
                    out UIAssetLoadResult<GameObject> immediateResult);
                if (immediateResult.Status !=
                    UIAssetLoadStatus.SynchronousLoadUnsupported)
                {
                    source.TrySetSucceeded(Map(immediateResult));
                    return source.Operation;
                }

                IUIOperation<UIAssetLoadResult<GameObject>> loadOperation =
                    assetProvider.Load<GameObject>(
                        definition.PrefabAssetId,
                        cancellationToken);
                if (loadOperation == null)
                {
                    throw new InvalidOperationException(
                        "IUIAssetProvider.Load returned null. AssetId=" +
                        definition.PrefabAssetId);
                }

                loadOperation.Register(completion =>
                    CompleteSource(source, completion));
            }
            catch (Exception exception)
            {
                source.TrySetFailed(exception);
            }

            return source.Operation;
        }

        private static IUIOperationSource<UILoadResult> CreateSource(
            IUIOperationFactory operationFactory,
            CancellationToken cancellationToken)
        {
            if (operationFactory == null)
            {
                throw new ArgumentNullException(nameof(operationFactory));
            }

            IUIOperationSource<UILoadResult> source =
                operationFactory.Create<UILoadResult>(
                    AppUIOperationDescriptor.Create(
                        "LoadPagePrefab",
                        cancellationToken));
            if (source == null || source.Operation == null)
            {
                throw new InvalidOperationException(
                    "IUIOperationFactory returned a null source or operation.");
            }

            source.TrySetRunning();
            return source;
        }

        private static UILoadResult Map(
            UIAssetLoadResult<GameObject> result)
        {
            return new UILoadResult(
                result.IsSuccess,
                result.Asset,
                result.Lease,
                result.ErrorMessage);
        }

        private static void CompleteSource(
            IUIOperationSource<UILoadResult> source,
            AppUIOperationCompletion<UIAssetLoadResult<GameObject>> completion)
        {
            switch (completion.Status)
            {
                case AppUIOperationStatus.Succeeded:
                    source.TrySetSucceeded(Map(completion.Result));
                    break;
                case AppUIOperationStatus.Cancelled:
                    source.TrySetCancelled();
                    break;
                case AppUIOperationStatus.Expired:
                    source.TrySetExpired();
                    break;
                case AppUIOperationStatus.Failed:
                    source.TrySetFailed(
                        completion.Exception ??
                        new InvalidOperationException(
                            "Failed asset operation has no exception."));
                    break;
                default:
                    source.TrySetFailed(new InvalidOperationException(
                        "Asset provider published a non-terminal completion."));
                    break;
            }
        }
    }
}
