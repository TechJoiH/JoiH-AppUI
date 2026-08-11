using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public sealed class AppUISelectionGroup<TValue> :
        IDisposable,
        IAppUISelectionToggleAuthority
    {
        private sealed class Item
        {
            public Button Button;
            public Toggle Toggle;
            public TValue Value;
            public AppUISelectionVisualState Visual;
            public Action<TValue> OnSelected;
            public Action<TValue, AppUISelectionConfirmCause> OnConfirmed;
            public UnityAction ButtonAction;
            public UnityAction<bool> ToggleAction;
            public AppUISelectionToggleMember ToggleMember;
        }

        private readonly List<Item> items = new List<Item>(16);
        private readonly EqualityComparer<TValue> comparer = EqualityComparer<TValue>.Default;
        private readonly AppUISelectionInteractionPolicy interactionPolicy;
        private bool hasSelection;
        private bool isRefreshingItems;
        private TValue selectedValue;

        public AppUISelectionGroup(
            AppUISelectionInteractionPolicy policy =
                AppUISelectionInteractionPolicy.ClickToSelect)
        {
            interactionPolicy = policy;
        }

        public bool HasSelection
        {
            get { return hasSelection; }
        }

        public TValue SelectedValue
        {
            get { return selectedValue; }
        }

        public Selectable SelectedSelectable
        {
            get
            {
                Item item = FindSelectedItem();
                if (item == null)
                {
                    return null;
                }

                return item.Toggle != null
                    ? (Selectable)item.Toggle
                    : item.Button;
            }
        }

        /// <summary>
        /// Registers a command-style item. Business selection changes only when the button is clicked
        /// or when Select/EnsureSelected is called by the owning controller.
        /// </summary>
        public void RegisterButton(
            Button button,
            TValue value,
            AppUISelectionVisualState visual,
            Action<TValue> onSelected,
            bool notifyRepeatedSelection = false)
        {
            if (button == null)
            {
                return;
            }

            Item item = new Item
            {
                Button = button,
                Value = value,
                Visual = visual,
                OnSelected = onSelected,
            };
            item.ButtonAction = delegate
            {
                if (notifyRepeatedSelection && hasSelection && comparer.Equals(selectedValue, value))
                {
                    item.OnSelected?.Invoke(value);
                    return;
                }

                Select(value);
            };
            button.onClick.AddListener(item.ButtonAction);
            items.Add(item);
            ApplyItemVisual(item);
        }

        /// <summary>
        /// Registers a toggle-style item. Prefab-authored isOn values are visual state only until the
        /// owning controller calls Select/EnsureSelected. ClickToSelect also selects when a toggle is
        /// turned on; MoveSelectAndConfirm selects from focus and reserves
        /// click/submit for confirm; ConfirmToSelect changes choice only when
        /// click/submit confirms the focused toggle.
        /// </summary>
        public void RegisterToggle(
            Toggle toggle,
            TValue value,
            AppUISelectionVisualState visual,
            Action<TValue> onSelected)
        {
            RegisterToggle(toggle, value, visual, onSelected, null);
        }

        public void RegisterToggle(
            Toggle toggle,
            TValue value,
            AppUISelectionVisualState visual,
            Action<TValue> onSelected,
            Action<TValue, AppUISelectionConfirmCause> onConfirmed)
        {
            if (toggle == null)
            {
                return;
            }

            Item item = new Item
            {
                Toggle = toggle,
                Value = value,
                Visual = visual,
                OnSelected = onSelected,
                OnConfirmed = onConfirmed,
            };
            item.ToggleAction = isOn =>
            {
                if (interactionPolicy ==
                        AppUISelectionInteractionPolicy.MoveSelectAndConfirm ||
                    interactionPolicy ==
                        AppUISelectionInteractionPolicy.ConfirmToSelect)
                {
                    RefreshItems();
                    return;
                }

                if (isOn)
                {
                    Select(value);
                }
            };
            toggle.onValueChanged.AddListener(item.ToggleAction);

            if (interactionPolicy !=
                AppUISelectionInteractionPolicy.ClickToSelect)
            {
                item.ToggleMember =
                    toggle.GetComponent<AppUISelectionToggleMember>();
                if (item.ToggleMember == null)
                {
                    Debug.LogError(
                        "<AppUISelectionGroup> MoveSelectAndConfirm toggle [" +
                        toggle.name +
                        "] is missing AppUISelectionToggleMember.",
                        toggle);
                }
                else
                {
                    item.ToggleMember.Configure(this, toggle);
                }
            }

            items.Add(item);
            ApplyItemVisual(item);
        }

        /// <summary>
        /// Selects a value as the data-layer choice and refreshes every registered visual.
        /// Use this for user choices and for default selection that must run business callbacks.
        /// </summary>
        public void Select(TValue value, bool notify = true)
        {
            bool changed = !hasSelection || !comparer.Equals(selectedValue, value);
            hasSelection = true;
            selectedValue = value;
            RefreshItems();
            if (!changed || !notify)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item != null && comparer.Equals(item.Value, value))
                {
                    item.OnSelected?.Invoke(value);
                    return;
                }
            }
        }

        /// <summary>
        /// Ensures the group has a real data-layer selection. Use this after registering a dynamic
        /// list when there is no existing model value and the first/default item must drive content.
        /// </summary>
        public void EnsureSelected(TValue defaultValue, bool notify = true)
        {
            if (hasSelection)
            {
                RefreshItems();
                return;
            }

            Select(defaultValue, notify);
        }

        /// <summary>
        /// Synchronizes an existing model value into UI without firing business callbacks. Do not use
        /// this to create default selection state for a new list; use Select or EnsureSelected instead.
        /// </summary>
        public void SetSelectedWithoutNotify(TValue value)
        {
            Select(value, false);
        }

        public void Clear(bool notify = false)
        {
            hasSelection = false;
            selectedValue = default;
            RefreshItems();
        }

        public void Dispose()
        {
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.Button != null && item.ButtonAction != null)
                {
                    item.Button.onClick.RemoveListener(item.ButtonAction);
                }

                if (item.Toggle != null && item.ToggleAction != null)
                {
                    item.Toggle.onValueChanged.RemoveListener(item.ToggleAction);
                }

                if (item.ToggleMember != null)
                {
                    item.ToggleMember.Unconfigure(this);
                }

                if (item.Visual != null)
                {
                    item.Visual.SetChoiceSelected(false);
                }
            }

            items.Clear();
            hasSelection = false;
            selectedValue = default;
        }

        void IAppUISelectionToggleAuthority.HandleToggleFocused(Toggle toggle)
        {
            if (interactionPolicy ==
                AppUISelectionInteractionPolicy.MoveSelectAndConfirm)
            {
                Item item = FindToggleItem(toggle);
                if (item != null)
                {
                    Select(item.Value);
                }
            }
        }

        void IAppUISelectionToggleAuthority.HandleToggleConfirmed(
            Toggle toggle,
            AppUISelectionConfirmCause cause)
        {
            Item item = FindToggleItem(toggle);
            if (item == null)
            {
                return;
            }

            if (interactionPolicy ==
                AppUISelectionInteractionPolicy.ConfirmToSelect)
            {
                Select(item.Value);
                return;
            }

            if (!hasSelection)
            {
                return;
            }

            Item selectedItem = FindSelectedItem();
            selectedItem?.OnConfirmed?.Invoke(selectedItem.Value, cause);
        }

        private void RefreshItems()
        {
            if (isRefreshingItems)
            {
                return;
            }

            isRefreshingItems = true;
            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ApplyItemVisual(items[i]);
                }
            }
            finally
            {
                isRefreshingItems = false;
            }
        }

        private void ApplyItemVisual(Item item)
        {
            if (item == null)
            {
                return;
            }

            bool selected = hasSelection && comparer.Equals(selectedValue, item.Value);
            if (item.Visual != null)
            {
                item.Visual.SetChoiceSelected(selected);
            }

            if (item.Toggle != null && item.Toggle.isOn != selected)
            {
                item.Toggle.SetIsOnWithoutNotify(selected);
            }
        }

        private Item FindSelectedItem()
        {
            if (!hasSelection)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item != null && comparer.Equals(item.Value, selectedValue))
                {
                    return item;
                }
            }

            return null;
        }

        private Item FindToggleItem(Toggle toggle)
        {
            if (toggle == null)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item != null && item.Toggle == toggle)
                {
                    return item;
                }
            }

            return null;
        }
    }
}
