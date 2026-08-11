using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 绑定作用域基类。
    /// 页面和 Group 都继承它，Editor 绑定工具以该组件作为扫描、生成和写回引用的根节点。
    /// </summary>
    public abstract class UIBindingScopeBase : MonoBehaviour, IUIBindingScope
    {
#if UNITY_EDITOR
        /// <summary>Editor 校验入口，只读检查当前 Scope 的绑定引用状态。</summary>
        public UIBindingValidationResult ValidateBindingsEditor()
        {
            UIBindingValidationResult result = new UIBindingValidationResult();
            ValidateBindingsEditorEx(result);
            return result;
        }

        /// <summary>业务可扩展的 Editor 校验钩子，用于追加自定义绑定检查。</summary>
        protected virtual void ValidateBindingsEditorEx(UIBindingValidationResult result)
        {
        }

        /// <summary>校验必需绑定是否为空，为空时向结果中记录 Missing 错误。</summary>
        protected void ValidateRequiredBinding(
            UIBindingValidationResult result,
            string memberName,
            Object target,
            string expectedType)
        {
            if (target == null)
            {
                result.AddMissing(memberName, expectedType);
            }
        }
#endif
    }
}
