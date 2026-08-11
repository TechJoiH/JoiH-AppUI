using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    internal enum AppUIFocusCancelDispatchResult
    {
        Continue = 0,
        Consumed = 1,
        Failed = 2,
    }

    /// <summary>
    /// AppUI 焦点组合服务：持有当前运行期唯一 NodeRegistry、Committer 和 SelectionObserver，
    /// 并只根据 UIPresentationCoordinator 发布的 Snapshot 驱动 Scope 资格。
    /// </summary>
    public sealed class UIFocusService
    {
        private readonly UIDefaultFocusResolver defaultFocusResolver =
            new UIDefaultFocusResolver();
        private readonly AppUIFocusNodeRegistry nodeRegistry;
        private readonly UIFocusCommitter focusCommitter;
        private readonly UIFocusSelectionObserver selectionObserver;
        private readonly Dictionary<long, AppUIFocusScope> scopesByPageInstance =
            new Dictionary<long, AppUIFocusScope>(16);
        private readonly Dictionary<string, long> pageInstanceByScopeId =
            new Dictionary<string, long>(16, StringComparer.Ordinal);

        private UIPageInstanceRegistry instanceRegistry;
        private UIInteractionSnapshot currentInteractionSnapshot =
            UIInteractionSnapshot.Empty;
        private AppUIFocusScope activeScope;

        public UIFocusService()
        {
            nodeRegistry = new AppUIFocusNodeRegistry();
            focusCommitter = new UIFocusCommitter(this, nodeRegistry);
            selectionObserver = new UIFocusSelectionObserver(
                this,
                focusCommitter);
        }

        internal AppUIFocusNodeRegistry NodeRegistry
        {
            get { return nodeRegistry; }
        }

        internal IUIFocusCommitter Committer
        {
            get { return focusCommitter; }
        }

        internal UIInteractionSnapshot CurrentInteractionSnapshot
        {
            get { return currentInteractionSnapshot; }
        }

        internal int ActiveScopeRevision
        {
            get { return activeScope != null ? activeScope.Revision : 0; }
        }

        internal void ConfigureInstanceRegistry(UIPageInstanceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (instanceRegistry != null && !ReferenceEquals(instanceRegistry, registry))
            {
                throw new InvalidOperationException(
                    "UIFocusService cannot switch UIPageInstanceRegistry during a runtime.");
            }

            instanceRegistry = registry;
        }

        internal IAppUIFocusScopeHandle AttachScope(
            UIPageInstance instance,
            AppUIFocusDefinition definition)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            AppUIFocusValidationReport validationReport =
                AppUIFocusDefinitionValidator.Validate(definition);
            if (!validationReport.Success)
            {
                throw new InvalidOperationException(
                    "Focus definition validation failed: " +
                    string.Join("; ", validationReport.Errors));
            }

            if (instance.RuntimeInstanceId <= 0)
            {
                throw new InvalidOperationException(
                    "FocusScope can only attach after UIPageInstanceRegistry registration.");
            }

            if (scopesByPageInstance.ContainsKey(instance.RuntimeInstanceId))
            {
                throw new InvalidOperationException(
                    "A FocusScope is already attached to page instance: " + instance.PageId);
            }

            string scopeId = string.IsNullOrWhiteSpace(definition.ScopeId)
                ? instance.PageId
                : definition.ScopeId;
            if (string.IsNullOrWhiteSpace(scopeId))
            {
                throw new InvalidOperationException("FocusScope requires a non-empty ScopeId.");
            }

            if (pageInstanceByScopeId.TryGetValue(scopeId, out long ownerInstanceId))
            {
                throw new InvalidOperationException(
                    "FocusScope id is already owned by another page instance. Scope=" +
                    scopeId +
                    ", InstanceId=" +
                    ownerInstanceId);
            }

            AppUIFocusScope scope = new AppUIFocusScope(
                instance,
                definition,
                scopeId,
                nodeRegistry,
                focusCommitter,
                selectionObserver);
            scopesByPageInstance.Add(instance.RuntimeInstanceId, scope);
            pageInstanceByScopeId.Add(scopeId, instance.RuntimeInstanceId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (definition.DebugTraceEnabled && instance.GameObject != null)
            {
                AppUIFocusDebugOverlay overlay =
                    instance.GameObject.GetComponent<AppUIFocusDebugOverlay>();
                if (overlay == null)
                {
                    overlay = instance.GameObject.AddComponent<AppUIFocusDebugOverlay>();
                }

                overlay.Configure(instance.RuntimeInstanceId);
            }
#endif
            return scope;
        }

        internal bool TryGetScope(
            UIPageInstance instance,
            out IAppUIFocusScopeHandle scopeHandle)
        {
            if (instance != null &&
                scopesByPageInstance.TryGetValue(
                    instance.RuntimeInstanceId,
                    out AppUIFocusScope scope))
            {
                scopeHandle = scope;
                return true;
            }

            scopeHandle = null;
            return false;
        }

        internal bool TryGetActiveScope(out AppUIFocusScope scope)
        {
            if (activeScope != null &&
                activeScope.Status == AppUIFocusScopeStatus.Active)
            {
                scope = activeScope;
                return true;
            }

            scope = null;
            return false;
        }

        internal AppUIFocusCancelDispatchResult TryHandleCancel(
            UIPageInstance instance,
            out Exception exception)
        {
            exception = null;
            if (instance == null ||
                activeScope == null ||
                activeScope.Status != AppUIFocusScopeStatus.Active ||
                activeScope.PageInstanceId != instance.RuntimeInstanceId ||
                activeScope.PageHandle != instance.ToInteractionHandle())
            {
                return AppUIFocusCancelDispatchResult.Continue;
            }

            try
            {
                EventSystem eventSystem = EventSystem.current;
                GameObject selectedObject = eventSystem != null
                    ? eventSystem.currentSelectedGameObject
                    : null;
                if (activeScope.TryGetFocusedControlPolicy(
                        selectedObject,
                        out string groupId,
                        out AppUIFocusNodeAddress nodeAddress,
                        out Selectable selectable,
                        out IAppUIFocusControlPolicy controlPolicy))
                {
                    AppUIFocusCancelContext controlContext =
                        new AppUIFocusCancelContext(
                            groupId,
                            nodeAddress,
                            selectable);
                    if (controlPolicy.TryHandleCancel(in controlContext) ==
                        AppUIFocusCancelHandlingResult.Consumed)
                    {
                        return AppUIFocusCancelDispatchResult.Consumed;
                    }
                }

                return activeScope.TryHandleActiveRegionCancel() ==
                       AppUIFocusCancelHandlingResult.Consumed
                    ? AppUIFocusCancelDispatchResult.Consumed
                    : AppUIFocusCancelDispatchResult.Continue;
            }
            catch (Exception caught)
            {
                exception = new InvalidOperationException(
                    "AppUI focus Cancel extension failed. Scope=" +
                    activeScope.ScopeId +
                    ", Region=" +
                    activeScope.ActiveRegionId,
                    caught);
                return AppUIFocusCancelDispatchResult.Failed;
            }
        }

        internal void ApplyInteractionSnapshot(UIInteractionSnapshot snapshot)
        {
            currentInteractionSnapshot = snapshot ?? UIInteractionSnapshot.Empty;
            activeScope = null;
            foreach (KeyValuePair<long, AppUIFocusScope> pair in scopesByPageInstance)
            {
                AppUIFocusScope scope = pair.Value;
                scope.ApplyInteractionSnapshot(currentInteractionSnapshot);
                if (scope.Status == AppUIFocusScopeStatus.Active)
                {
                    if (activeScope != null && !ReferenceEquals(activeScope, scope))
                    {
                        throw new InvalidOperationException(
                            "UIInteractionSnapshot activated more than one AppUI FocusScope.");
                    }

                    activeScope = scope;
                }
            }

            selectionObserver.MarkDirty();
            if (activeScope != null && activeScope.HasPendingRepair)
            {
                focusCommitter.QueueRepairForScope(activeScope);
            }
        }

        internal bool TryHandleSemanticSelection(
            UIPageInstance instance,
            AppUIFocusChangeReason reason)
        {
            if (instance == null ||
                !scopesByPageInstance.TryGetValue(
                    instance.RuntimeInstanceId,
                    out AppUIFocusScope scope))
            {
                return false;
            }

            EnsureScopeFocus(instance, scope, reason);
            return true;
        }

        internal bool TryValidateCommitRequest(
            in AppUIFocusCommitRequest request,
            out AppUIFocusScope scope,
            out AppUIFocusResolvedNode resolvedNode,
            out AppUIFocusRequestResult failure)
        {
            scope = null;
            resolvedNode = default;
            if (!request.IsValid ||
                currentInteractionSnapshot == null ||
                request.StackRevision != currentInteractionSnapshot.StackRevision ||
                request.PageHandle != currentInteractionSnapshot.TopInteractivePage)
            {
                failure = AppUIFocusRequestResult.StaleRevision;
                return false;
            }

            if (!scopesByPageInstance.TryGetValue(
                    request.PageHandle.InstanceId,
                    out scope))
            {
                failure = AppUIFocusRequestResult.ScopeInactive;
                return false;
            }

            if (instanceRegistry != null &&
                !instanceRegistry.TryResolve(request.PageHandle, out _))
            {
                failure = AppUIFocusRequestResult.StaleRevision;
                return false;
            }

            if (!nodeRegistry.TryResolveNode(
                    request.PageHandle,
                    request.NodeAddress,
                    out resolvedNode))
            {
                failure = AppUIFocusRequestResult.NodeMissing;
                return false;
            }

            if (!ReferenceEquals(resolvedNode.Selectable, request.Target) ||
                resolvedNode.RegistrationGeneration != request.RegistrationGeneration)
            {
                failure = AppUIFocusRequestResult.StaleRevision;
                return false;
            }

            return scope.TryValidateCommitRequest(
                in request,
                resolvedNode,
                out failure);
        }

        internal bool TryAcceptExternalSelection(
            AppUIFocusScope expectedActiveScope,
            AppUIFocusResolvedNode resolvedNode)
        {
            if (expectedActiveScope == null ||
                !ReferenceEquals(activeScope, expectedActiveScope) ||
                currentInteractionSnapshot.TopInteractivePage != resolvedNode.PageHandle ||
                !expectedActiveScope.IsResolvedNodeEligible(resolvedNode))
            {
                return false;
            }

            expectedActiveScope.AcceptExternalSelection(resolvedNode);
            expectedActiveScope.EnsureVisible(resolvedNode);
            return true;
        }

        internal void TryRepairActiveScope(long pageInstanceId)
        {
            if (activeScope == null ||
                activeScope.PageInstanceId != pageInstanceId ||
                activeScope.Status != AppUIFocusScopeStatus.Active)
            {
                return;
            }

            if (instanceRegistry == null ||
                !instanceRegistry.TryResolve(
                    activeScope.PageHandle,
                    out UIPageInstance instance))
            {
                activeScope.MarkPendingRepair();
                return;
            }

            EnsureScopeFocus(
                instance,
                activeScope,
                AppUIFocusChangeReason.SelectionRepair);
        }

        internal void NotifySelectionCleared(UIPageInteractionHandle previousOwner)
        {
            if (previousOwner.IsValid &&
                scopesByPageInstance.TryGetValue(
                    previousOwner.InstanceId,
                    out AppUIFocusScope ownerScope))
            {
                ownerScope.NotifySelectionCleared();
            }
        }

        internal void ClearCanonicalFocus()
        {
            foreach (KeyValuePair<long, AppUIFocusScope> pair in scopesByPageInstance)
            {
                pair.Value.NotifySelectionCleared();
            }
        }

        internal void ReconcileSelection()
        {
            selectionObserver.Reconcile();
            focusCommitter.DrainPendingRepair();
        }

        internal void DetachScope(UIPageInstance instance)
        {
            if (instance == null ||
                !scopesByPageInstance.TryGetValue(
                    instance.RuntimeInstanceId,
                    out AppUIFocusScope scope))
            {
                return;
            }

            scopesByPageInstance.Remove(instance.RuntimeInstanceId);
            if (ReferenceEquals(activeScope, scope))
            {
                activeScope = null;
            }

            if (pageInstanceByScopeId.TryGetValue(
                    scope.ScopeId,
                    out long ownerInstanceId) &&
                ownerInstanceId == instance.RuntimeInstanceId)
            {
                pageInstanceByScopeId.Remove(scope.ScopeId);
            }

            scope.Dispose();
            selectionObserver.MarkDirty();
        }

        internal void ClearScopes()
        {
            foreach (KeyValuePair<long, AppUIFocusScope> pair in scopesByPageInstance)
            {
                pair.Value.Dispose();
            }

            scopesByPageInstance.Clear();
            pageInstanceByScopeId.Clear();
            nodeRegistry.Clear();
            activeScope = null;
            currentInteractionSnapshot = UIInteractionSnapshot.Empty;
            focusCommitter.ResetObservationState();
            selectionObserver.Reset();
        }

        public void RestoreFocus(UIPageInstance instance)
        {
            if (instance == null || instance.GameObject == null)
            {
                return;
            }

            if (scopesByPageInstance.TryGetValue(
                    instance.RuntimeInstanceId,
                    out AppUIFocusScope scope))
            {
                EnsureScopeFocus(
                    instance,
                    scope,
                    AppUIFocusChangeReason.RestoreRequested);
                return;
            }

            Selectable selectable = defaultFocusResolver.Resolve(
                instance,
                UIDefaultFocusReason.RestoreRequested);
            if (selectable != null)
            {
                UIFocusCommitter.CommitLegacySelection(
                    selectable,
                    AppUIInteractionSourceKind.Programmatic);
            }
        }

        public void ClearIfOwned(UIPageInstance instance)
        {
            if (instance == null ||
                instance.GameObject == null ||
                EventSystem.current == null)
            {
                return;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null ||
                !selected.transform.IsChildOf(instance.GameObject.transform))
            {
                return;
            }

            if (currentInteractionSnapshot.StackRevision > 0)
            {
                AppUIFocusClearRequest request = new AppUIFocusClearRequest(
                    instance.ToInteractionHandle(),
                    currentInteractionSnapshot.StackRevision,
                    AppUIFocusClearReason.PageHidden);
                focusCommitter.ClearSelection(in request);
                return;
            }

            UIFocusCommitter.ClearLegacySelection();
        }

        private void EnsureScopeFocus(
            UIPageInstance instance,
            AppUIFocusScope scope,
            AppUIFocusChangeReason reason)
        {
            if (scope == null || scope.Status != AppUIFocusScopeStatus.Active)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            GameObject selectedObject = eventSystem != null
                ? eventSystem.currentSelectedGameObject
                : null;
            if (reason == AppUIFocusChangeReason.SelectionRepair &&
                selectedObject != null &&
                nodeRegistry.TryResolveNode(
                    selectedObject,
                    out AppUIFocusResolvedNode selectedNode) &&
                scope.IsResolvedNodeEligible(selectedNode))
            {
                if (scope.CurrentFocusedAddress != selectedNode.NodeAddress)
                {
                    scope.AcceptExternalSelection(selectedNode);
                }

                scope.ClearPendingRepair();
                return;
            }

            AppUIFocusResolvedNode targetNode;
            bool useHistory = reason == AppUIFocusChangeReason.Reopened ||
                              reason == AppUIFocusChangeReason.RestoreRequested ||
                              reason == AppUIFocusChangeReason.SelectionRepair;
            if (useHistory && scope.TryGetRecoveryNode(true, out targetNode))
            {
                CommitResolvedNode(scope, targetNode, reason);
                return;
            }

            UIDefaultFocusReason providerReason = ToDefaultFocusReason(reason);
            if (TryResolveDefaultFocusTarget(
                    instance,
                    scope,
                    providerReason,
                    reason,
                    out targetNode,
                    out bool pendingRealization))
            {
                CommitResolvedNode(scope, targetNode, reason);
                return;
            }

            if (pendingRealization)
            {
                scope.ClearPendingRepair();
                return;
            }

            Selectable providerTarget = defaultFocusResolver.ResolveProviderOnly(
                instance,
                providerReason);
            if (providerTarget != null &&
                nodeRegistry.TryResolveNode(
                    providerTarget,
                    out targetNode) &&
                scope.IsResolvedNodeEligible(targetNode))
            {
                CommitResolvedNode(scope, targetNode, reason);
                return;
            }

            if (scope.TryGetRecoveryNode(false, out targetNode))
            {
                CommitResolvedNode(scope, targetNode, reason);
                return;
            }

            scope.MarkPendingRepair();
        }

        private bool TryResolveDefaultFocusTarget(
            UIPageInstance instance,
            AppUIFocusScope scope,
            UIDefaultFocusReason reason,
            AppUIFocusChangeReason changeReason,
            out AppUIFocusResolvedNode resolvedNode,
            out bool pendingRealization)
        {
            resolvedNode = default;
            pendingRealization = false;
            IAppUIDefaultFocusTargetProvider provider =
                instance != null
                    ? instance.Controller as IAppUIDefaultFocusTargetProvider
                    : null;
            if (provider == null && instance != null && instance.GameObject != null)
            {
                provider = instance.GameObject.GetComponent<AppUIFocusAuthoring>();
            }
            if (provider == null)
            {
                return false;
            }

            AppUIFocusTarget target;
            bool hasTarget;
            try
            {
                hasTarget = provider.TryGetDefaultFocus(reason, out target);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                return false;
            }

            if (!hasTarget || !target.IsValid)
            {
                return false;
            }

            bool resolved;
            if (target.Kind == AppUIFocusTargetKind.NodeAddress)
            {
                resolved = nodeRegistry.TryResolveNode(
                    scope.PageHandle,
                    target.NodeAddress,
                    out resolvedNode);
                if (!resolved)
                {
                    pendingRealization = scope.FocusNode(
                            target.NodeAddress,
                            changeReason) ==
                        AppUIFocusRequestResult.PendingRealization;
                }
            }
            else
            {
                resolved = target.Kind == AppUIFocusTargetKind.Selectable &&
                           nodeRegistry.TryResolveNode(
                               target.Selectable,
                               out resolvedNode);
            }

            return resolved && scope.IsResolvedNodeEligible(resolvedNode);
        }

        private void CommitResolvedNode(
            AppUIFocusScope scope,
            AppUIFocusResolvedNode targetNode,
            AppUIFocusChangeReason reason)
        {
            if (!scope.TryCreateCommitRequest(
                    targetNode,
                    reason,
                    out AppUIFocusCommitRequest request,
                    out _))
            {
                scope.MarkPendingRepair();
                return;
            }

            AppUIFocusRequestResult result = focusCommitter.Commit(in request);
            if (result == AppUIFocusRequestResult.Focused ||
                result == AppUIFocusRequestResult.Consumed)
            {
                scope.ClearPendingRepair();
                return;
            }

            if (result != AppUIFocusRequestResult.Deferred)
            {
                scope.MarkPendingRepair();
            }
        }

        private static UIDefaultFocusReason ToDefaultFocusReason(
            AppUIFocusChangeReason reason)
        {
            switch (reason)
            {
                case AppUIFocusChangeReason.FirstOpened:
                    return UIDefaultFocusReason.PageOpened;
                case AppUIFocusChangeReason.Reopened:
                case AppUIFocusChangeReason.RestoreRequested:
                    return UIDefaultFocusReason.RestoreRequested;
                default:
                    return UIDefaultFocusReason.SelectionInvalid;
            }
        }
    }
}
