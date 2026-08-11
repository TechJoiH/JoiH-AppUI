using System.IO;
using Joi.H.AppUI;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Prefab 根节点期望类型。
    /// </summary>
    public enum UIBindingPrefabKind
    {
        /// <summary>
        /// 不限定 Page 或 Group。
        /// </summary>
        Any,

        /// <summary>
        /// 期望根节点是页面 Controller。
        /// </summary>
        Page,

        /// <summary>
        /// 期望根节点是 Group Controller。
        /// </summary>
        Group,
    }

    /// <summary>
    /// 单个 Scope 的只读绑定校验器。
    /// 检查 Prefab 类型、扫描结果、生成文件新旧、asmdef 边界和已有序列化引用。
    /// </summary>
    public static class UIBindingValidator
    {
        /// <summary>
        /// 校验一个 Scope。该方法只读：发现缺失或过期只报告，不生成、不写回、不保存。
        /// </summary>
        public static UIBindingValidationReport ValidateScope(UIBindingScopeBase scope)
        {
            UIBindingValidationReport report = new UIBindingValidationReport();
            if (scope == null)
            {
                report.AddError("Scope is null.");
                return report;
            }

            if (!ValidateExpectedKind(scope.gameObject, GetExpectedKind(scope), out string kindError))
            {
                report.AddError(kindError);
            }

            UIBindingScanResult scanResult = UIBindingScanner.Scan(scope);
            for (int i = 0; i < scanResult.Errors.Count; i++)
            {
                report.AddError(scanResult.Errors[i]);
            }

            UIBindingOwnershipValidator.AppendScanTargetErrors(scope, scanResult, report);
            // Prefab Variant 只能继承 base prefab 的绑定契约；只读校验在这里提前暴露删除、替换或新增 B_ 节点的问题。
            UIBindingVariantValidator.AppendValidationErrors(scope, scanResult, report);

            if (!UIBindingFileUtility.TryGetSourceInfo(
                    scope,
                    out UIBindingSourceInfo sourceInfo,
                    out string sourceError))
            {
                report.AddError(sourceError);
                return report;
            }

            if (!UIBindingFileUtility.TryValidateCompilationBoundary(
                    sourceInfo,
                    out string boundaryError))
            {
                report.AddError(boundaryError);
            }

            if (!File.Exists(sourceInfo.GeneratedPath))
            {
                report.AddError("Bindings file does not exist: " + sourceInfo.GeneratedPath);
            }
            else if (!UIBindingFileUtility.CanOverwriteGeneratedFile(sourceInfo.GeneratedPath, out string overwriteError))
            {
                report.AddError(overwriteError);
            }
            else
            {
                // 用代码生成器生成期望文本，但只做字符串比较，不写回磁盘。
                string expected = UIBindingCodeWriter.Write(sourceInfo, scanResult);
                string current = File.ReadAllText(sourceInfo.GeneratedPath);
                if (current != expected)
                {
                    report.AddError("Bindings file is not up to date with the current scan: " + sourceInfo.GeneratedPath);
                }
            }

            UIBindingOwnershipValidator.AppendSerializedReferenceErrors(scope, scanResult, report);

#if UNITY_EDITOR
            UIBindingValidationResult fieldResult = scope.ValidateBindingsEditor();
            for (int i = 0; i < fieldResult.Messages.Count; i++)
            {
                UIBindingValidationMessage message = fieldResult.Messages[i];
                string text = scope.GetType().Name + ": " + message.Message;
                if (!string.IsNullOrEmpty(message.MemberName))
                {
                    text += " " + message.MemberName;
                }

                if (message.Level == UIBindingValidationLevel.Error)
                {
                    report.AddError(text);
                }
                else
                {
                    report.AddInfo(text);
                }
            }
#endif

            if (!report.HasError)
            {
                report.AddInfo("Binding validation passed: " + scope.GetType().Name);
            }

            return report;
        }

        /// <summary>
        /// 根据 Scope 类型推导 Prefab 根节点期望类型。
        /// </summary>
        private static UIBindingPrefabKind GetExpectedKind(UIBindingScopeBase scope)
        {
            if (scope is PanelBaseController)
            {
                return UIBindingPrefabKind.Page;
            }

            if (scope is UIGroupBase)
            {
                return UIBindingPrefabKind.Group;
            }

            return UIBindingPrefabKind.Any;
        }

        /// <summary>
        /// 查找 Prefab 根节点上的唯一绑定 Scope，不限定 Page/Group 类型。
        /// </summary>
        public static bool TryGetRootScope(GameObject prefab, out UIBindingScopeBase scope, out string error)
        {
            return TryGetRootScope(prefab, UIBindingPrefabKind.Any, out scope, out error);
        }

        /// <summary>
        /// 查找 Prefab 根节点上的唯一绑定 Scope，并校验是否符合期望类型。
        /// </summary>
        public static bool TryGetRootScope(
            GameObject prefab,
            UIBindingPrefabKind expectedKind,
            out UIBindingScopeBase scope,
            out string error)
        {
            scope = null;
            error = string.Empty;
            if (prefab == null)
            {
                error = "Prefab is null.";
                return false;
            }

            UIBindingScopeBase[] scopes = prefab.GetComponents<UIBindingScopeBase>();
            if (scopes == null || scopes.Length == 0)
            {
                error = "Prefab root has no UIBindingScopeBase: " + AssetDatabase.GetAssetPath(prefab);
                return false;
            }

            if (scopes.Length > 1)
            {
                error = "Prefab root has multiple UIBindingScopeBase components: " + AssetDatabase.GetAssetPath(prefab);
                return false;
            }

            if (!ValidateExpectedKind(prefab, expectedKind, out error))
            {
                return false;
            }

            scope = scopes[0];
            return true;
        }

        /// <summary>
        /// 校验 Prefab 根节点是否符合 Page/Group 类型要求。
        /// </summary>
        private static bool ValidateExpectedKind(
            GameObject prefab,
            UIBindingPrefabKind expectedKind,
            out string error)
        {
            error = string.Empty;
            if (expectedKind == UIBindingPrefabKind.Any)
            {
                return true;
            }

            if (expectedKind == UIBindingPrefabKind.Page)
            {
                PanelBaseController[] controllers = prefab.GetComponents<PanelBaseController>();
                if (controllers == null || controllers.Length == 0)
                {
                    error = "Panel prefab root has no PanelBaseController: " + AssetDatabase.GetAssetPath(prefab);
                    return false;
                }

                if (controllers.Length > 1)
                {
                    error = "Panel prefab root has multiple PanelBaseController components: " + AssetDatabase.GetAssetPath(prefab);
                    return false;
                }

                return true;
            }

            if (expectedKind == UIBindingPrefabKind.Group && prefab.GetComponent<UIGroupBase>() == null)
            {
                error = "Group prefab root has no UIGroupBase: " + AssetDatabase.GetAssetPath(prefab);
                return false;
            }

            return true;
        }
    }
}
