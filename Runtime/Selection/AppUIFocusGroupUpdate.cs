using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public enum AppUIFocusGroupUpdateResult
    {
        Started,
        Completed,
        Aborted,
        TransactionAlreadyActive,
        ScopeDisposed,
        StaleRevision,
        ValidationFailed,
    }

    internal readonly struct AppUIFocusStagedNode
    {
        public AppUIFocusStagedNode(
            AppUIFocusNodeKey nodeKey,
            Selectable selectable,
            IAppUIFocusControlPolicy controlPolicy,
            int order)
        {
            NodeKey = nodeKey;
            Selectable = selectable;
            ControlPolicy = controlPolicy;
            Order = order;
        }

        public AppUIFocusNodeKey NodeKey { get; }

        public Selectable Selectable { get; }

        public IAppUIFocusControlPolicy ControlPolicy { get; }

        public int Order { get; }

    }

    /// <summary>
    /// 一个 Group 的完整快照更新。Register 只写入 staging；Complete 成功前活动快照不变。
    /// </summary>
    public sealed class AppUIFocusGroupUpdateTransaction : IDisposable
    {
        private enum TransactionState
        {
            Active,
            Completed,
            Aborted,
            ScopeDisposed,
        }

        private readonly AppUIFocusScope owner;
        private readonly List<AppUIFocusStagedNode> stagedNodes =
            new List<AppUIFocusStagedNode>(16);
        private TransactionState state = TransactionState.Active;
        private bool hasValidationFailure;

        internal AppUIFocusGroupUpdateTransaction(
            AppUIFocusScope scope,
            string groupId,
            int capturedScopeRevision,
            int capturedGroupRevision)
        {
            owner = scope ?? throw new ArgumentNullException(nameof(scope));
            ScopeId = scope.ScopeId;
            GroupId = groupId ?? string.Empty;
            CapturedScopeRevision = capturedScopeRevision;
            CapturedGroupRevision = capturedGroupRevision;
        }

        public string ScopeId { get; }

        public string GroupId { get; }

        public int CapturedScopeRevision { get; }

        public int CapturedGroupRevision { get; }

        internal IReadOnlyList<AppUIFocusStagedNode> StagedNodes
        {
            get { return stagedNodes; }
        }

        internal bool HasValidationFailure
        {
            get { return hasValidationFailure; }
        }

        public bool Register(
            AppUIFocusNodeKey nodeKey,
            Selectable selectable,
            int order = 0)
        {
            return Register(nodeKey, selectable, null, order);
        }

        public bool Register(
            AppUIFocusNodeKey nodeKey,
            Selectable selectable,
            IAppUIFocusControlPolicy controlPolicy,
            int order = 0)
        {
            EnsureActive();
            if (!nodeKey.IsValid || selectable == null || selectable.gameObject == null)
            {
                hasValidationFailure = true;
                return false;
            }

            stagedNodes.Add(
                new AppUIFocusStagedNode(
                    nodeKey,
                    selectable,
                    controlPolicy,
                    order));
            return true;
        }

        public AppUIFocusGroupUpdateResult Complete()
        {
            if (state == TransactionState.ScopeDisposed)
            {
                state = TransactionState.Completed;
                return AppUIFocusGroupUpdateResult.ScopeDisposed;
            }

            EnsureActive();
            AppUIFocusGroupUpdateResult result = owner.CompleteGroupUpdate(this);
            state = TransactionState.Completed;
            return result;
        }

        public AppUIFocusGroupUpdateResult Abort()
        {
            if (state == TransactionState.ScopeDisposed)
            {
                return AppUIFocusGroupUpdateResult.ScopeDisposed;
            }

            if (state == TransactionState.Aborted)
            {
                return AppUIFocusGroupUpdateResult.Aborted;
            }

            if (state == TransactionState.Completed)
            {
                throw new InvalidOperationException(
                    "A completed AppUI focus group update cannot be aborted.");
            }

            owner.AbortGroupUpdate(this);
            state = TransactionState.Aborted;
            return AppUIFocusGroupUpdateResult.Aborted;
        }

        public void Dispose()
        {
            if (state == TransactionState.Active)
            {
                Abort();
            }
        }

        internal void NotifyScopeDisposed()
        {
            if (state == TransactionState.Active)
            {
                state = TransactionState.ScopeDisposed;
            }
        }

        private void EnsureActive()
        {
            if (state != TransactionState.Active)
            {
                throw new InvalidOperationException(
                    "AppUI focus group update is no longer active.");
            }
        }
    }
}
