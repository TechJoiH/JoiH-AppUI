using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Dropdown 的显式 ControlPolicy 与 ChildRegion Cancel 适配器。
    /// 页面把同一实例同时注册到 Dropdown Node 和对应 ChildRegion；展开/自然收起时
    /// 通过 SynchronizeRegion 同步 Region 生命周期，Cancel 收起由统一管线调用。
    /// </summary>
    public sealed class AppUIFocusDropdownControlPolicy :
        IAppUIFocusControlPolicy,
        IAppUIFocusRegionCancelHandler
    {
        private readonly Dropdown dropdown;
        private readonly TMP_Dropdown tmpDropdown;
        private readonly string childRegionId;
        private bool uguiExpanded;

        public AppUIFocusDropdownControlPolicy(
            Dropdown focusDropdown,
            string focusChildRegionId)
        {
            dropdown = focusDropdown != null
                ? focusDropdown
                : throw new ArgumentNullException(nameof(focusDropdown));
            childRegionId = ValidateRegionId(focusChildRegionId);
        }

        public AppUIFocusDropdownControlPolicy(
            TMP_Dropdown focusDropdown,
            string focusChildRegionId)
        {
            tmpDropdown = focusDropdown != null
                ? focusDropdown
                : throw new ArgumentNullException(nameof(focusDropdown));
            childRegionId = ValidateRegionId(focusChildRegionId);
        }

        public string ChildRegionId
        {
            get { return childRegionId; }
        }

        public bool IsExpanded
        {
            get
            {
                return dropdown != null
                    ? uguiExpanded
                    : tmpDropdown != null && tmpDropdown.IsExpanded;
            }
        }

        internal Dropdown UGUIDropdown
        {
            get { return dropdown; }
        }

        internal TMP_Dropdown TMPDropdown
        {
            get { return tmpDropdown; }
        }

        /// <summary>
        /// 绑定运行时展开状态桥。桥只同步 ChildRegion 生命周期，不提交焦点；
        /// 页面释放或重建 Dropdown 时应 Dispose 返回值。
        /// </summary>
        public IDisposable Bind(IAppUIFocusScopeHandle scope)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            GameObject target = dropdown != null
                ? dropdown.gameObject
                : tmpDropdown.gameObject;
            AppUIFocusDropdownRegionBridge bridge =
                target.GetComponent<AppUIFocusDropdownRegionBridge>();
            if (bridge == null)
            {
                bridge = target.AddComponent<AppUIFocusDropdownRegionBridge>();
            }

            bridge.Initialize(scope, this);
            return bridge;
        }

        public AppUIFocusControlMoveMode GetMoveMode(
            in AppUIFocusMoveContext context)
        {
            // 关闭态是普通 Node；展开态的方向输入归 ChildRegion，而不是 Dropdown
            // 原生跨 Selectable Navigation。
            return AppUIFocusControlMoveMode.FrameworkOnly;
        }

        public AppUIFocusCancelHandlingResult TryHandleCancel(
            in AppUIFocusCancelContext context)
        {
            // Dropdown 的 Cancel 归 Active ChildRegion 处理，不能在控件阶段抢先消费。
            return AppUIFocusCancelHandlingResult.Continue;
        }

        AppUIFocusCancelHandlingResult IAppUIFocusRegionCancelHandler.TryHandleCancel(
            in AppUIFocusRegionCancelContext context)
        {
            if (!string.Equals(
                    context.RegionId,
                    childRegionId,
                    StringComparison.Ordinal))
            {
                return AppUIFocusCancelHandlingResult.Continue;
            }

            Collapse();
            return AppUIFocusCancelHandlingResult.Consumed;
        }

        /// <summary>
        /// 在 Dropdown 展开状态变化后同步 ChildRegion。页面可从 Submit/Click 后的 LateUpdate
        /// 或 Dropdown 包装组件调用；Region 关闭会恢复打开前的 SourceNode。
        /// </summary>
        public AppUIFocusRequestResult SynchronizeRegion(
            IAppUIFocusScopeHandle scope)
        {
            if (scope == null)
            {
                return AppUIFocusRequestResult.ScopeInactive;
            }

            AppUIFocusRegionStatus regionStatus = scope.GetRegionStatus(childRegionId);
            if (IsExpanded)
            {
                return regionStatus == AppUIFocusRegionStatus.Active
                    ? AppUIFocusRequestResult.Consumed
                    : scope.OpenRegion(
                        childRegionId,
                        AppUIFocusRegionEntryPolicy.LastFocusedOrDefault);
            }

            return regionStatus == AppUIFocusRegionStatus.Closed
                ? AppUIFocusRequestResult.Consumed
                : scope.CloseRegion(childRegionId);
        }

        /// <summary>
        /// UGUI Dropdown 没有公开 IsExpanded；其绑定层在 Show/Hide 状态变化后用此重载
        /// 明确发布展开状态。TMP_Dropdown 应使用无 bool 的重载读取原生状态。
        /// </summary>
        public AppUIFocusRequestResult SynchronizeRegion(
            IAppUIFocusScopeHandle scope,
            bool expanded)
        {
            if (dropdown == null)
            {
                throw new InvalidOperationException(
                    "The explicit expanded-state overload is only required by UGUI Dropdown.");
            }

            uguiExpanded = expanded;
            return SynchronizeRegion(scope);
        }

        public void Collapse()
        {
            if (dropdown != null)
            {
                dropdown.Hide();
                uguiExpanded = false;
            }

            if (tmpDropdown != null && tmpDropdown.IsExpanded)
            {
                tmpDropdown.Hide();
            }
        }

        private static string ValidateRegionId(string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId) ||
                string.Equals(
                    regionId,
                    AppUIFocusDefinition.RootRegionId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Dropdown requires a non-root ChildRegion id.",
                    nameof(regionId));
            }

            return regionId;
        }
    }

    /// <summary>
    /// Dropdown 展开状态与 Focus ChildRegion 的运行时桥。UGUI 通过 Submit/Click 后生成的
    /// Dropdown List 与 onValueChanged 观察状态；TMP 使用公开 IsExpanded。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppUIFocusDropdownRegionBridge :
        MonoBehaviour,
        ISubmitHandler,
        IPointerClickHandler,
        IDisposable
    {
        private IAppUIFocusScopeHandle scope;
        private AppUIFocusDropdownControlPolicy policy;
        private Dropdown uguiDropdown;
        private TMP_Dropdown tmpDropdown;
        private Transform uguiDropdownList;
        private bool pendingOpenObservation;
        private bool previousExpanded;
        private bool disposed;

        internal void Initialize(
            IAppUIFocusScopeHandle focusScope,
            AppUIFocusDropdownControlPolicy controlPolicy)
        {
            if (focusScope == null)
            {
                throw new ArgumentNullException(nameof(focusScope));
            }

            if (controlPolicy == null)
            {
                throw new ArgumentNullException(nameof(controlPolicy));
            }

            Unsubscribe();
            scope = focusScope;
            policy = controlPolicy;
            uguiDropdown = controlPolicy.UGUIDropdown;
            tmpDropdown = controlPolicy.TMPDropdown;
            disposed = false;
            enabled = true;
            pendingOpenObservation = false;
            uguiDropdownList = null;
            previousExpanded = policy.IsExpanded;
            Subscribe();
            if (previousExpanded)
            {
                Synchronize(true);
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            pendingOpenObservation = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            pendingOpenObservation = true;
        }

        private void LateUpdate()
        {
            if (disposed || scope == null || policy == null)
            {
                return;
            }

            bool expanded = tmpDropdown != null
                ? tmpDropdown.IsExpanded
                : ResolveUGUIExpandedState();
            if (expanded == previousExpanded)
            {
                pendingOpenObservation = false;
                return;
            }

            previousExpanded = expanded;
            pendingOpenObservation = false;
            Synchronize(expanded);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Unsubscribe();
            scope = null;
            policy = null;
            uguiDropdown = null;
            tmpDropdown = null;
            uguiDropdownList = null;
            pendingOpenObservation = false;
            enabled = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private bool ResolveUGUIExpandedState()
        {
            if (uguiDropdown == null)
            {
                return false;
            }

            if (uguiDropdownList != null)
            {
                return uguiDropdownList.gameObject.activeInHierarchy;
            }

            if (!pendingOpenObservation || uguiDropdown.template == null)
            {
                return false;
            }

            Transform parent = uguiDropdown.template.parent;
            if (parent == null)
            {
                return false;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null &&
                    !ReferenceEquals(child, uguiDropdown.template) &&
                    child.gameObject.activeInHierarchy &&
                    child.name.StartsWith("Dropdown List", StringComparison.Ordinal))
                {
                    uguiDropdownList = child;
                    return true;
                }
            }

            return false;
        }

        private void HandleValueChanged(int _)
        {
            previousExpanded = false;
            pendingOpenObservation = false;
            uguiDropdownList = null;
            Synchronize(false);
        }

        private void Synchronize(bool expanded)
        {
            if (scope == null || policy == null)
            {
                return;
            }

            if (uguiDropdown != null)
            {
                policy.SynchronizeRegion(scope, expanded);
            }
            else
            {
                policy.SynchronizeRegion(scope);
            }
        }

        private void Subscribe()
        {
            if (uguiDropdown != null)
            {
                uguiDropdown.onValueChanged.AddListener(HandleValueChanged);
            }

            if (tmpDropdown != null)
            {
                tmpDropdown.onValueChanged.AddListener(HandleValueChanged);
            }
        }

        private void Unsubscribe()
        {
            if (uguiDropdown != null)
            {
                uguiDropdown.onValueChanged.RemoveListener(HandleValueChanged);
            }

            if (tmpDropdown != null)
            {
                tmpDropdown.onValueChanged.RemoveListener(HandleValueChanged);
            }
        }
    }
}
