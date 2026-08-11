using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    [DisallowMultipleComponent]
    public sealed class AppUIHoverFrameVisualState : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField]
        private Graphic hoverFrame;

        private bool hovered;
        private bool focusSelected;

        public void SetHoverFrame(Graphic frame)
        {
            hoverFrame = frame;
            ConfigureFrame();
            RefreshVisual();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            RefreshVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            RefreshVisual();
        }

        public void OnSelect(BaseEventData eventData)
        {
            focusSelected = true;
            RefreshVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            hovered = false;
            focusSelected = false;
            RefreshVisual();
        }

        private void Awake()
        {
            ConfigureFrame();
            RefreshVisual();
        }

        private void OnEnable()
        {
            ConfigureFrame();
            RefreshVisual();
        }

        private void OnDisable()
        {
            hovered = false;
            focusSelected = false;
            SetFrameActive(false);
        }

        private void ConfigureFrame()
        {
            if (hoverFrame != null)
            {
                hoverFrame.raycastTarget = false;
            }
        }

        private void RefreshVisual()
        {
            SetFrameActive(hovered || focusSelected);
        }

        private void SetFrameActive(bool active)
        {
            if (hoverFrame != null && hoverFrame.gameObject.activeSelf != active)
            {
                hoverFrame.gameObject.SetActive(active);
            }
        }
    }
}
