using System;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 单个 B_ 节点扫描后的绑定描述，贯穿代码生成、引用写回和归属校验。
    /// </summary>
    public sealed class UIBindingInfo
    {
        /// <summary>
        /// 节点原始名称。
        /// </summary>
        public string NodeName;

        /// <summary>
        /// 从当前 Scope 根节点到目标节点的相对路径。
        /// </summary>
        public string NodePath;

        /// <summary>
        /// 触发绑定扫描的命名前缀，默认是 B_。
        /// </summary>
        public string BindingPrefix;

        /// <summary>
        /// 去掉 B_ 前缀后的原始业务名称。
        /// </summary>
        public string RawName;

        /// <summary>
        /// 生成的 private 字段名。
        /// </summary>
        public string FieldName;

        /// <summary>
        /// 生成的 protected 属性名。
        /// </summary>
        public string PropertyName;

        /// <summary>
        /// 目标对象的运行时类型。
        /// </summary>
        public Type TargetType;

        /// <summary>
        /// 生成代码中使用的类型名。
        /// </summary>
        public string CodeTypeName;

        /// <summary>
        /// 实际写入序列化字段的 Unity 对象。
        /// </summary>
        public UnityEngine.Object TargetObject;

        /// <summary>
        /// 绑定目标类别。
        /// </summary>
        public UIBindingTargetKind TargetKind;

        /// <summary>
        /// 当前绑定是否可用于生成和写回。
        /// </summary>
        public bool IsValid;

        /// <summary>
        /// 当前绑定无效时的人类可读错误。
        /// </summary>
        public string Error;

        /// <summary>
        /// 对应 SerializedObject 上的字段名，当前与生成字段名保持一致。
        /// </summary>
        public string SerializedFieldName
        {
            get { return FieldName; }
        }
    }
}
