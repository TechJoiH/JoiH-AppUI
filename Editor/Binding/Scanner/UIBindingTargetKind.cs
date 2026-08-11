namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 绑定目标的引用类别，用于生成代码和判断引用归属边界。
    /// </summary>
    public enum UIBindingTargetKind
    {
        /// <summary>
        /// 普通 Unity Component 引用。
        /// </summary>
        Component,

        /// <summary>
        /// GameObject 兜底引用。
        /// </summary>
        GameObject,

        /// <summary>
        /// Transform 兜底引用。
        /// 预留类型；当前扫描器不会生成 Transform fallback。
        /// </summary>
        Transform,

        /// <summary>
        /// RectTransform 兜底引用。
        /// 预留类型；当前扫描器不会生成 RectTransform fallback。
        /// </summary>
        RectTransform,

        /// <summary>
        /// 子绑定 Scope 组件本身；父 Scope 只能绑定这个组件，不能越界绑定其内部控件。
        /// </summary>
        BindingScope,
    }
}
