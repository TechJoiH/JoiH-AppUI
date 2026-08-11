using System;
using System.Collections.Generic;

namespace Joi.H.AppUI
{
    public sealed class UIPageFlowContract
    {
        private readonly Func<UIFlowContextBase, IUIFlowCommandResult, object> openDataFactory;

        public UIPageFlowContract(
            string pageId,
            string openDataTypeName,
            bool isImplemented,
            UIFlowPageKind pageKind,
            UIFlowCancelRule cancelRule,
            bool isFullScreen,
            bool blockLowerLayerInput,
            bool closeOnCancel,
            UIFlowActionKind defaultFlowAction,
            Func<UIFlowContextBase, IUIFlowCommandResult, object> openDataFactory)
        {
            PageId = pageId ?? string.Empty;
            OpenDataTypeName = openDataTypeName ?? string.Empty;
            IsImplemented = isImplemented;
            PageKind = pageKind;
            CancelRule = cancelRule;
            IsFullScreen = isFullScreen;
            BlockLowerLayerInput = blockLowerLayerInput;
            CloseOnCancel = closeOnCancel;
            DefaultFlowAction = defaultFlowAction;
            this.openDataFactory = openDataFactory;
        }

        public string PageId { get; private set; }
        public string OpenDataTypeName { get; private set; }
        public bool IsImplemented { get; private set; }
        public UIFlowPageKind PageKind { get; private set; }
        public UIFlowCancelRule CancelRule { get; private set; }
        public bool IsFullScreen { get; private set; }
        public bool BlockLowerLayerInput { get; private set; }
        public bool CloseOnCancel { get; private set; }
        public UIFlowActionKind DefaultFlowAction { get; private set; }

        public bool TryCreateOpenData(
            UIFlowContextBase context,
            IUIFlowCommandResult result,
            out object openData)
        {
            openData = null;
            if (openDataFactory == null)
            {
                return false;
            }

            openData = openDataFactory(context, result);
            return true;
        }
    }

    public interface IUIPageFlowContractRegistry
    {
        bool TryGet(string pageId, out UIPageFlowContract contract);
        bool Contains(string pageId);
    }

    public sealed class UIPageFlowContractRegistry : IUIPageFlowContractRegistry
    {
        private readonly List<UIPageFlowContract> contracts =
            new List<UIPageFlowContract>();
        private readonly Dictionary<string, UIPageFlowContract> contractByPageId =
            new Dictionary<string, UIPageFlowContract>();

        public IReadOnlyList<UIPageFlowContract> Contracts
        {
            get { return contracts; }
        }

        public void Register(UIPageFlowContract contract)
        {
            if (contract == null || string.IsNullOrEmpty(contract.PageId))
            {
                return;
            }

            if (!contractByPageId.ContainsKey(contract.PageId))
            {
                contracts.Add(contract);
                contractByPageId.Add(contract.PageId, contract);
                return;
            }

            contractByPageId[contract.PageId] = contract;
            for (int i = 0; i < contracts.Count; i++)
            {
                if (string.Equals(
                    contracts[i].PageId,
                    contract.PageId,
                    StringComparison.Ordinal))
                {
                    contracts[i] = contract;
                    return;
                }
            }
        }

        public bool TryGet(string pageId, out UIPageFlowContract contract)
        {
            contract = null;
            if (string.IsNullOrEmpty(pageId))
            {
                return false;
            }

            return contractByPageId.TryGetValue(pageId, out contract);
        }

        public bool Contains(string pageId)
        {
            return !string.IsNullOrEmpty(pageId) &&
                   contractByPageId.ContainsKey(pageId);
        }
    }

    public sealed class UIFlowSemanticExpectation
    {
        public UIFlowSemanticExpectation(
            string operationId,
            bool success,
            string errorCode,
            string nextPageId,
            UIFlowActionKind flowAction)
        {
            OperationId = operationId ?? string.Empty;
            Success = success;
            ErrorCode = errorCode ?? string.Empty;
            NextPageId = nextPageId ?? string.Empty;
            FlowAction = flowAction;
        }

        public string OperationId { get; private set; }
        public bool Success { get; private set; }
        public string ErrorCode { get; private set; }
        public string NextPageId { get; private set; }
        public UIFlowActionKind FlowAction { get; private set; }
    }
}
