using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Serialization;

namespace Joi.H.AppUI
{
    /// <summary>
    /// UI Definition 资产基类。
    /// 保存生成工具写入的只读标识、Prefab 资源 ID 和 Editor 下的 Controller 脚本信息。
    /// </summary>
    public abstract class UIDefinitionAssetBase : ScriptableObject, IUIDefinition
    {
        [SerializeField]
        private string m_DefinitionId;

        [SerializeField]
        [FormerlySerializedAs("m_PrefabResourceId")]
        private string m_PrefabAssetId;

#if UNITY_EDITOR
        [SerializeField]
        private MonoScript m_ControllerScript;

        [SerializeField]
        private string m_ControllerTypeName;
#endif

        /// <summary>Definition 唯一 ID。</summary>
        public string DefinitionId
        {
            get { return m_DefinitionId; }
        }

        /// <summary>Prefab 资源加载 ID。</summary>
        public string PrefabAssetId
        {
            get { return m_PrefabAssetId; }
        }

#if UNITY_EDITOR
        /// <summary>绑定生成时关联的 Controller 脚本。</summary>
        public MonoScript ControllerScript
        {
            get { return m_ControllerScript; }
        }

        /// <summary>Controller 完整类型名，用于同步窗口和校验工具展示。</summary>
        public string ControllerTypeName
        {
            get { return m_ControllerTypeName; }
        }
#endif
    }
}
