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
        /// 使用 Definition 的 PrefabAssetId 定位 Prefab。
        /// 支持完整 Assets 路径，也支持按文件名在工程中搜索。
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

            string assetId = definition.PrefabAssetId;
            if (UIEditorAssetIdResolverRegistry.Current.TryResolveAssetPath(
                    assetId,
                    out assetPath,
                    out _))
            {
                return true;
            }

            if (assetId.StartsWith("Assets/") && AssetDatabase.LoadAssetAtPath<GameObject>(assetId) != null)
            {
                assetPath = assetId;
                return true;
            }

            string[] guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(assetId) + " t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                // Resources id 通常不带扩展名，因此这里按文件名匹配，避免路径差异导致解析失败。
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetFileNameWithoutExtension(path) == Path.GetFileNameWithoutExtension(assetId))
                {
                    assetPath = path;
                    return true;
                }
            }

            error = "Prefab was not found for asset id: " + assetId;
            return false;
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
