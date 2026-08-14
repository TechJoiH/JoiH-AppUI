namespace Joi.H.AppUI
{
    /// <summary>
    /// Project-owned runtime capabilities required by AppUI.
    /// All three dependencies are mandatory; AppUI provides no fallback.
    /// Optional strategies belong to AppUIRuntimeConfiguration instead.
    /// </summary>
    public sealed class AppUIRuntimeDependencies
    {
        public AppUIRuntimeDependencies(
            IUIOperationFactory operationFactory,
            IUIAssetProvider assetProvider,
            IAppUIExecutionContext executionContext)
        {
            OperationFactory = operationFactory;
            AssetProvider = assetProvider;
            ExecutionContext = executionContext;
        }

        public IUIOperationFactory OperationFactory { get; }

        public IUIAssetProvider AssetProvider { get; }

        public IAppUIExecutionContext ExecutionContext { get; }
    }
}
