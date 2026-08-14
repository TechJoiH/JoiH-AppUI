using Joi.H.AppUI.Editor.Binding;
using UnityEditor;

namespace Joi.H.AppUI.Samples.CustomHost.Editor
{
    /// <summary>
    /// Sample GUID AssetId policy. The runtime catalog must use the same GUID
    /// values, so Editor authoring and runtime loading share one identifier.
    /// </summary>
    public sealed class CustomHostAssetIdResolver :
        IUIEditorAssetIdResolver
    {
        public const string Id = "sample.custom-host.asset-guid";

        public string ResolverId => Id;

        public bool TryGetAssetId(
            string prefabAssetPath,
            out string assetId,
            out string error)
        {
            assetId = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(prefabAssetPath))
            {
                error = "Prefab asset path is empty.";
                return false;
            }

            assetId = AssetDatabase.AssetPathToGUID(prefabAssetPath);
            if (!string.IsNullOrEmpty(assetId))
            {
                return true;
            }

            error = "AssetDatabase has no GUID for path: " +
                    prefabAssetPath;
            return false;
        }

        public bool TryResolveAssetPath(
            string assetId,
            out string prefabAssetPath,
            out string error)
        {
            prefabAssetPath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(assetId))
            {
                error = "AssetId is empty.";
                return false;
            }

            prefabAssetPath = AssetDatabase.GUIDToAssetPath(assetId);
            if (!string.IsNullOrEmpty(prefabAssetPath))
            {
                return true;
            }

            error = "AssetDatabase has no asset for GUID: " + assetId;
            return false;
        }
    }

    [InitializeOnLoad]
    public static class CustomHostAssetIdResolverRegistration
    {
        static CustomHostAssetIdResolverRegistration()
        {
            if (!UIEditorAssetIdResolverRegistry.Register(
                    new CustomHostAssetIdResolver(),
                    out string error))
            {
                UnityEngine.Debug.LogError(
                    "<Joi.H.AppUI.Sample> " + error);
            }
        }
    }
}
