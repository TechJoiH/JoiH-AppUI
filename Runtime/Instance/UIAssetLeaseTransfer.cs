using System;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Two-phase, one-shot transfer of a provider lease from AppUI to an
    /// instance strategy. Disposing an uncommitted transfer returns the lease
    /// to the provider even when a strategy abandoned a claim.
    /// </summary>
    public sealed class UIAssetLeaseTransfer : IDisposable
    {
        private readonly object sync = new object();
        private UIAssetLease assetLease;
        private UIAssetLeaseClaim activeClaim;
        private bool transferred;
        private bool disposed;

        public UIAssetLeaseTransfer(UIAssetLease lease)
        {
            assetLease = lease;
        }

        public bool IsTransferred
        {
            get
            {
                lock (sync)
                {
                    return transferred;
                }
            }
        }

        public UIAssetLeaseClaim Claim()
        {
            lock (sync)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(
                        nameof(UIAssetLeaseTransfer));
                }

                if (transferred || activeClaim != null)
                {
                    throw new InvalidOperationException(
                        "The asset lease transfer is already claimed.");
                }

                activeClaim = new UIAssetLeaseClaim(this, assetLease);
                return activeClaim;
            }
        }

        public void Dispose()
        {
            UIAssetLease release = null;
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                activeClaim = null;
                if (!transferred)
                {
                    release = assetLease;
                }

                assetLease = null;
            }

            release?.Dispose();
        }

        internal bool TryCommit(UIAssetLeaseClaim claim)
        {
            lock (sync)
            {
                if (disposed || transferred || claim == null ||
                    !ReferenceEquals(activeClaim, claim))
                {
                    return false;
                }

                transferred = true;
                activeClaim = null;
                assetLease = null;
                return true;
            }
        }

        internal void ReleaseClaim(UIAssetLeaseClaim claim)
        {
            lock (sync)
            {
                if (!disposed && !transferred &&
                    ReferenceEquals(activeClaim, claim))
                {
                    activeClaim = null;
                }
            }
        }
    }

    /// <summary>
    /// Reservation created by <see cref="UIAssetLeaseTransfer.Claim"/>. Only
    /// AppUI accepts the allocation and commits the reservation.
    /// </summary>
    public sealed class UIAssetLeaseClaim : IDisposable
    {
        private UIAssetLeaseTransfer owner;
        private bool committed;
        private bool disposed;

        internal UIAssetLeaseClaim(
            UIAssetLeaseTransfer transfer,
            UIAssetLease lease)
        {
            owner = transfer;
            AssetLease = lease;
        }

        public UIAssetLease AssetLease { get; }

        public bool IsCommitted => committed;

        public void Dispose()
        {
            UIAssetLeaseTransfer currentOwner;
            lock (this)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                currentOwner = owner;
                owner = null;
            }

            if (!committed)
            {
                currentOwner?.ReleaseClaim(this);
            }
        }

        internal bool TryCommit(UIAssetLeaseTransfer expectedOwner)
        {
            lock (this)
            {
                if (disposed || committed || owner == null ||
                    !ReferenceEquals(owner, expectedOwner) ||
                    !owner.TryCommit(this))
                {
                    return false;
                }

                committed = true;
                owner = null;
                return true;
            }
        }
    }
}
