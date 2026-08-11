using System;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Editor counterpart of IUIAssetProvider. It converts between a prefab's
    /// AssetDatabase path and the provider-facing asset id stored in definitions.
    /// </summary>
    public interface IUIEditorAssetIdResolver
    {
        bool TryGetAssetId(
            string prefabAssetPath,
            out string assetId,
            out string error);

        bool TryResolveAssetPath(
            string assetId,
            out string prefabAssetPath,
            out string error);
    }

    public static class UIEditorAssetIdResolverRegistry
    {
        private static IUIEditorAssetIdResolver current =
            new ResourcesUIEditorAssetIdResolver();

        public static IUIEditorAssetIdResolver Current
        {
            get { return current; }
        }

        public static void SetResolver(IUIEditorAssetIdResolver resolver)
        {
            current = resolver ??
                throw new ArgumentNullException(nameof(resolver));
        }

        public static void ResetToResources()
        {
            current = new ResourcesUIEditorAssetIdResolver();
        }
    }

    public sealed class ResourcesUIEditorAssetIdResolver :
        IUIEditorAssetIdResolver
    {
        private const string ResourcesPrefix = "Assets/Resources/";
        private const string PrefabExtension = ".prefab";

        public bool TryGetAssetId(
            string prefabAssetPath,
            out string assetId,
            out string error)
        {
            assetId = string.Empty;
            error = string.Empty;
            string normalized =
                (prefabAssetPath ?? string.Empty).Replace('\\', '/');
            if (!normalized.StartsWith(
                    ResourcesPrefix,
                    StringComparison.Ordinal) ||
                !normalized.EndsWith(
                    PrefabExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "Prefab must be under Assets/Resources for the default " +
                    "editor asset-id resolver. Path=" + normalized;
                return false;
            }

            string relative = normalized.Substring(ResourcesPrefix.Length);
            assetId = relative.Substring(
                0,
                relative.Length - PrefabExtension.Length);
            return !string.IsNullOrWhiteSpace(assetId);
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
                error = "Asset id is empty.";
                return false;
            }

            string candidate =
                ResourcesPrefix + assetId.Replace('\\', '/') +
                PrefabExtension;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(candidate) != null)
            {
                prefabAssetPath = candidate;
                return true;
            }

            error = "Resources prefab was not found. AssetId=" + assetId;
            return false;
        }
    }
}
