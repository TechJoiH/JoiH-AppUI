namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Prefab 解析器入口，集中暴露默认实现，便于后续替换为 Addressables 或项目自定义解析器。
    /// </summary>
    public static class UIBindingPrefabResolver
    {
        private static readonly IUIEditorPrefabResolver defaultResolver =
            new UIBindingAssetDatabaseResolver();

        /// <summary>
        /// 当前 Editor 工具使用的默认 Prefab 解析器。
        /// </summary>
        public static IUIEditorPrefabResolver DefaultResolver
        {
            get { return defaultResolver; }
        }
    }
}
