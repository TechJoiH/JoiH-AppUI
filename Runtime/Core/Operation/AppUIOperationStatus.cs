namespace Joi.H.AppUI
{
    /// <summary>
    /// Scheduling state of a host-provided AppUI operation.
    /// Domain success or failure remains part of the typed result.
    /// </summary>
    public enum AppUIOperationStatus
    {
        Created = 0,
        Running = 1,
        Cancelling = 2,
        Succeeded = 3,
        Cancelled = 4,
        Failed = 5,
        Expired = 6,
    }
}
