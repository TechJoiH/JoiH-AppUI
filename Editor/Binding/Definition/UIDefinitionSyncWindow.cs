using Joi.H.AppUI;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Definition 同步窗口。
    /// 负责展示草稿、让用户确认业务字段，并在点击应用后调用同步工具写回资产。
    /// </summary>
    public sealed class UIDefinitionSyncWindow : EditorWindow
    {
        private UIDefinitionSyncDraft draft;
        private Vector2 scroll;
        private string selectedSettingsPath = string.Empty;

        /// <summary>
        /// 打开窗口并基于当前 Scope 创建同步草稿。
        /// </summary>
        public static void Open(UIBindingScopeBase scope)
        {
            UIDefinitionSyncWindow window =
                GetWindow<UIDefinitionSyncWindow>("Definition 自动化");
            window.minSize = new Vector2(480f, 520f);
            window.Initialize(scope);
            window.Show();
        }

        /// <summary>
        /// 初始化窗口草稿；每次重新扫描都会重新读取当前资产状态。
        /// </summary>
        private void Initialize(UIBindingScopeBase scope)
        {
            Initialize(scope, selectedSettingsPath);
        }

        private void Initialize(UIBindingScopeBase scope, string settingsAssetPath)
        {
            draft = UIDefinitionSyncUtility.CreateDraft(scope, settingsAssetPath);
            selectedSettingsPath = draft.SettingsAssetPath;
            Repaint();
        }

        /// <summary>
        /// 绘制完整窗口内容。只在“应用同步”按钮处触发写操作。
        /// </summary>
        private void OnGUI()
        {
            if (draft == null)
            {
                EditorGUILayout.HelpBox("未找到可同步的 Controller。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Definition 自动化", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            DrawReadonlyInfo();
            EditorGUILayout.Space(8f);
            DrawErrors();
            EditorGUILayout.Space(8f);
            DrawOptions();
            EditorGUILayout.Space(12f);
            DrawActions();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制自动推导出的只读信息，帮助用户确认 Controller、Prefab 和 Definition 路径。
        /// </summary>
        private void DrawReadonlyInfo()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("类型", draft.KindLabel);
            EditorGUILayout.ObjectField("Controller", draft.Scope, typeof(UIBindingScopeBase), true);
            EditorGUILayout.ObjectField("Controller 脚本", draft.ControllerScript, typeof(MonoScript), false);
            EditorGUILayout.TextField("Controller 类型", draft.ControllerTypeName);
            EditorGUILayout.TextField("DefinitionId", draft.DefinitionId);
            EditorGUILayout.TextField("PrefabAssetId", draft.PrefabAssetId);
            EditorGUILayout.TextField("Prefab 路径", draft.PrefabAssetPath);
            EditorGUILayout.TextField("Definition 路径", draft.DefinitionAssetPath);
            EditorGUILayout.ObjectField("现有 Definition", draft.ExistingDefinition, typeof(UIDefinitionAssetBase), false);
            EditorGUI.EndDisabledGroup();

            DrawSettingsPicker();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("目标 Registry", draft.TargetRegistry, typeof(UnityEngine.Object), false);
            EditorGUILayout.TextField(
                "目标 Registry 名称",
                draft.TargetRegistry != null ? draft.TargetRegistry.name : string.Empty);
            EditorGUILayout.TextField("目标 Registry 路径", draft.TargetRegistryPath);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawSettingsPicker()
        {
            string[] paths = draft.SettingsAssetPaths ?? new string[0];
            if (paths.Length > 0)
            {
                string[] options = new string[paths.Length + 1];
                options[0] = "<选择 UIBindingSettings>";
                for (int i = 0; i < paths.Length; i++)
                {
                    options[i + 1] = paths[i];
                }

                int currentIndex = GetSettingsPopupIndex(paths, draft.SettingsAssetPath);
                int nextIndex = EditorGUILayout.Popup("Binding Settings", currentIndex, options);
                if (nextIndex != currentIndex)
                {
                    string nextPath = nextIndex <= 0 ? string.Empty : paths[nextIndex - 1];
                    Initialize(draft.Scope, nextPath);
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Binding Settings", draft.SettingsAssetPath);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUI.BeginChangeCheck();
            UIBindingSettings selectedSettings = (UIBindingSettings)EditorGUILayout.ObjectField(
                "Binding Settings Asset",
                draft.Settings,
                typeof(UIBindingSettings),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                string nextPath = selectedSettings != null ? AssetDatabase.GetAssetPath(selectedSettings) : string.Empty;
                Initialize(draft.Scope, nextPath);
                GUIUtility.ExitGUI();
            }
        }

        private static int GetSettingsPopupIndex(string[] paths, string selectedPath)
        {
            if (paths == null || string.IsNullOrEmpty(selectedPath))
            {
                return 0;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] == selectedPath)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// 绘制草稿错误或同步前提示。
        /// </summary>
        private void DrawErrors()
        {
            if (!draft.HasError)
            {
                EditorGUILayout.HelpBox(
                    "请确认下面的业务策略字段。点击“应用同步”后才会创建或更新 Definition 并注册到 Registry。",
                    MessageType.Info);
                return;
            }

            for (int i = 0; i < draft.Errors.Count; i++)
            {
                EditorGUILayout.HelpBox(draft.Errors[i], MessageType.Error);
            }
        }

        /// <summary>
        /// 根据 Page/Group 类型绘制可编辑业务字段。
        /// </summary>
        private void DrawOptions()
        {
            EditorGUI.BeginDisabledGroup(draft.HasError);
            if (draft.Kind == UIDefinitionSyncKind.Page)
            {
                DrawPageOptions();
            }
            else
            {
                EditorGUILayout.LabelField("组业务策略", EditorStyles.boldLabel);
                draft.GroupScope = (UIGroupScope)EditorGUILayout.EnumPopup("Scope", draft.GroupScope);
                draft.GroupIsReusable = EditorGUILayout.Toggle("IsReusable", draft.GroupIsReusable);
                draft.GroupIsItemTemplate = EditorGUILayout.Toggle("IsItemTemplate", draft.GroupIsItemTemplate);
                draft.GroupAllowNestedGroup =
                    EditorGUILayout.Toggle("AllowNestedGroup", draft.GroupAllowNestedGroup);
            }

            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// 绘制 Page Definition 的完整业务字段。
        /// </summary>
        private void DrawPageOptions()
        {
            EditorGUILayout.LabelField("页面业务策略", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("基础定位", EditorStyles.boldLabel);
            draft.PageLayerId = (UILayerId)EditorGUILayout.EnumPopup("LayerId", draft.PageLayerId);
            draft.PageCanvasDomain =
                (UICanvasDomain)EditorGUILayout.EnumPopup("CanvasDomain", draft.PageCanvasDomain);
            draft.PageDefaultPriorityOffset =
                EditorGUILayout.IntField("DefaultPriorityOffset", draft.PageDefaultPriorityOffset);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("生命周期", EditorStyles.boldLabel);
            draft.PageScope = (UIPageScope)EditorGUILayout.EnumPopup("Scope", draft.PageScope);
            draft.PageOpenPolicy = (UIOpenPolicy)EditorGUILayout.EnumPopup("OpenPolicy", draft.PageOpenPolicy);
            draft.PageIsCritical = EditorGUILayout.Toggle("IsCritical", draft.PageIsCritical);
            draft.PageIsFullScreen = EditorGUILayout.Toggle("IsFullScreen", draft.PageIsFullScreen);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("输入关闭", EditorStyles.boldLabel);
            draft.PageBlockLowerLayerInput =
                EditorGUILayout.Toggle("BlockLowerLayerInput", draft.PageBlockLowerLayerInput);
            draft.PageCloseOnCancel = EditorGUILayout.Toggle("CloseOnCancel", draft.PageCloseOnCancel);
            draft.PageCloseOnBackgroundClick =
                EditorGUILayout.Toggle("CloseOnBackgroundClick", draft.PageCloseOnBackgroundClick);
            draft.PageRequiresRaycaster = EditorGUILayout.Toggle("RequiresRaycaster", draft.PageRequiresRaycaster);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("运行行为", EditorStyles.boldLabel);
            draft.PageRefreshLanguageOnOpen =
                EditorGUILayout.Toggle("RefreshLanguageOnOpen", draft.PageRefreshLanguageOnOpen);
            draft.PageIsHighFrequency = EditorGUILayout.Toggle("IsHighFrequency", draft.PageIsHighFrequency);
            draft.PageEnableUpdate = EditorGUILayout.Toggle("EnableUpdate", draft.PageEnableUpdate);
            draft.PageEnableLateUpdate = EditorGUILayout.Toggle("EnableLateUpdate", draft.PageEnableLateUpdate);
            draft.PageUpdateWhenPaused = EditorGUILayout.Toggle("UpdateWhenPaused", draft.PageUpdateWhenPaused);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("策略", EditorStyles.boldLabel);
            draft.PageLoadStrategyId = EditorGUILayout.TextField("LoadStrategyId", draft.PageLoadStrategyId);
            draft.PageInstanceStrategyId = EditorGUILayout.TextField(
                "InstanceStrategyId",
                draft.PageInstanceStrategyId);
        }

        /// <summary>
        /// 绘制应用、重新扫描和关闭按钮。
        /// </summary>
        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(draft.HasError);
            if (GUILayout.Button("应用同步"))
            {
                UIBindingValidationReport report = UIDefinitionSyncUtility.Apply(draft);
                LogReport(report);
                if (!report.HasError && draft.Scope != null)
                {
                    Initialize(draft.Scope);
                }
            }

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("重新扫描"))
            {
                Initialize(draft.Scope);
            }

            if (GUILayout.Button("关闭"))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 将同步报告输出到 Console。
        /// </summary>
        private void LogReport(UIBindingValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            string text = report.ToString();
            if (string.IsNullOrEmpty(text))
            {
                text = "Definition 同步完成。";
            }

            if (report.HasError)
            {
                Debug.LogError(text, draft.Scope);
            }
            else
            {
                Debug.Log(text, draft.Scope);
            }
        }
    }
}
