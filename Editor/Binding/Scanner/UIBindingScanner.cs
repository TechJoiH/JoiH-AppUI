using System;
using System.Collections.Generic;
using Joi.H.AppUI;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Prefab 绑定扫描器。
    /// 从当前 Scope 根节点向下查找 B_ 节点，并在遇到子 Scope 时建立边界，避免父级越界绑定子级内部控件。
    /// </summary>
    public static class UIBindingScanner
    {
        /// <summary>
        /// 扫描一个绑定 Scope，返回可生成/写回的绑定列表。
        /// </summary>
        public static UIBindingScanResult Scan(UIBindingScopeBase scope)
        {
            UIBindingScanResult result = new UIBindingScanResult();
            if (scope == null)
            {
                result.AddError("Binding scope is null.");
                return result;
            }

            Dictionary<string, UIBindingInfo> byProperty =
                new Dictionary<string, UIBindingInfo>(StringComparer.Ordinal);
            ScanTransform(scope.transform, scope.transform, result, byProperty);

            if (result.Bindings.Count == 0)
            {
                result.AddInfo("Prefab has no bindable nodes.");
            }

            return result;
        }

        /// <summary>
        /// 深度遍历层级。遇到子 Scope 会停止继续向下扫描，除非该节点本身需要作为子 Scope 绑定。
        /// </summary>
        private static void ScanTransform(
            Transform root,
            Transform current,
            UIBindingScanResult result,
            Dictionary<string, UIBindingInfo> byProperty)
        {
            bool isRoot = current == root;
            if (!isRoot && TryCollectChildScope(root, current, result, byProperty))
            {
                return;
            }

            if (!isRoot && current.name.StartsWith(UIBindingRuleSet.BindingPrefix, StringComparison.Ordinal))
            {
                UIBindingInfo binding = CreateBindingInfo(root, current);
                AddBinding(result, byProperty, binding);
            }

            for (int i = 0; i < current.childCount; i++)
            {
                ScanTransform(root, current.GetChild(i), result, byProperty);
            }
        }

        /// <summary>
        /// 判断当前节点是否是子绑定 Scope。
        /// 子 Scope 不带 B_ 时作为扫描边界跳过；带 B_ 时只绑定 Scope 组件本身。
        /// </summary>
        private static bool TryCollectChildScope(
            Transform root,
            Transform current,
            UIBindingScanResult result,
            Dictionary<string, UIBindingInfo> byProperty)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            MonoBehaviour foundScope = null;
            int scopeCount = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour is IUIBindingScope)
                {
                    foundScope = behaviour;
                    scopeCount++;
                }
            }

            if (scopeCount <= 0)
            {
                return false;
            }

            if (scopeCount > 1)
            {
                result.AddError("Node has multiple IUIBindingScope components: " + GetPath(root, current));
                return true;
            }

            if (!current.name.StartsWith(UIBindingRuleSet.BindingPrefix, StringComparison.Ordinal))
            {
                result.AddInfo("Child scope skipped as scan boundary: " + GetPath(root, current));
                return true;
            }

            UIBindingInfo binding = CreateScopeBindingInfo(root, current, foundScope);
            AddBinding(result, byProperty, binding);
            return true;
        }

        /// <summary>
        /// 为普通 B_ 节点创建绑定信息，并根据组件规则选择最合适的目标对象。
        /// </summary>
        private static UIBindingInfo CreateBindingInfo(Transform root, Transform target)
        {
            UIBindingInfo info = new UIBindingInfo();
            info.NodeName = target.name;
            info.NodePath = GetPath(root, target);
            info.BindingPrefix = UIBindingRuleSet.BindingPrefix;
            info.RawName = target.name.Substring(UIBindingRuleSet.BindingPrefix.Length);

            ComponentMatch match = FindBestComponent(target);
            if (match.Target == null)
            {
                ApplyGameObjectFallback(info, target.gameObject);
            }
            else
            {
                ApplyComponentMatch(info, match);
            }

            return info;
        }

        /// <summary>
        /// 为带 B_ 的子 Scope 创建绑定信息。
        /// 父级只持有子 Scope 组件，不直接触达子 Scope 内部控件。
        /// </summary>
        private static UIBindingInfo CreateScopeBindingInfo(
            Transform root,
            Transform target,
            MonoBehaviour scope)
        {
            UIBindingInfo info = new UIBindingInfo();
            info.NodeName = target.name;
            info.NodePath = GetPath(root, target);
            info.BindingPrefix = UIBindingRuleSet.BindingPrefix;
            info.RawName = target.name.Substring(UIBindingRuleSet.BindingPrefix.Length);
            info.TargetObject = scope;
            info.TargetType = scope.GetType();
            info.TargetKind = UIBindingTargetKind.BindingScope;
            info.CodeTypeName = GetCodeTypeName(scope.GetType());
            info.IsValid = true;
            ApplyNames(info, "Group");
            return info;
        }

        /// <summary>
        /// 在节点所有组件中选择功能优先级最高的可绑定组件。
        /// </summary>
        private static ComponentMatch FindBestComponent(Transform target)
        {
            Component[] components = target.GetComponents<Component>();
            ComponentMatch best = default(ComponentMatch);
            int bestComponentIndex = int.MaxValue;
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                for (int ruleIndex = 0; ruleIndex < UIBindingRuleSet.DefaultComponentRules.Length; ruleIndex++)
                {
                    UIBindingComponentRule rule = UIBindingRuleSet.DefaultComponentRules[ruleIndex];
                    if (!rule.AllowImplicitSelect ||
                        rule.ComponentType == typeof(UIGroupBase) ||
                        !rule.ComponentType.IsAssignableFrom(component.GetType()))
                    {
                        continue;
                    }

                    // 优先级相同则保留组件列表中更靠前的组件，保持结果稳定。
                    if (best.Target == null ||
                        rule.FunctionPriority > best.Rule.FunctionPriority ||
                        rule.FunctionPriority == best.Rule.FunctionPriority && i < bestComponentIndex)
                    {
                        best = new ComponentMatch(component, rule);
                        bestComponentIndex = i;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// 将组件匹配结果写入绑定信息，并补齐生成字段名。
        /// </summary>
        private static void ApplyComponentMatch(UIBindingInfo info, ComponentMatch match)
        {
            info.TargetObject = match.Target;
            info.TargetType = match.Rule.ComponentType;
            info.CodeTypeName = match.Rule.CodeTypeName ?? GetCodeTypeName(match.Target.GetType());
            info.TargetKind = match.Rule.TargetKind;
            info.IsValid = true;
            ApplyNames(info, match.Rule.FieldSuffix);
        }

        /// <summary>
        /// 当没有可绑定组件时，按唯一 fallback 策略绑定节点 GameObject。
        /// 这里故意不生成 Transform/RectTransform，避免同一个 B_ 节点在不同配置下生成不同字段类型和后缀。
        /// </summary>
        private static void ApplyGameObjectFallback(UIBindingInfo info, GameObject target)
        {
            if (!UIBindingRuleSet.DefaultFallbackRule.EnableGameObjectFallback)
            {
                info.IsValid = false;
                info.Error = "Node uses the binding prefix but has no bindable component.";
                return;
            }

            info.TargetObject = target;
            info.TargetType = typeof(GameObject);
            info.CodeTypeName = "UnityEngine.GameObject";
            info.TargetKind = UIBindingTargetKind.GameObject;
            info.IsValid = true;
            ApplyNames(info, "Go");
        }

        /// <summary>
        /// 根据业务名和后缀生成属性名、字段名，并校验标识符合法性。
        /// </summary>
        private static void ApplyNames(UIBindingInfo info, string suffix = null)
        {
            string raw = UIBindingNameUtility.ToPascalIdentifier(info.RawName);
            string propertyName = raw + (suffix ?? string.Empty);
            info.PropertyName = propertyName;
            info.FieldName = "m_" + propertyName;
            if (!UIBindingNameUtility.IsValidIdentifier(propertyName) ||
                !UIBindingNameUtility.IsValidIdentifier(info.FieldName))
            {
                info.IsValid = false;
                info.Error = "Binding field name is invalid.";
            }
        }

        /// <summary>
        /// 添加绑定并检查重复属性名。重复名称会导致生成 partial class 编译失败，所以在扫描阶段阻断。
        /// </summary>
        private static void AddBinding(
            UIBindingScanResult result,
            Dictionary<string, UIBindingInfo> byProperty,
            UIBindingInfo binding)
        {
            if (!binding.IsValid)
            {
                result.AddError(binding.NodePath + ": " + binding.Error);
                return;
            }

            UIBindingInfo existing;
            if (byProperty.TryGetValue(binding.PropertyName, out existing))
            {
                result.AddError(
                    "Duplicate binding field name: " + binding.PropertyName + " at " +
                    existing.NodePath + " and " + binding.NodePath);
                return;
            }

            byProperty.Add(binding.PropertyName, binding);
            result.AddBinding(binding);
        }

        /// <summary>
        /// 生成从 Scope 根节点到目标节点的可读路径，用于错误定位。
        /// </summary>
        private static string GetPath(Transform root, Transform target)
        {
            if (root == target)
            {
                return root.name;
            }

            Stack<string> names = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }

            string path = root.name;
            while (names.Count > 0)
            {
                path += "/" + names.Pop();
            }

            return path;
        }

        /// <summary>
        /// 获取可写入 C# 源码的类型全名，嵌套类型使用点号形式。
        /// </summary>
        private static string GetCodeTypeName(Type type)
        {
            return type.FullName != null ? type.FullName.Replace('+', '.') : type.Name;
        }

        /// <summary>
        /// 组件匹配结果快照，避免扫描过程中重复查询组件规则。
        /// </summary>
        private readonly struct ComponentMatch
        {
            /// <summary>
            /// 被选中的组件。
            /// </summary>
            public readonly Component Target;

            /// <summary>
            /// 选中组件对应的匹配规则。
            /// </summary>
            public readonly UIBindingComponentRule Rule;

            /// <summary>
            /// 创建组件匹配结果。
            /// </summary>
            public ComponentMatch(Component target, UIBindingComponentRule rule)
            {
                Target = target;
                Rule = rule;
            }
        }
    }
}
