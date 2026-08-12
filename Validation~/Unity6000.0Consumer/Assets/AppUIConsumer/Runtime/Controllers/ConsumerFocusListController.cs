using UnityEngine.UI;

namespace Joi.H.AppUI.Validation.Consumer
{
    public partial class ConsumerFocusListController :
        PanelBaseController,
        IUIDefaultFocusProvider
    {
        private const string FocusGroupId = "consumer-list";
        private AppUIFocusGroupNavigator navigator;
        private Button firstButton;
        private Button secondButton;

        public Button FirstButton
        {
            get { return firstButton; }
        }

        public Button SecondButton
        {
            get { return secondButton; }
        }

        protected override void OnInitEx()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            firstButton = buttons.Length > 0 ? buttons[0] : null;
            secondButton = buttons.Length > 1 ? buttons[1] : null;
            navigator = new AppUIFocusGroupNavigator();
            navigator.RegisterNode(FocusGroupId, firstButton);
            navigator.RegisterNode(FocusGroupId, secondButton);
            navigator.OpenGroup(FocusGroupId);
        }

        protected override void OnDisposeEx()
        {
            navigator?.Dispose();
            navigator = null;
        }

        public bool TryGetDefaultFocus(
            UIDefaultFocusReason reason,
            out Selectable selectable)
        {
            selectable = firstButton;
            return selectable != null;
        }

        public bool MoveDown()
        {
            return navigator != null &&
                navigator.MoveWithinGroup(FocusGroupId, 1, false);
        }
    }
}
