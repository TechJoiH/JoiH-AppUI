using System.Collections.Generic;
using Joi.H.AppUI;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// App UI 绑定工具的项目级设置。
    /// 保存 Registry 引用、Definition 默认值和 Validate All 搜索范围。
    /// </summary>
    [CreateAssetMenu(fileName = "UIBindingSettings", menuName = "Joi.H AppUI/Binding Settings")]
    public sealed class UIBindingSettings : ScriptableObject
    {
        /// <summary>
        /// 是否在构建前执行只读 Validate All，并用错误阻断构建。
        /// </summary>
        public bool EnableBuildPreprocess;

        /// <summary>
        /// 页面 Definition 注册表。
        /// </summary>
        public UIPageDefinitionRegistry PageDefinitionRegistry;

        /// <summary>
        /// Group Definition 注册表。
        /// </summary>
        public UIGroupDefinitionRegistry GroupDefinitionRegistry;

        [Header("Page Definition Defaults")]
        /// <summary>
        /// 新建 Page Definition 时使用的默认 Layer。
        /// </summary>
        public UILayerId DefaultPageLayerId = UILayerId.PopupLayer;

        /// <summary>
        /// 新建 Page Definition 时使用的默认 CanvasDomain。
        /// </summary>
        public UICanvasDomain DefaultPageCanvasDomain = UICanvasDomain.Overlay;

        /// <summary>
        /// 新建 Page Definition 时使用的默认 Scope。
        /// </summary>
        public UIPageScope DefaultPageScope = UIPageScope.SceneScope;

        /// <summary>
        /// 新建 Page Definition 时使用的默认打开策略。
        /// </summary>
        public UIOpenPolicy DefaultPageOpenPolicy = UIOpenPolicy.RefreshExisting;

        [Header("Group Definition Defaults")]
        /// <summary>
        /// 新建 Group Definition 时使用的默认 Scope。
        /// </summary>
        public UIGroupScope DefaultGroupScope = UIGroupScope.Reusable;

        /// <summary>
        /// 新建 Group Definition 时默认是否可复用。
        /// </summary>
        public bool DefaultGroupIsReusable = true;

        /// <summary>
        /// 新建 Group Definition 时默认是否作为列表项模板。
        /// </summary>
        public bool DefaultGroupIsItemTemplate;

        /// <summary>
        /// 新建 Group Definition 时默认是否允许嵌套 Group。
        /// </summary>
        public bool DefaultGroupAllowNestedGroup = true;

        /// <summary>
        /// Validate All 扫描 Group Definition 的搜索根路径。
        /// </summary>
        public List<string> GroupDefinitionSearchRoots = new List<string>();

        /// <summary>
        /// Validate All 扫描 Group Prefab 的搜索根路径。
        /// </summary>
        public List<string> GroupPrefabSearchRoots = new List<string>();
    }
}
