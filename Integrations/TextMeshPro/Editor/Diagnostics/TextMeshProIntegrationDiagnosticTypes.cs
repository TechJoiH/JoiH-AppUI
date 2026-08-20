using UnityEngine;

namespace Joi.H.AppUI.Integrations.TextMeshPro.Editor
{
    internal enum TextMeshProIntegrationDiagnosticState
    {
        Pass,
        Warning,
        Failure,
        NotVerifiable,
    }

    internal readonly struct TextMeshProIntegrationDiagnostic
    {
        public TextMeshProIntegrationDiagnostic(
            string code,
            TextMeshProIntegrationDiagnosticState state,
            string fact,
            string impact,
            string fix,
            Object context = null)
        {
            Code = code ?? string.Empty;
            State = state;
            Fact = fact ?? string.Empty;
            Impact = impact ?? string.Empty;
            Fix = fix ?? string.Empty;
            Context = context;
        }

        public string Code { get; }
        public TextMeshProIntegrationDiagnosticState State { get; }
        public string Fact { get; }
        public string Impact { get; }
        public string Fix { get; }
        public Object Context { get; }
    }
}
