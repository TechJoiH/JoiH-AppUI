using System;
using TMPro;

namespace Joi.H.AppUI.Integrations.TextMeshPro
{
    public sealed class TextMeshProFocusDropdownControlPolicy :
        AppUIFocusDropdownControlPolicyBase
    {
        private readonly TMP_Dropdown dropdown;

        public TextMeshProFocusDropdownControlPolicy(
            TMP_Dropdown focusDropdown,
            string childRegionId)
            : base(childRegionId)
        {
            dropdown = focusDropdown != null
                ? focusDropdown
                : throw new ArgumentNullException(nameof(focusDropdown));
        }

        public TMP_Dropdown Dropdown => dropdown;
        public bool IsExpanded => dropdown.IsExpanded;

        protected override bool IsControlExpanded => dropdown.IsExpanded;

        public IDisposable Bind(IAppUIFocusScopeHandle scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            TextMeshProDropdownRegionBridge bridge =
                dropdown.GetComponent<TextMeshProDropdownRegionBridge>();
            if (bridge == null)
            {
                bridge = dropdown.gameObject.AddComponent<TextMeshProDropdownRegionBridge>();
            }

            bridge.Initialize(scope, this);
            return bridge;
        }

        public void Collapse()
        {
            CollapseControl();
        }

        protected override void CollapseControl()
        {
            dropdown.Hide();
        }
    }
}
