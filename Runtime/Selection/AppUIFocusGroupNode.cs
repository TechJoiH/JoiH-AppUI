using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    [DisallowMultipleComponent]
    public sealed class AppUIFocusGroupNode :
        MonoBehaviour,
        IMoveHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private AppUIFocusGroupNavigator navigator;
        private Selectable selectable;
        private AppUISelectionToggleMember toggleMember;
        private IAppUIFocusControlPolicy controlPolicy;
        private string groupId = string.Empty;

        public void Initialize(
            AppUIFocusGroupNavigator focusNavigator,
            string focusGroupId,
            Selectable focusSelectable,
            IAppUIFocusControlPolicy focusControlPolicy = null)
        {
            Selectable resolvedSelectable = focusSelectable != null
                ? focusSelectable
                : GetComponent<Selectable>();
            InitializeResolved(
                focusNavigator,
                focusGroupId,
                resolvedSelectable,
                AppUIFocusControlPolicies.Resolve(
                    resolvedSelectable,
                    focusControlPolicy));
        }

        internal void InitializeResolved(
            AppUIFocusGroupNavigator focusNavigator,
            string focusGroupId,
            Selectable focusSelectable,
            IAppUIFocusControlPolicy resolvedControlPolicy)
        {
            navigator = focusNavigator;
            groupId = focusGroupId ?? string.Empty;
            selectable = focusSelectable;
            toggleMember = GetComponent<AppUISelectionToggleMember>();
            controlPolicy = resolvedControlPolicy;
        }

        internal void Detach(
            AppUIFocusGroupNavigator focusNavigator,
            Selectable focusSelectable)
        {
            if (!ReferenceEquals(navigator, focusNavigator) ||
                !ReferenceEquals(selectable, focusSelectable))
            {
                return;
            }

            navigator = null;
            selectable = null;
            toggleMember = null;
            controlPolicy = null;
            groupId = string.Empty;
        }

        public void OnMove(AxisEventData eventData)
        {
            if (toggleMember == null)
            {
                toggleMember = GetComponent<AppUISelectionToggleMember>();
            }

            if (toggleMember != null)
            {
                return;
            }

            HandleMove(eventData);
        }

        internal void HandleMoveFromToggleMember(AxisEventData eventData)
        {
            HandleMove(eventData);
        }

        private void HandleMove(AxisEventData eventData)
        {
            if (eventData == null ||
                navigator == null ||
                selectable == null)
            {
                return;
            }

            if (navigator.HandleMoveInput(
                    groupId,
                    selectable,
                    eventData,
                    controlPolicy))
            {
                eventData.Use();
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (navigator != null && selectable != null)
            {
                navigator.NotifySelectionObserved(groupId, selectable);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (navigator != null && selectable != null)
            {
                navigator.NotifySelectionDeselected(selectable);
            }
        }

        internal bool TryCommitPointerFocus(AppUIFocusChangeReason reason)
        {
            return navigator != null &&
                   selectable != null &&
                   navigator.FocusSelectable(selectable, reason);
        }
    }
}
