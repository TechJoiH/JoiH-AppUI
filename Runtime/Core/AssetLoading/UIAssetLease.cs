using System;
using System.Threading;

namespace Joi.H.AppUI
{
    /// <summary>
    /// One-shot ownership token returned by an asset provider.
    /// Dispose is idempotent and invokes the provider release callback at most once.
    /// </summary>
    public sealed class UIAssetLease : IDisposable
    {
        private Action release;

        public UIAssetLease(Action releaseAction)
        {
            release = releaseAction ??
                throw new ArgumentNullException(nameof(releaseAction));
        }

        public bool IsValid
        {
            get { return Volatile.Read(ref release) != null; }
        }

        public void Dispose()
        {
            Action callback = Interlocked.Exchange(ref release, null);
            callback?.Invoke();
        }
    }
}
