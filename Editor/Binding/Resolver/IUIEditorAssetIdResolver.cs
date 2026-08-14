using System;
using System.Collections.Generic;
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
        string ResolverId { get; }

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
        private static readonly Dictionary<string, IUIEditorAssetIdResolver>
            resolvers =
                new Dictionary<string, IUIEditorAssetIdResolver>(
                    StringComparer.Ordinal);

        public static bool Register(
            IUIEditorAssetIdResolver resolver,
            out string error)
        {
            error = string.Empty;
            if (resolver == null)
            {
                error = "Cannot register a null IUIEditorAssetIdResolver.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(resolver.ResolverId))
            {
                error =
                    "IUIEditorAssetIdResolver.ResolverId must be non-empty.";
                return false;
            }

            if (resolvers.ContainsKey(resolver.ResolverId))
            {
                error = "Duplicate editor AssetId resolver id was rejected: " +
                        resolver.ResolverId;
                return false;
            }

            resolvers.Add(resolver.ResolverId, resolver);
            return true;
        }

        public static bool TryGet(
            string resolverId,
            out IUIEditorAssetIdResolver resolver)
        {
            if (string.IsNullOrEmpty(resolverId))
            {
                resolver = null;
                return false;
            }

            return resolvers.TryGetValue(resolverId, out resolver);
        }

        public static bool TryGetSelected(
            UIBindingSettings settings,
            out IUIEditorAssetIdResolver resolver,
            out string error)
        {
            resolver = null;
            error = string.Empty;
            if (settings == null)
            {
                error =
                    "UIBindingSettings is missing. Open Project Settings > App UI Binding.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.SelectedAssetIdResolverId))
            {
                error =
                    "UIBindingSettings.SelectedAssetIdResolverId is empty. " +
                    "Choose a registered resolver in Project Settings > App UI Binding.";
                return false;
            }

            if (!resolvers.TryGetValue(
                    settings.SelectedAssetIdResolverId,
                    out resolver))
            {
                error =
                    "No IUIEditorAssetIdResolver is registered for id '" +
                    settings.SelectedAssetIdResolverId +
                    "'. Register that resolver before using AppUI Editor tools.";
                return false;
            }

            return true;
        }

        public static string[] GetRegisteredResolverIds()
        {
            string[] ids = new string[resolvers.Count];
            resolvers.Keys.CopyTo(ids, 0);
            Array.Sort(ids, StringComparer.Ordinal);
            return ids;
        }

        public static void Clear()
        {
            resolvers.Clear();
        }
    }

    public sealed class ResourcesUIEditorAssetIdResolver :
        IUIEditorAssetIdResolver
    {
        public const string Id = "resources";
        private const string ResourcesPrefix = "Assets/Resources/";
        private const string PrefabExtension = ".prefab";

        public string ResolverId
        {
            get { return Id; }
        }

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
