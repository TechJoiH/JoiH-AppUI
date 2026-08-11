using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// UI Layer 运行时配置器。
    /// 该类只负责初始化阶段的 LayerRoot、CanvasDomain、sortingOrder 和内置层完整性校验；页面运行期 sibling 排序交给 Presentation 处理。
    /// </summary>
    internal sealed class UILayerRuntimeConfigurator
    {
        /// <summary>
        /// 框架内置层清单。
        /// 初始化校验会确保这些层至少注册一个 UILayerRoot，避免运行期打开页面时才发现 Root 缺失。
        /// </summary>
        public static readonly UILayerId[] BuiltInLayerIds =
        {
            UILayerId.SystemLayer,
            UILayerId.HudLayer,
            UILayerId.OverlayLayer,
            UILayerId.PopupLayer,
            UILayerId.ModalLayer,
            UILayerId.NoticeLayer,
            UILayerId.GuideLayer,
            UILayerId.LoadingLayer,
            UILayerId.DebugLayer,
        };

        private readonly UILayerSettings layerSettings;
        private readonly List<LayerSortEntry> layerSortEntries = new List<LayerSortEntry>(9);

        /// <summary>
        /// 创建配置器。
        /// settings 为空时使用内置默认 CanvasDomain 与 sortingOrder。
        /// </summary>
        public UILayerRuntimeConfigurator(UILayerSettings settings)
        {
            layerSettings = settings;
        }

        /// <summary>
        /// 安全应用 LayerRoot sibling 排序。
        /// 这里捕获异常是为了让初始化校验继续执行，便于一次性暴露更多配置问题。
        /// </summary>
        public void ApplyLayerSortingSafe(UILayerRoot[] layerRoots)
        {
            try
            {
                ApplyLayerSorting(layerRoots);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        /// <summary>
        /// 按 SortingOrder 设置 LayerRoot sibling 排序。
        /// order 越小 sibling index 越小、越底层；order 越大 sibling index 越大、越顶层。
        /// </summary>
        public void ApplyLayerSorting(UILayerRoot[] layerRoots)
        {
            if (layerRoots == null)
            {
                return;
            }

            layerSortEntries.Clear();
            for (int i = 0; i < layerRoots.Length; i++)
            {
                UILayerRoot root = layerRoots[i];
                if (root == null || !IsValidLayerId(root.LayerId))
                {
                    continue;
                }

                int sortingOrder;
                if (!TryResolveLayerSetting(root.LayerId, out _, out sortingOrder))
                {
                    continue;
                }

                Canvas layerCanvas = root.GetComponent<Canvas>();
                if (layerCanvas != null && layerCanvas.overrideSorting)
                {
                    layerCanvas.sortingOrder = sortingOrder;
                }

                layerSortEntries.Add(new LayerSortEntry(root, root.LayerId, sortingOrder));
            }

            layerSortEntries.Sort(CompareLayerSortEntries);
            for (int i = 0; i < layerSortEntries.Count; i++)
            {
                UILayerRoot root = layerSortEntries[i].Root;
                if (root != null)
                {
                    root.transform.SetSiblingIndex(i);
                }
            }
        }

        /// <summary>
        /// 校验 LayerRoot 清单，并把当前存在的 CanvasDomain 写入 canvasDomains。
        /// 该方法只记录错误，不抛异常，让页面 Definition 的 Critical 聚合逻辑决定是否中断初始化。
        /// </summary>
        public void ValidateLayerRoots(UILayerRoot[] layerRoots, HashSet<UICanvasDomain> canvasDomains)
        {
            if (layerRoots == null || layerRoots.Length == 0)
            {
                Debug.LogError("<Joi.H.AppUI> UILayerRoot list is empty.");
                return;
            }

            HashSet<UILayerId> layerIds = new HashSet<UILayerId>();
            for (int i = 0; i < layerRoots.Length; i++)
            {
                UILayerRoot root = layerRoots[i];
                if (root == null)
                {
                    continue;
                }

                if (!IsValidLayerId(root.LayerId))
                {
                    Debug.LogError("<Joi.H.AppUI> UILayerRoot has invalid LayerId: " + root.LayerId, root);
                    continue;
                }

                if (!IsValidCanvasDomain(root.CanvasDomain))
                {
                    Debug.LogError("<Joi.H.AppUI> UILayerRoot has invalid CanvasDomain: " + root.CanvasDomain, root);
                    continue;
                }

                layerIds.Add(root.LayerId);
                canvasDomains?.Add(root.CanvasDomain);

                UICanvasDomain expectedDomain;
                int sortingOrder;
                if (TryResolveLayerSetting(root.LayerId, out expectedDomain, out sortingOrder) &&
                    root.CanvasDomain != expectedDomain)
                {
                    Debug.LogError(
                        "<Joi.H.AppUI> UILayerRoot CanvasDomain does not match layer setting. Layer=" +
                        root.LayerId +
                        ", Root=" +
                        root.CanvasDomain +
                        ", Expected=" +
                        expectedDomain,
                        root);
                }
            }

            for (int i = 0; i < BuiltInLayerIds.Length; i++)
            {
                UILayerId layerId = BuiltInLayerIds[i];
                if (!layerIds.Contains(layerId))
                {
                    Debug.LogError("<Joi.H.AppUI> UILayerRoot is missing for built-in layer: " + layerId);
                }
            }
        }

        /// <summary>
        /// 解析指定 Layer 的 CanvasDomain 与 sortingOrder。
        /// 配置资产优先；配置缺失或枚举非法时回退到内置默认值。
        /// </summary>
        public bool TryResolveLayerSetting(
            UILayerId layerId,
            out UICanvasDomain canvasDomain,
            out int sortingOrder)
        {
            UILayerSetting setting;
            if (layerSettings != null && layerSettings.TryGetSetting(layerId, out setting) && setting != null)
            {
                canvasDomain = setting.CanvasDomain;
                sortingOrder = setting.SortingOrder;
                if (!IsValidCanvasDomain(canvasDomain))
                {
                    Debug.LogError(
                        "<Joi.H.AppUI> UILayerSettings has invalid CanvasDomain for " +
                        layerId +
                        ": " +
                        canvasDomain,
                        layerSettings);
                    return TryGetDefaultLayerSetting(layerId, out canvasDomain, out sortingOrder);
                }

                return true;
            }

            return TryGetDefaultLayerSetting(layerId, out canvasDomain, out sortingOrder);
        }

        /// <summary>
        /// 返回框架内置 Layer 配置。
        /// Popup 与 Overlay 共享 Overlay CanvasDomain，通过 LayerRoot sibling 顺序区分显示层级。
        /// </summary>
        public static bool TryGetDefaultLayerSetting(
            UILayerId layerId,
            out UICanvasDomain canvasDomain,
            out int sortingOrder)
        {
            switch (layerId)
            {
                case UILayerId.SystemLayer:
                    canvasDomain = UICanvasDomain.System;
                    sortingOrder = 1000;
                    return true;
                case UILayerId.HudLayer:
                    canvasDomain = UICanvasDomain.Hud;
                    sortingOrder = 2000;
                    return true;
                case UILayerId.OverlayLayer:
                case UILayerId.PopupLayer:
                    canvasDomain = UICanvasDomain.Overlay;
                    sortingOrder = 3000;
                    return true;
                case UILayerId.ModalLayer:
                    canvasDomain = UICanvasDomain.Modal;
                    sortingOrder = 4000;
                    return true;
                case UILayerId.NoticeLayer:
                    canvasDomain = UICanvasDomain.Notice;
                    sortingOrder = 5000;
                    return true;
                case UILayerId.GuideLayer:
                    canvasDomain = UICanvasDomain.Guide;
                    sortingOrder = 6000;
                    return true;
                case UILayerId.LoadingLayer:
                    canvasDomain = UICanvasDomain.Loading;
                    sortingOrder = 7000;
                    return true;
                case UILayerId.DebugLayer:
                    canvasDomain = UICanvasDomain.Debug;
                    sortingOrder = 8000;
                    return true;
                default:
                    canvasDomain = UICanvasDomain.System;
                    sortingOrder = 0;
                    return false;
            }
        }

        /// <summary>
        /// 判断 LayerId 是否是有效枚举值。
        /// </summary>
        public static bool IsValidLayerId(UILayerId layerId)
        {
            return Enum.IsDefined(typeof(UILayerId), layerId);
        }

        /// <summary>
        /// 判断 CanvasDomain 是否是有效枚举值。
        /// </summary>
        public static bool IsValidCanvasDomain(UICanvasDomain canvasDomain)
        {
            return Enum.IsDefined(typeof(UICanvasDomain), canvasDomain);
        }

        /// <summary>
        /// 判断页面 Scope 是否是有效枚举值。
        /// </summary>
        public static bool IsValidPageScope(UIPageScope scope)
        {
            return Enum.IsDefined(typeof(UIPageScope), scope);
        }

        /// <summary>
        /// 判断 OpenPolicy 是否是有效枚举值。
        /// </summary>
        public static bool IsValidOpenPolicy(UIOpenPolicy openPolicy)
        {
            return Enum.IsDefined(typeof(UIOpenPolicy), openPolicy);
        }

        /// <summary>
        /// 检查 LayerRoot 使用的 UI Canvas 是否有启用的 GraphicRaycaster。
        /// 独立 Canvas Layer 优先检查自身 GraphicRaycaster，普通 Layer 回退到父级主 Canvas。
        /// </summary>
        public static bool HasEnabledGraphicRaycaster(UILayerRoot layerRoot)
        {
            if (layerRoot == null || layerRoot.ContentRoot == null)
            {
                return false;
            }

            Canvas layerCanvas = layerRoot.GetComponent<Canvas>();
            if (layerCanvas != null && layerCanvas.overrideSorting)
            {
                GraphicRaycaster layerRaycaster = layerRoot.GetComponent<GraphicRaycaster>();
                return layerRaycaster != null && layerRaycaster.enabled;
            }

            GraphicRaycaster raycaster = layerRoot.ContentRoot.GetComponentInParent<GraphicRaycaster>(true);
            return raycaster != null && raycaster.enabled;
        }

        /// <summary>
        /// 判断指定 Layer 是否适合作为全屏页面承载层。
        /// HUD、Popup 和 Notice 通常是轻量覆盖层，不承载业务全屏页面。
        /// </summary>
        public static bool CanLayerHostFullScreen(UILayerId layerId)
        {
            return layerId != UILayerId.HudLayer &&
                   layerId != UILayerId.PopupLayer &&
                   layerId != UILayerId.NoticeLayer;
        }

        private static int CompareLayerSortEntries(LayerSortEntry left, LayerSortEntry right)
        {
            int sortingOrderCompare = left.SortingOrder.CompareTo(right.SortingOrder);
            if (sortingOrderCompare != 0)
            {
                return sortingOrderCompare;
            }

            return ((int)left.LayerId).CompareTo((int)right.LayerId);
        }

        private readonly struct LayerSortEntry
        {
            public readonly UILayerRoot Root;
            public readonly UILayerId LayerId;
            public readonly int SortingOrder;

            public LayerSortEntry(UILayerRoot root, UILayerId layerId, int sortingOrder)
            {
                Root = root;
                LayerId = layerId;
                SortingOrder = sortingOrder;
            }
        }
    }
}
