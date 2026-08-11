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
        /// Uses the registered editor asset-id resolver for the selected prefab.
        /// Falls back to the file name when the resolver rejects the path.
        /// </summary>
        public static string GetSelectedPrefabAssetId()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(path))
            {
                if (UIEditorAssetIdResolverRegistry.Current.TryGetAssetId(
                        path,
                        out string assetId,
                        out _))
                {
                    return assetId;
                }

                return Path.GetFileNameWithoutExtension(path);
            }

            path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selected);
            if (!string.IsNullOrEmpty(path))
            {
                if (UIEditorAssetIdResolverRegistry.Current.TryGetAssetId(
                        path,
                        out string prefabAssetId,
                        out _))
                {
                    return prefabAssetId;
                }

                return Path.GetFileNameWithoutExtension(path);
            }

            return selected.name;
        }

    }
}
