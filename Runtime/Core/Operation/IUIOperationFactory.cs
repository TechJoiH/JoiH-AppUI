namespace Joi.H.AppUI
{
    /// <summary>
    /// Creates operation sources using the asynchronous model selected by the host.
    /// AppUI Core intentionally provides no implementation.
    /// </summary>
    public interface IUIOperationFactory
    {
        IUIOperationSource<TResult> Create<TResult>(
            AppUIOperationDescriptor descriptor);
    }
}
