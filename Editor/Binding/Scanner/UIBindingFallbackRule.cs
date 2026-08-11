namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 当带 B_ 前缀的节点没有匹配组件时使用的兜底绑定规则。
    /// 当前工具链只支持 GameObject fallback；Transform/RectTransform 不作为可配置策略，避免生成字段类型漂移。
    /// </summary>
    public sealed class UIBindingFallbackRule
    {
        /// <summary>
        /// 是否允许将节点本身作为 GameObject 绑定。
        /// 关闭后，无匹配组件的 B_ 节点会直接报错，不会退化成 Transform 或 RectTransform。
        /// </summary>
        public bool EnableGameObjectFallback = true;
    }
}
