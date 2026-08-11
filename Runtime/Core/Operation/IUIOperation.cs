using System;
using System.Threading;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Backend-neutral operation handle consumed by AppUI callers.
    /// </summary>
    public interface IUIOperation<TResult>
    {
        AppUIOperationStatus Status { get; }

        bool IsTerminal { get; }

        CancellationToken CancellationToken { get; }

        bool RequestCancellation();

        IDisposable Register(
            Action<AppUIOperationCompletion<TResult>> continuation);

        bool TryGetCompletion(
            out AppUIOperationCompletion<TResult> completion);
    }
}
