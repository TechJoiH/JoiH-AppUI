namespace Joi.H.AppUI
{
    /// <summary>
    /// Domain result returned by a project-owned page transition operation.
    /// </summary>
    public readonly struct UITransitionResult
    {
        private UITransitionResult(bool success, string errorMessage)
        {
            Success = success;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Success { get; }

        public string ErrorMessage { get; }

        public static UITransitionResult Ok()
        {
            return new UITransitionResult(true, string.Empty);
        }

        public static UITransitionResult Failed(string errorMessage)
        {
            return new UITransitionResult(false, errorMessage);
        }
    }
}
