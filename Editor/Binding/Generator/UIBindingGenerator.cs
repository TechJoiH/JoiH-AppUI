using System.IO;
using Joi.H.AppUI;
using UnityEditor;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 绑定代码生成入口。
    /// 该类会真正写出 .Bindings.cs，调用前必须确认这是用户主动触发的生成流程。
    /// </summary>
    public static class UIBindingGenerator
    {
        /// <summary>
        /// 扫描当前 Scope 并生成 partial 绑定文件。
        /// 任一前置检查失败都会直接返回失败结果，不扫描后续写入、不 ImportAsset。
        /// </summary>
        public static UIBindingGenerationResult Generate(UIBindingScopeBase scope)
        {
            // Prefab Mode 保存检查必须在扫描和写文件之前执行，避免基于未保存对象生成错误代码。
            if (!UIBindingPrefabSaveUtility.TrySaveCurrentPrefabModeBeforeWrite(
                    scope,
                    out string prefabSaveError))
            {
                return UIBindingGenerationResult.Fail(null, prefabSaveError);
            }

            UIBindingScanResult scanResult = UIBindingScanner.Scan(scope);
            if (scanResult.HasError)
            {
                return UIBindingGenerationResult.Fail(scanResult, "Scan has errors. Generation stopped.");
            }

            if (!UIBindingOwnershipValidator.TryValidateScanTargets(
                    scope,
                    scanResult,
                    out string ownershipError))
            {
                return UIBindingGenerationResult.Fail(scanResult, ownershipError);
            }

            // 解析 Controller 源文件，并确认它可以与生成文件组成 partial class。
            // Variant 不能用自身扫描结果扩展或破坏 base prefab 的绑定契约；失败时停止写 .Bindings.cs。
            if (!UIBindingVariantValidator.TryValidate(
                    scope,
                    scanResult,
                    out string variantError))
            {
                return UIBindingGenerationResult.Fail(scanResult, variantError);
            }

            if (!UIBindingFileUtility.TryGetSourceInfo(
                    scope,
                    out UIBindingSourceInfo sourceInfo,
                    out string sourceError))
            {
                return UIBindingGenerationResult.Fail(scanResult, sourceError);
            }

            if (!UIBindingFileUtility.TryValidateCompilationBoundary(
                    sourceInfo,
                    out string boundaryError))
            {
                return UIBindingGenerationResult.Fail(scanResult, boundaryError);
            }

            // 覆盖保护最后检查：只有自动生成文件允许被替换。
            if (!UIBindingFileUtility.CanOverwriteGeneratedFile(
                    sourceInfo.GeneratedPath,
                    out string overwriteError))
            {
                return UIBindingGenerationResult.Fail(scanResult, overwriteError);
            }

            // 所有校验完成后才进入磁盘写入和 AssetDatabase 导入。
            string code = UIBindingCodeWriter.Write(sourceInfo, scanResult);
            File.WriteAllText(sourceInfo.GeneratedPath, code, System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(sourceInfo.GeneratedPath);
            return UIBindingGenerationResult.Ok(
                sourceInfo.GeneratedPath,
                scanResult,
                "Bindings file generated: " + sourceInfo.GeneratedPath);
        }
    }
}
