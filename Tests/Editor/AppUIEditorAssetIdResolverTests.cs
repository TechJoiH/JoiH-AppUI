using Joi.H.AppUI.Editor.Binding;
using NUnit.Framework;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIEditorAssetIdResolverTests
    {
        [TearDown]
        public void TearDown()
        {
            UIEditorAssetIdResolverRegistry.ResetToResources();
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
        public void Registry_AllowsConsumerResolverInjection()
        {
            PrefixResolver resolver = new PrefixResolver();

            UIEditorAssetIdResolverRegistry.SetResolver(resolver);

            Assert.That(
                UIEditorAssetIdResolverRegistry.Current,
                Is.SameAs(resolver));
            Assert.That(
                resolver.TryGetAssetId(
                    "Assets/UI/Settings.prefab",
                    out string assetId,
                    out _),
                Is.True);
            Assert.That(assetId, Is.EqualTo("appui:Settings"));
        }

        private sealed class PrefixResolver : IUIEditorAssetIdResolver
        {
            public bool TryGetAssetId(
                string prefabAssetPath,
                out string assetId,
                out string error)
            {
                assetId = "appui:Settings";
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
