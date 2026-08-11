using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    [DisallowMultipleComponent]
    public sealed class AppUISelectionVisualState : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField]
        private Graphic selectionFrame;

        [SerializeField]
        private AppUISelectionVisualMode mode = AppUISelectionVisualMode.FocusOrChoice;

        private bool focusSelected;
        private bool choiceSelected;

        public void SetSelectionFrame(Graphic frame)
        {
            selectionFrame = frame;
            ConfigureFrame();
            RefreshVisual();
        }

        public void SetMode(AppUISelectionVisualMode visualMode)
        {
            mode = visualMode;
            RefreshVisual();
        }

        public void SetChoiceSelected(bool selected)
        {
            if (choiceSelected == selected)
            {
                RefreshVisual();
                return;
            }

            choiceSelected = selected;
            RefreshVisual();
        }

        public void OnSelect(BaseEventData eventData)
        {
            focusSelected = true;
            RefreshVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
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
            focusSelected = false;
            SetFrameActive(false);
        }

        private void ConfigureFrame()
        {
            if (selectionFrame != null)
            {
                selectionFrame.raycastTarget = false;
            }
        }

        private void RefreshVisual()
        {
            bool visible = false;
            switch (mode)
            {
                case AppUISelectionVisualMode.FocusOnly:
                    visible = focusSelected;
                    break;
                case AppUISelectionVisualMode.ChoiceOnly:
                    visible = choiceSelected;
                    break;
                case AppUISelectionVisualMode.FocusOrChoice:
                    visible = focusSelected || choiceSelected;
                    break;
            }

            SetFrameActive(visible);
        }

        private void SetFrameActive(bool active)
        {
            if (selectionFrame != null && selectionFrame.gameObject.activeSelf != active)
            {
                selectionFrame.gameObject.SetActive(active);
            }
        }
    }
}
