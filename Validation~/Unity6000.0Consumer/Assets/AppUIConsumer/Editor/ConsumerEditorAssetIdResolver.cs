using Joi.H.AppUI.Editor.Binding;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Validation.Consumer.Editor
{
    /// <summary>
    /// Consumer-owned AssetId policy. Validation definitions intentionally use
    /// canonical AssetDatabase paths as both Editor and runtime identifiers.
    /// </summary>
    public sealed class ConsumerEditorAssetIdResolver :
        IUIEditorAssetIdResolver
    {
        public const string Id = "validation.asset-path";

        public string ResolverId => Id;

        public bool TryGetAssetId(
            string prefabAssetPath,
            out string assetId,
            out string error)
        {
            assetId = (prefabAssetPath ?? string.Empty).Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(assetId) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(assetId) != null)
            {
                error = string.Empty;
                return true;
            }

            error = "Validation prefab does not exist: " + assetId;
            return false;
        }

        public bool TryResolveAssetPath(
            string assetId,
            out string prefabAssetPath,
            out string error)
        {
            prefabAssetPath = (assetId ?? string.Empty).Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(prefabAssetPath) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath) !=
                null)
            {
                error = string.Empty;
                return true;
            }

            error = "Validation AssetId does not resolve to a prefab: " +
                    prefabAssetPath;
            return false;
        }
    }

    [InitializeOnLoad]
    public static class ConsumerEditorAssetIdResolverRegistration
    {
        static ConsumerEditorAssetIdResolverRegistration()
        {
            if (!UIEditorAssetIdResolverRegistry.Register(
                    new ConsumerEditorAssetIdResolver(),
                    out string error))
            {
                Debug.LogError("<Joi.H.AppUI.Validation> " + error);
            }
        }
    }
}
