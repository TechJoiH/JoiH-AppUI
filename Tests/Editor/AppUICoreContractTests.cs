using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUICoreContractTests
    {
        private readonly List<Object> createdObjects = new List<Object>(8);

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void PageRegistry_DuplicateId_KeepsFirstDefinition()
        {
            UIPageDefinition first = CreateDefinition<UIPageDefinition>(
                "settings",
                "UI/SettingsA");
            UIPageDefinition duplicate = CreateDefinition<UIPageDefinition>(
                "settings",
                "UI/SettingsB");
            UIPageDefinitionRegistry registry =
                Track(ScriptableObject.CreateInstance<UIPageDefinitionRegistry>());
            SetObjectArray(registry, "m_Pages", first, duplicate);

            registry.RebuildIndex();

            Assert.That(registry.TryGet("settings", out UIPageDefinition found),
                Is.True);
            Assert.That(found, Is.SameAs(first));
        }

        [Test]
        public void GroupRegistry_Rebuild_IndexesReusableGroup()
        {
            UIGroupDefinition group = CreateDefinition<UIGroupDefinition>(
                "inventory-item",
                "UI/InventoryItem");
            UIGroupDefinitionRegistry registry =
                Track(ScriptableObject.CreateInstance<UIGroupDefinitionRegistry>());
            SetObjectArray(registry, "m_Groups", group);

            registry.RebuildIndex();

            Assert.That(registry.TryGet("inventory-item", out UIGroupDefinition found),
                Is.True);
            Assert.That(found, Is.SameAs(group));
        }

        [Test]
        public void BindingValidation_MissingMember_SetsErrorAndMetadata()
        {
            UIBindingValidationResult result = new UIBindingValidationResult();

            result.AddMissing("ConfirmButton", "UnityEngine.UI.Button");

            Assert.That(result.HasError, Is.True);
            Assert.That(result.Messages, Has.Count.EqualTo(1));
            Assert.That(result.Messages[0].MemberName,
                Is.EqualTo("ConfirmButton"));
            Assert.That(result.Messages[0].ExpectedType,
                Is.EqualTo("UnityEngine.UI.Button"));
        }

        [Test]
        public void NoticeScope_GlobalNeverMatchesBatchRelease()
        {
            UINoticeScope global = UINoticeScope.Global();
            UINoticeScope scene = UINoticeScope.Scene("scene-a");

            Assert.That(
                global.Matches(UIPageScope.SceneScope, "scene-a"),
                Is.False);
            Assert.That(
                scene.Matches(UIPageScope.SceneScope, "scene-a"),
                Is.True);
            Assert.That(
                scene.Matches(UIPageScope.SceneScope, "scene-b"),
                Is.False);
        }

        private T CreateDefinition<T>(string id, string prefabId)
            where T : UIDefinitionAssetBase
        {
            T definition = Track(ScriptableObject.CreateInstance<T>());
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("m_DefinitionId").stringValue = id;
            serialized.FindProperty("m_PrefabAssetId").stringValue =
                prefabId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void SetObjectArray(
            Object target,
            string propertyName,
            params Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue =
                    values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private T Track<T>(T value)
            where T : Object
        {
            createdObjects.Add(value);
            return value;
        }
    }
}
