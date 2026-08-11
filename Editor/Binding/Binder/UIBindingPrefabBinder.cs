using Joi.H.AppUI;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 将扫描到的 B_ 绑定目标写回 Controller 序列化字段的 Editor 工具。
    /// 写入前会先执行保存检查、归属校验和编译边界校验，确保不会把 Prefab 或脚本推入不一致状态。
    /// </summary>
    public static class UIBindingPrefabBinder
    {
        /// <summary>
        /// 执行一次绑定引用写回。该方法是显式写操作，只允许由 Inspector 按钮等用户主动流程调用。
        /// </summary>
        public static UIBindingBindResult Bind(UIBindingScopeBase scope)
        {
            UIBindingBindResult bindResult = new UIBindingBindResult();
            // Prefab Mode 内存在未保存修改时，必须先让 Unity 原生弹窗处理，避免对即将重载的对象写引用。
            if (!UIBindingPrefabSaveUtility.TrySaveCurrentPrefabModeBeforeWrite(
                    scope,
                    out string prefabSaveError))
            {
                bindResult.AddError(prefabSaveError);
                return bindResult;
            }

            UIBindingScanResult scanResult = UIBindingScanner.Scan(scope);
            if (scanResult.HasError)
            {
                for (int i = 0; i < scanResult.Errors.Count; i++)
                {
                    bindResult.AddError(scanResult.Errors[i]);
                }

                return bindResult;
            }

            // 扫描结果必须属于当前 Scope；父 Scope 不能把子 Scope 内部控件写入自己的字段。
            if (!UIBindingOwnershipValidator.TryValidateScanTargets(
                    scope,
                    scanResult,
                    out string ownershipError))
            {
                bindResult.AddError(ownershipError);
                return bindResult;
            }

            // Variant 的引用写回不能修复“删除/替换/新增 B_ 绑定”这种契约破坏，必须先让用户回到 base prefab 修正。
            if (!UIBindingVariantValidator.TryValidate(
                    scope,
                    scanResult,
                    out string variantError))
            {
                bindResult.AddError(variantError);
                return bindResult;
            }

            if (!UIBindingFileUtility.TryGetSourceInfo(
                    scope,
                    out UIBindingSourceInfo sourceInfo,
                    out string sourceError))
            {
                bindResult.AddError(sourceError);
                return bindResult;
            }

            // 手写 Controller 与 .Bindings.cs 必须处于同一 asmdef 边界，否则 partial class 无法合并。
            if (!UIBindingFileUtility.TryValidateCompilationBoundary(
                    sourceInfo,
                    out string boundaryError))
            {
                bindResult.AddError(boundaryError);
                return bindResult;
            }

            // 前置检查全部通过后才真正写入 SerializedObject。
            if (!UIBindingSerializedFieldWriter.TryWrite(scope, scanResult, bindResult))
            {
                return bindResult;
            }

            UIBindingPrefabSaveUtility.Save(scope);
            bindResult.AddInfo("Binding references written.");
            AppendValidation(scope, bindResult);
            return bindResult;
        }

        /// <summary>
        /// 写回后立即执行一次只读校验，把仍需人工处理的问题追加到结果中。
        /// </summary>
        private static void AppendValidation(UIBindingScopeBase scope, UIBindingBindResult bindResult)
        {
            UIBindingValidationReport report = UIBindingValidator.ValidateScope(scope);
            for (int i = 0; i < report.Errors.Count; i++)
            {
                bindResult.AddError(report.Errors[i]);
            }

            for (int i = 0; i < report.Infos.Count; i++)
            {
                bindResult.AddInfo(report.Infos[i]);
            }
        }
    }
}
