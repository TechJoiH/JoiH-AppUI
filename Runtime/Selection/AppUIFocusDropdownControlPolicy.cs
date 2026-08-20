using System;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// UGUI Dropdown adapter for the technology-neutral child Region policy.
    /// </summary>
    public sealed class AppUIFocusDropdownControlPolicy :
        AppUIFocusDropdownControlPolicyBase
    {
        private readonly Dropdown dropdown;
        private bool expanded;

        public AppUIFocusDropdownControlPolicy(
            Dropdown focusDropdown,
            string focusChildRegionId)
            : base(focusChildRegionId)
        {
            dropdown = focusDropdown != null
                ? focusDropdown
                : throw new ArgumentNullException(nameof(focusDropdown));
        }

        public bool IsExpanded
        {
            get { return expanded; }
        }

        protected override bool IsControlExpanded
        {
            get { return expanded; }
        }

        internal Dropdown UGUIDropdown
        {
            get { return dropdown; }
        }

        public IDisposable Bind(IAppUIFocusScopeHandle scope)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            AppUIFocusDropdownRegionBridge bridge =
                dropdown.GetComponent<AppUIFocusDropdownRegionBridge>();
            if (bridge == null)
            {
                bridge = dropdown.gameObject
                    .AddComponent<AppUIFocusDropdownRegionBridge>();
            }

            bridge.Initialize(scope, this);
            return bridge;
        }

        /// <summary>
        /// UGUI Dropdown has no public expanded-state property, so its bridge
        /// explicitly publishes the observed state through this overload.
        /// </summary>
        public AppUIFocusRequestResult SynchronizeRegion(
            IAppUIFocusScopeHandle scope,
            bool isExpanded)
        {
            expanded = isExpanded;
            return SynchronizeRegion(scope);
        }

        public void Collapse()
        {
            CollapseControl();
        }

        protected override void CollapseControl()
        {
            dropdown.Hide();
            expanded = false;
        }
    }
}
