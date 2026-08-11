using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// App UI Notice 运行时服务。
    /// 负责 Toast、Tooltip、FloatingText 和 DamageNumber 的创建、池化、生命周期 Tick 与 Scope 清理。
    /// </summary>
    public sealed class NoticeService : INoticeService
    {
        private readonly NoticePool toastPool = new NoticePool(UINoticeKind.Toast);
        private readonly NoticePool tooltipPool = new NoticePool(UINoticeKind.Tooltip);
        private readonly NoticePool floatingTextPool = new NoticePool(UINoticeKind.FloatingText);
        private readonly NoticePool damageNumberPool = new NoticePool(UINoticeKind.DamageNumber);

        private int nextId;
        private RectTransform noticeRoot;
        private Canvas noticeCanvas;
        private IUIAssetProvider assetProvider;
        private AppUINoticeSettings settings;
        private bool warnedMissingRoot;

        /// <summary>
        /// 初始化 NoticeService。
        /// 每次 UI Runtime/Profile 重新初始化时都会调用，旧池会先释放，避免跨 Root 持有无效 Transform。
        /// </summary>
        public void Initialize(
            RectTransform root,
            IUIAssetProvider provider,
            AppUINoticeSettings noticeSettings)
        {
            Dispose();
            noticeRoot = root;
            noticeCanvas = noticeRoot != null ? noticeRoot.GetComponentInParent<Canvas>() : null;
            assetProvider = provider;
            settings = noticeSettings ?? AppUINoticeSettings.CreateDefault();
            warnedMissingRoot = false;

            ConfigurePool(toastPool, settings.Toast);
            ConfigurePool(tooltipPool, settings.Tooltip);
            ConfigurePool(floatingTextPool, settings.FloatingText);
            ConfigurePool(damageNumberPool, settings.DamageNumber);

            PrewarmPool(toastPool);
            PrewarmPool(tooltipPool);
            PrewarmPool(floatingTextPool);
            PrewarmPool(damageNumberPool);
        }

        /// <summary>显示一条默认全局 Toast。</summary>
        public ToastHandle Toast(string text)
        {
            return Toast(ToastNoticeRequest.Create(text));
        }

        /// <summary>显示 Toast，并记录请求中的 Scope 以便后续批量清理。</summary>
        public ToastHandle Toast(in ToastNoticeRequest request)
        {
            NoticeInstance instance = SpawnAutoNotice(
                toastPool,
                request.Text,
                ResolveColor(request.TextColor, toastPool.Settings.TextColor),
                request.Scope,
                request.Duration,
                request.FadeDuration,
                toastPool.Settings.RiseDistance,
                Vector2.zero,
                false);
            return new ToastHandle(instance != null ? instance.Id : 0);
        }

        /// <summary>显示 Tooltip；Tooltip 不自动关闭，直到 HideTooltip、ClearScope 或 ClearAll。</summary>
        public TooltipHandle ShowTooltip(in TooltipNoticeRequest request)
        {
            if (!TryResolvePosition(
                    request.CoordinateMode,
                    request.ScreenPosition,
                    request.WorldPosition,
                    request.Target,
                    request.Camera,
                    request.Offset,
                    out Vector2 anchoredPosition))
            {
                return new TooltipHandle(0);
            }

            NoticeInstance instance = SpawnManualNotice(
                tooltipPool,
                request.Text,
                ResolveColor(request.TextColor, tooltipPool.Settings.TextColor),
                request.Scope,
                anchoredPosition);
            if (instance == null)
            {
                return new TooltipHandle(0);
            }

            // Tooltip 需要在 Tick 中持续跟随目标或世界坐标，因此保留原始请求信息。
            instance.CoordinateMode = request.CoordinateMode;
            instance.ScreenPosition = request.ScreenPosition;
            instance.WorldPosition = request.WorldPosition;
            instance.Target = request.Target;
            instance.Camera = request.Camera;
            instance.Offset = request.Offset;
            instance.FollowTarget = request.FollowTarget || request.CoordinateMode == UINoticeCoordinateMode.Target;
            return new TooltipHandle(instance.Id);
        }

        /// <summary>隐藏指定 Tooltip；不存在时安全忽略。</summary>
        public void HideTooltip(TooltipHandle handle)
        {
            if (!handle.IsValid)
            {
                return;
            }

            ReleaseById(tooltipPool, handle.Id, false);
        }

        /// <summary>显示一条自动上浮淡出的 FloatingText。</summary>
        public FloatingTextHandle FloatingText(in FloatingTextNoticeRequest request)
        {
            if (!TryResolvePosition(
                    request.CoordinateMode,
                    request.ScreenPosition,
                    request.WorldPosition,
                    request.Target,
                    request.Camera,
                    request.Offset,
                    out Vector2 anchoredPosition))
            {
                return new FloatingTextHandle(0);
            }

            NoticeInstance instance = SpawnAutoNotice(
                floatingTextPool,
                request.Text,
                ResolveColor(request.TextColor, floatingTextPool.Settings.TextColor),
                request.Scope,
                request.Duration,
                request.FadeDuration,
                request.RiseDistance,
                anchoredPosition,
                true);
            return new FloatingTextHandle(instance != null ? instance.Id : 0);
        }

        /// <summary>显示一条自动上浮淡出的 DamageNumber。</summary>
        public DamageNumberHandle DamageNumber(in DamageNumberNoticeRequest request)
        {
            if (!TryResolvePosition(
                    request.CoordinateMode,
                    request.ScreenPosition,
                    request.WorldPosition,
                    request.Target,
                    request.Camera,
                    request.Offset,
                    out Vector2 anchoredPosition))
            {
                return new DamageNumberHandle(0);
            }

            string text = !string.IsNullOrEmpty(request.Text) ? request.Text : request.Amount.ToString();
            Color fallbackColor = request.IsCritical
                ? new Color(1f, 0.86f, 0.16f, 1f)
                : damageNumberPool.Settings.TextColor;
            NoticeInstance instance = SpawnAutoNotice(
                damageNumberPool,
                text,
                ResolveColor(request.TextColor, fallbackColor),
                request.Scope,
                request.Duration,
                request.FadeDuration,
                request.RiseDistance,
                anchoredPosition,
                true);
            return new DamageNumberHandle(instance != null ? instance.Id : 0);
        }

        /// <summary>
        /// 清理指定 Scope 下的 active Notice。
        /// 该方法只回收正在显示的实例，不销毁池中对象，方便场景切换后继续复用。
        /// </summary>
        public void ClearScope(UIPageScope scope, string sceneScopeId)
        {
            if (scope == UIPageScope.GlobalScope)
            {
                return;
            }

            ClearScope(toastPool, scope, sceneScopeId);
            ClearScope(tooltipPool, scope, sceneScopeId);
            ClearScope(floatingTextPool, scope, sceneScopeId);
            ClearScope(damageNumberPool, scope, sceneScopeId);
        }

        /// <summary>清理所有 active Notice，并将对象回收到池中。</summary>
        public void ClearAll()
        {
            ReleaseAllActive(toastPool, false);
            ReleaseAllActive(tooltipPool, false);
            ReleaseAllActive(floatingTextPool, false);
            ReleaseAllActive(damageNumberPool, false);
        }

        /// <summary>
        /// 每帧推进 Notice 生命周期。
        /// AppUIManager 使用 unscaledDeltaTime 调用，确保暂停或慢动作时 UI 提示仍能自然淡出。
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (noticeRoot == null)
            {
                return;
            }

            TickPool(toastPool, unscaledDeltaTime);
            TickPool(tooltipPool, unscaledDeltaTime);
            TickPool(floatingTextPool, unscaledDeltaTime);
            TickPool(damageNumberPool, unscaledDeltaTime);
            RefreshToastLayout();
        }

        /// <summary>
        /// 释放资源句柄。
        /// Must run before the host clears or replaces its asset provider.
        /// </summary>
        public void ReleaseLoadedResources()
        {
            ReleasePoolResource(toastPool);
            ReleasePoolResource(tooltipPool);
            ReleasePoolResource(floatingTextPool);
            ReleasePoolResource(damageNumberPool);
        }

        /// <summary>
        /// 销毁所有池对象并释放资源引用。
        /// 仅在 UI Runtime 关闭、Profile 切换或 Manager 销毁时调用。
        /// </summary>
        public void Dispose()
        {
            DestroyPoolObjects(toastPool);
            DestroyPoolObjects(tooltipPool);
            DestroyPoolObjects(floatingTextPool);
            DestroyPoolObjects(damageNumberPool);
            ReleaseLoadedResources();
            noticeRoot = null;
            noticeCanvas = null;
            assetProvider = null;
        }

        /// <summary>
        /// 配置单类 Notice 对象池。
        /// 这里会尝试同步加载配置 prefab；失败时不阻断服务，而是降级到内置 fallback 视图。
        /// </summary>
        private void ConfigurePool(NoticePool pool, AppUINoticeVisualSettings visualSettings)
        {
            pool.Settings = visualSettings;
            pool.Prefab = null;
            pool.AssetLease = null;
            string assetId = visualSettings != null ? visualSettings.PrefabAssetId : string.Empty;
            if (string.IsNullOrEmpty(assetId) || assetProvider == null)
            {
                return;
            }

            if (assetProvider.TryLoad(
                    assetId,
                    out UIAssetLoadResult<GameObject> result) &&
                result.IsSuccess)
            {
                pool.Prefab = result.Asset;
                pool.AssetLease = result.Lease;
                return;
            }

            Debug.LogWarning(
                "<Joi.H.AppUI> Notice prefab load failed; using fallback view. " +
                "AssetId=" + assetId + ", Error=" + result.ErrorMessage);
            result.Lease?.Dispose();
        }

        /// <summary>
        /// 按配置预热对象池。
        /// 预热对象默认隐藏并压入 inactive 栈，避免第一批提示触发时集中 Instantiate。
        /// </summary>
        private void PrewarmPool(NoticePool pool)
        {
            if (noticeRoot == null || pool.Settings == null)
            {
                return;
            }

            int count = pool.Settings.PrewarmCount;
            for (int i = 0; i < count; i++)
            {
                NoticeViewBase view = CreateView(pool);
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                    pool.Inactive.Push(view);
                }
            }
        }

        /// <summary>
        /// 创建自动生命周期 Notice。
        /// Toast、FloatingText、DamageNumber 共用该流程：取实例、解析时长、激活并交给 Tick 自动淡出回池。
        /// </summary>
        private NoticeInstance SpawnAutoNotice(
            NoticePool pool,
            string text,
            Color color,
            UINoticeScope scope,
            float requestDuration,
            float requestFade,
            float requestRise,
            Vector2 anchoredPosition,
            bool useProvidedPosition)
        {
            if (!EnsureReady())
            {
                return null;
            }

            NoticeInstance instance = Acquire(pool);
            if (instance == null)
            {
                return null;
            }

            float duration = requestDuration > 0f ? requestDuration : pool.Settings.DefaultDuration;
            float fade = requestFade > 0f ? requestFade : pool.Settings.FadeDuration;
            float rise = requestRise > 0f ? requestRise : pool.Settings.RiseDistance;
            Vector2 startPosition = useProvidedPosition ? anchoredPosition : GetToastPosition(pool.Active.Count - 1);
            ActivateInstance(instance, text, color, scope, startPosition, true, duration, fade, rise);
            return instance;
        }

        /// <summary>
        /// 创建手动生命周期 Notice。
        /// Tooltip 使用该流程，直到调用 HideTooltip、ClearScope 或 ClearAll 才回收。
        /// </summary>
        private NoticeInstance SpawnManualNotice(
            NoticePool pool,
            string text,
            Color color,
            UINoticeScope scope,
            Vector2 anchoredPosition)
        {
            if (!EnsureReady())
            {
                return null;
            }

            NoticeInstance instance = Acquire(pool);
            if (instance == null)
            {
                return null;
            }

            ActivateInstance(instance, text, color, scope, anchoredPosition, false, 0f, 0f, 0f);
            return instance;
        }

        /// <summary>
        /// 将池对象切换为 active 状态。
        /// 这里集中写入 scope、生命周期参数、文本与初始位置，保证回收复用后的状态干净一致。
        /// </summary>
        private void ActivateInstance(
            NoticeInstance instance,
            string text,
            Color color,
            UINoticeScope scope,
            Vector2 anchoredPosition,
            bool autoRelease,
            float duration,
            float fade,
            float rise)
        {
            instance.Scope = scope;
            instance.Elapsed = 0f;
            instance.Duration = Mathf.Max(0.01f, duration);
            instance.FadeDuration = Mathf.Max(0.01f, fade);
            instance.RiseDistance = Mathf.Max(0f, rise);
            instance.BasePosition = anchoredPosition;
            instance.AutoRelease = autoRelease;
            instance.FollowTarget = false;
            instance.Target = null;
            instance.Camera = null;
            instance.Offset = Vector2.zero;

            NoticeViewBase view = instance.View;
            view.gameObject.SetActive(true);
            view.EnsureInitialized();
            view.SetFallbackFontSize(instance.Pool.Settings.FontSize);
            view.SetText(text, color);
            view.SetAlpha(1f);
            view.SetAnchoredPosition(anchoredPosition);
        }

        /// <summary>
        /// 从对象池取得一个 Notice 实例。
        /// 取得前会先执行 active 数量限制，超限时回收同类最旧实例，避免提示无限增长。
        /// </summary>
        private NoticeInstance Acquire(NoticePool pool)
        {
            EnforceMaxActive(pool);
            NoticeViewBase view = pool.Inactive.Count > 0 ? pool.Inactive.Pop() : CreateView(pool);
            if (view == null)
            {
                return null;
            }

            NoticeInstance instance = new NoticeInstance
            {
                Id = AllocateId(),
                Pool = pool,
                Kind = pool.Kind,
                View = view,
            };
            pool.Active.Add(instance);
            return instance;
        }

        /// <summary>
        /// 分配运行时句柄 ID。
        /// ID 只要求本次服务生命周期内唯一；溢出后回到 1，0 永远表示无效句柄。
        /// </summary>
        private int AllocateId()
        {
            nextId++;
            if (nextId <= 0)
            {
                nextId = 1;
            }

            return nextId;
        }

        /// <summary>
        /// 执行同类 Notice 的最大 active 数量限制。
        /// 策略是回收最旧实例，保证最新提示优先显示。
        /// </summary>
        private void EnforceMaxActive(NoticePool pool)
        {
            int max = pool.Settings != null ? pool.Settings.MaxActiveCount : 0;
            while (max > 0 && pool.Active.Count >= max)
            {
                ReleaseInstance(pool.Active[0], false);
            }
        }

        /// <summary>
        /// 创建一个可池化 Notice 视图。
        /// 优先实例化配置 prefab；没有 prefab 时创建最小可用的 uGUI/TMP fallback。
        /// </summary>
        private NoticeViewBase CreateView(NoticePool pool)
        {
            GameObject viewObject = pool.Prefab != null
                ? UnityEngine.Object.Instantiate(pool.Prefab, noticeRoot, false)
                : CreateFallbackViewObject(pool);
            if (viewObject == null)
            {
                return null;
            }

            viewObject.name = "Notice_" + pool.Kind;
            viewObject.transform.SetParent(noticeRoot, false);

            NoticeViewBase view = EnsureViewComponent(viewObject, pool.Kind);
            view.EnsureInitialized();
            view.SetFallbackFontSize(pool.Settings.FontSize);
            view.SetAlpha(1f);
            view.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// 构建内置 fallback 视图。
        /// 该视图只保证功能可运行，美术项目后续可以通过 AppUINoticeSettings 替换 prefab。
        /// </summary>
        private GameObject CreateFallbackViewObject(NoticePool pool)
        {
            GameObject viewObject = new GameObject(
                "Notice_" + pool.Kind,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));
            RectTransform rectTransform = (RectTransform)viewObject.transform;
            rectTransform.sizeDelta = GetFallbackSize(pool.Kind);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            Image background = viewObject.GetComponent<Image>();
            background.raycastTarget = false;
            background.color = GetFallbackBackgroundColor(pool.Kind);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(viewObject.transform, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.fontSize = pool.Settings.FontSize;
            text.color = pool.Settings.TextColor;
            return viewObject;
        }

        /// <summary>根据 Notice 类型返回 fallback 视图尺寸。</summary>
        private static Vector2 GetFallbackSize(UINoticeKind kind)
        {
            switch (kind)
            {
                case UINoticeKind.Tooltip:
                    return new Vector2(280f, 56f);
                case UINoticeKind.DamageNumber:
                    return new Vector2(180f, 56f);
                case UINoticeKind.FloatingText:
                    return new Vector2(220f, 52f);
                default:
                    return new Vector2(360f, 56f);
            }
        }

        /// <summary>
        /// 返回 fallback 背景色。
        /// 飘字和伤害数字默认无背景，Toast/Tooltip 默认带半透明底避免读不清。
        /// </summary>
        private static Color GetFallbackBackgroundColor(UINoticeKind kind)
        {
            if (kind == UINoticeKind.FloatingText || kind == UINoticeKind.DamageNumber)
            {
                return new Color(0f, 0f, 0f, 0f);
            }

            return new Color(0f, 0f, 0f, 0.58f);
        }

        /// <summary>
        /// 确保视图对象带有 NoticeViewBase 派生组件。
        /// prefab 已经自带组件时保持原组件，fallback 或简易 prefab 缺失时按类型补齐。
        /// </summary>
        private static NoticeViewBase EnsureViewComponent(GameObject viewObject, UINoticeKind kind)
        {
            NoticeViewBase view = viewObject.GetComponent<NoticeViewBase>();
            if (view != null)
            {
                return view;
            }

            switch (kind)
            {
                case UINoticeKind.Toast:
                    return viewObject.AddComponent<ToastNoticeView>();
                case UINoticeKind.Tooltip:
                    return viewObject.AddComponent<TooltipNoticeView>();
                case UINoticeKind.FloatingText:
                    return viewObject.AddComponent<FloatingTextNoticeView>();
                case UINoticeKind.DamageNumber:
                    return viewObject.AddComponent<DamageNumberNoticeView>();
                default:
                    return viewObject.AddComponent<NoticeViewBase>();
            }
        }

        /// <summary>
        /// 检查 NoticeLayer 是否已绑定。
        /// 缺少 Root 时只打一条错误日志，避免每帧或每次调用刷屏。
        /// </summary>
        private bool EnsureReady()
        {
            if (noticeRoot != null)
            {
                return true;
            }

            if (!warnedMissingRoot)
            {
                warnedMissingRoot = true;
                Debug.LogError("<Joi.H.AppUI> NoticeService is not ready because NoticeLayer root is missing.");
            }

            return false;
        }

        /// <summary>
        /// 推进单类对象池中的 active Notice。
        /// 自动 Notice 在这里处理淡出、上浮和到期回收；手动 Notice 只刷新跟随坐标。
        /// </summary>
        private void TickPool(NoticePool pool, float unscaledDeltaTime)
        {
            for (int i = pool.Active.Count - 1; i >= 0; i--)
            {
                NoticeInstance instance = pool.Active[i];
                if (instance == null || instance.View == null)
                {
                    pool.Active.RemoveAt(i);
                    continue;
                }

                if (!instance.AutoRelease)
                {
                    TickManualInstance(instance);
                    continue;
                }

                instance.Elapsed += Mathf.Max(0f, unscaledDeltaTime);
                float fadeStart = instance.Duration;
                float total = instance.Duration + instance.FadeDuration;
                if (instance.Elapsed >= total)
                {
                    ReleaseInstance(instance, false);
                    continue;
                }

                float alpha = instance.Elapsed <= fadeStart
                    ? 1f
                    : 1f - ((instance.Elapsed - fadeStart) / instance.FadeDuration);
                float progress = Mathf.Clamp01(instance.Elapsed / total);
                instance.View.SetAlpha(alpha);
                instance.View.SetAnchoredPosition(
                    instance.BasePosition + new Vector2(0f, instance.RiseDistance * progress));
            }
        }

        /// <summary>
        /// 刷新手动 Notice 的跟随位置。
        /// Tooltip 可能跟随 Transform 或世界坐标，目标失效时保持最后一次有效位置。
        /// </summary>
        private void TickManualInstance(NoticeInstance instance)
        {
            if (!instance.FollowTarget)
            {
                return;
            }

            if (TryResolvePosition(
                    instance.CoordinateMode,
                    instance.ScreenPosition,
                    instance.WorldPosition,
                    instance.Target,
                    instance.Camera,
                    instance.Offset,
                    out Vector2 anchoredPosition))
            {
                instance.BasePosition = anchoredPosition;
                instance.View.SetAnchoredPosition(anchoredPosition);
            }
        }

        /// <summary>
        /// 重新计算 Toast 堆叠顺序。
        /// Toast 使用 NoticeLayer 顶部居中堆叠，其他类型保持调用方传入坐标。
        /// </summary>
        private void RefreshToastLayout()
        {
            int toastIndex = 0;
            for (int i = 0; i < toastPool.Active.Count; i++)
            {
                NoticeInstance instance = toastPool.Active[i];
                if (instance == null || instance.View == null)
                {
                    continue;
                }

                // Toast 使用固定顶中堆叠位置；其他 Notice 使用请求坐标，不参与这里的重排。
                instance.BasePosition = GetToastPosition(toastIndex);
                toastIndex++;
            }
        }

        /// <summary>根据 Toast 在 active 列表中的顺序计算顶中堆叠坐标。</summary>
        private Vector2 GetToastPosition(int index)
        {
            if (noticeRoot == null)
            {
                return Vector2.zero;
            }

            float top = noticeRoot.rect.height * 0.5f - 96f;
            return new Vector2(0f, top - Mathf.Max(0, index) * 64f);
        }

        /// <summary>
        /// 将 Screen/World/Target 坐标统一转换为 NoticeRoot 的本地坐标。
        /// World 模式需要有效 Camera；Screen 模式未传坐标时默认屏幕中心。
        /// </summary>
        private bool TryResolvePosition(
            UINoticeCoordinateMode mode,
            Vector2 screenPosition,
            Vector3 worldPosition,
            Transform target,
            Camera requestCamera,
            Vector2 offset,
            out Vector2 anchoredPosition)
        {
            anchoredPosition = Vector2.zero;
            if (!EnsureReady())
            {
                return false;
            }

            Vector2 finalScreenPosition;
            switch (mode)
            {
                case UINoticeCoordinateMode.World:
                    Camera worldCamera = requestCamera != null ? requestCamera : Camera.main;
                    if (worldCamera == null)
                    {
                        Debug.LogError("<Joi.H.AppUI> World Notice requires a Camera.");
                        return false;
                    }

                    finalScreenPosition = worldCamera.WorldToScreenPoint(worldPosition);
                    break;
                case UINoticeCoordinateMode.Target:
                    if (target == null)
                    {
                        Debug.LogError("<Joi.H.AppUI> Target Notice requires a Transform target.");
                        return false;
                    }

                    Camera targetCamera = requestCamera != null ? requestCamera : Camera.main;
                    finalScreenPosition = RectTransformUtility.WorldToScreenPoint(targetCamera, target.position);
                    break;
                default:
                    finalScreenPosition = screenPosition == Vector2.zero
                        ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                        : screenPosition;
                    break;
            }

            finalScreenPosition += offset;
            Camera uiCamera = noticeCanvas != null && noticeCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? noticeCanvas.worldCamera
                : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                noticeRoot,
                finalScreenPosition,
                uiCamera,
                out anchoredPosition);
        }

        /// <summary>
        /// 清理单个对象池中匹配 Scope 的 active Notice。
        /// 只回收显示中的对象，inactive 池对象不带业务归属，不需要逐个清理。
        /// </summary>
        private void ClearScope(NoticePool pool, UIPageScope scope, string sceneScopeId)
        {
            for (int i = pool.Active.Count - 1; i >= 0; i--)
            {
                NoticeInstance instance = pool.Active[i];
                if (instance != null && instance.Scope.Matches(scope, sceneScopeId ?? string.Empty))
                {
                    ReleaseInstance(instance, false);
                }
            }
        }

        /// <summary>按句柄 ID 回收指定 active Notice；找不到时安全忽略。</summary>
        private void ReleaseById(NoticePool pool, int id, bool destroy)
        {
            for (int i = pool.Active.Count - 1; i >= 0; i--)
            {
                NoticeInstance instance = pool.Active[i];
                if (instance != null && instance.Id == id)
                {
                    ReleaseInstance(instance, destroy);
                    return;
                }
            }
        }

        /// <summary>
        /// 回收单个对象池内所有 active Notice。
        /// destroy=true 用于 Runtime 销毁，destroy=false 用于普通 ClearAll 回池复用。
        /// </summary>
        private void ReleaseAllActive(NoticePool pool, bool destroy)
        {
            for (int i = pool.Active.Count - 1; i >= 0; i--)
            {
                ReleaseInstance(pool.Active[i], destroy);
            }
        }

        /// <summary>
        /// 回收一个 Notice 实例。
        /// 普通回收会重置视图并放回 inactive 栈；Runtime 关闭时直接销毁 GameObject。
        /// </summary>
        private void ReleaseInstance(NoticeInstance instance, bool destroy)
        {
            if (instance == null || instance.Pool == null)
            {
                return;
            }

            NoticePool pool = instance.Pool;
            pool.Active.Remove(instance);
            NoticeViewBase view = instance.View;
            if (view == null)
            {
                return;
            }

            view.ResetForPool();
            view.gameObject.SetActive(false);
            if (destroy)
            {
                UnityEngine.Object.Destroy(view.gameObject);
            }
            else
            {
                pool.Inactive.Push(view);
            }
        }

        /// <summary>销毁对象池中所有 active 和 inactive 视图对象。</summary>
        private void DestroyPoolObjects(NoticePool pool)
        {
            ReleaseAllActive(pool, true);
            while (pool.Inactive.Count > 0)
            {
                NoticeViewBase view = pool.Inactive.Pop();
                if (view != null)
                {
                    UnityEngine.Object.Destroy(view.gameObject);
                }
            }
        }

        /// <summary>
        /// 释放单类 Notice prefab 的资源句柄。
        /// 释放异常只记录日志，不能阻断 UI Runtime 关闭流程。
        /// </summary>
        private void ReleasePoolResource(NoticePool pool)
        {
            if (pool.AssetLease != null && pool.AssetLease.IsValid)
            {
                try
                {
                    pool.AssetLease.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                }
            }

            pool.AssetLease = null;
            pool.Prefab = null;
        }

        /// <summary>
        /// 解析请求颜色。
        /// Unity Color 默认值是透明黑；这里将它视为“未传颜色”，避免默认请求显示成透明文本。
        /// </summary>
        private static Color ResolveColor(Color requestColor, Color fallback)
        {
            return requestColor.a <= 0f &&
                   Mathf.Approximately(requestColor.r, 0f) &&
                   Mathf.Approximately(requestColor.g, 0f) &&
                   Mathf.Approximately(requestColor.b, 0f)
                ? fallback
                : requestColor;
        }

        /// <summary>
        /// Notice 内部分类。
        /// 不暴露到公开 API，服务仅用它选择对象池、fallback 视图和默认布局。
        /// </summary>
        private enum UINoticeKind
        {
            Toast,
            Tooltip,
            FloatingText,
            DamageNumber,
        }

        /// <summary>
        /// 单类 Notice 的对象池状态。
        /// Active 保存正在显示的实例，Inactive 保存可复用视图，Prefab/AssetLease 记录配置资源。
        /// </summary>
        private sealed class NoticePool
        {
            public readonly UINoticeKind Kind;
            public readonly List<NoticeInstance> Active = new List<NoticeInstance>(16);
            public readonly Stack<NoticeViewBase> Inactive = new Stack<NoticeViewBase>(16);
            public AppUINoticeVisualSettings Settings;
            public GameObject Prefab;
            public UIAssetLease AssetLease;

            /// <summary>创建指定类型的对象池，并给 Settings 一个安全默认值。</summary>
            public NoticePool(UINoticeKind kind)
            {
                Kind = kind;
                Settings = AppUINoticeSettings.CreateDefault().Toast;
                AssetLease = null;
            }
        }

        /// <summary>
        /// 单个 Notice 播放实例的运行时状态。
        /// 它不直接继承 MonoBehaviour，便于服务统一管理生命周期、Scope 和坐标跟随。
        /// </summary>
        private sealed class NoticeInstance
        {
            public int Id;
            public UINoticeKind Kind;
            public UINoticeScope Scope;
            public NoticePool Pool;
            public NoticeViewBase View;
            public bool AutoRelease;
            public bool FollowTarget;
            public float Elapsed;
            public float Duration;
            public float FadeDuration;
            public float RiseDistance;
            public Vector2 BasePosition;
            public UINoticeCoordinateMode CoordinateMode;
            public Vector2 ScreenPosition;
            public Vector3 WorldPosition;
            public Transform Target;
            public Camera Camera;
            public Vector2 Offset;
        }
    }
}
