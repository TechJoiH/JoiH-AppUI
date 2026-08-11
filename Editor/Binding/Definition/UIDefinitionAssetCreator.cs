using System.IO;
using Joi.H.AppUI;
using UnityEditor;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Definition 资产创建工具，供右键菜单和同步流程复用。
    /// </summary>
    public static class UIDefinitionAssetCreator
    {
        /// <summary>
        /// 在当前选择 Prefab 同目录下创建指定类型的 Definition，并写入基础只读字段默认值。
        /// </summary>
        public static T CreateDefinition<T>(string assetName, string defaultId)
            where T : UIDefinitionAssetBase
        {
            string directory = UIDefinitionEditorUtility.GetSelectedPrefabDirectory();
            string safeAssetName = Path.GetFileName(assetName);
            if (string.IsNullOrEmpty(safeAssetName))
            {
                safeAssetName = typeof(T).Name;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(directory, safeAssetName + ".asset").Replace('\\', '/'));
            T asset = ScriptableObjectUtility.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            FillDefaults(asset, defaultId);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            return asset;
        }

        /// <summary>
        /// 通过 SerializedObject 写入基类私有字段，保持 DefinitionId 与 PrefabAssetId 的初始值一致。
        /// </summary>
        private static void FillDefaults(UIDefinitionAssetBase asset, string defaultId)
        {
            if (asset == null || string.IsNullOrEmpty(defaultId))
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty definitionId = serializedObject.FindProperty("m_DefinitionId");
            SerializedProperty prefabAssetId = serializedObject.FindProperty("m_PrefabAssetId");
            if (definitionId != null)
            {
                definitionId.stringValue = defaultId;
            }

            if (prefabAssetId != null)
            {
                prefabAssetId.stringValue = defaultId;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }

    /// <summary>
    /// ScriptableObject 创建包装，隔离 Unity API 直接调用，便于后续测试或替换。
    /// </summary>
    internal static class ScriptableObjectUtility
    {
        /// <summary>
        /// 创建 ScriptableObject 实例。
        /// </summary>
        public static T CreateInstance<T>()
            where T : UnityEngine.ScriptableObject
        {
            return UnityEngine.ScriptableObject.CreateInstance<T>();
        }
    }
}
