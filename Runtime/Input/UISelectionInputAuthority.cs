using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Presentation 选择资格适配器。语义页面只请求 UIFocusService / Committer，
    /// 未迁移页面走不写 History 的 Legacy Committer 入口。
    /// </summary>
    internal sealed class UISelectionInputAuthority
    {
        private readonly UIDefaultFocusResolver defaultFocusResolver =
            new UIDefaultFocusResolver();

        public void Refresh(
            UIInteractionSnapshot snapshot,
            UIPageInstanceRegistry instanceRegistry,
            UIFocusService focusService,
            AppUIFocusChangeReason reason)
        {
            if (snapshot == null || instanceRegistry == null || focusService == null)
            {
                return;
            }

            UIPageInstance topInteractivePage = null;
            instanceRegistry.TryResolve(
                snapshot.TopInteractivePage,
                out topInteractivePage);
            if (topInteractivePage != null &&
                focusService.TryHandleSemanticSelection(
                    topInteractivePage,
                    reason))
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (!IsCurrentSelectionAllowed(
                    selected,
                    topInteractivePage,
                    snapshot,
                    instanceRegistry))
            {
                UIFocusCommitter.ClearLegacySelection();
            }

            if (!UIDefaultFocusResolver.CanOwnSelection(topInteractivePage))
            {
                return;
            }

            selected = eventSystem.currentSelectedGameObject;
            if (reason != AppUIFocusChangeReason.FirstOpened &&
                UIDefaultFocusResolver.IsSelectionOwnedBy(
                    selected,
                    topInteractivePage) &&
                UIDefaultFocusResolver.IsSelectableUsable(selected))
            {
                return;
            }

            Selectable selectable = defaultFocusResolver.Resolve(
                topInteractivePage,
                ToDefaultFocusReason(reason));
            if (selectable != null)
            {
                UIFocusCommitter.CommitLegacySelection(
                    selectable,
                    AppUIInteractionSourceKind.Programmatic);
            }
            else
            {
                UIFocusCommitter.ClearLegacySelection();
            }
        }

        public void ClearOwnedSelection(IReadOnlyList<UIPageInstance> pages)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (FindOwner(selected, pages) != null)
            {
                UIFocusCommitter.ClearLegacySelection();
            }
        }

        private static UIDefaultFocusReason ToDefaultFocusReason(
            AppUIFocusChangeReason reason)
        {
            switch (reason)
            {
                case AppUIFocusChangeReason.FirstOpened:
                    return UIDefaultFocusReason.PageOpened;
                case AppUIFocusChangeReason.Reopened:
                case AppUIFocusChangeReason.RestoreRequested:
                    return UIDefaultFocusReason.RestoreRequested;
                default:
                    return UIDefaultFocusReason.SelectionInvalid;
            }
        }

        private static bool IsCurrentSelectionAllowed(
            GameObject selected,
            UIPageInstance topInteractivePage,
            UIInteractionSnapshot snapshot,
            UIPageInstanceRegistry instanceRegistry)
        {
            if (selected == null)
            {
                return topInteractivePage == null;
            }

            UIPageInstance owner = FindOwner(
                selected,
                snapshot,
                instanceRegistry);
            if (owner == null)
            {
                return topInteractivePage == null;
            }

            return owner == topInteractivePage &&
                   UIDefaultFocusResolver.CanOwnSelection(owner) &&
                   UIDefaultFocusResolver.IsSelectableUsable(selected);
        }

        private static UIPageInstance FindOwner(
            GameObject selected,
            UIInteractionSnapshot snapshot,
            UIPageInstanceRegistry instanceRegistry)
        {
            if (selected == null || snapshot == null || instanceRegistry == null)
            {
                return null;
            }

            Transform selectedTransform = selected.transform;
            for (int i = 0; i < snapshot.PageStateCount; i++)
            {
                UIPageInteractionState state = snapshot.GetPageState(i);
                if (!instanceRegistry.TryResolve(
                        state.Page,
                        out UIPageInstance page) ||
                    page.GameObject == null)
                {
                    continue;
                }

                if (selectedTransform.IsChildOf(page.GameObject.transform))
                {
                    return page;
                }
            }

            return null;
        }

        private static UIPageInstance FindOwner(
            GameObject selected,
            IReadOnlyList<UIPageInstance> pages)
        {
            if (selected == null || pages == null)
            {
                return null;
            }

            Transform selectedTransform = selected.transform;
            for (int i = 0; i < pages.Count; i++)
            {
                UIPageInstance page = pages[i];
                if (page == null || page.GameObject == null)
                {
                    continue;
                }

                if (selectedTransform.IsChildOf(page.GameObject.transform))
                {
                    return page;
                }
            }

            return null;
        }
    }
}
