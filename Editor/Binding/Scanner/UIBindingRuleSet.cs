using Joi.H.AppUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// App UI 绑定扫描的默认规则表。
    /// 这里集中定义 B_ 前缀和常用 uGUI/TMP 组件的优先级。
    /// </summary>
    public static class UIBindingRuleSet
    {
        /// <summary>
        /// 被扫描为绑定目标的节点名前缀。
        /// </summary>
        public const string BindingPrefix = "B_";

        /// <summary>
        /// 默认组件匹配规则。顺序不直接代表优先级，真正选择时使用 FunctionPriority。
        /// </summary>
        public static readonly UIBindingComponentRule[] DefaultComponentRules =
        {
            new UIBindingComponentRule(typeof(UIGroupBase), "Group", null, UIBindingTargetKind.BindingScope, 1000, true),
            new UIBindingComponentRule(typeof(Button), "Btn", "UnityEngine.UI.Button", UIBindingTargetKind.Component, 900, true),
            new UIBindingComponentRule(typeof(Toggle), "Toggle", "UnityEngine.UI.Toggle", UIBindingTargetKind.Component, 880, true),
            new UIBindingComponentRule(typeof(TMP_InputField), "Input", "TMPro.TMP_InputField", UIBindingTargetKind.Component, 860, true),
            new UIBindingComponentRule(typeof(TMP_Dropdown), "Dropdown", "TMPro.TMP_Dropdown", UIBindingTargetKind.Component, 840, true),
            new UIBindingComponentRule(typeof(Slider), "Slider", "UnityEngine.UI.Slider", UIBindingTargetKind.Component, 820, true),
            new UIBindingComponentRule(typeof(ScrollRect), "Scroll", "UnityEngine.UI.ScrollRect", UIBindingTargetKind.Component, 800, true),
            new UIBindingComponentRule(typeof(TMP_Text), "Txt", "TMPro.TMP_Text", UIBindingTargetKind.Component, 700, true),
            new UIBindingComponentRule(typeof(Image), "Img", "UnityEngine.UI.Image", UIBindingTargetKind.Component, 680, true),
            new UIBindingComponentRule(typeof(RawImage), "RawImg", "UnityEngine.UI.RawImage", UIBindingTargetKind.Component, 660, true),
            new UIBindingComponentRule(typeof(Animator), "Anim", "UnityEngine.Animator", UIBindingTargetKind.Component, 500, true),
            new UIBindingComponentRule(typeof(Canvas), "Canvas", "UnityEngine.Canvas", UIBindingTargetKind.Component, 480, true),
        };

        /// <summary>
        /// 默认兜底规则，用于没有匹配组件的 B_ 节点。
        /// 当前唯一 fallback 是 GameObject，并固定生成 UnityEngine.GameObject 字段与 Go 后缀。
        /// </summary>
        public static readonly UIBindingFallbackRule DefaultFallbackRule = new UIBindingFallbackRule();
    }
}
