namespace Joi.H.AppUI
{
    /// <summary>
    /// UI Definition 通用接口。
    /// PageDefinition 和 GroupDefinition 都通过该接口暴露定义 ID 与 prefab 资源 ID。
    /// </summary>
    public interface IUIDefinition
    {
        /// <summary>Definition 唯一 ID，通常由同步工具按 Controller 或 Prefab 生成。</summary>
        string DefinitionId { get; }

        /// <summary>用于加载 prefab 的资源 ID。</summary>
        string PrefabAssetId { get; }
    }
}
