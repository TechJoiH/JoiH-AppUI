using System.Collections.Generic;

namespace Joi.H.AppUI
{
    public sealed class UIFlowSemanticValidationReport
    {
        private readonly List<string> errors = new List<string>();

        public IReadOnlyList<string> Errors
        {
            get { return errors; }
        }

        public bool Success
        {
            get { return errors.Count == 0; }
        }

        public void AddError(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                errors.Add(error);
            }
        }
    }

    public static class UIFlowSemanticValidator
    {
        public static void ValidateResult(
            UIFlowSemanticExpectation expectation,
            IUIFlowCommandResult result,
            UIFlowSemanticValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (expectation == null)
            {
                report.AddError("Semantic expectation is missing.");
                return;
            }

            if (result == null)
            {
                report.AddError(
                    "Semantic result is missing for " +
                    expectation.OperationId +
                    ".");
                return;
            }

            if (result.Success != expectation.Success)
            {
                report.AddError(
                    expectation.OperationId +
                    " Success mismatch.");
            }

            if (!string.Equals(
                    result.ErrorCode ?? string.Empty,
                    expectation.ErrorCode ?? string.Empty,
                    System.StringComparison.Ordinal))
            {
                report.AddError(
                    expectation.OperationId +
                    " ErrorCode mismatch.");
            }

            if (!string.Equals(
                    result.NextPageId ?? string.Empty,
                    expectation.NextPageId ?? string.Empty,
                    System.StringComparison.Ordinal))
            {
                report.AddError(
                    expectation.OperationId +
                    " NextPageId mismatch.");
            }

            if (result.FlowAction != expectation.FlowAction)
            {
                report.AddError(
                    expectation.OperationId +
                    " FlowAction mismatch.");
            }
        }
    }
}
