using System;
using System.Collections.Generic;

namespace Joi.H.AppUI.Editor.Binding
{
    public static class UIBindingRuleProviderRegistry
    {
        public const string BuiltInProviderId = "joih.appui.builtin";

        private static readonly Dictionary<string, ProviderSnapshot> Providers =
            new Dictionary<string, ProviderSnapshot>(StringComparer.Ordinal);

        public static bool Register(IUIBindingRuleProvider provider, out string error)
        {
            error = string.Empty;
            if (provider == null)
            {
                error = "Binding rule provider is null.";
                return false;
            }

            string providerId = provider.ProviderId;
            if (string.IsNullOrWhiteSpace(providerId))
            {
                error = "Binding rule provider ID cannot be empty.";
                return false;
            }

            if (string.Equals(providerId, BuiltInProviderId, StringComparison.Ordinal))
            {
                error = "Binding rule provider ID is reserved: " + providerId;
                return false;
            }

            if (Providers.ContainsKey(providerId))
            {
                error = "Binding rule provider ID is already registered: " + providerId;
                return false;
            }

            IReadOnlyList<UIBindingComponentRule> sourceRules = provider.Rules;
            if (sourceRules == null)
            {
                error = "Binding rule provider returned a null rule list: " + providerId;
                return false;
            }

            List<UIBindingComponentRule> copiedRules = new List<UIBindingComponentRule>(sourceRules.Count);
            for (int i = 0; i < sourceRules.Count; i++)
            {
                UIBindingComponentRule rule = sourceRules[i];
                if (rule == null)
                {
                    error = "Binding rule provider contains a null rule: " + providerId;
                    return false;
                }

                copiedRules.Add(rule);
            }

            Providers.Add(providerId, new ProviderSnapshot(copiedRules));
            return true;
        }

        public static bool TryBuildSnapshot(
            UIBindingSettings settings,
            out UIBindingRuleSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (settings == null)
            {
                error = "UIBindingSettings is required to resolve binding rule providers.";
                return false;
            }

            List<UIBindingComponentRule> rules =
                new List<UIBindingComponentRule>(UIBindingRuleSet.BuiltInComponentRules.Length + 8);
            rules.AddRange(UIBindingRuleSet.BuiltInComponentRules);
            List<string> providerIds = new List<string> { BuiltInProviderId };
            HashSet<string> selectedIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<string> enabledIds = settings.EnabledRuleProviderIds;
            for (int i = 0; i < enabledIds.Count; i++)
            {
                string providerId = enabledIds[i];
                if (string.IsNullOrWhiteSpace(providerId))
                {
                    error = "Enabled binding rule provider ID cannot be empty.";
                    return false;
                }

                if (!selectedIds.Add(providerId))
                {
                    error = "Enabled binding rule provider ID is duplicated: " + providerId;
                    return false;
                }

                if (!Providers.TryGetValue(providerId, out ProviderSnapshot provider))
                {
                    error = "Enabled binding rule provider is not registered: " + providerId;
                    return false;
                }

                providerIds.Add(providerId);
                rules.AddRange(provider.Rules);
            }

            if (!ValidateCollisions(rules, out error)) return false;

            rules.Sort(CompareRules);
            snapshot = new UIBindingRuleSnapshot(rules, UIBindingRuleSet.DefaultFallbackRule, providerIds);
            return true;
        }

        public static string[] GetRegisteredProviderIds()
        {
            string[] ids = new string[Providers.Count];
            Providers.Keys.CopyTo(ids, 0);
            Array.Sort(ids, StringComparer.Ordinal);
            return ids;
        }

        public static void Clear()
        {
            Providers.Clear();
        }

        private static bool ValidateCollisions(IReadOnlyList<UIBindingComponentRule> rules, out string error)
        {
            Dictionary<string, UIBindingComponentRule> byId =
                new Dictionary<string, UIBindingComponentRule>(StringComparer.Ordinal);
            Dictionary<Type, UIBindingComponentRule> byComponent =
                new Dictionary<Type, UIBindingComponentRule>();
            for (int i = 0; i < rules.Count; i++)
            {
                UIBindingComponentRule rule = rules[i];
                if (byId.TryGetValue(rule.RuleId, out UIBindingComponentRule existingId))
                {
                    error = "Binding rule ID collision: " + rule.RuleId +
                        " (" + existingId.ComponentType.FullName + " and " + rule.ComponentType.FullName + ").";
                    return false;
                }

                byId.Add(rule.RuleId, rule);
                if (byComponent.TryGetValue(rule.ComponentType, out UIBindingComponentRule existingType))
                {
                    error = "Binding component type collision: " + rule.ComponentType.FullName +
                        " (" + existingType.RuleId + " and " + rule.RuleId + ").";
                    return false;
                }

                byComponent.Add(rule.ComponentType, rule);
            }

            error = string.Empty;
            return true;
        }

        private static int CompareRules(UIBindingComponentRule left, UIBindingComponentRule right)
        {
            int priority = right.FunctionPriority.CompareTo(left.FunctionPriority);
            return priority != 0
                ? priority
                : StringComparer.Ordinal.Compare(left.RuleId, right.RuleId);
        }

        private sealed class ProviderSnapshot
        {
            public ProviderSnapshot(List<UIBindingComponentRule> rules)
            {
                Rules = rules;
            }

            public List<UIBindingComponentRule> Rules { get; }
        }
    }
}
