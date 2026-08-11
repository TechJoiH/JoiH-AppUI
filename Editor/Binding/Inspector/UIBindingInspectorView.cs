using Joi.H.AppUI;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// App UI 绑定工具的 Inspector 绘制逻辑。
    /// 所有写操作都来自这里的显式按钮，Validate 按钮保持只读。
    /// </summary>
    public static class UIBindingInspectorView
    {
        /// <summary>
        /// 绘制生成绑定、写回引用、只读校验和 Definition 同步入口。
        /// </summary>
        public static void Draw(UIBindingScopeBase scope)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("App UI 绑定工具", EditorStyles.boldLabel);

            if (GUILayout.Button(UIBindingInspectorStrings.GenerateButton))
            {
                UIBindingGenerationResult result = UIBindingGenerator.Generate(scope);
                LogGenerationResult(result);
                if (result != null && result.Success)
                {
                    // 只有生成成功后才刷新 AssetDatabase，失败路径不触发额外导入。
                    AssetDatabase.Refresh();
                }
            }

            if (GUILayout.Button(UIBindingInspectorStrings.BindButton))
            {
                UIBindingBindResult result = UIBindingPrefabBinder.Bind(scope);
                LogBindResult(result);
            }

            if (GUILayout.Button(UIBindingInspectorStrings.ValidateButton))
            {
                UIBindingValidationReport report = UIBindingValidator.ValidateScope(scope);
                if (report.HasError)
                {
                    Debug.LogError(report.ToString(), scope);
                }
                else
                {
                    Debug.Log(report.ToString(), scope);
                }
            }

            DrawDefinitionAutomation(scope);

            EditorGUILayout.HelpBox(
                "如果生成字段缺失，请等待 Unity 编译完成后再写回引用。",
                MessageType.Info);
        }

        /// <summary>
        /// 根据当前 Scope 类型显示 Page 或 Group 的 Definition 同步按钮。
        /// </summary>
        private static void DrawDefinitionAutomation(UIBindingScopeBase scope)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(UIBindingInspectorStrings.DefinitionAutomationTitle, EditorStyles.boldLabel);

            if (scope is PanelBaseController)
            {
                if (GUILayout.Button(UIBindingInspectorStrings.SyncPageDefinitionButton))
                {
                    UIDefinitionSyncWindow.Open(scope);
                }

                return;
            }

            if (scope is UIGroupBase)
            {
                if (GUILayout.Button(UIBindingInspectorStrings.SyncGroupDefinitionButton))
                {
                    UIDefinitionSyncWindow.Open(scope);
                }
            }
        }

        /// <summary>
        /// 将生成结果输出到 Console。
        /// </summary>
        private static void LogGenerationResult(UIBindingGenerationResult result)
        {
            if (result == null)
            {
                return;
            }

            for (int i = 0; i < result.Infos.Count; i++)
            {
                Debug.Log(result.Infos[i]);
            }

            for (int i = 0; i < result.Errors.Count; i++)
            {
                Debug.LogError(result.Errors[i]);
            }
        }

        /// <summary>
        /// 将写回结果输出到 Console。
        /// </summary>
        private static void LogBindResult(UIBindingBindResult result)
        {
            if (result == null)
            {
                return;
            }

            for (int i = 0; i < result.Infos.Count; i++)
            {
                Debug.Log(result.Infos[i]);
            }

            for (int i = 0; i < result.Errors.Count; i++)
            {
                Debug.LogError(result.Errors[i]);
            }
        }
    }
}
