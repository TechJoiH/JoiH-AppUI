using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    [DisallowMultipleComponent]
    public sealed class AppUISelectableFocusBridge : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField]
        private Selectable selectable;

        private AppUIFocusGroupNode focusGroupNode;

        public void SetSelectable(Selectable target)
        {
            selectable = target;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Selectable target = ResolveSelectable();
            if (target == null ||
                !target.IsActive() ||
                !target.IsInteractable() ||
                EventSystem.current == null ||
                EventSystem.current.currentSelectedGameObject == target.gameObject)
            {
                return;
            }

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

            UIFocusCommitter.CommitLegacySelection(
                target,
                AppUIInteractionSourceKind.Pointer);
        }

        private void Awake()
        {
            ResolveSelectable();
        }

        private void OnValidate()
        {
            ResolveSelectable();
        }

        private Selectable ResolveSelectable()
        {
            if (selectable == null)
            {
                selectable = GetComponent<Selectable>();
            }

            return selectable;
        }
    }
}
