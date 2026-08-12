using System.Threading;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Host-facing metadata supplied when AppUI requests an operation source.
    /// </summary>
    public readonly struct AppUIOperationDescriptor
    {
        private AppUIOperationDescriptor(
            string name,
            CancellationToken cancellationToken)
        {
            Name = name ?? string.Empty;
            CancellationToken = cancellationToken;
        }

        public string Name { get; }

        public CancellationToken CancellationToken { get; }

        public static AppUIOperationDescriptor Create(
            string name,
            CancellationToken cancellationToken = default)
        {
            return new AppUIOperationDescriptor(name, cancellationToken);
        }
    }
}
