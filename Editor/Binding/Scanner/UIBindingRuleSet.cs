using Joi.H.AppUI;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>UGUI-only built-in binding rules. Optional UI technologies contribute Providers instead.</summary>
    public static class UIBindingRuleSet
    {
        public const string BindingPrefix = "B_";

        public static readonly UIBindingComponentRule[] BuiltInComponentRules =
        {
            new UIBindingComponentRule("builtin.ui-group", typeof(UIGroupBase), "Group", null, UIBindingTargetKind.BindingScope, 1000, true),
            new UIBindingComponentRule("builtin.button", typeof(Button), "Btn", "UnityEngine.UI.Button", UIBindingTargetKind.Component, 900, true),
            new UIBindingComponentRule("builtin.toggle", typeof(Toggle), "Toggle", "UnityEngine.UI.Toggle", UIBindingTargetKind.Component, 880, true),
            new UIBindingComponentRule("builtin.input-field", typeof(InputField), "Input", "UnityEngine.UI.InputField", UIBindingTargetKind.Component, 860, true),
            new UIBindingComponentRule("builtin.dropdown", typeof(Dropdown), "Dropdown", "UnityEngine.UI.Dropdown", UIBindingTargetKind.Component, 840, true),
            new UIBindingComponentRule("builtin.slider", typeof(Slider), "Slider", "UnityEngine.UI.Slider", UIBindingTargetKind.Component, 820, true),
            new UIBindingComponentRule("builtin.scroll-rect", typeof(ScrollRect), "Scroll", "UnityEngine.UI.ScrollRect", UIBindingTargetKind.Component, 800, true),
            new UIBindingComponentRule("builtin.scrollbar", typeof(Scrollbar), "Scrollbar", "UnityEngine.UI.Scrollbar", UIBindingTargetKind.Component, 780, true),
            new UIBindingComponentRule("builtin.text", typeof(Text), "Txt", "UnityEngine.UI.Text", UIBindingTargetKind.Component, 700, true),
            new UIBindingComponentRule("builtin.image", typeof(Image), "Img", "UnityEngine.UI.Image", UIBindingTargetKind.Component, 680, true),
            new UIBindingComponentRule("builtin.raw-image", typeof(RawImage), "RawImg", "UnityEngine.UI.RawImage", UIBindingTargetKind.Component, 660, true),
            new UIBindingComponentRule("builtin.animator", typeof(Animator), "Anim", "UnityEngine.Animator", UIBindingTargetKind.Component, 500, true),
            new UIBindingComponentRule("builtin.canvas", typeof(Canvas), "Canvas", "UnityEngine.Canvas", UIBindingTargetKind.Component, 480, true),
        };

        public static readonly UIBindingFallbackRule DefaultFallbackRule = new UIBindingFallbackRule();

        public static UIBindingRuleSnapshot CreateBuiltInSnapshot()
        {
            return new UIBindingRuleSnapshot(
                BuiltInComponentRules,
                DefaultFallbackRule,
                new[] { UIBindingRuleProviderRegistry.BuiltInProviderId });
        }
    }
}
