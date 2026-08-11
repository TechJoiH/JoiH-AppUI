namespace Joi.H.AppUI
{
    public enum AppUISelectionInteractionPolicy
    {
        ClickToSelect = 0,
        MoveSelectAndConfirm = 1,
        /// <summary>
        /// Focus movement is visual only. Pointer click or Submit changes the
        /// selected value, matching legacy Toggle navigation.
        /// </summary>
        ConfirmToSelect = 2,
    }

    public enum AppUISelectionConfirmCause
    {
        PointerClick = 0,
        Submit = 1,
    }

    public enum AppUIInteractionSourceKind
    {
        Programmatic = 0,
        Pointer = 1,
        Navigation = 2,
    }
}
