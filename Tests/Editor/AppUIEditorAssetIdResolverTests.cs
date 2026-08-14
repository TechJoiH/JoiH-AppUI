using Joi.H.AppUI.Editor.Binding;
using NUnit.Framework;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIEditorAssetIdResolverTests
    {
        [TearDown]
        public void TearDown()
        {
            UIEditorAssetIdResolverRegistry.Clear();
        }

        [Test]
        public void ResourcesResolver_ConvertsPrefabPathToAssetId()
        {
            ResourcesUIEditorAssetIdResolver resolver =
                new ResourcesUIEditorAssetIdResolver();

            bool success = resolver.TryGetAssetId(
                "Assets/Resources/UI/Settings.prefab",
                out string assetId,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(assetId, Is.EqualTo("UI/Settings"));
        }

        [Test]
        public void Registry_MissingSelection_ReturnsConfiguredDiagnostic()
        {
            UIBindingSettings settings =
                UnityEngine.ScriptableObject.CreateInstance<UIBindingSettings>();
            PrefixResolver resolver = new PrefixResolver("prefix", "first:");
            Assert.That(
                UIEditorAssetIdResolverRegistry.Register(resolver, out _),
                Is.True);

            bool success = UIEditorAssetIdResolverRegistry.TryGetSelected(
                settings,
                out _,
                out string error);

            Assert.That(success, Is.False);
            StringAssert.Contains("SelectedAssetIdResolverId", error);
            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void Registry_DuplicateId_IsRejectedWithoutReplacingOriginal()
        {
            PrefixResolver original =
                new PrefixResolver("shared", "original:");
            PrefixResolver duplicate =
                new PrefixResolver("shared", "replacement:");

            Assert.That(
                UIEditorAssetIdResolverRegistry.Register(
                    original,
                    out string firstError),
                Is.True,
                firstError);
            Assert.That(
                UIEditorAssetIdResolverRegistry.Register(
                    duplicate,
                    out string duplicateError),
                Is.False);
            StringAssert.Contains("shared", duplicateError);
            Assert.That(
                UIEditorAssetIdResolverRegistry.TryGet(
                    "shared",
                    out IUIEditorAssetIdResolver resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(original));
        }

        [Test]
        public void Settings_SelectedResolverId_ResolvesDeterministically()
        {
            UIBindingSettings settings =
                UnityEngine.ScriptableObject.CreateInstance<UIBindingSettings>();
            settings.SelectedAssetIdResolverId = "second";
            PrefixResolver first = new PrefixResolver("first", "first:");
            PrefixResolver second = new PrefixResolver("second", "second:");
            UIEditorAssetIdResolverRegistry.Register(first, out _);
            UIEditorAssetIdResolverRegistry.Register(second, out _);

            Assert.That(
                UIEditorAssetIdResolverRegistry.TryGetSelected(
                    settings,
                    out IUIEditorAssetIdResolver selected,
                    out string error),
                Is.True,
                error);
            Assert.That(selected, Is.SameAs(second));
            Assert.That(
                selected.TryGetAssetId(
                    "Assets/UI/Settings.prefab",
                    out string assetId,
                    out _),
                Is.True);
            Assert.That(assetId, Is.EqualTo("second:Settings"));
            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void Registry_DoesNotInstallResourcesImplicitly()
        {
            UIBindingSettings settings =
                UnityEngine.ScriptableObject.CreateInstance<UIBindingSettings>();
            settings.SelectedAssetIdResolverId =
                ResourcesUIEditorAssetIdResolver.Id;

            bool success = UIEditorAssetIdResolverRegistry.TryGetSelected(
                settings,
                out _,
                out string error);

            Assert.That(success, Is.False);
            StringAssert.Contains(
                ResourcesUIEditorAssetIdResolver.Id,
                error);
            UnityEngine.Object.DestroyImmediate(settings);
        }

        private sealed class PrefixResolver : IUIEditorAssetIdResolver
        {
            private readonly string prefix;

            public PrefixResolver(string resolverId, string prefix)
            {
                ResolverId = resolverId;
                this.prefix = prefix;
            }

            public string ResolverId { get; }

            public bool TryGetAssetId(
                string prefabAssetPath,
                out string assetId,
                out string error)
            {
                assetId = prefix + "Settings";
                error = string.Empty;
                return true;
            }

            public bool TryResolveAssetPath(
                string assetId,
                out string prefabAssetPath,
                out string error)
            {
                prefabAssetPath = "Assets/UI/Settings.prefab";
                error = string.Empty;
                return true;
            }
        }
    }
}
