using System;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Producer side used by AppUI Runtime to advance a host-provided operation.
    /// </summary>
    public interface IUIOperationSource<TResult>
    {
        IUIOperation<TResult> Operation { get; }

        bool TrySetRunning();

        bool TrySetSucceeded(TResult result);

        bool TrySetCancelled();

        bool TrySetFailed(Exception exception);

        bool TrySetExpired();
    }
}
