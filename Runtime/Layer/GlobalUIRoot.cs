using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 全局 UI 根组件。
    /// Collects Canvas, UILayerRoot, and AppUIManager references for AppUIRuntimeHost.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AppUIManager))]
    public sealed class GlobalUIRoot : MonoBehaviour
    {
        [SerializeField]
        private Canvas[] canvases;

        [SerializeField]
        private UILayerRoot[] layerRoots;

        [SerializeField]
        private AppUIManager uiManager;

        /// <summary>当前 UI Root 下的 Canvas 列表。</summary>
        public IReadOnlyList<Canvas> Canvases
        {
            get { return canvases; }
        }

        /// <summary>当前 UI Root 下的 LayerRoot 列表。</summary>
        public UILayerRoot[] LayerRoots
        {
            get
            {
                RefreshIfNeeded();
                return layerRoots;
            }
        }

        /// <summary>当前 Root 上的 AppUIManager。</summary>
        public AppUIManager UIManager
        {
            get
            {
                if (uiManager == null)
                {
                    uiManager = GetComponent<AppUIManager>();
                }

                return uiManager;
            }
        }

        private void Awake()
        {
            RefreshIfNeeded();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshIfNeeded();
        }
#endif

        /// <summary>
        /// Writes references when an integrating application builds the root at runtime.
        /// </summary>
        public void Configure(Canvas[] rootCanvases, UILayerRoot[] roots, AppUIManager manager)
        {
            canvases = rootCanvases;
            layerRoots = roots;
            uiManager = manager != null ? manager : GetComponent<AppUIManager>();
        }

        private void RefreshIfNeeded()
        {
            // 这些引用主要服务场景或 prefab 配置；为空时从子节点自动补齐，减少手工漏配。
            if (uiManager == null)
            {
                uiManager = GetComponent<AppUIManager>();
            }

            if (canvases == null || canvases.Length == 0)
            {
                canvases = GetComponentsInChildren<Canvas>(true);
            }

            if (layerRoots == null || layerRoots.Length == 0)
            {
                layerRoots = GetComponentsInChildren<UILayerRoot>(true);
            }
        }
    }
}
