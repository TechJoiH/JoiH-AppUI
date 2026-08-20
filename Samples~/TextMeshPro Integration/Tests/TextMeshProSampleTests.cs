using System;
using System.IO;
using Joi.H.AppUI;
using Joi.H.AppUI.Editor.Binding;
using Joi.H.AppUI.Integrations.TextMeshPro;
using Joi.H.AppUI.Integrations.TextMeshPro.Editor;
using Joi.H.AppUI.Samples.TextMeshPro.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Joi.H.AppUI.Samples.TextMeshPro.Tests
{
    public sealed class TextMeshProSampleTests
    {
        private const string Root = TextMeshProSampleValidationCommand.SampleRoot;

        [Test]
        public void Sample_ConfigurationContainsTMPInputResolver()
        {
            AppUIRuntimeConfiguration configuration = new AppUIRuntimeConfiguration(
                null, null, new IAppUIFocusControlPolicyResolver[] { new TextMeshProInputFieldPolicyResolver() });
            Assert.That(configuration.FocusPolicyResolvers.Count, Is.EqualTo(1));
            Assert.That(configuration.FocusPolicyResolvers[0].ResolverId,
                Is.EqualTo("joih.appui.tmp.input-field"));
        }

        [Test]
        public void Sample_BindingSettingsSelectTMPProvider()
        {
            UIBindingSettings settings = AssetDatabase.LoadAssetAtPath<UIBindingSettings>(
                Root + "/Settings/TextMeshProBindingSettings.asset");
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.EnabledRuleProviderIds, Does.Contain(TextMeshProBindingRuleProvider.Id));
        }

        [Test]
        public void Sample_InputFieldCancel_DoesNotClosePage()
        {
            UIPageDefinition definition = AssetDatabase.LoadAssetAtPath<UIPageDefinition>(
                Root + "/Definitions/TextMeshProPageDefinition.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.CloseOnCancel, Is.False,
                "Focused TMP InputField Cancel is consumed by its resolver, not converted into a page close policy.");
        }

        [Test]
        public void Sample_DropdownUsesExplicitChildRegionPolicy()
        {
            GameObject page = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "/Prefabs/TextMeshProPage.prefab");
            TextMeshProSamplePageController controller = page.GetComponent<TextMeshProSamplePageController>();
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(controller));
            string source = File.ReadAllText(scriptPath);
            StringAssert.Contains("new TextMeshProFocusDropdownControlPolicy", source);
            StringAssert.Contains("\"options\"", source);
        }

        [Test]
        public void Sample_NoticePrefabUsesTextMeshProNoticeView()
        {
            GameObject notice = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "/Prefabs/TextMeshProNotice.prefab");
            Assert.That(notice, Is.Not.Null);
            Assert.That(notice.GetComponent<TextMeshProNoticeView>(), Is.Not.Null);
        }

        [Test]
        public void Sample_PageOpenRefreshClose_Completes()
        {
            EditorSceneManager.OpenScene(TextMeshProSampleValidationCommand.ScenePath);
            TextMeshProSampleInstaller installer =
                UnityEngine.Object.FindFirstObjectByType<TextMeshProSampleInstaller>();
            Assert.That(installer, Is.Not.Null);
            installer.Initialize();
            AssertSucceeded(installer.Host.Manager.Open(TextMeshProSampleInstaller.PageId));
            AssertSucceeded(installer.Host.Manager.Refresh(TextMeshProSampleInstaller.PageId, "updated"));
            AssertSucceeded(installer.Host.Manager.Close(TextMeshProSampleInstaller.PageId));
        }

        private static void AssertSucceeded<TResult>(IUIOperation<TResult> operation)
        {
            Assert.That(operation.IsTerminal, Is.True);
            Assert.That(operation.TryGetCompletion(out AppUIOperationCompletion<TResult> completion), Is.True);
            Assert.That(completion.Status, Is.EqualTo(AppUIOperationStatus.Succeeded),
                completion.Exception != null ? completion.Exception.ToString() : string.Empty);
        }
    }
}
