using Joi.H.AppUI.Integrations.TextMeshPro.Editor;
using NUnit.Framework;

namespace Joi.H.AppUI.Tests.TextMeshPro
{
    public sealed class TextMeshProIntegrationDiagnosticsTests
    {
        [Test]
        public void Diagnostics_ProviderSelectedAndValid_ReportsPass()
        {
            Assert.That(
                TextMeshProIntegrationDiagnostics.EvaluateProviderEnabled(true, true).State,
                Is.EqualTo(TextMeshProIntegrationDiagnosticState.Pass));
            Assert.That(
                TextMeshProIntegrationDiagnostics.EvaluateBindingRules(true, string.Empty).State,
                Is.EqualTo(TextMeshProIntegrationDiagnosticState.Pass));
        }

        [Test]
        public void Diagnostics_SelectedProviderMissing_ReportsFailureWithFix()
        {
            TextMeshProIntegrationDiagnostic result =
                TextMeshProIntegrationDiagnostics.EvaluateProviderEnabled(false, true);
            Assert.That(result.State, Is.EqualTo(TextMeshProIntegrationDiagnosticState.Failure));
            Assert.That(result.Fix, Does.Contain("joih.appui.tmp"));
        }

        [Test]
        public void Diagnostics_NoticeDisabled_ReportsPassWithoutPrefab()
        {
            Assert.That(
                TextMeshProIntegrationDiagnostics.EvaluateNotice(false, false, "Toast").State,
                Is.EqualTo(TextMeshProIntegrationDiagnosticState.Pass));
        }

        [Test]
        public void Diagnostics_EnabledNoticeMissingPrefab_ReportsFailure()
        {
            Assert.That(
                TextMeshProIntegrationDiagnostics.EvaluateNotice(true, false, "Toast").State,
                Is.EqualTo(TextMeshProIntegrationDiagnosticState.Failure));
        }

        [Test]
        public void Diagnostics_NoInitializedHost_ReportsNotVerifiable()
        {
            Assert.That(
                TextMeshProIntegrationDiagnostics.EvaluateHost("Host", false, new string[0]).State,
                Is.EqualTo(TextMeshProIntegrationDiagnosticState.NotVerifiable));
        }

        [Test]
        public void Diagnostics_InitializedHostWithResolver_ReportsPass()
        {
            Assert.That(
                TextMeshProIntegrationDiagnostics.EvaluateHost(
                    "Host", true, new[] { "joih.appui.tmp.input-field" }).State,
                Is.EqualTo(TextMeshProIntegrationDiagnosticState.Pass));
        }

        [Test]
        public void Diagnostics_InitializedHostWithoutResolver_ReportsWarning()
        {
            Assert.That(
                TextMeshProIntegrationDiagnostics.EvaluateHost("Host", true, new string[0]).State,
                Is.EqualTo(TextMeshProIntegrationDiagnosticState.Warning));
        }

        [Test]
        public void Diagnostics_MultipleHosts_AreReportedSeparately()
        {
            TextMeshProIntegrationDiagnostic first =
                TextMeshProIntegrationDiagnostics.EvaluateHost(
                    "First", true, new[] { "joih.appui.tmp.input-field" });
            TextMeshProIntegrationDiagnostic second =
                TextMeshProIntegrationDiagnostics.EvaluateHost("Second", true, new string[0]);
            Assert.That(first.Fact, Does.Contain("First"));
            Assert.That(second.Fact, Does.Contain("Second"));
            Assert.That(first.State, Is.Not.EqualTo(second.State));
        }
    }
}
