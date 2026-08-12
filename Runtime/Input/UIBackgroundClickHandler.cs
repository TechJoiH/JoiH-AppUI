using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Joi.H.AppUI
{
    public sealed class UIBackgroundClickHandler :
        MonoBehaviour,
        IPointerClickHandler
    {
        [SerializeField]
        private string pageId;

        [SerializeField]
        private UIPageDefinition pageDefinition;

        private IUIService uiService;
        private IDisposable closeSubscription;

        public void Initialize(IUIService service, string targetPageId)
        {
            Initialize(service, targetPageId, null);
        }

        public void Initialize(
            IUIService service,
            string targetPageId,
            UIPageDefinition definition)
        {
            uiService = service;
            pageId = targetPageId ?? string.Empty;
            pageDefinition = definition;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            eventData?.Use();
            if (!CanCloseOnBackgroundClick())
            {
                return;
            }

            IUIOperation<UICloseResult> operation = uiService.Close(pageId);
            closeSubscription?.Dispose();
            closeSubscription = operation.Register(completion =>
            {
                if (completion.Status == AppUIOperationStatus.Failed)
                {
                    Debug.LogError(
                        completion.Exception ??
                        new InvalidOperationException(
                            "Background close failed without exception."));
                }
            });
        }

        private void OnDisable()
        {
            closeSubscription?.Dispose();
            closeSubscription = null;
        }

        private void OnDestroy()
        {
            closeSubscription?.Dispose();
            closeSubscription = null;
        }

        private bool CanCloseOnBackgroundClick()
        {
            return uiService != null &&
                   !string.IsNullOrEmpty(pageId) &&
                   pageDefinition != null &&
                   pageDefinition.CloseOnBackgroundClick &&
                   IsBackgroundClickLayer(pageDefinition.LayerId);
        }

        private static bool IsBackgroundClickLayer(UILayerId layerId)
        {
            return layerId == UILayerId.PopupLayer ||
                   layerId == UILayerId.ModalLayer;
        }
    }
}
