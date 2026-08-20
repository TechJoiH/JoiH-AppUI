using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Observes UGUI Dropdown list visibility and synchronizes its child Focus
    /// Region. Dispose always removes the value-change listener.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppUIFocusDropdownRegionBridge :
        MonoBehaviour,
        ISubmitHandler,
        IPointerClickHandler,
        IDisposable
    {
        private IAppUIFocusScopeHandle scope;
        private AppUIFocusDropdownControlPolicy policy;
        private Dropdown dropdown;
        private Transform dropdownList;
        private bool pendingOpenObservation;
        private bool previousExpanded;
        private bool disposed;

        internal void Initialize(
            IAppUIFocusScopeHandle focusScope,
            AppUIFocusDropdownControlPolicy controlPolicy)
        {
            if (focusScope == null)
            {
                throw new ArgumentNullException(nameof(focusScope));
            }

            if (controlPolicy == null)
            {
                throw new ArgumentNullException(nameof(controlPolicy));
            }

            Unsubscribe();
            scope = focusScope;
            policy = controlPolicy;
            dropdown = controlPolicy.UGUIDropdown;
            disposed = false;
            enabled = true;
            pendingOpenObservation = false;
            dropdownList = null;
            previousExpanded = policy.IsExpanded;
            Subscribe();
            if (previousExpanded)
            {
                Synchronize(true);
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            pendingOpenObservation = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            pendingOpenObservation = true;
        }

        private void LateUpdate()
        {
            if (disposed || scope == null || policy == null)
            {
                return;
            }

            bool expanded = ResolveExpandedState();
            if (expanded == previousExpanded)
            {
                pendingOpenObservation = false;
                return;
            }

            previousExpanded = expanded;
            pendingOpenObservation = false;
            Synchronize(expanded);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Unsubscribe();
            scope = null;
            policy = null;
            dropdown = null;
            dropdownList = null;
            pendingOpenObservation = false;
            enabled = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private bool ResolveExpandedState()
        {
            if (dropdown == null)
            {
                return false;
            }

            if (dropdownList != null)
            {
                return dropdownList.gameObject.activeInHierarchy;
            }

            if (!pendingOpenObservation || dropdown.template == null)
            {
                return false;
            }

            Transform parent = dropdown.template.parent;
            if (parent == null)
            {
                return false;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null &&
                    !ReferenceEquals(child, dropdown.template) &&
                    child.gameObject.activeInHierarchy &&
                    child.name.StartsWith(
                        "Dropdown List",
                        StringComparison.Ordinal))
                {
                    dropdownList = child;
                    return true;
                }
            }

            return false;
        }

        private void HandleValueChanged(int _)
        {
            previousExpanded = false;
            pendingOpenObservation = false;
            dropdownList = null;
            Synchronize(false);
        }

        private void Synchronize(bool expanded)
        {
            if (scope != null && policy != null)
            {
                policy.SynchronizeRegion(scope, expanded);
            }
        }

        private void Subscribe()
        {
            dropdown?.onValueChanged.AddListener(HandleValueChanged);
        }

        private void Unsubscribe()
        {
            dropdown?.onValueChanged.RemoveListener(HandleValueChanged);
        }
    }
}
