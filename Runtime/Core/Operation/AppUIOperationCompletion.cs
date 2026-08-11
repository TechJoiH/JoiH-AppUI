using System;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Immutable terminal value published by an AppUI operation.
    /// </summary>
    public readonly struct AppUIOperationCompletion<TResult>
    {
        private AppUIOperationCompletion(
            AppUIOperationStatus status,
            TResult result,
            Exception exception)
        {
            Status = status;
            Result = result;
            Exception = exception;
        }

        public AppUIOperationStatus Status { get; }

        public TResult Result { get; }

        public Exception Exception { get; }

        public static AppUIOperationCompletion<TResult> Succeeded(
            TResult result)
        {
            return new AppUIOperationCompletion<TResult>(
                AppUIOperationStatus.Succeeded,
                result,
                null);
        }

        public static AppUIOperationCompletion<TResult> Cancelled()
        {
            return new AppUIOperationCompletion<TResult>(
                AppUIOperationStatus.Cancelled,
                default,
                null);
        }

        public static AppUIOperationCompletion<TResult> Failed(
            Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return new AppUIOperationCompletion<TResult>(
                AppUIOperationStatus.Failed,
                default,
                exception);
        }

        public static AppUIOperationCompletion<TResult> Expired()
        {
            return new AppUIOperationCompletion<TResult>(
                AppUIOperationStatus.Expired,
                default,
                null);
        }
    }
}
