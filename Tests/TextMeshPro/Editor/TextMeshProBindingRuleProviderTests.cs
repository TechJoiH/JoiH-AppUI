using Joi.H.AppUI.Editor.Binding;
using Joi.H.AppUI.Integrations.TextMeshPro.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Tests.TextMeshPro
{
    public sealed class TextMeshProBindingRuleProviderTests
    {
        [TearDown]
        public void TearDown()
        {
            UIBindingRuleProviderRegistry.Clear();
        }

        [Test]
        public void TextMeshProBindingProvider_ExportsThreeStableRules()
        {
            TextMeshProBindingRuleProvider provider = new TextMeshProBindingRuleProvider();
            Assert.That(provider.ProviderId, Is.EqualTo("joih.appui.tmp"));
            Assert.That(provider.Rules.Count, Is.EqualTo(3));
            Assert.That(provider.Rules[0].RuleId, Is.EqualTo("joih.appui.tmp.binding.input-field"));
            Assert.That(provider.Rules[1].RuleId, Is.EqualTo("joih.appui.tmp.binding.dropdown"));
            Assert.That(provider.Rules[2].RuleId, Is.EqualTo("joih.appui.tmp.binding.text"));
        }

        [Test]
        public void TextMeshProBindingProvider_IsAppliedOnlyWhenSelected()
        {
            UIBindingSettings settings = ScriptableObject.CreateInstance<UIBindingSettings>();
            Assert.That(UIBindingRuleProviderRegistry.Register(
                new TextMeshProBindingRuleProvider(), out string registerError), Is.True, registerError);
            SetSelected(settings, "joih.appui.tmp");

            Assert.That(UIBindingRuleProviderRegistry.TryBuildSnapshot(
                settings, out UIBindingRuleSnapshot snapshot, out string error), Is.True, error);
            Assert.That(snapshot.ProviderIds, Does.Contain("joih.appui.tmp"));
            Assert.That(snapshot.Rules, Has.Some.Property("RuleId").EqualTo("joih.appui.tmp.binding.text"));
            UnityEngine.Object.DestroyImmediate(settings);
        }

        private static void SetSelected(UIBindingSettings settings, string id)
        {
            SerializedObject serialized = new SerializedObject(settings);
            SerializedProperty property = serialized.FindProperty("enabledRuleProviderIds");
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
