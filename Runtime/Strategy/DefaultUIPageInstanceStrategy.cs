using System;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Default symmetric instance strategy: instantiate under the requested UI
    /// parent, destroy on release, and return the lease immediately.
    /// </summary>
    public sealed class DefaultUIPageInstanceStrategy :
        IUIPageInstanceStrategy
    {
        public string StrategyId => string.Empty;

        public UIPageInstanceAllocation Create(
            UIPageInstanceCreationRequest request)
        {
            if (request.Prefab == null)
            {
                throw new ArgumentException(
                    "Page prefab is required.",
                    nameof(request));
            }

            if (request.Parent == null)
            {
                throw new ArgumentException(
                    "Page parent is required.",
                    nameof(request));
            }

            if (request.AssetLeaseTransfer == null)
            {
                throw new ArgumentException(
                    "Asset lease transfer is required.",
                    nameof(request));
            }

            UIAssetLeaseClaim claim =
                request.AssetLeaseTransfer.Claim();
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(
                    request.Prefab,
                    request.Parent,
                    false);
                instance.name = request.Prefab.name;
                instance.SetActive(false);
                return new UIPageInstanceAllocation(
                    instance,
                    claim,
                    ReleaseDefaultInstance);
            }
            catch
            {
                claim.Dispose();
                if (instance != null)
                {
                    DestroyInstance(instance);
                }

                throw;
            }
        }

        private static UIPageInstanceReleaseDisposition
            ReleaseDefaultInstance(UIPageInstanceReleaseContext context)
        {
            if (context.GameObject != null)
            {
                DestroyInstance(context.GameObject);
            }

            return UIPageInstanceReleaseDisposition.ReleaseLease;
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
