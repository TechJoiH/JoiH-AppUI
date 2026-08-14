using System.IO;
using Joi.H.AppUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 基于 AssetDatabase 的默认 Prefab 解析器。
    /// 只在 Editor 工具中使用，运行时资源加载仍由 Runtime load strategy 负责。
    /// </summary>
    public sealed class UIBindingAssetDatabaseResolver : IUIEditorPrefabResolver
    {
        /// <summary>
        /// 使用唯一 UIBindingSettings 显式选择的 AssetId resolver 定位 Prefab。
        /// 不对 AssetId 进行路径或文件名降级解释。
        /// </summary>
        public bool TryResolve(IUIDefinition definition, out string assetPath, out string error)
        {
            assetPath = string.Empty;
            error = string.Empty;
            if (definition == null || string.IsNullOrEmpty(definition.PrefabAssetId))
            {
                error = "Definition or PrefabAssetId is empty.";
                return false;
            }

            if (!UIBindingSettingsUtility.TryFindUniqueSettings(
                    out UIBindingSettings settings,
                    out _,
                    out error))
            {
                return false;
            }

            if (!UIEditorAssetIdResolverRegistry.TryGetSelected(
                    settings,
                    out IUIEditorAssetIdResolver resolver,
                    out error))
            {
                return false;
            }

            return resolver.TryResolveAssetPath(
                definition.PrefabAssetId,
                out assetPath,
                out error);
        }

        /// <summary>
        /// 从当前选择对象解析 Prefab 资产路径；Prefab 实例会回溯到最近的源 Prefab。
        /// </summary>
        public bool TryResolve(GameObject selectedPrefab, out string assetPath, out string error)
        {
            assetPath = string.Empty;
            error = string.Empty;
            if (selectedPrefab == null)
            {
                error = "Selected prefab is null.";
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(selectedPrefab);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return true;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.IsPartOfPrefabContents(selectedPrefab))
            {
                assetPath = prefabStage.assetPath;
                if (!string.IsNullOrEmpty(assetPath))
                {
                    return true;
                }
            }

            assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedPrefab);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return true;
            }

            error = "Cannot resolve prefab asset path.";
            return false;
        }
    }
}
