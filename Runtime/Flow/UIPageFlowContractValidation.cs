using System.Collections.Generic;

namespace Joi.H.AppUI
{
    public sealed class UIPageFlowContractValidationReport
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

    public static class UIPageFlowContractValidator
    {
        public static UIPageFlowContractValidationReport Validate(
            UIPageFlowContractRegistry flowRegistry,
            UIPageDefinitionRegistry definitionRegistry)
        {
            UIPageFlowContractValidationReport report =
                new UIPageFlowContractValidationReport();
            if (flowRegistry == null)
            {
                report.AddError("Flow contract registry is missing.");
                return report;
            }

            IReadOnlyList<UIPageFlowContract> contracts = flowRegistry.Contracts;
            for (int i = 0; i < contracts.Count; i++)
            {
                ValidateContract(contracts[i], definitionRegistry, report);
            }

            return report;
        }

        private static void ValidateContract(
            UIPageFlowContract contract,
            UIPageDefinitionRegistry definitionRegistry,
            UIPageFlowContractValidationReport report)
        {
            if (contract == null)
            {
                report.AddError("Flow contract contains a null entry.");
                return;
            }

            if (string.IsNullOrEmpty(contract.PageId))
            {
                report.AddError("Flow contract page id is empty.");
                return;
            }

            if (string.IsNullOrEmpty(contract.OpenDataTypeName))
            {
                report.AddError(
                    "Flow contract [" +
                    contract.PageId +
                    "] has no OpenData type.");
            }

            UIPageDefinition definition = null;
            bool hasDefinition = definitionRegistry != null &&
                                 definitionRegistry.TryGet(
                                     contract.PageId,
                                     out definition);
            if (contract.IsImplemented && !hasDefinition)
            {
                report.AddError(
                    "Implemented flow page [" +
                    contract.PageId +
                    "] has no UIPageDefinition.");
                return;
            }

            if (!hasDefinition || definition == null)
            {
                return;
            }

            if (definition.IsFullScreen != contract.IsFullScreen)
            {
                report.AddError(
                    "Flow contract [" +
                    contract.PageId +
                    "] IsFullScreen does not match UIPageDefinition.");
            }

            if (definition.BlockLowerLayerInput != contract.BlockLowerLayerInput)
            {
                report.AddError(
                    "Flow contract [" +
                    contract.PageId +
                    "] BlockLowerLayerInput does not match UIPageDefinition.");
            }

            if (definition.CloseOnCancel != contract.CloseOnCancel)
            {
                report.AddError(
                    "Flow contract [" +
                    contract.PageId +
                    "] CloseOnCancel does not match UIPageDefinition.");
            }
        }
    }
}
