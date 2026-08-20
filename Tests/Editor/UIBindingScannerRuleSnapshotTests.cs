using System;
using System.Collections.Generic;
using Joi.H.AppUI.Editor.Binding;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI.Tests
{
    public sealed class UIBindingScannerRuleSnapshotTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        [TestCase(typeof(InputField), "UnityEngine.UI.InputField", "Input")]
        [TestCase(typeof(Dropdown), "UnityEngine.UI.Dropdown", "Dropdown")]
        [TestCase(typeof(Slider), "UnityEngine.UI.Slider", "Slider")]
        [TestCase(typeof(ScrollRect), "UnityEngine.UI.ScrollRect", "Scroll")]
        [TestCase(typeof(Scrollbar), "UnityEngine.UI.Scrollbar", "Scrollbar")]
        [TestCase(typeof(Text), "UnityEngine.UI.Text", "Txt")]
        [TestCase(typeof(Image), "UnityEngine.UI.Image", "Img")]
        public void BuiltInRules_MapExactUGUITypeAndSuffix(
            Type componentType,
            string codeType,
            string suffix)
        {
            TestScope scope = CreateScope();
            GameObject child = CreateChild(scope, "B_Value");
            child.AddComponent(componentType);

            UIBindingScanResult result = UIBindingScanner.Scan(scope);

            Assert.That(result.HasError, Is.False, string.Join("\n", result.Errors));
            Assert.That(result.Bindings, Has.Count.EqualTo(1));
            Assert.That(result.Bindings[0].CodeTypeName, Is.EqualTo(codeType));
            Assert.That(result.Bindings[0].PropertyName, Is.EqualTo("Value" + suffix));
        }

        [Test]
        public void BuiltInRules_NoMatchingComponent_UsesGameObjectFallback()
        {
            TestScope scope = CreateScope();
            CreateChild(scope, "B_Value");

            UIBindingScanResult result = UIBindingScanner.Scan(scope);

            Assert.That(result.HasError, Is.False, string.Join("\n", result.Errors));
            Assert.That(result.Bindings[0].CodeTypeName, Is.EqualTo("UnityEngine.GameObject"));
            Assert.That(result.Bindings[0].PropertyName, Is.EqualTo("ValueGo"));
        }

        private TestScope CreateScope()
        {
            GameObject root = new GameObject("Root");
            created.Add(root);
            return root.AddComponent<TestScope>();
        }

        private static GameObject CreateChild(TestScope scope, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(scope.transform, false);
            return child;
        }

        private sealed class TestScope : UIBindingScopeBase
        {
        }
    }
}
