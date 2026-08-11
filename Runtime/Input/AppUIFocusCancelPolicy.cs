using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面向统一 Cancel 管线提供最终关闭按钮策略；页面业务 IUICancelHandler
    /// 不得再反向调用该策略。
    /// </summary>
    public interface IAppUIFocusCancelPolicyProvider
    {
        AppUIFocusCancelPolicy GetFocusCancelPolicy();
    }

    public sealed class AppUIFocusCancelPolicy
    {
        private readonly AppUIFocusGroupNavigator navigator;
        private readonly IAppUIFocusScopeHandle focusScope;
        private readonly AppUIFocusNodeAddress closeNodeAddress;
        private readonly string closeGroupId;
        private readonly Button closeButton;
        private readonly Action closeFallback;
        private int lastHandledCancelFrame = -1;

        public AppUIFocusCancelPolicy(
            AppUIFocusGroupNavigator focusNavigator,
            string focusCloseGroupId,
            Button focusCloseButton,
            Action fallbackCloseAction)
        {
            navigator = focusNavigator;
            closeGroupId = focusCloseGroupId ?? string.Empty;
            closeButton = focusCloseButton;
            closeFallback = fallbackCloseAction;
        }

        public AppUIFocusCancelPolicy(
            IAppUIFocusScopeHandle scope,
            AppUIFocusNodeAddress focusCloseNodeAddress,
            Button focusCloseButton,
            Action fallbackCloseAction)
        {
            focusScope = scope;
            closeNodeAddress = focusCloseNodeAddress;
            closeGroupId = focusCloseNodeAddress.GroupId;
            closeButton = focusCloseButton;
            closeFallback = fallbackCloseAction;
        }

        public bool HandleCancel()
        {
            int frameCount = Time.frameCount;
            if (lastHandledCancelFrame == frameCount)
            {
                return true;
            }

            if (!HandleCancelCore())
            {
                return false;
            }

            lastHandledCancelFrame = frameCount;
            return true;
        }

        private bool HandleCancelCore()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || !IsUsable(closeButton))
            {
                return false;
            }

            GameObject closeObject = closeButton.gameObject;
            if (eventSystem.currentSelectedGameObject != closeObject)
            {
                if (focusScope != null)
                {
                    AppUIFocusRequestResult result = focusScope.FocusNode(
                        closeNodeAddress,
                        AppUIFocusChangeReason.CancelPreview);
                    return result == AppUIFocusRequestResult.Focused ||
                           result == AppUIFocusRequestResult.Consumed ||
                           result == AppUIFocusRequestResult.Deferred ||
                           result == AppUIFocusRequestResult.DeferredWhileSuspended;
                }

                if (navigator == null ||
                    !navigator.FocusNode(
                        closeGroupId,
                        closeButton,
                        AppUIFocusChangeReason.CancelPreview))
                {
                    UIFocusCommitter.CommitLegacySelection(
                        closeButton,
                        AppUIInteractionSourceKind.Programmatic);
                }

                return true;
            }

            if (!ExecuteEvents.Execute(
                    closeObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.submitHandler))
            {
                if (closeFallback != null)
                {
                    closeFallback.Invoke();
                }
            }

            return true;
        }

        private static bool IsUsable(Button button)
        {
            return button != null &&
                button.gameObject != null &&
                button.gameObject.activeInHierarchy &&
                button.IsActive() &&
                button.IsInteractable();
        }
    }
}
