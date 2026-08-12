using System;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Routes host operation completions to the injected Unity execution context.
    /// </summary>
    internal static class UIOperationObserver
    {
        public static IDisposable Observe<TResult>(
            IUIOperation<TResult> operation,
            IAppUIExecutionContext executionContext,
            Action<AppUIOperationCompletion<TResult>> continuation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }

            return operation.Register(completion =>
            {
                if (executionContext.IsCurrent)
                {
                    continuation.Invoke(completion);
                    return;
                }

                executionContext.Post(
                    () => continuation.Invoke(completion));
            });
        }
    }
}
