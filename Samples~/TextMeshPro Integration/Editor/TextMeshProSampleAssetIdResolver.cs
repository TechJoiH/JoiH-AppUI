using Joi.H.AppUI.Editor.Binding;
using UnityEditor;

namespace Joi.H.AppUI.Samples.TextMeshPro.Editor
{
    public sealed class TextMeshProSampleAssetIdResolver : IUIEditorAssetIdResolver
    {
        public const string Id = "sample.textmeshpro.asset-guid";
        public string ResolverId => Id;
        public bool TryGetAssetId(string prefabAssetPath, out string assetId, out string error)
        {
            assetId = AssetDatabase.AssetPathToGUID(prefabAssetPath);
            error = string.IsNullOrEmpty(assetId) ? "No GUID for: " + prefabAssetPath : string.Empty;
            return !string.IsNullOrEmpty(assetId);
        }
        public bool TryResolveAssetPath(string assetId, out string prefabAssetPath, out string error)
        {
            prefabAssetPath = AssetDatabase.GUIDToAssetPath(assetId);
            error = string.IsNullOrEmpty(prefabAssetPath) ? "No asset for GUID: " + assetId : string.Empty;
            return !string.IsNullOrEmpty(prefabAssetPath);
        }
    }

    [InitializeOnLoad]
    internal static class TextMeshProSampleAssetIdResolverRegistration
    {
        static TextMeshProSampleAssetIdResolverRegistration()
        {
            UIEditorAssetIdResolverRegistry.Register(new TextMeshProSampleAssetIdResolver(), out _);
        }
    }
}
