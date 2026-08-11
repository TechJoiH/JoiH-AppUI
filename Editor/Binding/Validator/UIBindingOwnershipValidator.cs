using System.Collections.Generic;
using Joi.H.AppUI;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 绑定引用归属校验器。
    /// 确保扫描目标和现有序列化引用都属于当前 Scope，父 Scope 不越界引用子 Scope 内部控件。
    /// </summary>
    public static class UIBindingOwnershipValidator
    {
        /// <summary>
        /// 校验扫描结果中的所有目标是否属于当前 Scope。
        /// </summary>
        public static bool TryValidateScanTargets(
            UIBindingScopeBase scope,
            UIBindingScanResult scanResult,
            out string error)
        {
            List<string> errors = new List<string>(4);
            AppendScanTargetErrors(scope, scanResult, errors);
            if (errors.Count == 0)
            {
                error = string.Empty;
                return true;
            }

            error = string.Join("\n", errors.ToArray());
            return false;
        }

        /// <summary>
        /// 将扫描目标归属错误追加到校验报告。
        /// </summary>
        public static void AppendScanTargetErrors(
            UIBindingScopeBase scope,
            UIBindingScanResult scanResult,
            UIBindingValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            List<string> errors = new List<string>(4);
            AppendScanTargetErrors(scope, scanResult, errors);
            for (int i = 0; i < errors.Count; i++)
            {
                report.AddError(errors[i]);
            }
        }

        /// <summary>
        /// 校验 Controller 上已有序列化引用是否越界。
        /// 这能捕获人工拖错引用的情况，即使生成扫描本身是正确的。
        /// </summary>
        public static void AppendSerializedReferenceErrors(
            UIBindingScopeBase scope,
            UIBindingScanResult scanResult,
            UIBindingValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (scope == null || scanResult == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(scope);
            for (int i = 0; i < scanResult.Bindings.Count; i++)
            {
                UIBindingInfo binding = scanResult.Bindings[i];
                SerializedProperty property = serializedObject.FindProperty(binding.SerializedFieldName);
                if (property == null ||
                    property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                Object currentValue = property.objectReferenceValue;
                if (currentValue == null)
                {
                    continue;
                }

                // 子 Scope 字段必须精确引用扫描得到的子 Scope 组件本身。
                if (binding.TargetKind == UIBindingTargetKind.BindingScope &&
                    currentValue != binding.TargetObject)
                {
                    report.AddError(
                        "Serialized binding reference does not match scanned child Scope: " +
                        binding.PropertyName + ". Expected " +
                        DescribeObject(scope.transform, binding.TargetObject) + ", current " +
                        DescribeObject(scope.transform, currentValue) + ".");
                    continue;
                }

                if (!TryValidateTargetOwnership(
                        scope,
                        binding.TargetKind,
                        currentValue,
                        binding.PropertyName,
                        out string error))
                {
                    report.AddError(error);
                }
            }
        }

        /// <summary>
        /// 将扫描目标归属错误收集到列表，供生成和写回流程快速失败。
        /// </summary>
        private static void AppendScanTargetErrors(
            UIBindingScopeBase scope,
            UIBindingScanResult scanResult,
            List<string> errors)
        {
            if (errors == null)
            {
                return;
            }

            if (scope == null)
            {
                errors.Add("Binding scope is null.");
                return;
            }

            if (scanResult == null)
            {
                errors.Add("Binding scan result is null.");
                return;
            }

            for (int i = 0; i < scanResult.Bindings.Count; i++)
            {
                UIBindingInfo binding = scanResult.Bindings[i];
                if (!TryValidateTargetOwnership(
                        scope,
                        binding.TargetKind,
                        binding.TargetObject,
                        binding.PropertyName,
                        out string error))
                {
                    errors.Add(error);
                }
            }
        }

        /// <summary>
        /// 校验单个引用目标是否位于当前 Scope 内，且没有跨过子 Scope 边界。
        /// </summary>
        private static bool TryValidateTargetOwnership(
            UIBindingScopeBase scope,
            UIBindingTargetKind targetKind,
            Object targetObject,
            string memberName,
            out string error)
        {
            error = string.Empty;
            if (scope == null)
            {
                error = "Binding scope is null.";
                return false;
            }

            if (targetObject == null)
            {
                error = "Binding target is null: " + memberName;
                return false;
            }

            if (!TryGetTargetTransform(targetObject, out Transform targetTransform))
            {
                error =
                    "Binding target is not a GameObject or Component: " +
                    memberName + " -> " + targetObject.name;
                return false;
            }

            Transform root = scope.transform;
            if (!IsDescendantOrSelf(root, targetTransform))
            {
                error =
                    "Binding target is outside current Scope: " +
                    memberName + " -> " + GetPath(root, targetTransform);
                return false;
            }

            if (targetKind == UIBindingTargetKind.BindingScope)
            {
                // 子 Scope 是唯一允许父 Scope 绑定的跨边界对象，但只能绑定 Scope 组件本身。
                return TryValidateBindingScopeTarget(
                    root,
                    targetObject,
                    targetTransform,
                    memberName,
                    out error);
            }

            Transform childScope = FindChildScopeBoundary(root, targetTransform, true);
            if (childScope != null)
            {
                error =
                    "Binding target crosses child Scope: " +
                    memberName + " -> " + GetPath(root, targetTransform) +
                    " (child Scope: " + GetPath(root, childScope) + ").";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 校验 BindingScope 类型字段是否引用直接子 Scope 组件。
        /// </summary>
        private static bool TryValidateBindingScopeTarget(
            Transform root,
            Object targetObject,
            Transform targetTransform,
            string memberName,
            out string error)
        {
            error = string.Empty;
            MonoBehaviour targetBehaviour = targetObject as MonoBehaviour;
            if (targetBehaviour == null || !(targetBehaviour is IUIBindingScope))
            {
                error =
                    "Binding scope field must reference an IUIBindingScope component: " +
                    memberName + " -> " + DescribeObject(root, targetObject);
                return false;
            }

            if (targetTransform == root)
            {
                error =
                    "Binding scope field must reference a child Scope, not the current Scope root: " +
                    memberName;
                return false;
            }

            Transform parentScope = FindChildScopeBoundary(root, targetTransform.parent, true);
            if (parentScope != null)
            {
                error =
                    "Binding scope target is nested inside another child Scope: " +
                    memberName + " -> " + GetPath(root, targetTransform) +
                    " (parent child Scope: " + GetPath(root, parentScope) + ").";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 从 GameObject 或 Component 引用解析 Transform。
        /// </summary>
        private static bool TryGetTargetTransform(Object targetObject, out Transform transform)
        {
            GameObject gameObject = targetObject as GameObject;
            if (gameObject != null)
            {
                transform = gameObject.transform;
                return transform != null;
            }

            Component component = targetObject as Component;
            if (component != null)
            {
                transform = component.transform;
                return transform != null;
            }

            transform = null;
            return false;
        }

        /// <summary>
        /// 判断 target 是否是 root 自身或其子节点。
        /// </summary>
        private static bool IsDescendantOrSelf(Transform root, Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current == root)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// 从目标节点向上查找第一个子 Scope 边界。
        /// </summary>
        private static Transform FindChildScopeBoundary(
            Transform root,
            Transform target,
            bool includeTarget)
        {
            Transform current = includeTarget ? target : target != null ? target.parent : null;
            while (current != null && current != root)
            {
                if (HasBindingScope(current))
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// 判断节点上是否挂有 IUIBindingScope。
        /// </summary>
        private static bool HasBindingScope(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour is IUIBindingScope)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 生成带路径的对象描述，用于错误信息定位。
        /// </summary>
        private static string DescribeObject(Transform root, Object targetObject)
        {
            if (targetObject == null)
            {
                return "<null>";
            }

            if (TryGetTargetTransform(targetObject, out Transform transform))
            {
                return targetObject.name + " at " + GetPath(root, transform);
            }

            return targetObject.name;
        }

        /// <summary>
        /// 生成从根节点到目标节点的层级路径。
        /// </summary>
        private static string GetPath(Transform root, Transform target)
        {
            if (target == null)
            {
                return "<missing>";
            }

            Stack<string> names = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }

            string path = current == root && root != null ? root.name : "<outside>";
            while (names.Count > 0)
            {
                path += "/" + names.Pop();
            }

            return path;
        }
    }
}
