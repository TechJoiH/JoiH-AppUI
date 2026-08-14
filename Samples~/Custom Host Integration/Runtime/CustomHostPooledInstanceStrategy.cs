using System;
using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI.Samples.CustomHost
{
    /// <summary>
    /// Optional one-entry-per-AssetId pool demonstrating symmetric instance
    /// and asset ownership. The installer evicts the pool after AppUI shutdown.
    /// </summary>
    public sealed class CustomHostPooledInstanceStrategy :
        IUIPageInstanceStrategy,
        IDisposable
    {
        public const string Id = "sample.custom-host.pool";

        private readonly Dictionary<string, PoolEntry> entries =
            new Dictionary<string, PoolEntry>(StringComparer.Ordinal);
        private bool disposed;

        public string StrategyId => Id;

        public UIPageInstanceAllocation Create(
            UIPageInstanceCreationRequest request)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(CustomHostPooledInstanceStrategy));
            }

            if (request.Definition == null || request.Prefab == null ||
                request.Parent == null || request.AssetLeaseTransfer == null)
            {
                throw new ArgumentException(
                    "Definition, prefab, parent, and lease transfer are required.",
                    nameof(request));
            }

            string key = request.Definition.PrefabAssetId;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "Definition.PrefabAssetId is required for sample pooling.",
                    nameof(request));
            }

            UIAssetLeaseClaim claim = request.AssetLeaseTransfer.Claim();
            GameObject instance = null;
            try
            {
                if (entries.TryGetValue(key, out PoolEntry retained))
                {
                    entries.Remove(key);
                    retained.Lease?.Dispose();
                    instance = retained.Instance;
                }

                if (instance == null)
                {
                    instance = UnityEngine.Object.Instantiate(
                        request.Prefab,
                        request.Parent,
                        false);
                    instance.name = request.Prefab.name;
                }
                else
                {
                    instance.transform.SetParent(request.Parent, false);
                }

                instance.SetActive(false);
                return new UIPageInstanceAllocation(
                    instance,
                    claim,
                    context => Release(key, context));
            }
            catch
            {
                claim.Dispose();
                Destroy(instance);
                throw;
            }
        }

        public void EvictAll()
        {
            foreach (KeyValuePair<string, PoolEntry> pair in entries)
            {
                Destroy(pair.Value.Instance);
                pair.Value.Lease?.Dispose();
            }

            entries.Clear();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            EvictAll();
        }

        private UIPageInstanceReleaseDisposition Release(
            string key,
            UIPageInstanceReleaseContext context)
        {
            if (!context.OwnsAssetLease || disposed ||
                context.GameObject == null)
            {
                Destroy(context.GameObject);
                return UIPageInstanceReleaseDisposition.ReleaseLease;
            }

            if (entries.TryGetValue(key, out PoolEntry previous))
            {
                Destroy(previous.Instance);
                previous.Lease?.Dispose();
            }

            context.GameObject.SetActive(false);
            entries[key] = new PoolEntry(
                context.GameObject,
                context.AssetLease);
            return UIPageInstanceReleaseDisposition.RetainLease;
        }

        private static void Destroy(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private readonly struct PoolEntry
        {
            public PoolEntry(GameObject instance, UIAssetLease lease)
            {
                Instance = instance;
                Lease = lease;
            }

            public GameObject Instance { get; }
            public UIAssetLease Lease { get; }
        }
    }
}
