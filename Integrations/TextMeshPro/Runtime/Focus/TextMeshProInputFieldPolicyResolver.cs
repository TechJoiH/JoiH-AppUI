using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI.Integrations.TextMeshPro
{
    public sealed class TextMeshProInputFieldPolicyResolver :
        IAppUIFocusControlPolicyResolver
    {
        public const string Id = "joih.appui.tmp.input-field";
        private static readonly IAppUIFocusControlPolicy Policy = new InputPolicy();

        public string ResolverId => Id;

        public bool TryResolve(Selectable selectable, out IAppUIFocusControlPolicy policy)
        {
            if (selectable is TMP_InputField)
            {
                policy = Policy;
                return true;
            }

            policy = null;
            return false;
        }

        private sealed class InputPolicy : IAppUIFocusControlPolicy
        {
            public AppUIFocusControlMoveMode GetMoveMode(in AppUIFocusMoveContext context)
            {
                return AppUIFocusControlMoveMode.FrameworkOnly;
            }

            public AppUIFocusCancelHandlingResult TryHandleCancel(in AppUIFocusCancelContext context)
            {
                if (context.CurrentSelectable is TMP_InputField inputField && inputField.isFocused)
                {
                    inputField.DeactivateInputField();
                    return AppUIFocusCancelHandlingResult.Consumed;
                }

                return AppUIFocusCancelHandlingResult.Continue;
            }
        }
    }
}
