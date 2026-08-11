using System.Collections.Generic;
using Joi.H.AppUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Prefab Variant 绑定契约校验器。
    /// Variant 只能继承 base prefab 定义的 B_ 绑定契约，不允许通过删除、替换组件或新增 B_ 节点改变同一个 Controller 的生成字段集合。
    /// </summary>
    public static class UIBindingVariantValidator
    {
        /// <summary>
        /// 校验 Variant 契约，并把发现的问题追加到报告。
        /// 该入口只读：只扫描 prefab、读取 SerializedObject 和 Prefab source 关系，不写回任何字段或资产。
        /// </summary>
        public static void AppendValidationErrors(
            UIBindingScopeBase scope,
            UIBindingScanResult currentScanResult,
            UIBindingValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            List<string> errors = new List<string>(4);
            AppendValidationErrors(scope, currentScanResult, errors);
            for (int i = 0; i < errors.Count; i++)
            {
                report.AddError(errors[i]);
            }
        }

        /// <summary>
        /// 校验 Variant 契约，供 Generate / Bind 前置检查使用。
        /// 返回 false 时调用方必须停止写文件或写引用，避免把破坏契约的 Variant 固化下来。
        /// </summary>
        public static bool TryValidate(
            UIBindingScopeBase scope,
            UIBindingScanResult currentScanResult,
            out string error)
        {
            List<string> errors = new List<string>(4);
            AppendValidationErrors(scope, currentScanResult, errors);
            if (errors.Count == 0)
            {
                error = string.Empty;
                return true;
            }

            error = string.Join("\n", errors.ToArray());
            return false;
        }

        /// <summary>
        /// 执行 Variant 契约校验的内部入口：非 Variant 直接跳过，Variant 则加载 base prefab 并逐项比较绑定契约。
        /// </summary>
        private static void AppendValidationErrors(
            UIBindingScopeBase scope,
            UIBindingScanResult currentScanResult,
            List<string> errors)
        {
            if (errors == null)
            {
                return;
            }

            if (scope == null)
            {
                errors.Add("Prefab Variant binding contract validation failed: scope is null.");
                return;
            }

            if (currentScanResult == null)
            {
                errors.Add("Prefab Variant binding contract validation failed: scan result is null.");
                return;
            }

            if (!TryGetVariantContext(scope, out VariantContext context, out string contextError))
            {
                if (!string.IsNullOrEmpty(contextError))
                {
                    errors.Add(contextError);
                }

                return;
            }

            if (!UIBindingValidator.TryGetRootScope(
                    context.BaseRoot,
                    out UIBindingScopeBase baseScope,
                    out string baseScopeError))
            {
                errors.Add(
                    "Prefab Variant base prefab has invalid binding scope. Variant: " +
                    context.VariantPath + ", Base: " + context.BasePath + ". " + baseScopeError);
                return;
            }

            if (baseScope.GetType() != scope.GetType())
            {
                errors.Add(
                    "Prefab Variant controller type differs from base prefab. Variant: " +
                    scope.GetType().FullName + ", Base: " + baseScope.GetType().FullName +
                    ". Use the same Controller or create a separate prefab/Definition.");
            }

            UIBindingScanResult baseScanResult = UIBindingScanner.Scan(baseScope);
            AppendBaseScanErrors(baseScanResult, context, errors);
            if (baseScanResult.HasError)
            {
                return;
            }

            // 以序列化字段名作为契约键：同一个 Controller 的字段集合必须由 base prefab 决定。
            Dictionary<string, UIBindingInfo> currentByField = BuildByField(currentScanResult);
            ValidateBaseContract(scope, baseScanResult, currentByField, context, errors);
            ValidateNoExtraVariantBindings(baseScanResult, currentScanResult, context, errors);
        }

        /// <summary>
        /// 逐个检查 base prefab 定义的必需绑定，确认 Variant 中仍有对应节点、类型和序列化引用。
        /// </summary>
        private static void ValidateBaseContract(
            UIBindingScopeBase scope,
            UIBindingScanResult baseScanResult,
            Dictionary<string, UIBindingInfo> currentByField,
            VariantContext context,
            List<string> errors)
        {
            SerializedObject serializedObject = new SerializedObject(scope);
            for (int i = 0; i < baseScanResult.Bindings.Count; i++)
            {
                UIBindingInfo baseBinding = baseScanResult.Bindings[i];
                if (!currentByField.TryGetValue(baseBinding.SerializedFieldName, out UIBindingInfo currentBinding))
                {
                    // base 有而 Variant 扫描不到，通常意味着 inherited B_ 节点被删除、重命名或移出当前 Scope。
                    errors.Add(
                        "Prefab Variant is missing required binding from base prefab: " +
                        baseBinding.PropertyName + " at " + baseBinding.NodePath +
                        ". Variant: " + context.VariantPath + ", Base: " + context.BasePath + ".");
                    continue;
                }

                ValidateBindingShape(baseBinding, currentBinding, context, errors);
                ValidateSourceCorrespondence(baseBinding, currentBinding, context, errors);
                ValidateSerializedField(serializedObject, baseBinding, currentBinding, context, errors);
            }
        }

        /// <summary>
        /// 比较绑定种类和生成代码类型，防止 Variant 把 Button 改成 Toggle、TMP_Text 改成 Image 等。
        /// </summary>
        private static void ValidateBindingShape(
            UIBindingInfo baseBinding,
            UIBindingInfo currentBinding,
            VariantContext context,
            List<string> errors)
        {
            if (baseBinding.TargetKind != currentBinding.TargetKind ||
                baseBinding.CodeTypeName != currentBinding.CodeTypeName)
            {
                errors.Add(
                    "Prefab Variant binding type differs from base prefab: " +
                    baseBinding.PropertyName + ". Expected " +
                    baseBinding.TargetKind + " / " + baseBinding.CodeTypeName +
                    ", current " + currentBinding.TargetKind + " / " + currentBinding.CodeTypeName +
                    ". Variant: " + context.VariantPath + ".");
            }
        }

        /// <summary>
        /// 确认当前绑定目标确实继承自 base 的同一个目标对象，而不是在 Variant 中新增同名 B_ 节点顶替。
        /// </summary>
        private static void ValidateSourceCorrespondence(
            UIBindingInfo baseBinding,
            UIBindingInfo currentBinding,
            VariantContext context,
            List<string> errors)
        {
            Object currentSource = PrefabUtility.GetCorrespondingObjectFromSource(currentBinding.TargetObject);
            if (currentSource != baseBinding.TargetObject)
            {
                errors.Add(
                    "Prefab Variant binding target does not inherit from the base binding target: " +
                    baseBinding.PropertyName + ". Base target: " +
                    DescribeObject(baseBinding.TargetObject) + ", current target: " +
                    DescribeObject(currentBinding.TargetObject) + ". Variant: " + context.VariantPath + ".");
            }
        }

        /// <summary>
        /// 读取 Controller 上的生成字段，确认必需字段存在、非空，并指向 Variant 当前扫描到的目标对象。
        /// </summary>
        private static void ValidateSerializedField(
            SerializedObject serializedObject,
            UIBindingInfo baseBinding,
            UIBindingInfo currentBinding,
            VariantContext context,
            List<string> errors)
        {
            SerializedProperty property = serializedObject.FindProperty(baseBinding.SerializedFieldName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                errors.Add(
                    "Prefab Variant generated binding field is missing or not an object reference: " +
                    baseBinding.SerializedFieldName + ". Variant: " + context.VariantPath + ".");
                return;
            }

            Object currentValue = property.objectReferenceValue;
            if (currentValue == null)
            {
                errors.Add(
                    "Prefab Variant required binding field is null: " +
                    baseBinding.PropertyName + " (" + baseBinding.CodeTypeName + "). Variant: " +
                    context.VariantPath + ".");
                return;
            }

            if (currentValue != currentBinding.TargetObject)
            {
                errors.Add(
                    "Prefab Variant serialized binding field does not match the scanned target: " +
                    baseBinding.PropertyName + ". Expected " +
                    DescribeObject(currentBinding.TargetObject) + ", current " +
                    DescribeObject(currentValue) + ". Variant: " + context.VariantPath + ".");
            }
        }

        /// <summary>
        /// 检查 Variant 是否额外新增了 base 中不存在的 B_ 绑定；新增代码访问点必须先回到 base prefab 定义。
        /// </summary>
        private static void ValidateNoExtraVariantBindings(
            UIBindingScanResult baseScanResult,
            UIBindingScanResult currentScanResult,
            VariantContext context,
            List<string> errors)
        {
            Dictionary<string, UIBindingInfo> baseByField = BuildByField(baseScanResult);
            for (int i = 0; i < currentScanResult.Bindings.Count; i++)
            {
                UIBindingInfo currentBinding = currentScanResult.Bindings[i];
                if (!baseByField.TryGetValue(currentBinding.SerializedFieldName, out UIBindingInfo baseBinding))
                {
                    // Variant 允许新增视觉节点，但 B_ 节点会改变生成字段集合，因此必须报错。
                    errors.Add(
                        "Prefab Variant adds a new B_ binding that is not defined by the base prefab: " +
                        currentBinding.PropertyName + " at " + currentBinding.NodePath +
                        ". Add the binding to the base prefab first or use a separate Controller/Definition. Variant: " +
                        context.VariantPath + ".");
                    continue;
                }

                Object currentSource = PrefabUtility.GetCorrespondingObjectFromSource(currentBinding.TargetObject);
                if (currentSource != baseBinding.TargetObject)
                {
                    errors.Add(
                        "Prefab Variant replaces an inherited B_ binding target instead of using the base target: " +
                        currentBinding.PropertyName + " at " + currentBinding.NodePath +
                        ". Add/rename bindings on the base prefab first. Variant: " + context.VariantPath + ".");
                }
            }
        }

        /// <summary>
        /// 将 base prefab 自身的扫描错误透传到 Variant 报告中，避免在有破损 base 时继续做无意义比较。
        /// </summary>
        private static void AppendBaseScanErrors(
            UIBindingScanResult baseScanResult,
            VariantContext context,
            List<string> errors)
        {
            for (int i = 0; i < baseScanResult.Errors.Count; i++)
            {
                errors.Add(
                    "Prefab Variant base prefab scan has errors. Variant: " +
                    context.VariantPath + ", Base: " + context.BasePath + ". " +
                    baseScanResult.Errors[i]);
            }
        }

        /// <summary>
        /// 以生成字段名建立查找表；字段名是绑定契约的稳定键，比节点路径更贴近 Controller 代码边界。
        /// </summary>
        private static Dictionary<string, UIBindingInfo> BuildByField(UIBindingScanResult scanResult)
        {
            Dictionary<string, UIBindingInfo> result =
                new Dictionary<string, UIBindingInfo>(scanResult.Bindings.Count);
            for (int i = 0; i < scanResult.Bindings.Count; i++)
            {
                UIBindingInfo binding = scanResult.Bindings[i];
                if (!result.ContainsKey(binding.SerializedFieldName))
                {
                    result.Add(binding.SerializedFieldName, binding);
                }
            }

            return result;
        }

        /// <summary>
        /// 判断当前 Scope 是否来自 Prefab Variant，并解析其直接 base prefab 根节点和路径信息。
        /// </summary>
        private static bool TryGetVariantContext(
            UIBindingScopeBase scope,
            out VariantContext context,
            out string error)
        {
            context = default(VariantContext);
            error = string.Empty;

            GameObject variantRoot = scope.gameObject;
            if (PrefabUtility.GetPrefabAssetType(variantRoot) != PrefabAssetType.Variant)
            {
                return false;
            }

            GameObject baseRoot = PrefabUtility.GetCorrespondingObjectFromSource(variantRoot);
            if (baseRoot == null)
            {
                error = "Prefab Variant cannot resolve its base prefab: " + GetPrefabPath(variantRoot);
                return false;
            }

            context = new VariantContext(
                variantRoot,
                baseRoot,
                GetPrefabPath(variantRoot),
                GetPrefabPath(baseRoot));
            return true;
        }

        /// <summary>
        /// 获取用于错误提示的 prefab 路径；Prefab Mode 对象没有直接 AssetPath 时回退到最近 prefab 根路径。
        /// </summary>
        private static string GetPrefabPath(GameObject prefabRoot)
        {
            if (prefabRoot == null)
            {
                return "<null>";
            }

            string path = AssetDatabase.GetAssetPath(prefabRoot);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
            return string.IsNullOrEmpty(path) ? prefabRoot.name : path;
        }

        /// <summary>
        /// 生成对象描述文本，帮助错误信息同时展示对象名和资产路径。
        /// </summary>
        private static string DescribeObject(Object target)
        {
            if (target == null)
            {
                return "<null>";
            }

            string path = AssetDatabase.GetAssetPath(target);
            return string.IsNullOrEmpty(path) ? target.name : target.name + " (" + path + ")";
        }

        /// <summary>
        /// 保存一次 Variant 校验所需的上下文，避免每个比较方法重复解析 prefab source 关系。
        /// </summary>
        private readonly struct VariantContext
        {
            public readonly GameObject VariantRoot;
            public readonly GameObject BaseRoot;
            public readonly string VariantPath;
            public readonly string BasePath;

            public VariantContext(
                GameObject variantRoot,
                GameObject baseRoot,
                string variantPath,
                string basePath)
            {
                VariantRoot = variantRoot;
                BaseRoot = baseRoot;
                VariantPath = variantPath;
                BasePath = basePath;
            }
        }
    }
}
