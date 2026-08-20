using System;
using System.Collections.Generic;
using Joi.H.AppUI.Editor.Binding;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class UIBindingRuleProviderTests
    {
        private UIBindingSettings settings;

        [SetUp]
        public void SetUp()
        {
            UIBindingRuleProviderRegistry.Clear();
            settings = ScriptableObject.CreateInstance<UIBindingSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UIBindingRuleProviderRegistry.Clear();
            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void ProviderRegistry_DuplicateProviderId_IsRejected()
        {
            Assert.That(UIBindingRuleProviderRegistry.Register(Provider("same"), out _), Is.True);
            Assert.That(UIBindingRuleProviderRegistry.Register(Provider("same"), out string error), Is.False);
            StringAssert.Contains("same", error);
        }

        [Test]
        public void ProviderRegistry_SelectedMissingProvider_BlocksSnapshot()
        {
            SetEnabled("missing");
            Assert.That(UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out _, out string error), Is.False);
            StringAssert.Contains("missing", error);
        }

        [Test]
        public void ProviderRegistry_DuplicateRuleId_BlocksSnapshot()
        {
            Register("a", Rule("shared", typeof(Button), 50));
            Register("b", Rule("shared", typeof(Toggle), 40));
            SetEnabled("a", "b");
            Assert.That(UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out _, out string error), Is.False);
            StringAssert.Contains("shared", error);
        }

        [Test]
        public void ProviderRegistry_SameComponentAcrossEnabledProviders_BlocksSnapshot()
        {
            Register("a", Rule("a.rule", typeof(Button), 50));
            Register("b", Rule("b.rule", typeof(Button), 40));
            SetEnabled("a", "b");
            Assert.That(UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out _, out string error), Is.False);
            StringAssert.Contains(typeof(Button).FullName, error);
        }

        [Test]
        public void ProviderRegistry_OptionalRuleCannotOverrideBuiltInComponent()
        {
            Register("a", Rule("custom.button", typeof(Button), 2000));
            SetEnabled("a");
            Assert.That(UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out _, out string error), Is.False);
            StringAssert.Contains("builtin.button", error);
        }

        [Test]
        public void ProviderRegistry_PriorityTie_SortsByOrdinalRuleId()
        {
            Register("a", Rule("z.rule", typeof(LayoutElement), 50));
            Register("b", Rule("a.rule", typeof(ContentSizeFitter), 50));
            SetEnabled("a", "b");
            Assert.That(UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out UIBindingRuleSnapshot snapshot, out string error), Is.True, error);
            Assert.That(IndexOf(snapshot, "a.rule"), Is.LessThan(IndexOf(snapshot, "z.rule")));
        }

        [Test]
        public void ProviderRegistry_SourceMutation_DoesNotChangeBuiltSnapshot()
        {
            List<UIBindingComponentRule> rules = new List<UIBindingComponentRule>
            {
                Rule("stable", typeof(LayoutElement), 50),
            };
            Assert.That(UIBindingRuleProviderRegistry.Register(new ProviderStub("a", rules), out _), Is.True);
            rules.Clear();
            SetEnabled("a");
            Assert.That(UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out UIBindingRuleSnapshot snapshot, out string error), Is.True, error);
            Assert.That(IndexOf(snapshot, "stable"), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Settings_DuplicateEnabledProviderId_BlocksSnapshot()
        {
            Assert.That(UIBindingRuleProviderRegistry.Register(Provider("a"), out _), Is.True);
            SetEnabled("a", "a");
            Assert.That(UIBindingRuleProviderRegistry.TryBuildSnapshot(settings, out _, out string error), Is.False);
            StringAssert.Contains("duplicated", error);
        }

        private void Register(string id, UIBindingComponentRule rule)
        {
            Assert.That(
                UIBindingRuleProviderRegistry.Register(
                    new ProviderStub(id, new[] { rule }),
                    out string error),
                Is.True,
                error);
        }

        private static IUIBindingRuleProvider Provider(string id)
        {
            return new ProviderStub(id, Array.Empty<UIBindingComponentRule>());
        }

        private static UIBindingComponentRule Rule(string id, Type type, int priority)
        {
            return new UIBindingComponentRule(
                id,
                type,
                "Test",
                type.FullName,
                UIBindingTargetKind.Component,
                priority,
                true);
        }

        private void SetEnabled(params string[] ids)
        {
            SerializedObject serialized = new SerializedObject(settings);
            SerializedProperty property = serialized.FindProperty("enabledRuleProviderIds");
            property.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = ids[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int IndexOf(UIBindingRuleSnapshot snapshot, string ruleId)
        {
            for (int i = 0; i < snapshot.Rules.Count; i++)
            {
                if (snapshot.Rules[i].RuleId == ruleId) return i;
            }

            return -1;
        }

        private sealed class ProviderStub : IUIBindingRuleProvider
        {
            public ProviderStub(string providerId, IReadOnlyList<UIBindingComponentRule> rules)
            {
                ProviderId = providerId;
                Rules = rules;
            }

            public string ProviderId { get; }
            public IReadOnlyList<UIBindingComponentRule> Rules { get; }
        }
    }
}
