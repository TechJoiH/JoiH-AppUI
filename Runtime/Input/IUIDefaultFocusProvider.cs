using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public enum UIDefaultFocusReason
    {
        PageOpened = 0,
        SelectionInvalid = 1,
        RestoreRequested = 2,
    }

    public interface IUIDefaultFocusProvider
    {
        bool TryGetDefaultFocus(UIDefaultFocusReason reason, out Selectable selectable);
    }

    internal sealed class UIDefaultFocusResolver
    {
        private readonly List<Selectable> selectables = new List<Selectable>(8);

        public Selectable Resolve(UIPageInstance page, UIDefaultFocusReason reason)
        {
            if (!CanOwnSelection(page))
            {
                return null;
            }

            Selectable providerSelectable = ResolveProviderOnly(page, reason);
            if (providerSelectable != null)
            {
                return providerSelectable;
            }

            return FindFirstSelectable(page);
        }

        public static bool CanOwnSelection(UIPageInstance page)
        {
            return page != null &&
                   page.GameObject != null &&
                   page.GameObject.activeInHierarchy &&
                   page.IsOpenAndStackVisible &&
                   !page.IsPaused &&
                   !page.IsInputBlocked;
        }

        public static bool IsSelectionOwnedBy(GameObject selected, UIPageInstance page)
        {
            return selected != null &&
                   page != null &&
                   page.GameObject != null &&
                   selected.transform.IsChildOf(page.GameObject.transform);
        }

        public static bool IsSelectableUsable(GameObject selected)
        {
            if (selected == null || !selected.activeInHierarchy)
            {
                return false;
            }

            Selectable selectable = selected.GetComponent<Selectable>();
            return IsSelectableUsable(selectable);
        }

        internal Selectable ResolveProviderOnly(
            UIPageInstance page,
            UIDefaultFocusReason reason)
        {
            IUIDefaultFocusProvider provider = page.Controller as IUIDefaultFocusProvider;
            if (provider == null)
            {
                return null;
            }

            bool hasDefaultFocus;
            Selectable selectable;
            try
            {
                hasDefaultFocus = provider.TryGetDefaultFocus(reason, out selectable);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                return null;
            }

            if (!hasDefaultFocus)
            {
                return null;
            }

            if (IsSelectableOwnedAndUsable(selectable, page))
            {
                return selectable;
            }

            LogInvalidProviderSelectable(page, reason, selectable);
            return null;
        }

        private Selectable FindFirstSelectable(UIPageInstance page)
        {
            selectables.Clear();
            if (!CanOwnSelection(page))
            {
                return null;
            }

            page.GameObject.GetComponentsInChildren<Selectable>(true, selectables);
            for (int i = 0; i < selectables.Count; i++)
            {
                Selectable selectable = selectables[i];
                if (IsSelectableUsable(selectable))
                {
                    selectables.Clear();
                    return selectable;
                }
            }

            selectables.Clear();
            return null;
        }

        private static bool IsSelectableOwnedAndUsable(Selectable selectable, UIPageInstance page)
        {
            return IsSelectableUsable(selectable) &&
                   page != null &&
                   page.GameObject != null &&
                   selectable.transform.IsChildOf(page.GameObject.transform);
        }

        private static bool IsSelectableUsable(Selectable selectable)
        {
            return selectable != null &&
                   selectable.gameObject != null &&
                   selectable.gameObject.activeInHierarchy &&
                   selectable.IsActive() &&
                   selectable.IsInteractable();
        }

        private static void LogInvalidProviderSelectable(
            UIPageInstance page,
            UIDefaultFocusReason reason,
            Selectable selectable)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string pageId = page != null ? page.PageId : string.Empty;
            string selectableName = selectable != null ? selectable.name : "null";
            Debug.LogWarning(
                "<Joi.H.AppUI> Default focus provider returned invalid selectable. PageId=" +
                pageId +
                ", Reason=" +
                reason +
                ", Selectable=" +
                selectableName);
#endif
        }
    }
}
