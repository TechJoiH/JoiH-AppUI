namespace Joi.H.AppUI
{
    public static class AppUIInteractionSourceAuthority
    {
        private static AppUISelectionToggleMember pointerSuppressedMember;
        private static bool pointerMovementRequired;

        public static AppUIInteractionSourceKind CurrentSource { get; private set; }

        public static void NotifyNavigation()
        {
            CurrentSource = AppUIInteractionSourceKind.Navigation;
            pointerMovementRequired = true;
        }

        public static void NotifyProgrammatic()
        {
            CurrentSource = AppUIInteractionSourceKind.Programmatic;
            pointerMovementRequired = true;
        }

        internal static bool TryAcquirePointer(
            AppUISelectionToggleMember member,
            bool pointerMoved)
        {
            if (member == null || pointerSuppressedMember == member)
            {
                return false;
            }

            if (pointerSuppressedMember != null)
            {
                return false;
            }

            if (pointerMovementRequired && !pointerMoved)
            {
                return false;
            }

            CurrentSource = AppUIInteractionSourceKind.Pointer;
            pointerMovementRequired = false;
            return true;
        }

        internal static bool TryAcquirePointerForClick(
            AppUISelectionToggleMember member)
        {
            if (pointerSuppressedMember == member)
            {
                return false;
            }

            pointerSuppressedMember = null;
            CurrentSource = AppUIInteractionSourceKind.Pointer;
            pointerMovementRequired = false;
            return true;
        }

        internal static void SuppressPointerUntilExit(AppUISelectionToggleMember member)
        {
            pointerSuppressedMember = member;
        }

        internal static void CompletePointerConfirmation()
        {
            CurrentSource = AppUIInteractionSourceKind.Pointer;
            pointerMovementRequired = false;
        }

        internal static void NotifyPointerExit(AppUISelectionToggleMember member)
        {
            if (pointerSuppressedMember == member)
            {
                pointerSuppressedMember = null;
            }
        }

        internal static void Release(AppUISelectionToggleMember member)
        {
            NotifyPointerExit(member);
        }

        public static void Reset()
        {
            pointerSuppressedMember = null;
            pointerMovementRequired = false;
            CurrentSource = AppUIInteractionSourceKind.Programmatic;
        }
    }
}
