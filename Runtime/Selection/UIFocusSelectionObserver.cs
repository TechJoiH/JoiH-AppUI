using UnityEngine;
using UnityEngine.EventSystems;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 全局 Selection 观察桥。Node 回调只转发变化，LateUpdate 统一采样最终 EventSystem 状态；
    /// 资格判断、History 和 Repair 均由 Committer / UIFocusService 处理。
    /// </summary>
    internal sealed class UIFocusSelectionObserver : IAppUIFocusSelectionObservationSink
    {
        private readonly UIFocusService focusService;
        private readonly IUIFocusCommitter committer;

        private int lastSelectedObjectId = int.MinValue;
        private int lastStackRevision = -1;
        private int lastScopeRevision = -1;
        private bool selectionDirty = true;

        public UIFocusSelectionObserver(
            UIFocusService owner,
            IUIFocusCommitter focusCommitter)
        {
            focusService = owner;
            committer = focusCommitter;
        }

        public void NotifySelected(GameObject selectedObject)
        {
            AppUIFocusSelectionObservation observation =
                new AppUIFocusSelectionObservation(
                    selectedObject,
                    AppUIFocusSelectionObservationSource.SelectCallback);
            committer.ObserveSelection(in observation);
            CaptureState(selectedObject);
            selectionDirty = false;
        }

        public void NotifyDeselected(GameObject deselectedObject)
        {
            AppUIFocusSelectionObservation observation =
                new AppUIFocusSelectionObservation(
                    deselectedObject,
                    AppUIFocusSelectionObservationSource.DeselectCallback);
            committer.ObserveSelection(in observation);
            selectionDirty = true;
        }

        public void Reconcile()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selectedObject = eventSystem != null
                ? eventSystem.currentSelectedGameObject
                : null;
            int selectedObjectId = selectedObject != null
                ? selectedObject.GetInstanceID()
                : 0;
            int stackRevision = focusService.CurrentInteractionSnapshot != null
                ? focusService.CurrentInteractionSnapshot.StackRevision
                : 0;
            int scopeRevision = focusService.ActiveScopeRevision;
            if (!selectionDirty &&
                selectedObjectId == lastSelectedObjectId &&
                stackRevision == lastStackRevision &&
                scopeRevision == lastScopeRevision)
            {
                return;
            }

            AppUIFocusSelectionObservation observation =
                new AppUIFocusSelectionObservation(
                    selectedObject,
                    AppUIFocusSelectionObservationSource.LateUpdate);
            committer.ObserveSelection(in observation);
            CaptureState(selectedObject);
            selectionDirty = false;
        }

        public void MarkDirty()
        {
            selectionDirty = true;
        }

        public void Reset()
        {
            lastSelectedObjectId = int.MinValue;
            lastStackRevision = -1;
            lastScopeRevision = -1;
            selectionDirty = true;
        }

        private void CaptureState(GameObject selectedObject)
        {
            lastSelectedObjectId = selectedObject != null
                ? selectedObject.GetInstanceID()
                : 0;
            lastStackRevision = focusService.CurrentInteractionSnapshot != null
                ? focusService.CurrentInteractionSnapshot.StackRevision
                : 0;
            lastScopeRevision = focusService.ActiveScopeRevision;
        }
    }
}
