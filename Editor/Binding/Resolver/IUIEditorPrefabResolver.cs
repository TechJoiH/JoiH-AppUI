using Joi.H.AppUI;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Editor 下的 Prefab 路径解析接口。
    /// Definition 同步和校验通过它把资源 ID 或当前选择对象解析为 AssetDatabase 路径。
    /// </summary>
    public interface IUIEditorPrefabResolver
    {
        /// <summary>
        /// 根据 Definition 中的 PrefabAssetId 查找对应 Prefab 资产路径。
        /// </summary>
        bool TryResolve(IUIDefinition definition, out string assetPath, out string error);

        /// <summary>
        /// 根据当前选择的 Prefab 或 Prefab 实例查找源 Prefab 资产路径。
        /// </summary>
        bool TryResolve(GameObject selectedPrefab, out string assetPath, out string error);
    }
}
