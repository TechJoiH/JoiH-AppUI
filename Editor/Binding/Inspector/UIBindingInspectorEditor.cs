using Joi.H.AppUI;
using UnityEditor;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// UIBindingScopeBase 的自定义 Inspector，把生成、写回、校验和 Definition 同步按钮挂到 Controller 面板上。
    /// </summary>
    [CustomEditor(typeof(UIBindingScopeBase), true)]
    [CanEditMultipleObjects]
    public sealed class UIBindingInspectorEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 绘制默认 Inspector 后追加 App UI 绑定工具区。
        /// 多选时不执行写操作，避免一次按钮影响多个 Controller。
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets == null || targets.Length != 1)
            {
                EditorGUILayout.HelpBox("UI 绑定工具一次只能操作一个 Controller。", MessageType.Info);
                return;
            }

            UIBindingScopeBase scope = target as UIBindingScopeBase;
            if (scope != null)
            {
                UIBindingInspectorView.Draw(scope);
            }
        }
    }
}
