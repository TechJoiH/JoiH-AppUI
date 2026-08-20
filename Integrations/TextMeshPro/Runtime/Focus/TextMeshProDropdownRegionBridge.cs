using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Joi.H.AppUI.Integrations.TextMeshPro
{
    [DisallowMultipleComponent]
    public sealed class TextMeshProDropdownRegionBridge :
        MonoBehaviour,
        ISubmitHandler,
        IPointerClickHandler,
        IDisposable
    {
        private IAppUIFocusScopeHandle scope;
        private TextMeshProFocusDropdownControlPolicy policy;
        private TMP_Dropdown dropdown;
        private bool previousExpanded;
        private bool disposed;

        public void Initialize(
            IAppUIFocusScopeHandle focusScope,
            TextMeshProFocusDropdownControlPolicy controlPolicy)
        {
            if (focusScope == null) throw new ArgumentNullException(nameof(focusScope));
            if (controlPolicy == null) throw new ArgumentNullException(nameof(controlPolicy));

            Unsubscribe();
            scope = focusScope;
            policy = controlPolicy;
            dropdown = controlPolicy.Dropdown;
            previousExpanded = controlPolicy.IsExpanded;
            disposed = false;
            enabled = true;
            Subscribe();
            Synchronize();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            ObserveNow();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ObserveNow();
        }

        private void LateUpdate()
        {
            ObserveNow();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Unsubscribe();
            scope = null;
            policy = null;
            dropdown = null;
            enabled = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void HandleValueChanged(int _)
        {
            ObserveNow();
        }

        private void ObserveNow()
        {
            if (disposed || policy == null) return;
            bool expanded = policy.IsExpanded;
            if (expanded == previousExpanded) return;
            previousExpanded = expanded;
            Synchronize();
        }

        private void Synchronize()
        {
            if (scope != null && policy != null) policy.SynchronizeRegion(scope);
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
