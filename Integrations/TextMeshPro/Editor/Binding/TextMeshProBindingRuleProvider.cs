using System.Collections.Generic;
using Joi.H.AppUI.Editor.Binding;
using TMPro;
using UnityEditor;

namespace Joi.H.AppUI.Integrations.TextMeshPro.Editor
{
    public sealed class TextMeshProBindingRuleProvider : IUIBindingRuleProvider
    {
        public const string Id = "joih.appui.tmp";

        private static readonly UIBindingComponentRule[] ProviderRules =
        {
            new UIBindingComponentRule(
                "joih.appui.tmp.binding.input-field",
                typeof(TMP_InputField),
                "Input",
                "TMPro.TMP_InputField",
                UIBindingTargetKind.Component,
                860,
                true),
            new UIBindingComponentRule(
                "joih.appui.tmp.binding.dropdown",
                typeof(TMP_Dropdown),
                "Dropdown",
                "TMPro.TMP_Dropdown",
                UIBindingTargetKind.Component,
                840,
                true),
            new UIBindingComponentRule(
                "joih.appui.tmp.binding.text",
                typeof(TMP_Text),
                "Txt",
                "TMPro.TMP_Text",
                UIBindingTargetKind.Component,
                700,
                true),
        };

        public string ProviderId => Id;
        public IReadOnlyList<UIBindingComponentRule> Rules => ProviderRules;
    }

    [InitializeOnLoad]
    internal static class TextMeshProBindingRuleProviderRegistration
    {
        static TextMeshProBindingRuleProviderRegistration()
        {
            UIBindingRuleProviderRegistry.Register(
                new TextMeshProBindingRuleProvider(),
                out _);
        }
    }
}
