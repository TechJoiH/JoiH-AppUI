using System.Collections.Generic;
using System.IO;
using Joi.H.AppUI;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Definition 同步目标类型。
    /// </summary>
    public enum UIDefinitionSyncKind
    {
        /// <summary>
        /// 页面 Definition。
        /// </summary>
        Page,

        /// <summary>
        /// 组 Definition。
        /// </summary>
        Group,
    }

    /// <summary>
    /// Definition 同步窗口使用的临时草稿。
    /// 草稿保存自动推导出的只读信息，以及用户在窗口中修改的业务字段。
    /// </summary>
    public sealed class UIDefinitionSyncDraft
    {
        /// <summary>
        /// 草稿创建阶段发现的错误；存在错误时窗口禁止应用同步。
        /// </summary>
        public readonly List<string> Errors = new List<string>(4);

        /// <summary>
        /// 发起同步的绑定 Scope。
        /// </summary>
        public UIBindingScopeBase Scope;

        /// <summary>
        /// 当前同步的是 Page 还是 Group。
        /// </summary>
        public UIDefinitionSyncKind Kind;

        /// <summary>
        /// 项目级绑定设置。
        /// </summary>
        public UIBindingSettings Settings;

        /// <summary>
        /// Available UIBindingSettings asset paths for the Inspector sync picker.
        /// </summary>
        public string[] SettingsAssetPaths = new string[0];

        /// <summary>
        /// UIBindingSettings asset path used by the Inspector sync flow.
        /// </summary>
        public string SettingsAssetPath = string.Empty;

        /// <summary>
        /// Registry that will receive the synced Definition.
        /// </summary>
        public UnityEngine.Object TargetRegistry;

        /// <summary>
        /// Asset path of the target Registry.
        /// </summary>
        public string TargetRegistryPath = string.Empty;

        /// <summary>
        /// DefinitionId，由 Prefab/Controller 名称推导，不在窗口中手填。
        /// </summary>
        public string DefinitionId = string.Empty;

        /// <summary>
        /// PrefabAssetId，由 Prefab 资源路径推导，不在窗口中手填。
        /// </summary>
        public string PrefabAssetId = string.Empty;

        /// <summary>
        /// 目标 Prefab 资产路径。
        /// </summary>
        public string PrefabAssetPath = string.Empty;

        /// <summary>
        /// Definition 资产路径。
        /// </summary>
        public string DefinitionAssetPath = string.Empty;

        /// <summary>
        /// Controller 对应的 MonoScript。
        /// </summary>
        public MonoScript ControllerScript;

        /// <summary>
        /// Controller 完整类型名。
        /// </summary>
        public string ControllerTypeName = string.Empty;

        /// <summary>
        /// 已存在的 Definition；为空时应用同步会创建新资产。
        /// </summary>
        public UIDefinitionAssetBase ExistingDefinition;

        /// <summary>
        /// Page Definition 可编辑字段快照。
        /// </summary>
        public UILayerId PageLayerId;
        public UICanvasDomain PageCanvasDomain;
        public UIPageScope PageScope;
        public UIOpenPolicy PageOpenPolicy;
        public int PageDefaultPriorityOffset;
        public bool PageIsCritical;
        public bool PageIsFullScreen;
        public bool PageBlockLowerLayerInput;
        public bool PageRefreshLanguageOnOpen;
        public bool PageCloseOnCancel;
        public bool PageCloseOnBackgroundClick;
        public string PageLoadStrategyId = string.Empty;
        public string PageInstanceStrategyId = string.Empty;
        public bool PageIsHighFrequency;
        public bool PageRequiresRaycaster;
        public bool PageEnableUpdate;
        public bool PageEnableLateUpdate;
        public bool PageUpdateWhenPaused;

        /// <summary>
        /// Group Definition 可编辑字段快照。
        /// </summary>
        public UIGroupScope GroupScope;
        public bool GroupIsReusable;
        public bool GroupIsItemTemplate;
        public bool GroupAllowNestedGroup;

        /// <summary>
        /// 草稿是否存在阻断同步的错误。
        /// </summary>
        public bool HasError
        {
            get { return Errors.Count > 0; }
        }

        /// <summary>
        /// 窗口中显示的同步类型名称。
        /// </summary>
        public string KindLabel
        {
            get { return Kind == UIDefinitionSyncKind.Page ? "页面" : "组"; }
        }
    }

    /// <summary>
    /// Definition 同步核心工具。
    /// 负责创建同步草稿、应用用户选择、创建或更新 Definition，并注册到对应 Registry。
    /// </summary>
    public static class UIDefinitionSyncUtility
    {
        /// <summary>
        /// 根据当前 Scope 创建同步草稿。
        /// 该阶段只读取脚本、Prefab、Registry 和已有 Definition，不创建或保存任何资产。
        /// </summary>
        public static UIDefinitionSyncDraft CreateDraft(UIBindingScopeBase scope)
        {
            return CreateDraft(scope, string.Empty);
        }

        public static UIDefinitionSyncDraft CreateDraft(UIBindingScopeBase scope, string settingsAssetPath)
        {
            UIDefinitionSyncDraft draft = new UIDefinitionSyncDraft();
            draft.Scope = scope;
            ResolveSettings(draft, settingsAssetPath);

            if (scope == null)
            {
                draft.Errors.Add("未选中有效的 UI Controller。");
                return draft;
            }

            if (scope is PanelBaseController)
            {
                draft.Kind = UIDefinitionSyncKind.Page;
            }
            else if (scope is UIGroupBase)
            {
                draft.Kind = UIDefinitionSyncKind.Group;
            }
            else
            {
                draft.Errors.Add("Definition 自动化只支持 PanelBaseController 或 UIGroupBase。");
                return draft;
            }

            FillControllerInfo(draft);
            FillPrefabInfo(draft);
            FillDefinitionInfo(draft);
            FillDefaultOptions(draft);
            return draft;
        }

        private static void ResolveSettings(UIDefinitionSyncDraft draft, string settingsAssetPath)
        {
            draft.SettingsAssetPaths = UIBindingSettingsUtility.FindSettingsPaths();
            if (!string.IsNullOrEmpty(settingsAssetPath))
            {
                if (!UIBindingSettingsUtility.TryLoadSettingsAtPath(
                        settingsAssetPath,
                        out draft.Settings,
                        out draft.SettingsAssetPath,
                        out string explicitError))
                {
                    draft.Errors.Add(explicitError);
                }

                return;
            }

            if (draft.SettingsAssetPaths.Length == 0)
            {
                draft.Errors.Add("No UIBindingSettings asset was found. Create one through Create/Joi.H AppUI/Binding Settings.");
                return;
            }

            if (draft.SettingsAssetPaths.Length > 1)
            {
                draft.Errors.Add(
                    "Multiple UIBindingSettings assets were found. Select one in the Definition sync window.");
                return;
            }

            if (!UIBindingSettingsUtility.TryLoadSettingsAtPath(
                    draft.SettingsAssetPaths[0],
                    out draft.Settings,
                    out draft.SettingsAssetPath,
                    out string loadError))
            {
                draft.Errors.Add(loadError);
            }
        }

        /// <summary>
        /// 应用同步草稿。
        /// 为避免窗口打开后资产状态变化，应用前会重新创建最新草稿，再复制用户在窗口中选择的业务字段。
        /// </summary>
        public static UIBindingValidationReport Apply(UIDefinitionSyncDraft draft)
        {
            UIBindingValidationReport report = new UIBindingValidationReport();
            if (draft == null || draft.Scope == null)
            {
                report.AddError("同步失败：缺少有效的同步上下文。");
                return report;
            }

            UIDefinitionSyncDraft latest = CreateDraft(draft.Scope, draft.SettingsAssetPath);
            CopyUserOptions(draft, latest);
            if (latest.HasError)
            {
                AppendErrors(latest.Errors, report);
                return report;
            }

            // 根据同步类型创建或更新目标 Definition，具体业务字段由对应分支写入。
            UIDefinitionAssetBase definition = latest.Kind == UIDefinitionSyncKind.Page
                ? SyncPageDefinition(latest, report)
                : SyncGroupDefinition(latest, report);

            if (definition == null || report.HasError)
            {
                return report;
            }

            // 通用字段统一通过 SerializedObject 写入，避免直接访问基类私有序列化字段。
            WriteCommonDefinitionFields(definition, latest);
            EditorUtility.SetDirty(definition);

            if (latest.Kind == UIDefinitionSyncKind.Page)
            {
                RegisterPageDefinition(latest.Settings.PageDefinitionRegistry, (UIPageDefinition)definition, report);
            }
            else
            {
                RegisterGroupDefinition(latest.Settings.GroupDefinitionRegistry, (UIGroupDefinition)definition, report);
            }

            AssetDatabase.SaveAssets();
            AppendBindingValidation(latest.Scope, report);
            AppendSetupValidation(latest, definition, report);
            return report;
        }

        /// <summary>
        /// 填充 Controller 脚本、类型名和默认 Definition 资产路径。
        /// </summary>
        private static void FillControllerInfo(UIDefinitionSyncDraft draft)
        {
            draft.ControllerScript = MonoScript.FromMonoBehaviour(draft.Scope);
            draft.ControllerTypeName = draft.Scope.GetType().FullName;

            if (draft.ControllerScript == null)
            {
                draft.Errors.Add("无法定位 Controller 脚本。");
            }

            if (!UIBindingFileUtility.TryGetSourceInfo(
                    draft.Scope,
                    out UIBindingSourceInfo sourceInfo,
                    out string sourceError))
            {
                draft.Errors.Add(sourceError);
                return;
            }

            string definitionId = RemoveSuffix(sourceInfo.TypeName, "Controller");
            draft.DefinitionId = string.IsNullOrEmpty(definitionId) ? sourceInfo.TypeName : definitionId;
            string scriptDirectory = Path.GetDirectoryName(sourceInfo.ScriptPath);
            draft.DefinitionAssetPath = Path.Combine(
                    scriptDirectory,
                    "Definitions",
                    draft.DefinitionId + ".asset")
                .Replace('\\', '/');
        }

        /// <summary>
        /// 填充 Prefab 路径与 PrefabAssetId，并确认 Prefab 根 Scope 与当前 Controller 类型一致。
        /// </summary>
        private static void FillPrefabInfo(UIDefinitionSyncDraft draft)
        {
            if (!UIBindingPrefabResolver.DefaultResolver.TryResolve(
                    draft.Scope.gameObject,
                    out string prefabPath,
                    out string prefabError))
            {
                draft.Errors.Add(prefabError);
                return;
            }

            draft.PrefabAssetPath = prefabPath.Replace('\\', '/');
            string prefabName = Path.GetFileNameWithoutExtension(draft.PrefabAssetPath);
            if (!string.IsNullOrEmpty(prefabName))
            {
                draft.DefinitionId = prefabName;
            }

            if (!UIEditorAssetIdResolverRegistry.TryGetSelected(
                    draft.Settings,
                    out IUIEditorAssetIdResolver resolver,
                    out string resolverError))
            {
                draft.Errors.Add(resolverError);
            }
            else if (!resolver.TryGetAssetId(
                         draft.PrefabAssetPath,
                         out string assetId,
                         out string assetIdError))
            {
                draft.Errors.Add(assetIdError);
            }
            else
            {
                draft.PrefabAssetId = assetId;
            }

            // 同步工具只允许根 Controller 与当前选择对象一致，避免把子对象误同步成根 Definition。
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(draft.PrefabAssetPath);
            UIBindingPrefabKind expectedKind =
                draft.Kind == UIDefinitionSyncKind.Page ? UIBindingPrefabKind.Page : UIBindingPrefabKind.Group;
            if (!UIBindingValidator.TryGetRootScope(prefab, expectedKind, out UIBindingScopeBase rootScope, out string scopeError))
            {
                draft.Errors.Add(scopeError);
                return;
            }

            if (rootScope.GetType() != draft.Scope.GetType())
            {
                draft.Errors.Add("Prefab root 上的 Controller 类型与当前选中对象不一致。");
            }
        }

        /// <summary>
        /// 在 Registry 或目标路径中查找已存在的 Definition。
        /// 找不到时不创建资产，只把路径留给 Apply 阶段处理。
        /// </summary>
        private static void FillDefinitionInfo(UIDefinitionSyncDraft draft)
        {
            if (draft.Settings == null)
            {
                return;
            }

            if (draft.Kind == UIDefinitionSyncKind.Page)
            {
                UIPageDefinitionRegistry registry = draft.Settings.PageDefinitionRegistry;
                draft.TargetRegistry = registry;
                draft.TargetRegistryPath = registry != null ? AssetDatabase.GetAssetPath(registry) : string.Empty;
                if (registry == null)
                {
                    draft.Errors.Add("未配置 UIPageDefinitionRegistry，无法注册页面定义。");
                    return;
                }

                draft.ExistingDefinition = FindPageDefinition(registry, draft.DefinitionId, draft.PrefabAssetId, draft.Errors);
                if (draft.ExistingDefinition == null)
                {
                    draft.ExistingDefinition = LoadDefinitionAtPath<UIPageDefinition>(draft.DefinitionAssetPath, draft.Errors);
                }
            }
            else
            {
                UIGroupDefinitionRegistry registry = draft.Settings.GroupDefinitionRegistry;
                draft.TargetRegistry = registry;
                draft.TargetRegistryPath = registry != null ? AssetDatabase.GetAssetPath(registry) : string.Empty;
                if (registry == null)
                {
                    draft.Errors.Add("未配置 UIGroupDefinitionRegistry，无法注册组定义。");
                    return;
                }

                draft.ExistingDefinition = FindGroupDefinition(registry, draft.DefinitionId, draft.PrefabAssetId, draft.Errors);
                if (draft.ExistingDefinition == null)
                {
                    draft.ExistingDefinition = LoadDefinitionAtPath<UIGroupDefinition>(draft.DefinitionAssetPath, draft.Errors);
                }
            }
        }

        /// <summary>
        /// 填充窗口业务字段默认值。
        /// 已有 Definition 优先读取资产当前值；新建 Definition 使用设置资产里的枚举默认值和类型默认值。
        /// </summary>
        private static void FillDefaultOptions(UIDefinitionSyncDraft draft)
        {
            UIBindingSettings settings = draft.Settings;
            if (draft.Kind == UIDefinitionSyncKind.Page)
            {
                UIPageDefinition existing = draft.ExistingDefinition as UIPageDefinition;
                if (existing != null)
                {
                    draft.PageLayerId = existing.LayerId;
                    draft.PageCanvasDomain = existing.CanvasDomain;
                    draft.PageScope = existing.Scope;
                    draft.PageOpenPolicy = existing.OpenPolicy;
                    draft.PageDefaultPriorityOffset = existing.DefaultPriorityOffset;
                    draft.PageIsCritical = existing.IsCritical;
                    draft.PageIsFullScreen = existing.IsFullScreen;
                    draft.PageBlockLowerLayerInput = existing.BlockLowerLayerInput;
                    draft.PageRefreshLanguageOnOpen = existing.RefreshLanguageOnOpen;
                    draft.PageCloseOnCancel = existing.CloseOnCancel;
                    draft.PageCloseOnBackgroundClick = existing.CloseOnBackgroundClick;
                    draft.PageLoadStrategyId = existing.LoadStrategyId ?? string.Empty;
                    draft.PageInstanceStrategyId = existing.InstanceStrategyId ?? string.Empty;
                    draft.PageIsHighFrequency = existing.IsHighFrequency;
                    draft.PageRequiresRaycaster = existing.RequiresRaycaster;
                    draft.PageEnableUpdate = existing.EnableUpdate;
                    draft.PageEnableLateUpdate = existing.EnableLateUpdate;
                    draft.PageUpdateWhenPaused = existing.UpdateWhenPaused;
                    return;
                }

                draft.PageLayerId = settings != null ? settings.DefaultPageLayerId : UILayerId.PopupLayer;
                draft.PageCanvasDomain = settings != null ? settings.DefaultPageCanvasDomain : UICanvasDomain.Overlay;
                draft.PageScope = settings != null ? settings.DefaultPageScope : UIPageScope.SceneScope;
                draft.PageOpenPolicy = settings != null ? settings.DefaultPageOpenPolicy : UIOpenPolicy.RefreshExisting;
                draft.PageLoadStrategyId = string.Empty;
                draft.PageInstanceStrategyId = string.Empty;
                return;
            }

            UIGroupDefinition group = draft.ExistingDefinition as UIGroupDefinition;
            if (group != null)
            {
                draft.GroupScope = group.Scope;
                draft.GroupIsReusable = group.IsReusable;
                draft.GroupIsItemTemplate = group.IsItemTemplate;
                draft.GroupAllowNestedGroup = group.AllowNestedGroup;
                return;
            }

            draft.GroupScope = settings != null ? settings.DefaultGroupScope : UIGroupScope.Reusable;
            draft.GroupIsReusable = settings == null || settings.DefaultGroupIsReusable;
            draft.GroupIsItemTemplate = settings != null && settings.DefaultGroupIsItemTemplate;
            draft.GroupAllowNestedGroup = settings == null || settings.DefaultGroupAllowNestedGroup;
        }

        /// <summary>
        /// 创建或更新 Page Definition，并写入页面专属业务字段。
        /// </summary>
        private static UIPageDefinition SyncPageDefinition(
            UIDefinitionSyncDraft draft,
            UIBindingValidationReport report)
        {
            UIPageDefinition definition = ResolveOrCreateDefinition<UIPageDefinition>(draft, report);
            if (definition == null)
            {
                return null;
            }

            definition.LayerId = draft.PageLayerId;
            definition.CanvasDomain = draft.PageCanvasDomain;
            definition.Scope = draft.PageScope;
            definition.OpenPolicy = draft.PageOpenPolicy;
            definition.DefaultPriorityOffset = draft.PageDefaultPriorityOffset;
            definition.IsCritical = draft.PageIsCritical;
            definition.IsFullScreen = draft.PageIsFullScreen;
            definition.BlockLowerLayerInput = draft.PageBlockLowerLayerInput;
            definition.RefreshLanguageOnOpen = draft.PageRefreshLanguageOnOpen;
            definition.CloseOnCancel = draft.PageCloseOnCancel;
            definition.CloseOnBackgroundClick = draft.PageCloseOnBackgroundClick;
            definition.LoadStrategyId = draft.PageLoadStrategyId ?? string.Empty;
            definition.InstanceStrategyId = draft.PageInstanceStrategyId ?? string.Empty;
            definition.IsHighFrequency = draft.PageIsHighFrequency;
            definition.RequiresRaycaster = draft.PageRequiresRaycaster;
            definition.EnableUpdate = draft.PageEnableUpdate;
            definition.EnableLateUpdate = draft.PageEnableLateUpdate;
            definition.UpdateWhenPaused = draft.PageUpdateWhenPaused;
            report.AddInfo("页面定义已同步：" + AssetDatabase.GetAssetPath(definition));
            return definition;
        }

        /// <summary>
        /// 创建或更新 Group Definition，并写入组专属业务字段。
        /// </summary>
        private static UIGroupDefinition SyncGroupDefinition(
            UIDefinitionSyncDraft draft,
            UIBindingValidationReport report)
        {
            UIGroupDefinition definition = ResolveOrCreateDefinition<UIGroupDefinition>(draft, report);
            if (definition == null)
            {
                return null;
            }

            definition.Scope = draft.GroupScope;
            definition.IsReusable = draft.GroupIsReusable;
            definition.IsItemTemplate = draft.GroupIsItemTemplate;
            definition.AllowNestedGroup = draft.GroupAllowNestedGroup;
            report.AddInfo("组定义已同步：" + AssetDatabase.GetAssetPath(definition));
            return definition;
        }

        /// <summary>
        /// 解析已有 Definition，或在目标路径创建新 Definition 资产。
        /// </summary>
        private static T ResolveOrCreateDefinition<T>(
            UIDefinitionSyncDraft draft,
            UIBindingValidationReport report)
            where T : UIDefinitionAssetBase
        {
            T existing = draft.ExistingDefinition as T;
            if (existing != null)
            {
                return existing;
            }

            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(draft.DefinitionAssetPath);
            if (mainAsset != null)
            {
                report.AddError("目标路径已存在非目标类型资产，已停止同步：" + draft.DefinitionAssetPath);
                return null;
            }

            string directory = Path.GetDirectoryName(draft.DefinitionAssetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                // Definition 默认放在 Controller 同目录的 Definitions 子目录下，缺失时按路径逐级创建。
                EnsureAssetFolder(directory.Replace('\\', '/'));
            }

            T definition = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(definition, draft.DefinitionAssetPath);
            report.AddInfo("已创建 Definition 资产：" + draft.DefinitionAssetPath);
            return definition;
        }

        /// <summary>
        /// 写入 Definition 基类中的通用字段。
        /// </summary>
        private static void WriteCommonDefinitionFields(
            UIDefinitionAssetBase definition,
            UIDefinitionSyncDraft draft)
        {
            SerializedObject serializedObject = new SerializedObject(definition);
            SetString(serializedObject, "m_DefinitionId", draft.DefinitionId);
            SetString(serializedObject, "m_PrefabAssetId", draft.PrefabAssetId);
            SetObject(serializedObject, "m_ControllerScript", draft.ControllerScript);
            SetString(serializedObject, "m_ControllerTypeName", draft.ControllerTypeName);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 将 Page Definition 注册到页面 Registry，并在有新增时重建运行时查询索引。
        /// </summary>
        private static void RegisterPageDefinition(
            UIPageDefinitionRegistry registry,
            UIPageDefinition definition,
            UIBindingValidationReport report)
        {
            if (AddDefinitionToRegistry(registry, "m_Pages", definition, report))
            {
                registry.RebuildIndex();
            }
        }

        /// <summary>
        /// 将 Group Definition 注册到组 Registry，并在有新增时重建运行时查询索引。
        /// </summary>
        private static void RegisterGroupDefinition(
            UIGroupDefinitionRegistry registry,
            UIGroupDefinition definition,
            UIBindingValidationReport report)
        {
            if (AddDefinitionToRegistry(registry, "m_Groups", definition, report))
            {
                registry.RebuildIndex();
            }
        }

        /// <summary>
        /// 向 Registry 序列化数组追加 Definition。
        /// 已经存在时不重复添加，但仍视为成功。
        /// </summary>
        private static bool AddDefinitionToRegistry(
            Object registry,
            string listPropertyName,
            UIDefinitionAssetBase definition,
            UIBindingValidationReport report)
        {
            SerializedObject serializedObject = new SerializedObject(registry);
            SerializedProperty list = serializedObject.FindProperty(listPropertyName);
            if (list == null || !list.isArray)
            {
                report.AddError("Registry 结构不符合预期：" + listPropertyName);
                return false;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                if (item.objectReferenceValue == definition)
                {
                    report.AddInfo("Registry 已包含该 Definition，跳过重复注册。");
                    return true;
                }
            }

            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = definition;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
            report.AddInfo("已注册到 Registry：" + registry.name);
            return true;
        }

        /// <summary>
        /// 按 DefinitionId 和 PrefabAssetId 在页面 Registry 中查找唯一匹配项。
        /// </summary>
        private static UIPageDefinition FindPageDefinition(
            UIPageDefinitionRegistry registry,
            string definitionId,
            string prefabAssetId,
            List<string> errors)
        {
            UIPageDefinition found = null;
            for (int i = 0; i < registry.Pages.Count; i++)
            {
                UIPageDefinition page = registry.Pages[i];
                if (page == null || page.DefinitionId != definitionId)
                {
                    continue;
                }

                if (!IsSameOrEmptyAssetId(page.PrefabAssetId, prefabAssetId))
                {
                    errors.Add("Registry 中存在相同 DefinitionId 但 PrefabAssetId 不同的页面定义：" + definitionId);
                    continue;
                }

                if (found != null && found != page)
                {
                    errors.Add("Registry 中存在重复页面 DefinitionId：" + definitionId);
                    continue;
                }

                found = page;
            }

            return found;
        }

        /// <summary>
        /// 按 DefinitionId 和 PrefabAssetId 在组 Registry 中查找唯一匹配项。
        /// </summary>
        private static UIGroupDefinition FindGroupDefinition(
            UIGroupDefinitionRegistry registry,
            string definitionId,
            string prefabAssetId,
            List<string> errors)
        {
            UIGroupDefinition found = null;
            for (int i = 0; i < registry.Groups.Count; i++)
            {
                UIGroupDefinition group = registry.Groups[i];
                if (group == null || group.DefinitionId != definitionId)
                {
                    continue;
                }

                if (!IsSameOrEmptyAssetId(group.PrefabAssetId, prefabAssetId))
                {
                    errors.Add("Registry 中存在相同 DefinitionId 但 PrefabAssetId 不同的组定义：" + definitionId);
                    continue;
                }

                if (found != null && found != group)
                {
                    errors.Add("Registry 中存在重复组 DefinitionId：" + definitionId);
                    continue;
                }

                found = group;
            }

            return found;
        }

        /// <summary>
        /// 从指定路径读取 Definition，并校验资产类型。
        /// </summary>
        private static T LoadDefinitionAtPath<T>(string assetPath, List<string> errors)
            where T : UIDefinitionAssetBase
        {
            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset == null)
            {
                return null;
            }

            T typed = mainAsset as T;
            if (typed == null)
            {
                errors.Add("目标 Definition 路径已存在其他类型资产：" + assetPath);
            }

            return typed;
        }

        /// <summary>
        /// 同步完成后执行一次绑定只读校验，把残留问题追加到报告里。
        /// </summary>
        private static void AppendBindingValidation(
            UIBindingScopeBase scope,
            UIBindingValidationReport report)
        {
            UIBindingValidationReport bindingReport = UIBindingValidator.ValidateScope(scope);
            for (int i = 0; i < bindingReport.Errors.Count; i++)
            {
                report.AddError(bindingReport.Errors[i]);
            }

            for (int i = 0; i < bindingReport.Infos.Count; i++)
            {
                report.AddInfo(bindingReport.Infos[i]);
            }
        }

        /// <summary>
        /// 校验同步结果是否真正写入 Definition，并确认 Registry 包含该资产。
        /// </summary>
        private static void AppendSetupValidation(
            UIDefinitionSyncDraft draft,
            UIDefinitionAssetBase definition,
            UIBindingValidationReport report)
        {
            if (definition.DefinitionId != draft.DefinitionId)
            {
                report.AddError("DefinitionId 写入后与预期不一致。");
            }

            if (definition.PrefabAssetId != draft.PrefabAssetId)
            {
                report.AddError("PrefabAssetId 写入后与预期不一致。");
            }

            if (draft.Kind == UIDefinitionSyncKind.Page)
            {
                if (!ContainsDefinition(draft.Settings.PageDefinitionRegistry.Pages, (UIPageDefinition)definition))
                {
                    report.AddError("UIPageDefinitionRegistry 未包含同步后的 Definition。");
                }

                return;
            }

            if (!ContainsDefinition(draft.Settings.GroupDefinitionRegistry.Groups, (UIGroupDefinition)definition))
            {
                report.AddError("UIGroupDefinitionRegistry 未包含同步后的 Definition。");
            }
        }

        /// <summary>
        /// 判断 Registry 列表是否包含目标 Definition。
        /// </summary>
        private static bool ContainsDefinition<T>(IReadOnlyList<T> definitions, T definition)
            where T : UIDefinitionAssetBase
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] == definition)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 将窗口里用户修改过的业务字段复制到最新草稿。
        /// 这样可以在应用前重新读取资产状态，同时保留用户在窗口中的选择。
        /// </summary>
        private static void CopyUserOptions(UIDefinitionSyncDraft source, UIDefinitionSyncDraft target)
        {
            target.PageLayerId = source.PageLayerId;
            target.PageCanvasDomain = source.PageCanvasDomain;
            target.PageScope = source.PageScope;
            target.PageOpenPolicy = source.PageOpenPolicy;
            target.PageDefaultPriorityOffset = source.PageDefaultPriorityOffset;
            target.PageIsCritical = source.PageIsCritical;
            target.PageIsFullScreen = source.PageIsFullScreen;
            target.PageBlockLowerLayerInput = source.PageBlockLowerLayerInput;
            target.PageRefreshLanguageOnOpen = source.PageRefreshLanguageOnOpen;
            target.PageCloseOnCancel = source.PageCloseOnCancel;
            target.PageCloseOnBackgroundClick = source.PageCloseOnBackgroundClick;
            target.PageLoadStrategyId = source.PageLoadStrategyId;
            target.PageInstanceStrategyId = source.PageInstanceStrategyId;
            target.PageIsHighFrequency = source.PageIsHighFrequency;
            target.PageRequiresRaycaster = source.PageRequiresRaycaster;
            target.PageEnableUpdate = source.PageEnableUpdate;
            target.PageEnableLateUpdate = source.PageEnableLateUpdate;
            target.PageUpdateWhenPaused = source.PageUpdateWhenPaused;
            target.GroupScope = source.GroupScope;
            target.GroupIsReusable = source.GroupIsReusable;
            target.GroupIsItemTemplate = source.GroupIsItemTemplate;
            target.GroupAllowNestedGroup = source.GroupAllowNestedGroup;
        }

        /// <summary>
        /// 将草稿错误追加到统一校验报告。
        /// </summary>
        private static void AppendErrors(List<string> errors, UIBindingValidationReport report)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                report.AddError(errors[i]);
            }
        }

        /// <summary>
        /// 安全写入 SerializedObject 字符串字段。
        /// </summary>
        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        /// <summary>
        /// 安全写入 SerializedObject 对象引用字段。
        /// </summary>
        private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        /// <summary>
        /// 确保目标资产目录存在。只处理 Assets 下的项目路径。
        /// </summary>
        private static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                return;
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        /// <summary>
        /// 判断已有资源 ID 是否为空或与预期一致。
        /// 为空视为兼容，方便旧资产逐步补齐。
        /// </summary>
        private static bool IsSameOrEmptyAssetId(string existing, string expected)
        {
            return string.IsNullOrEmpty(existing) || existing == expected;
        }

        /// <summary>
        /// 从字符串末尾移除指定后缀，用于由 Controller 类型名推导 DefinitionId。
        /// </summary>
        private static string RemoveSuffix(string value, string suffix)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(suffix) || !value.EndsWith(suffix))
            {
                return value;
            }

            return value.Substring(0, value.Length - suffix.Length);
        }
    }
}
