using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Joi.H.AppUI.Editor.Binding
{
    public sealed class UIBindingRuleSnapshot
    {
        internal UIBindingRuleSnapshot(
            IReadOnlyList<UIBindingComponentRule> rules,
            UIBindingFallbackRule fallbackRule,
            IReadOnlyList<string> providerIds)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (fallbackRule == null) throw new ArgumentNullException(nameof(fallbackRule));
            if (providerIds == null) throw new ArgumentNullException(nameof(providerIds));

            Rules = new ReadOnlyCollection<UIBindingComponentRule>(Copy(rules));
            FallbackRule = new UIBindingFallbackRule
            {
                EnableGameObjectFallback = fallbackRule.EnableGameObjectFallback,
            };
            ProviderIds = new ReadOnlyCollection<string>(Copy(providerIds));
        }

        public IReadOnlyList<UIBindingComponentRule> Rules { get; }
        public UIBindingFallbackRule FallbackRule { get; }
        public IReadOnlyList<string> ProviderIds { get; }

        private static List<T> Copy<T>(IReadOnlyList<T> source)
        {
            List<T> result = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            return result;
        }
    }
}
