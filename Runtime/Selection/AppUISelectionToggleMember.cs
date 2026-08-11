using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    internal interface IAppUISelectionToggleAuthority
    {
        void HandleToggleFocused(Toggle toggle);

        void HandleToggleConfirmed(
            Toggle toggle,
            AppUISelectionConfirmCause cause);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Toggle))]
    public sealed class AppUISelectionToggleMember :
        MonoBehaviour,
        ISelectHandler,
        IPointerEnterHandler,
        IPointerMoveHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        ISubmitHandler,
        IMoveHandler
    {
        private IAppUISelectionToggleAuthority authority;
        private AppUIFocusGroupNode focusGroupNode;
        private Toggle toggle;

        internal void Configure(
            IAppUISelectionToggleAuthority selectionAuthority,
            Toggle targetToggle)
        {
            authority = selectionAuthority;
            toggle = targetToggle != null ? targetToggle : GetComponent<Toggle>();
            focusGroupNode = GetComponent<AppUIFocusGroupNode>();
        }

        internal void Unconfigure(IAppUISelectionToggleAuthority selectionAuthority)
        {
            if (authority != selectionAuthority)
            {
                return;
            }

            authority = null;
            AppUIInteractionSourceAuthority.Release(this);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SelectCurrentToggle();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            TrySelectFromPointer(HasPointerMoved(eventData));
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (eventData == null || eventData.delta.sqrMagnitude <= 0f)
            {
                return;
            }

            TrySelectFromPointer(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AppUIInteractionSourceAuthority.NotifyPointerExit(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null ||
                eventData.button != PointerEventData.InputButton.Left ||
                authority == null ||
                !IsUsable() ||
                !AppUIInteractionSourceAuthority.TryAcquirePointerForClick(this))
            {
                return;
            }

            AppUIInteractionSourceAuthority.SuppressPointerUntilExit(this);
            try
            {
                authority.HandleToggleConfirmed(
                    toggle,
                    AppUISelectionConfirmCause.PointerClick);
            }
            finally
            {
                AppUIInteractionSourceAuthority.CompletePointerConfirmation();
                eventData.Use();
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (authority == null || !IsUsable())
            {
                return;
            }

            AppUIInteractionSourceAuthority.NotifyNavigation();
            try
            {
                authority.HandleToggleConfirmed(
                    toggle,
                    AppUISelectionConfirmCause.Submit);
            }
            finally
            {
                AppUIInteractionSourceAuthority.NotifyNavigation();
                eventData?.Use();
            }
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData == null || !IsUsable())
            {
                return;
            }

            if (focusGroupNode == null)
            {
                focusGroupNode = GetComponent<AppUIFocusGroupNode>();
            }

            focusGroupNode?.HandleMoveFromToggleMember(eventData);
        }

        private void OnDisable()
        {
            AppUIInteractionSourceAuthority.Release(this);
        }

        private void TrySelectFromPointer(bool pointerMoved)
        {
            if (authority == null ||
                !IsUsable() ||
                !AppUIInteractionSourceAuthority.TryAcquirePointer(
                    this,
                    pointerMoved))
            {
                return;
            }

            FocusAndSelectCurrentToggle();
        }

        private void FocusAndSelectCurrentToggle()
        {
            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject != toggle.gameObject)
            {
                if (focusGroupNode == null)
                {
                    focusGroupNode = GetComponent<AppUIFocusGroupNode>();
                }

                if (focusGroupNode != null &&
                    focusGroupNode.TryCommitPointerFocus(
                        AppUIFocusChangeReason.PointerHover))
                {
                    return;
                }

                AppUIFocusRequestResult result =
                    UIFocusCommitter.CommitLegacySelection(
                        toggle,
                        AppUIInteractionSourceKind.Pointer);
                if (result == AppUIFocusRequestResult.Focused ||
                    result == AppUIFocusRequestResult.Deferred)
                {
                    return;
                }
            }

            SelectCurrentToggle();
        }

        private void SelectCurrentToggle()
        {
            if (authority != null && IsUsable())
            {
                authority.HandleToggleFocused(toggle);
            }
        }

        private bool IsUsable()
        {
            if (toggle == null)
            {
                toggle = GetComponent<Toggle>();
            }

            return toggle != null &&
                   toggle.IsActive() &&
                   toggle.IsInteractable() &&
                   toggle.gameObject.activeInHierarchy;
        }

        private static bool HasPointerMoved(PointerEventData eventData)
        {
            return eventData != null && eventData.delta.sqrMagnitude > 0f;
        }
    }
}
