using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// App UI 绑定全量校验窗口。
    /// 按只读原则运行 Validate All，只展示报告，不自动修复。
    /// </summary>
    public sealed class UIBindingValidationWindow : EditorWindow
    {
        private Vector2 scroll;
        private string reportText = string.Empty;

        /// <summary>
        /// 打开全量校验窗口。
        /// </summary>
        [MenuItem("Tools/Joi.H AppUI/Binding Validation")]
        public static void Open()
        {
            GetWindow<UIBindingValidationWindow>("UI 绑定校验");
        }

        /// <summary>
        /// 绘制窗口并在用户点击按钮时执行一次只读全量校验。
        /// </summary>
        private void OnGUI()
        {
            if (GUILayout.Button("全量校验"))
            {
                UIBindingValidationReport report =
                    UIBindingValidateAllRunner.ValidateAll(UIBindingSettingsUtility.FindSettings());
                reportText = report.ToString();
                if (string.IsNullOrEmpty(reportText))
                {
                    reportText = "全量校验完成，没有消息。";
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(reportText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}
