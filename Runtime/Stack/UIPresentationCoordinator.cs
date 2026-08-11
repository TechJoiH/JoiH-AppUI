using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// UI 展示状态协调器。
    /// 负责把页面生命周期提交后的状态同步到栈可见性、PauseDepth、输入阻断、背景点击 Shield、焦点和同层 sibling 顺序。
    /// </summary>
    internal sealed class UIPresentationCoordinator
    {
        private readonly IUIService uiService;
        private readonly UIPageInstanceRegistry instanceRegistry;
        private readonly UILayerController layerController;
        private readonly UIStackCoordinator stackCoordinator;
        private readonly UIFocusService focusService;
        private readonly UIInputBlocker inputBlocker;
        private readonly UISelectionInputAuthority selectionAuthority;

        private readonly List<UIPageInstance> stackSnapshot = new List<UIPageInstance>(16);
        private readonly List<UIPageInstance> pauseSnapshot = new List<UIPageInstance>(16);
        private readonly Dictionary<UIPageInstance, int> pauseDepthTargets =
            new Dictionary<UIPageInstance, int>(16);
        private readonly List<UIPageInstance> inputBlockSnapshot = new List<UIPageInstance>(16);
        private readonly Dictionary<UIPageInstance, int> inputBlockDepthTargets =
            new Dictionary<UIPageInstance, int>(16);
        private readonly List<UIPageInstance> backgroundClickSnapshot = new List<UIPageInstance>(8);
        private readonly Dictionary<UIPageInstance, BackgroundClickShieldState> backgroundClickShields =
            new Dictionary<UIPageInstance, BackgroundClickShieldState>(8);
        private readonly List<UIPageInstance> staleBackgroundClickShields = new List<UIPageInstance>(8);
        private readonly List<UIPageInteractionState> interactionStateBuffer =
            new List<UIPageInteractionState>(16);
        private readonly List<UIPageInstance> layerSortSnapshot = new List<UIPageInstance>(16);

        private UIInteractionSnapshot currentInteractionSnapshot = UIInteractionSnapshot.Empty;
        private UIPageInteractionHandle pageOpenedFocusTarget;
        private UIPageInteractionHandle previousTopInteractivePage;
        private AppUIFocusChangeReason pageOpenedFocusReason;
        private bool hasPageOpenedFocusReason;
        private int nextStackRevision;
        private int nextLayerSortSequence;

        /// <summary>最近一次 Commit 发布的不可变交互快照。</summary>
        internal UIInteractionSnapshot CurrentInteractionSnapshot
        {
            get { return currentInteractionSnapshot; }
        }

        /// <summary>
        /// 创建展示协调器。
        /// 传入的服务都由 AppUIManager 持有，Coordinator 只编排展示状态，不拥有页面生命周期。
        /// </summary>
        public UIPresentationCoordinator(
            IUIService service,
            UIPageInstanceRegistry registry,
            UILayerController layers,
            UIStackCoordinator stacks,
            UIFocusService focus,
            UIInputBlocker blocker,
            UISelectionInputAuthority selection)
        {
            uiService = service;
            instanceRegistry = registry;
            layerController = layers;
            stackCoordinator = stacks;
            focusService = focus;
            inputBlocker = blocker;
            selectionAuthority = selection;
            focusService?.ConfigureInstanceRegistry(instanceRegistry);
        }

        /// <summary>
        /// 将页面作为已打开页面提交到同层排序与栈中，并立即刷新完整展示链。
        /// Open 成功和 Hidden 重开都走这里，确保页面顺序、StackVisible、输入和焦点一起更新。
        /// </summary>
        public void PushOpened(
            UIPageInstance instance,
            AppUIFocusChangeReason focusReason = AppUIFocusChangeReason.FirstOpened)
        {
            if (instance == null)
            {
                return;
            }

            instance.StackVisible = true;
            AssignLayerSortSequence(instance);
            RefreshLayerSiblingOrderSafe(instance.LayerId);
            stackCoordinator.Push(instance);
            if (!instanceRegistry.TryCreateInteractionHandle(
                    instance,
                    out pageOpenedFocusTarget))
            {
                pageOpenedFocusTarget = default;
            }

            pageOpenedFocusReason = focusReason;
            hasPageOpenedFocusReason = pageOpenedFocusTarget.IsValid;

            Commit();
        }

        /// <summary>
        /// 从栈中移除页面。
        /// Close/Release 和失败清理会先移除栈，再由外层决定何时统一 Commit。
        /// </summary>
        public void RemoveFromStack(UIPageInstance instance)
        {
            try
            {
                stackCoordinator.Remove(instance);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        /// <summary>
        /// 如果当前 EventSystem 焦点属于该页面，则清空焦点。
        /// 关闭或隐藏页面前清焦点，可以避免选中已销毁控件。
        /// </summary>
        public void ClearFocusIfOwned(UIPageInstance instance)
        {
            try
            {
                focusService.ClearIfOwned(instance);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        /// <summary>
        /// 释放或失败清理前复位单个页面的展示相关状态。
        /// 该方法不执行完整 Commit，只把实例从栈、焦点、PauseDepth、InputBlockDepth 中摘除，方便外层批量释放后统一刷新。
        /// </summary>
        public void ResetInstancePresentationState(UIPageInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            RemoveFromStack(instance);
            DetachFocusScopeSafe(instance);
            ClearFocusIfOwned(instance);
            SetPauseDepthSafe(instance, 0);
            SetInputBlockDepthSafe(instance, 0);
            instance.StackVisible = false;
            SetInstanceActive(instance, false);
        }

        /// <summary>
        /// 安全设置页面 GameObject active。
        /// 栈隐藏和 Hide 异常兜底会使用该方法，只改变可见性，不触发生命周期。
        /// </summary>
        public void SetInstanceActive(UIPageInstance instance, bool active)
        {
            if (instance == null)
            {
                return;
            }

            SetActiveSafe(instance.GameObject, active);
        }

        /// <summary>
        /// 提交完整展示状态链。
        /// 固定顺序为 Stack -> PauseDepth -> InputBlock -> Snapshot -> FocusScope -> BackgroundClick -> Selection，
        /// 避免 Scope 或焦点根据尚未完成的页面状态作出另一份交互结论。
        /// </summary>
        public void Commit()
        {
            previousTopInteractivePage =
                currentInteractionSnapshot.TopInteractivePage;
            RefreshStackPresentationSafe();
            RefreshPauseDepthSafe();
            RefreshInputBlockerSafe();
            PublishInteractionSnapshotSafe();
            ApplyFocusScopesSafe();
            RefreshBackgroundClickSafe();
            RefreshSelectionAuthoritySafe();
        }

        /// <summary>
        /// 查询全局最顶层可见页面。
        /// CloseTop 使用该结果作为唯一候选，再交给 CloseAsync 统一执行 CanClose 和 operation 流程。
        /// </summary>
        public bool TryGetTopVisiblePage(out UIPageInstance instance)
        {
            return stackCoordinator.TryGetTopVisiblePage(out instance);
        }

        /// <summary>
        /// 查询指定 Layer 的最顶层可见页面。
        /// 旧的 CloseTop(layerId) 语义使用该方法，避免命中被全屏栈隐藏的页面。
        /// </summary>
        public bool TryGetTopVisiblePage(UILayerId layerId, out UIPageInstance instance)
        {
            return stackCoordinator.TryGetTopVisiblePage(layerId, out instance);
        }

        /// <summary>
        /// 查询当前最顶层可交互页面。
        /// SelectionAuthority 和 Cancel fallback 都使用同一套跨 Layer 阻断规则。
        /// </summary>
        public bool TryGetTopInteractivePage(out UIPageInstance instance)
        {
            return instanceRegistry.TryResolve(
                currentInteractionSnapshot.TopInteractivePage,
                out instance);
        }

        /// <summary>
        /// 根据当前 EventSystem 选中对象解析 Cancel 目标。
        /// 若选中对象无效，会先刷新焦点权威，再回退到最顶层可交互页面。
        /// </summary>
        public UIPageInstance ResolveCancelTarget()
        {
            UIPageInstance instance = ResolveCancelTargetFromSelection();
            if (IsCancelTarget(instance))
            {
                return instance;
            }

            RefreshSelectionAuthoritySafe();
            instance = ResolveCancelTargetFromSelection();
            if (IsCancelTarget(instance))
            {
                return instance;
            }

            return TryGetTopInteractivePage(out instance) && IsCancelTarget(instance)
                ? instance
                : null;
        }

        /// <summary>
        /// 清理属于 App UI 页面的当前选中对象。
        /// Manager 销毁时使用，防止 EventSystem 保留已释放页面的 Selectable。
        /// </summary>
        public void ClearOwnedSelection(IReadOnlyList<UIPageInstance> pages)
        {
            try
            {
                selectionAuthority.ClearOwnedSelection(pages);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        /// <summary>
        /// 清空 Presentation 持有的运行时状态。
        /// 该方法用于 Manager 销毁，负责清栈、恢复 InputBlocker、销毁背景点击 Shield 和清理临时列表。
        /// </summary>
        public void Clear()
        {
            stackCoordinator.Clear();
            inputBlocker.Clear();
            ClearBackgroundClickShields();
            stackSnapshot.Clear();
            pauseSnapshot.Clear();
            pauseDepthTargets.Clear();
            inputBlockSnapshot.Clear();
            inputBlockDepthTargets.Clear();
            interactionStateBuffer.Clear();
            layerSortSnapshot.Clear();
            currentInteractionSnapshot = UIInteractionSnapshot.Empty;
            pageOpenedFocusTarget = default;
            previousTopInteractivePage = default;
            hasPageOpenedFocusReason = false;
            focusService.ClearScopes();
        }

        private UIPageInstance ResolveCancelTargetFromSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            {
                return null;
            }

            Transform selectedTransform = eventSystem.currentSelectedGameObject.transform;
            for (int i = 0; i < currentInteractionSnapshot.PageStateCount; i++)
            {
                UIPageInteractionState state = currentInteractionSnapshot.GetPageState(i);
                if (!instanceRegistry.TryResolve(state.Page, out UIPageInstance instance) ||
                    instance.GameObject == null)
                {
                    continue;
                }

                if (selectedTransform.IsChildOf(instance.GameObject.transform))
                {
                    return instance;
                }
            }

            return null;
        }

        private static bool IsCancelTarget(UIPageInstance instance)
        {
            return instance != null &&
                   instance.GameObject != null &&
                   instance.GameObject.activeInHierarchy &&
                   instance.IsOpenAndStackVisible &&
                   !instance.IsPaused &&
                   !instance.IsInputBlocked;
        }

        private void RefreshStackPresentationSafe()
        {
            try
            {
                stackCoordinator.RebuildVisibility();
                stackCoordinator.GetSnapshot(stackSnapshot);
                for (int i = 0; i < stackSnapshot.Count; i++)
                {
                    UIPageInstance instance = stackSnapshot[i];
                    if (instance == null)
                    {
                        continue;
                    }

                    bool active = instance.IsOpenAndStackVisible;
                    if (!active)
                    {
                        ClearFocusIfOwned(instance);
                    }

                    SetActiveSafe(instance.GameObject, active);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
            finally
            {
                stackSnapshot.Clear();
            }
        }

        private void RefreshPauseDepthSafe()
        {
            try
            {
                pauseDepthTargets.Clear();
                stackCoordinator.GetSnapshot(pauseSnapshot);

                int blockingDepth = 0;
                for (int i = pauseSnapshot.Count - 1; i >= 0; i--)
                {
                    UIPageInstance instance = pauseSnapshot[i];
                    if (!IsVisibleOpen(instance))
                    {
                        continue;
                    }

                    pauseDepthTargets[instance] = blockingDepth;
                    if (BlocksLowerPagesForPauseAndInput(instance))
                    {
                        blockingDepth++;
                    }
                }

                List<UIPageInstance> pages = instanceRegistry.GetSnapshot();
                for (int i = 0; i < pages.Count; i++)
                {
                    UIPageInstance instance = pages[i];
                    int targetDepth = 0;
                    if (IsVisibleOpen(instance) &&
                        pauseDepthTargets.TryGetValue(instance, out int calculatedDepth))
                    {
                        targetDepth = calculatedDepth;
                    }

                    SetPauseDepthSafe(instance, targetDepth);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
            finally
            {
                pauseSnapshot.Clear();
                pauseDepthTargets.Clear();
            }
        }

        private void SetPauseDepthSafe(UIPageInstance instance, int targetDepth)
        {
            if (instance == null)
            {
                return;
            }

            if (targetDepth < 0)
            {
                targetDepth = 0;
            }

            int previousDepth = instance.PauseDepth;
            if (previousDepth == targetDepth)
            {
                return;
            }

            instance.PauseDepth = targetDepth;
            try
            {
                if (previousDepth <= 0 && targetDepth > 0)
                {
                    instance.Controller?.OnPause();
                }
                else if (previousDepth > 0 && targetDepth <= 0)
                {
                    instance.Controller?.OnResume();
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        private static bool IsVisibleOpen(UIPageInstance instance)
        {
            return instance != null && instance.IsOpenAndStackVisible;
        }

        private static bool BlocksLowerPagesForPauseAndInput(UIPageInstance instance)
        {
            if (instance == null)
            {
                return false;
            }

            UIPageDefinition definition = instance.Definition;
            return IsPauseBlockingLayer(instance.LayerId) ||
                   (definition != null &&
                    (definition.BlockLowerLayerInput || definition.IsFullScreen));
        }

        private static bool IsPauseBlockingLayer(UILayerId layerId)
        {
            return layerId == UILayerId.ModalLayer ||
                   layerId == UILayerId.GuideLayer ||
                   layerId == UILayerId.LoadingLayer;
        }

        private void RefreshInputBlockerSafe()
        {
            try
            {
                inputBlocker.BeginRefresh();
                inputBlockDepthTargets.Clear();
                stackCoordinator.GetSnapshot(inputBlockSnapshot);

                int blockingDepth = 0;
                for (int i = inputBlockSnapshot.Count - 1; i >= 0; i--)
                {
                    UIPageInstance instance = inputBlockSnapshot[i];
                    if (!IsVisibleOpen(instance))
                    {
                        continue;
                    }

                    inputBlockDepthTargets[instance] = blockingDepth;
                    if (BlocksLowerPagesForPauseAndInput(instance))
                    {
                        RectTransform contentRoot;
                        if (TryGetLayerContentRoot(instance, out contentRoot))
                        {
                            inputBlocker.SetBoundaryShield(instance, contentRoot);
                        }

                        blockingDepth++;
                    }
                }

                List<UIPageInstance> pages = instanceRegistry.GetSnapshot();
                for (int i = 0; i < pages.Count; i++)
                {
                    UIPageInstance instance = pages[i];
                    int targetDepth = 0;
                    if (IsVisibleOpen(instance) &&
                        inputBlockDepthTargets.TryGetValue(instance, out int calculatedDepth))
                    {
                        targetDepth = calculatedDepth;
                    }

                    SetInputBlockDepthSafe(instance, targetDepth);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
            finally
            {
                try
                {
                    inputBlocker.EndRefresh();
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                }

                inputBlockSnapshot.Clear();
                inputBlockDepthTargets.Clear();
            }
        }

        private bool TryGetLayerContentRoot(UIPageInstance instance, out RectTransform contentRoot)
        {
            contentRoot = null;
            if (instance == null ||
                !layerController.TryGetRoot(instance.LayerId, out UILayerRoot layerRoot) ||
                layerRoot == null)
            {
                return false;
            }

            contentRoot = layerRoot.ContentRoot;
            return contentRoot != null;
        }

        private void SetInputBlockDepthSafe(UIPageInstance instance, int targetDepth)
        {
            try
            {
                inputBlocker.SetBlockedDepth(instance, targetDepth);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        private void RefreshBackgroundClickSafe()
        {
            try
            {
                BeginBackgroundClickRefresh();
                stackCoordinator.GetSnapshot(backgroundClickSnapshot);
                for (int i = 0; i < backgroundClickSnapshot.Count; i++)
                {
                    UIPageInstance instance = backgroundClickSnapshot[i];
                    if (!NeedsBackgroundClickShield(instance))
                    {
                        continue;
                    }

                    RectTransform contentRoot;
                    if (TryGetLayerContentRoot(instance, out contentRoot))
                    {
                        SetBackgroundClickShield(instance, contentRoot);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
            finally
            {
                try
                {
                    EndBackgroundClickRefresh();
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                }

                backgroundClickSnapshot.Clear();
            }
        }

        private void BeginBackgroundClickRefresh()
        {
            foreach (KeyValuePair<UIPageInstance, BackgroundClickShieldState> pair in backgroundClickShields)
            {
                pair.Value.Seen = false;
            }
        }

        private void SetBackgroundClickShield(UIPageInstance instance, RectTransform contentRoot)
        {
            if (instance == null || contentRoot == null || instance.GameObject == null)
            {
                return;
            }

            BackgroundClickShieldState state = GetOrCreateBackgroundClickShield(instance);
            if (state == null)
            {
                return;
            }

            state.Seen = true;
            if (state.Handler != null)
            {
                state.Handler.Initialize(uiService, instance.PageId, instance.Definition);
            }

            RefreshBackgroundClickShieldTransform(state, contentRoot, instance.GameObject.transform);
        }

        private BackgroundClickShieldState GetOrCreateBackgroundClickShield(UIPageInstance instance)
        {
            BackgroundClickShieldState state;
            if (backgroundClickShields.TryGetValue(instance, out state) && state.GameObject != null)
            {
                return state;
            }

            if (state != null)
            {
                DestroyBackgroundClickShield(state);
                backgroundClickShields.Remove(instance);
            }

            GameObject shieldObject = new GameObject(
                BuildBackgroundClickShieldName(instance),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(UIBackgroundClickHandler));
            shieldObject.hideFlags = HideFlags.DontSave;

            Image image = shieldObject.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            UIBackgroundClickHandler handler = shieldObject.GetComponent<UIBackgroundClickHandler>();
            handler.Initialize(uiService, instance.PageId, instance.Definition);

            state = new BackgroundClickShieldState
            {
                GameObject = shieldObject,
                RectTransform = shieldObject.transform as RectTransform,
                Image = image,
                Handler = handler,
                Seen = true,
            };
            backgroundClickShields.Add(instance, state);
            return state;
        }

        private static void RefreshBackgroundClickShieldTransform(
            BackgroundClickShieldState state,
            RectTransform contentRoot,
            Transform pageTransform)
        {
            if (state == null || state.GameObject == null || state.RectTransform == null)
            {
                return;
            }

            RectTransform rectTransform = state.RectTransform;
            if (rectTransform.parent != contentRoot)
            {
                rectTransform.SetParent(contentRoot, false);
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            if (state.Image != null)
            {
                state.Image.color = Color.clear;
                state.Image.raycastTarget = true;
            }

            state.GameObject.SetActive(true);
            Transform layerChild = GetDirectChildUnder(contentRoot, pageTransform);
            if (layerChild == null || layerChild == rectTransform)
            {
                rectTransform.SetAsFirstSibling();
                return;
            }

            int targetIndex = layerChild.GetSiblingIndex();
            int currentIndex = rectTransform.GetSiblingIndex();
            if (currentIndex >= 0 && currentIndex < targetIndex)
            {
                targetIndex--;
            }

            rectTransform.SetSiblingIndex(Mathf.Max(0, targetIndex));
        }

        private void EndBackgroundClickRefresh()
        {
            staleBackgroundClickShields.Clear();
            foreach (KeyValuePair<UIPageInstance, BackgroundClickShieldState> pair in backgroundClickShields)
            {
                if (!pair.Value.Seen)
                {
                    staleBackgroundClickShields.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleBackgroundClickShields.Count; i++)
            {
                DestroyBackgroundClickShield(staleBackgroundClickShields[i]);
            }

            staleBackgroundClickShields.Clear();
        }

        private void DestroyBackgroundClickShield(UIPageInstance instance)
        {
            BackgroundClickShieldState state;
            if (!backgroundClickShields.TryGetValue(instance, out state))
            {
                return;
            }

            DestroyBackgroundClickShield(state);
            backgroundClickShields.Remove(instance);
        }

        private void ClearBackgroundClickShields()
        {
            foreach (KeyValuePair<UIPageInstance, BackgroundClickShieldState> pair in backgroundClickShields)
            {
                DestroyBackgroundClickShield(pair.Value);
            }

            backgroundClickShields.Clear();
            backgroundClickSnapshot.Clear();
            staleBackgroundClickShields.Clear();
        }

        private static bool NeedsBackgroundClickShield(UIPageInstance instance)
        {
            return IsVisibleOpen(instance) &&
                   instance.Definition != null &&
                   instance.Definition.CloseOnBackgroundClick &&
                   IsBackgroundClickLayer(instance.LayerId);
        }

        private static bool IsBackgroundClickLayer(UILayerId layerId)
        {
            return layerId == UILayerId.PopupLayer || layerId == UILayerId.ModalLayer;
        }

        private static Transform GetDirectChildUnder(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                return null;
            }

            Transform current = target;
            while (current != null && current.parent != root)
            {
                current = current.parent;
            }

            return current;
        }

        private static string BuildBackgroundClickShieldName(UIPageInstance instance)
        {
            string targetPageId = instance != null && !string.IsNullOrEmpty(instance.PageId)
                ? instance.PageId
                : "Unknown";
            return "UIBackgroundClickHandler.Shield." + targetPageId;
        }

        private static void DestroyBackgroundClickShield(BackgroundClickShieldState state)
        {
            if (state == null || state.GameObject == null)
            {
                return;
            }

            state.GameObject.SetActive(false);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(state.GameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(state.GameObject);
            }
        }

        private void RefreshSelectionAuthoritySafe()
        {
            try
            {
                AppUIFocusChangeReason reason;
                if (hasPageOpenedFocusReason &&
                    currentInteractionSnapshot.TopInteractivePage ==
                    pageOpenedFocusTarget)
                {
                    reason = pageOpenedFocusReason;
                }
                else if (currentInteractionSnapshot.TopInteractivePage.IsValid &&
                         currentInteractionSnapshot.TopInteractivePage !=
                         previousTopInteractivePage)
                {
                    reason = AppUIFocusChangeReason.RestoreRequested;
                }
                else
                {
                    reason = AppUIFocusChangeReason.SelectionRepair;
                }

                selectionAuthority.Refresh(
                    currentInteractionSnapshot,
                    instanceRegistry,
                    focusService,
                    reason);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
            finally
            {
                pageOpenedFocusTarget = default;
                hasPageOpenedFocusReason = false;
            }
        }

        /// <summary>
        /// 在 StackVisible、PauseDepth 和 InputBlockDepth 完成后发布唯一交互快照。
        /// 即使构建失败也发布带新 Revision 的空结论，确保旧请求无法继续使用过期版本。
        /// </summary>
        private void PublishInteractionSnapshotSafe()
        {
            int stackRevision = GetNextStackRevision();
            try
            {
                interactionStateBuffer.Clear();
                List<UIPageInstance> pages = instanceRegistry.GetSnapshot();
                for (int i = 0; i < pages.Count; i++)
                {
                    UIPageInstance instance = pages[i];
                    if (!instanceRegistry.TryCreateInteractionHandle(
                            instance,
                            out UIPageInteractionHandle handle))
                    {
                        continue;
                    }

                    interactionStateBuffer.Add(
                        new UIPageInteractionState(
                            handle,
                            instance.StackVisible,
                            instance.PauseDepth,
                            instance.InputBlockDepth));
                }

                UIPageInteractionHandle topInteractiveHandle = default;
                if (stackCoordinator.TryGetTopInteractivePage(out UIPageInstance topInteractivePage))
                {
                    instanceRegistry.TryCreateInteractionHandle(
                        topInteractivePage,
                        out topInteractiveHandle);
                }

                currentInteractionSnapshot = new UIInteractionSnapshot(
                    stackRevision,
                    topInteractiveHandle,
                    interactionStateBuffer);
            }
            catch (Exception exception)
            {
                currentInteractionSnapshot = new UIInteractionSnapshot(
                    stackRevision,
                    default,
                    null);
                Debug.LogError(exception);
            }
            finally
            {
                interactionStateBuffer.Clear();
            }
        }

        private void ApplyFocusScopesSafe()
        {
            try
            {
                focusService.ApplyInteractionSnapshot(currentInteractionSnapshot);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        private void DetachFocusScopeSafe(UIPageInstance instance)
        {
            try
            {
                focusService.DetachScope(instance);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        private int GetNextStackRevision()
        {
            if (nextStackRevision == int.MaxValue)
            {
                nextStackRevision = 0;
            }

            nextStackRevision++;
            return nextStackRevision;
        }

        private static void SetActiveSafe(GameObject gameObject, bool active)
        {
            if (gameObject == null)
            {
                return;
            }

            try
            {
                if (gameObject.activeSelf != active)
                {
                    gameObject.SetActive(active);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        private void AssignLayerSortSequence(UIPageInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            nextLayerSortSequence++;
            instance.LayerSortSequence = nextLayerSortSequence;
        }

        private void RefreshLayerSiblingOrderSafe(UILayerId layerId)
        {
            try
            {
                RefreshLayerSiblingOrder(layerId);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
            finally
            {
                layerSortSnapshot.Clear();
            }
        }

        private void RefreshLayerSiblingOrder(UILayerId layerId)
        {
            if (!layerController.TryGetRoot(layerId, out UILayerRoot layerRoot) ||
                layerRoot == null ||
                layerRoot.ContentRoot == null)
            {
                return;
            }

            Transform contentRoot = layerRoot.ContentRoot;
            List<UIPageInstance> pages = instanceRegistry.GetSnapshot();
            for (int i = 0; i < pages.Count; i++)
            {
                UIPageInstance instance = pages[i];
                if (instance == null ||
                    instance.LayerId != layerId ||
                    instance.GameObject == null ||
                    instance.GameObject.transform.parent != contentRoot)
                {
                    continue;
                }

                layerSortSnapshot.Add(instance);
            }

            layerSortSnapshot.Sort(CompareLayerSiblingOrder);
            for (int i = 0; i < layerSortSnapshot.Count; i++)
            {
                UIPageInstance instance = layerSortSnapshot[i];
                if (instance != null && instance.GameObject != null)
                {
                    instance.GameObject.transform.SetSiblingIndex(i);
                }
            }
        }

        private static int CompareLayerSiblingOrder(UIPageInstance left, UIPageInstance right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int offsetCompare = GetDefaultPriorityOffset(left).CompareTo(GetDefaultPriorityOffset(right));
            if (offsetCompare != 0)
            {
                return offsetCompare;
            }

            return left.LayerSortSequence.CompareTo(right.LayerSortSequence);
        }

        private static int GetDefaultPriorityOffset(UIPageInstance instance)
        {
            return instance != null && instance.Definition != null
                ? instance.Definition.DefaultPriorityOffset
                : 0;
        }

        private sealed class BackgroundClickShieldState
        {
            public GameObject GameObject;
            public RectTransform RectTransform;
            public Image Image;
            public UIBackgroundClickHandler Handler;
            public bool Seen;
        }
    }
}
