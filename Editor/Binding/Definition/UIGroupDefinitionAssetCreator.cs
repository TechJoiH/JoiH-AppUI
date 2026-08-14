using Joi.H.AppUI;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Group Definition 的右键创建菜单。
    /// </summary>
    public static class UIGroupDefinitionAssetCreator
    {
        /// <summary>
        /// 基于当前选择 Prefab 创建 UIGroupDefinition。
        /// </summary>
        [MenuItem("Assets/App UI/Create Group Definition", priority = 2101)]
        public static void Create()
        {
            if (!UIDefinitionEditorUtility.TryGetSelectedPrefabAssetId(
                    out string assetId,
                    out string error))
            {
                Debug.LogError("<Joi.H.AppUI> " + error);
                UIBindingSettingsProvider.OpenSettings();
                return;
            }

            UIGroupDefinition definition =
                UIDefinitionAssetCreator.CreateDefinition<UIGroupDefinition>(
                    string.IsNullOrEmpty(assetId) ? "UIGroupDefinition" : assetId + "GroupDefinition",
                    assetId);
            EditorUtility.SetDirty(definition);
            Debug.Log("UIGroupDefinition created. Fill DefinitionId and PrefabAssetId.", definition);
        }
    }
}
