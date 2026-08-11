using Joi.H.AppUI;
using UnityEditor;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 负责把扫描出的绑定目标写入 Controller 上由 .Bindings.cs 生成的序列化字段。
    /// </summary>
    public static class UIBindingSerializedFieldWriter
    {
        /// <summary>
        /// 按扫描结果逐个写入 ObjectReference 字段。
        /// 只有字段存在且类型正确时才写入；错误会进入结果对象，便于调用方统一展示。
        /// </summary>
        public static bool TryWrite(
            UIBindingScopeBase scope,
            UIBindingScanResult scanResult,
            UIBindingBindResult bindResult)
        {
            SerializedObject serializedObject = new SerializedObject(scope);
            bool changed = false;

            for (int i = 0; i < scanResult.Bindings.Count; i++)
            {
                UIBindingInfo binding = scanResult.Bindings[i];
                // 字段来自生成代码，找不到通常表示 Unity 还没完成编译或生成文件已过期。
                SerializedProperty property = serializedObject.FindProperty(binding.SerializedFieldName);
                if (property == null)
                {
                    bindResult.AddError(
                        "Generated field was not found. Wait for Unity compilation and retry: " +
                        binding.SerializedFieldName);
                    continue;
                }

                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    bindResult.AddError("Generated field is not an object reference: " + binding.SerializedFieldName);
                    continue;
                }

                if (property.objectReferenceValue != binding.TargetObject)
                {
                    property.objectReferenceValue = binding.TargetObject;
                    changed = true;
                }
            }

            if (changed)
            {
                // 写回引用是用户主动修复动作，因此这里直接提交 SerializedObject 并标记 Dirty。
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(scope);
            }

            return bindResult.Success;
        }
    }
}
