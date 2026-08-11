#if UNITY_EDITOR
using System;
using Joi.H.AppUI.Editor;
using Joi.H.AppUI.Editor.Binding;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Validate All 的焦点扩展。只读取 Registry、Prefab 与源码；不会保存 Prefab、写回引用或创建资产。
    /// </summary>
    public static class AppUIFocusProjectValidator
    {
        public static void AppendProjectValidation(
            UIPageDefinitionRegistry registry,
            UIBindingValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            AppUIFocusValidationReport writeReport =
                AppUIFocusP0Validator.ValidateProjectFocusWrites();
            for (int i = 0; i < writeReport.Errors.Count; i++)
            {
                report.AddError("Focus write guard: " + writeReport.Errors[i]);
            }

            if (registry == null)
            {
                report.AddInfo(
                    "Focus Validate All skipped page prefabs because UIPageDefinitionRegistry is not configured.");
                return;
            }

            for (int i = 0; i < registry.Pages.Count; i++)
            {
                UIPageDefinition page = registry.Pages[i];
                if (page == null)
                {
                    continue;
                }

                ValidatePage(page, report);
            }
        }

        private static void ValidatePage(
            UIPageDefinition page,
            UIBindingValidationReport report)
        {
            if (!UIBindingPrefabResolver.DefaultResolver.TryResolve(
                    page,
                    out string path,
                    out string error))
            {
                report.AddError("Focus Page[" + page.PageId + "]: " + error);
                return;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                ValidateFocusRoot(root, page.PageId, path, report);
            }
            catch (Exception exception)
            {
                report.AddError(
                    CreatePrefix(page.PageId, path) +
                    "failed to inspect focus declaration: " +
                    exception.Message);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        internal static void ValidateFocusRoot(
            GameObject root,
            string pageId,
            string path,
            UIBindingValidationReport report)
        {
            string prefix = CreatePrefix(pageId, path);
            if (root == null)
            {
                report.AddError(prefix + "prefab root is missing.");
                return;
            }

            PanelBaseController controller = root.GetComponent<PanelBaseController>();
            AppUIFocusAuthoring rootAuthoring = root.GetComponent<AppUIFocusAuthoring>();
            AppUIFocusAuthoring nestedAuthoring =
                root.GetComponentInChildren<AppUIFocusAuthoring>(true);
            if (rootAuthoring == null && nestedAuthoring != null)
            {
                report.AddError(
                    prefix +
                    "AppUIFocusAuthoring must be placed on the page root because runtime lookup is root-only.");
                return;
            }

            IAppUIFocusDefinitionProvider provider =
                controller as IAppUIFocusDefinitionProvider;
            if (provider != null && rootAuthoring != null)
            {
                report.AddWarning(
                    prefix +
                    "Controller focus definition takes precedence; root AppUIFocusAuthoring is ignored at runtime.");
            }

            if (provider == null)
            {
                provider = rootAuthoring;
            }

            if (provider == null)
            {
                report.AddInfo(prefix + "page has no focus declaration.");
                return;
            }

            if (provider is AppUIFocusAuthoring authoring)
            {
                AppendFocusReport(prefix, authoring.ValidateAuthoring(), report);
                return;
            }

            AppUIFocusDefinition definition;
            try
            {
                definition = provider.BuildFocusDefinition();
            }
            catch (Exception exception)
            {
                report.AddError(
                    prefix +
                    "Controller BuildFocusDefinition failed in isolated Prefab contents: " +
                    exception.Message);
                return;
            }

            if (definition == null)
            {
                report.AddError(prefix + "Controller returned a null focus definition.");
                return;
            }

            bool hasDefault = TryResolveDefaultFocusAddress(
                controller,
                definition,
                out AppUIFocusNodeAddress defaultAddress,
                out string defaultError);
            if (!hasDefault)
            {
                report.AddError(prefix + defaultError);
            }

            AppUIFocusValidationReport focusReport =
                AppUIFocusDefinitionValidator.Validate(
                    definition,
                    defaultAddress,
                    true);
            AppendFocusReport(prefix, focusReport, report);
        }

        private static bool TryResolveDefaultFocusAddress(
            PanelBaseController controller,
            AppUIFocusDefinition definition,
            out AppUIFocusNodeAddress address,
            out string error)
        {
            address = default;
            error = string.Empty;
            try
            {
                if (controller is IAppUIDefaultFocusTargetProvider targetProvider)
                {
                    if (!targetProvider.TryGetDefaultFocus(
                            UIDefaultFocusReason.PageOpened,
                            out AppUIFocusTarget target) ||
                        !target.IsValid)
                    {
                        error = "default focus provider did not return a valid target for PageOpened.";
                        return false;
                    }

                    if (target.Kind == AppUIFocusTargetKind.NodeAddress)
                    {
                        address = target.NodeAddress;
                    }
                    else if (!TryFindNodeAddress(
                                 definition,
                                 target.Selectable,
                                 out address))
                    {
                        error = "default focus Selectable is not registered in the Definition.";
                        return false;
                    }
                }
                else if (controller is IUIDefaultFocusProvider legacyProvider)
                {
                    if (!legacyProvider.TryGetDefaultFocus(
                            UIDefaultFocusReason.PageOpened,
                            out Selectable selectable) ||
                        !TryFindNodeAddress(definition, selectable, out address))
                    {
                        error = "legacy default focus Selectable is missing or not registered in the Definition.";
                        return false;
                    }
                }
                else
                {
                    error = "focus Definition provider must also declare a default focus provider.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = "default focus provider threw during validation: " + exception.Message;
                return false;
            }

            if (!IsAddressDeclaredOrVirtualized(definition, address))
            {
                error =
                    "default focus address is neither a static Node nor owned by a virtualized Group: " +
                    address;
                return false;
            }

            return true;
        }

        private static bool TryFindNodeAddress(
            AppUIFocusDefinition definition,
            Selectable selectable,
            out AppUIFocusNodeAddress address)
        {
            for (int i = 0; i < definition.NodeCount; i++)
            {
                AppUIFocusNodeDefinition node = definition.GetNode(i);
                if (ReferenceEquals(node.Selectable, selectable))
                {
                    address = node.Address;
                    return true;
                }
            }

            address = default;
            return false;
        }

        private static bool IsAddressDeclaredOrVirtualized(
            AppUIFocusDefinition definition,
            AppUIFocusNodeAddress address)
        {
            if (!address.IsValid)
            {
                return false;
            }

            for (int i = 0; i < definition.NodeCount; i++)
            {
                if (definition.GetNode(i).Address == address)
                {
                    return true;
                }
            }

            for (int i = 0; i < definition.GroupCount; i++)
            {
                AppUIFocusGroupDefinition group = definition.GetGroup(i);
                if (string.Equals(
                        group.GroupId,
                        address.GroupId,
                        StringComparison.Ordinal) &&
                    group.VirtualizationAdapter != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendFocusReport(
            string prefix,
            AppUIFocusValidationReport focusReport,
            UIBindingValidationReport report)
        {
            for (int i = 0; i < focusReport.Errors.Count; i++)
            {
                report.AddError(prefix + focusReport.Errors[i]);
            }

            for (int i = 0; i < focusReport.Warnings.Count; i++)
            {
                report.AddWarning(prefix + focusReport.Warnings[i]);
            }
        }

        private static string CreatePrefix(string pageId, string path)
        {
            return "Focus Page[" +
                   (pageId ?? string.Empty) +
                   "] " +
                   (path ?? string.Empty) +
                   ": ";
        }
    }
}
#endif
