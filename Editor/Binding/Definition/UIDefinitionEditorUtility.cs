using System.IO;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Definition 创建菜单使用的 Editor 辅助工具。
    /// </summary>
    public static class UIDefinitionEditorUtility
    {
        /// <summary>
        /// 获取当前选择 Prefab 所在目录；无法解析时回退到 Assets。
        /// </summary>
        public static string GetSelectedPrefabDirectory()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return "Assets";
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selected);
            }

            if (string.IsNullOrEmpty(path))
            {
                return "Assets";
            }

            return Path.GetDirectoryName(path).Replace('\\', '/');
        }

        /// <summary>
        /// Uses the project-selected editor asset-id resolver for the selected
        /// prefab. Missing settings or resolver selection is an explicit error.
        /// </summary>
        public static bool TryGetSelectedPrefabAssetId(
            out string assetId,
            out string error)
        {
            assetId = string.Empty;
            error = string.Empty;
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                error = "No prefab is selected.";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                path = PrefabUtility
                    .GetPrefabAssetPathOfNearestInstanceRoot(selected);
            }

            if (string.IsNullOrEmpty(path))
            {
                error = "Cannot resolve the selected prefab asset path.";
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

            return resolver.TryGetAssetId(path, out assetId, out error);
        }

    }
}
