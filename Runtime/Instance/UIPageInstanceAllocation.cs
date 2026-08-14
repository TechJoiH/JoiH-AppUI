using System;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Tells AppUI whether an accepted instance strategy retained the provider
    /// lease after the page left the active AppUI instance registry.
    /// </summary>
    public enum UIPageInstanceReleaseDisposition
    {
        ReleaseLease,
        RetainLease,
    }

    public readonly struct UIPageInstanceReleaseContext
    {
        internal UIPageInstanceReleaseContext(
            GameObject gameObject,
            UIAssetLease assetLease,
            bool ownsAssetLease)
        {
            GameObject = gameObject;
            AssetLease = assetLease;
            OwnsAssetLease = ownsAssetLease;
        }

        public GameObject GameObject { get; }
        public UIAssetLease AssetLease { get; }
        public bool OwnsAssetLease { get; }
    }

    public delegate UIPageInstanceReleaseDisposition
        UIPageInstanceReleaseHandler(UIPageInstanceReleaseContext context);

    /// <summary>
    /// Symmetric lifetime handle returned by an instance strategy. AppUI first
    /// validates its GameObject, then atomically accepts its lease claim. Dispose
    /// invokes the strategy release handler exactly once.
    /// </summary>
    public sealed class UIPageInstanceAllocation : IDisposable
    {
        private readonly object sync = new object();
        private readonly UIAssetLeaseClaim claim;
        private readonly UIPageInstanceReleaseHandler releaseHandler;
        private bool accepted;
        private bool released;

        public UIPageInstanceAllocation(
            GameObject gameObject,
            UIAssetLeaseClaim assetLeaseClaim,
            UIPageInstanceReleaseHandler handler)
        {
            GameObject = gameObject ??
                throw new ArgumentNullException(nameof(gameObject));
            claim = assetLeaseClaim ??
                throw new ArgumentNullException(nameof(assetLeaseClaim));
            releaseHandler = handler ??
                throw new ArgumentNullException(nameof(handler));
        }

        public GameObject GameObject { get; }

        public bool IsAccepted
        {
            get
            {
                lock (sync)
                {
                    return accepted;
                }
            }
        }

        public void Dispose()
        {
            ReleaseCore();
        }

        internal bool TryAccept(UIAssetLeaseTransfer transfer)
        {
            lock (sync)
            {
                if (released || accepted || transfer == null ||
                    !claim.TryCommit(transfer))
                {
                    return false;
                }

                accepted = true;
                return true;
            }
        }

        internal void Reject()
        {
            ReleaseCore();
        }

        private void ReleaseCore()
        {
            bool ownsLease;
            lock (sync)
            {
                if (released)
                {
                    return;
                }

                released = true;
                ownsLease = accepted;
            }

            UIPageInstanceReleaseDisposition disposition =
                UIPageInstanceReleaseDisposition.ReleaseLease;
            Exception releaseException = null;
            try
            {
                disposition = releaseHandler.Invoke(
                    new UIPageInstanceReleaseContext(
                        GameObject,
                        claim.AssetLease,
                        ownsLease));
            }
            catch (Exception exception)
            {
                releaseException = exception;
            }
            finally
            {
                if (!ownsLease)
                {
                    claim.Dispose();
                }
                else if (disposition !=
                             UIPageInstanceReleaseDisposition.RetainLease &&
                         claim.AssetLease != null &&
                         claim.AssetLease.IsValid)
                {
                    claim.AssetLease.Dispose();
                }
            }

            if (releaseException != null)
            {
                throw releaseException;
            }
        }
    }
}
