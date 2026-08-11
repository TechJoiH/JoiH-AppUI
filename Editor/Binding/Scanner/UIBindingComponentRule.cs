using System;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 描述一种可绑定组件的匹配规则，包括生成字段后缀、代码类型和组件优先级。
    /// </summary>
    public sealed class UIBindingComponentRule
    {
        /// <summary>
        /// 需要匹配的组件类型，支持派生类型。
        /// </summary>
        public Type ComponentType;

        /// <summary>
        /// 生成属性名时追加的业务后缀，例如 Btn、Txt、Img。
        /// </summary>
        public string FieldSuffix;

        /// <summary>
        /// 生成代码中使用的类型名；为空时使用组件真实类型。
        /// </summary>
        public string CodeTypeName;

        /// <summary>
        /// 绑定目标类别，用于后续归属校验和写回。
        /// </summary>
        public UIBindingTargetKind TargetKind;

        /// <summary>
        /// 功能优先级。一个节点有多个组件时，优先选择更符合业务语义的组件。
        /// </summary>
        public int FunctionPriority;

        /// <summary>
        /// 是否允许扫描器在没有显式指定组件时自动选择该规则。
        /// </summary>
        public bool AllowImplicitSelect;

        /// <summary>
        /// 创建一条默认或自定义绑定匹配规则。
        /// </summary>
        public UIBindingComponentRule(
            Type componentType,
            string fieldSuffix,
            string codeTypeName,
            UIBindingTargetKind targetKind,
            int functionPriority,
            bool allowImplicitSelect)
        {
            ComponentType = componentType;
            FieldSuffix = fieldSuffix;
            CodeTypeName = codeTypeName;
            TargetKind = targetKind;
            FunctionPriority = functionPriority;
            AllowImplicitSelect = allowImplicitSelect;
        }
    }
}
