using System.Collections.Generic;
using Joi.H.AppUI;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// App UI 绑定全量校验运行器。
    /// 该链路严格只读：扫描、读取和报告问题，不生成、不写回、不保存、不自动创建 Definition。
    /// </summary>
    public static class UIBindingValidateAllRunner
    {
        /// <summary>
        /// 根据项目设置执行全量校验。
        /// </summary>
        public static UIBindingValidationReport ValidateAll(UIBindingSettings settings)
        {
            UIBindingValidationReport report = new UIBindingValidationReport();
            // Validate All 是只读入口：问题只进入报告，修复必须由用户点击 Inspector 显式按钮完成。
            UIBindingValidateAllReadOnlyGuard.AppendAuditErrors(report);

            if (settings == null)
            {
                AppUIFocusProjectValidator.AppendProjectValidation(null, report);
                report.AddInfo("未配置 UIBindingSettings，已跳过全量校验。");
                return report;
            }

            HashSet<string> visitedPrefabPaths = new HashSet<string>();
            ValidatePages(settings, visitedPrefabPaths, report);
            ValidateGroupRegistry(settings, visitedPrefabPaths, report);
            ValidateGroupDefinitions(settings, visitedPrefabPaths, report);
            ValidateGroupPrefabs(settings, visitedPrefabPaths, report);
            // Focus Prefab 校验会进入隔离 Prefab contents；放在其他 Settings 读取之后，保持资产生命周期边界清晰。
            AppUIFocusProjectValidator.AppendProjectValidation(
                settings.PageDefinitionRegistry,
                report);
            return report;
        }

        /// <summary>
        /// 校验页面 Registry 中登记的所有 Page Prefab。
        /// </summary>
        private static void ValidatePages(
            UIBindingSettings settings,
            HashSet<string> visitedPrefabPaths,
            UIBindingValidationReport report)
        {
            UIPageDefinitionRegistry registry = settings.PageDefinitionRegistry;
            if (registry == null)
            {
                report.AddInfo("未配置 UIPageDefinitionRegistry。");
                return;
            }

            for (int i = 0; i < registry.Pages.Count; i++)
            {
                UIPageDefinition page = registry.Pages[i];
                if (page == null)
                {
                    continue;
                }

                ValidateDefinitionPrefab(page, UIBindingPrefabKind.Page, visitedPrefabPaths, report);
            }
        }

        /// <summary>
        /// 校验 Group Registry 中登记的所有 Group Prefab。
        /// </summary>
        private static void ValidateGroupRegistry(
            UIBindingSettings settings,
            HashSet<string> visitedPrefabPaths,
            UIBindingValidationReport report)
        {
            UIGroupDefinitionRegistry registry = settings.GroupDefinitionRegistry;
            if (registry == null)
            {
                return;
            }

            for (int i = 0; i < registry.Groups.Count; i++)
            {
                UIGroupDefinition group = registry.Groups[i];
                if (group == null)
                {
                    continue;
                }

                ValidateDefinitionPrefab(group, UIBindingPrefabKind.Group, visitedPrefabPaths, report);
            }
        }

        /// <summary>
        /// 按配置的搜索目录扫描独立 Group Definition，并校验其 Prefab。
        /// </summary>
        private static void ValidateGroupDefinitions(
            UIBindingSettings settings,
            HashSet<string> visitedPrefabPaths,
            UIBindingValidationReport report)
        {
            string[] roots = ToSearchRoots(settings.GroupDefinitionSearchRoots);
            if (roots.Length == 0)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:UIGroupDefinition", roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UIGroupDefinition definition = AssetDatabase.LoadAssetAtPath<UIGroupDefinition>(path);
                if (definition != null)
                {
                    ValidateDefinitionPrefab(definition, UIBindingPrefabKind.Group, visitedPrefabPaths, report);
                }
            }
        }

        /// <summary>
        /// 扫描独立 Group Prefab，发现未绑定 Definition 的 Prefab 时只报错不自动注册。
        /// </summary>
        private static void ValidateGroupPrefabs(
            UIBindingSettings settings,
            HashSet<string> visitedPrefabPaths,
            UIBindingValidationReport report)
        {
            string[] roots = ToSearchRoots(settings.GroupPrefabSearchRoots);
            if (roots.Length == 0)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (visitedPrefabPaths.Contains(path))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<UIGroupBase>() == null)
                {
                    continue;
                }

                // 缺 Definition 是配置错误，Validate All 不创建资产、不写 Registry。
                report.AddError("Standalone Group prefab is missing a UIGroupDefinition or definition override: " + path);
                ValidatePrefabAsset(prefab, path, UIBindingPrefabKind.Group, visitedPrefabPaths, report);
            }
        }

        /// <summary>
        /// 从 Definition 解析 Prefab 并继续校验 Prefab 根 Scope。
        /// </summary>
        private static void ValidateDefinitionPrefab(
            IUIDefinition definition,
            UIBindingPrefabKind expectedKind,
            HashSet<string> visitedPrefabPaths,
            UIBindingValidationReport report)
        {
            if (!UIBindingPrefabResolver.DefaultResolver.TryResolve(definition, out string path, out string error))
            {
                report.AddError(error);
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            ValidatePrefabAsset(prefab, path, expectedKind, visitedPrefabPaths, report);
        }

        /// <summary>
        /// 校验单个 Prefab 资产。visitedPrefabPaths 用于避免同一 Prefab 被多个入口重复校验。
        /// </summary>
        private static void ValidatePrefabAsset(
            GameObject prefab,
            string path,
            UIBindingPrefabKind expectedKind,
            HashSet<string> visitedPrefabPaths,
            UIBindingValidationReport report)
        {
            if (string.IsNullOrEmpty(path) || visitedPrefabPaths.Contains(path))
            {
                return;
            }

            visitedPrefabPaths.Add(path);
            if (!UIBindingValidator.TryGetRootScope(
                    prefab,
                    expectedKind,
                    out UIBindingScopeBase scope,
                    out string scopeError))
            {
                report.AddError(scopeError);
                return;
            }

            UIBindingValidationReport scopeReport = UIBindingValidator.ValidateScope(scope);
            for (int i = 0; i < scopeReport.Errors.Count; i++)
            {
                report.AddError(path + ": " + scopeReport.Errors[i]);
            }

            for (int i = 0; i < scopeReport.Infos.Count; i++)
            {
                report.AddInfo(path + ": " + scopeReport.Infos[i]);
            }
        }

        /// <summary>
        /// 过滤空搜索根，返回 AssetDatabase.FindAssets 可用的路径数组。
        /// </summary>
        private static string[] ToSearchRoots(List<string> roots)
        {
            if (roots == null || roots.Count == 0)
            {
                return new string[0];
            }

            List<string> validRoots = new List<string>(roots.Count);
            for (int i = 0; i < roots.Count; i++)
            {
                string root = roots[i];
                if (!string.IsNullOrEmpty(root))
                {
                    validRoots.Add(root);
                }
            }

            return validRoots.ToArray();
        }
    }
}
