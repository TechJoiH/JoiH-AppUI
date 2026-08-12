using System;

namespace Joi.H.AppUI
{
    public enum AppUIInitializationStatus
    {
        Success = 0,
        AlreadyInitialized = 1,
        MissingDependencies = 2,
        MissingOperationFactory = 3,
        MissingAssetProvider = 4,
        MissingExecutionContext = 5,
        MissingManager = 6,
        MissingRegistry = 7,
        InvalidLayerConfiguration = 8,
        AlreadyInitializedWithDifferentDependencies = 9,
        DependencyContractFailed = 10,
    }

    /// <summary>
    /// Structured result returned by explicit AppUI runtime initialization.
    /// </summary>
    public readonly struct AppUIInitializationResult
    {
        private AppUIInitializationResult(
            AppUIInitializationStatus status,
            Exception exception)
        {
            Status = status;
            Exception = exception;
        }

        public AppUIInitializationStatus Status { get; }

        public Exception Exception { get; }

        public bool Success
        {
            get
            {
                return Status == AppUIInitializationStatus.Success ||
                       Status == AppUIInitializationStatus.AlreadyInitialized;
            }
        }

        public static AppUIInitializationResult Ok()
        {
            return new AppUIInitializationResult(
                AppUIInitializationStatus.Success,
                null);
        }

        public static AppUIInitializationResult AlreadyInitialized()
        {
            return new AppUIInitializationResult(
                AppUIInitializationStatus.AlreadyInitialized,
                null);
        }

        public static AppUIInitializationResult Failure(
            AppUIInitializationStatus status,
            Exception exception = null)
        {
            if (status == AppUIInitializationStatus.Success ||
                status == AppUIInitializationStatus.AlreadyInitialized)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "A successful status cannot create a failure result.");
            }

            return new AppUIInitializationResult(status, exception);
        }
    }
}
