using Joi.H.AppUI;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Page Definition 的右键创建菜单。
    /// </summary>
    public static class UIPageDefinitionAssetCreator
    {
        /// <summary>
        /// 基于当前选择 Prefab 创建 UIPageDefinition。
        /// </summary>
        [MenuItem("Assets/App UI/Create Page Definition", priority = 2100)]
        public static void Create()
        {
            string assetId = UIDefinitionEditorUtility.GetSelectedPrefabAssetId();
            UIPageDefinition definition =
                UIDefinitionAssetCreator.CreateDefinition<UIPageDefinition>(
                    string.IsNullOrEmpty(assetId) ? "UIPageDefinition" : assetId + "Definition",
                    assetId);
            EditorUtility.SetDirty(definition);
            Debug.Log("UIPageDefinition created. Fill DefinitionId and PrefabAssetId.", definition);
        }
    }
}
