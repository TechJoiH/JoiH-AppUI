using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// UI Group 定义资产。
    /// Group 主要服务绑定生成和复用组件管理，不直接进入页面栈。
    /// </summary>
    [CreateAssetMenu(fileName = "UIGroupDefinition", menuName = "Joi.H AppUI/Group Definition")]
    public sealed class UIGroupDefinition : UIDefinitionAssetBase
    {
        /// <summary>Group 唯一 ID，沿用基类 DefinitionId。</summary>
        public string GroupId
        {
            get { return DefinitionId; }
        }

        /// <summary>Group 作用域，区分嵌入、复用和列表项模板。</summary>
        public UIGroupScope Scope = UIGroupScope.Reusable;

        /// <summary>是否作为可复用组件。</summary>
        public bool IsReusable = true;

        /// <summary>是否作为列表项模板。</summary>
        public bool IsItemTemplate;

        /// <summary>是否允许 Group 内继续嵌套子 Group。</summary>
        public bool AllowNestedGroup = true;
    }
}
