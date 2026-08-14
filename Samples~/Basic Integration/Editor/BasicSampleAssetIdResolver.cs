using Joi.H.AppUI.Editor.Binding;
using UnityEditor;

namespace Joi.H.AppUI.Samples.Basic.Editor
{
    public sealed class BasicSampleAssetIdResolver :
        IUIEditorAssetIdResolver
    {
        public const string Id = "sample.basic.asset-guid";

        public string ResolverId => Id;

        public bool TryGetAssetId(
            string prefabAssetPath,
            out string assetId,
            out string error)
        {
            assetId = AssetDatabase.AssetPathToGUID(prefabAssetPath);
            error = string.IsNullOrEmpty(assetId)
                ? "AssetDatabase has no GUID for path: " + prefabAssetPath
                : string.Empty;
            return !string.IsNullOrEmpty(assetId);
        }

        public bool TryResolveAssetPath(
            string assetId,
            out string prefabAssetPath,
            out string error)
        {
            prefabAssetPath = AssetDatabase.GUIDToAssetPath(assetId);
            error = string.IsNullOrEmpty(prefabAssetPath)
                ? "AssetDatabase has no asset for GUID: " + assetId
                : string.Empty;
            return !string.IsNullOrEmpty(prefabAssetPath);
        }
    }

    [InitializeOnLoad]
    public static class BasicSampleAssetIdResolverRegistration
    {
        static BasicSampleAssetIdResolverRegistration()
        {
            if (!UIEditorAssetIdResolverRegistry.Register(
                    new BasicSampleAssetIdResolver(),
                    out string error))
            {
                UnityEngine.Debug.LogError(
                    "<Joi.H.AppUI.Sample> " + error);
            }
        }
    }
}
