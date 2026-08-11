using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// UI Layer 根节点组件。
    /// 标记某个 RectTransform 作为指定 UILayerId 的 ContentRoot，并声明它所属的 CanvasDomain。
    /// </summary>
    public sealed class UILayerRoot : MonoBehaviour
    {
        [SerializeField]
        private UILayerId layerId = UILayerId.SystemLayer;

        [SerializeField]
        private UICanvasDomain canvasDomain = UICanvasDomain.System;

        [SerializeField]
        private RectTransform contentRoot;

        /// <summary>该 Root 对应的 LayerId。</summary>
        public UILayerId LayerId
        {
            get { return layerId; }
        }

        /// <summary>该 Root 所属 CanvasDomain。</summary>
        public UICanvasDomain CanvasDomain
        {
            get { return canvasDomain; }
        }

        /// <summary>页面实例挂载的根 RectTransform；未显式指定时回退到自身 Transform。</summary>
        public RectTransform ContentRoot
        {
            get
            {
                if (contentRoot == null)
                {
                    contentRoot = transform as RectTransform;
                }

                return contentRoot;
            }
        }

        /// <summary>
        /// 运行时构建 UI Root 时写入 Layer 配置。
        /// Configures a layer created by an integrating application at runtime.
        /// </summary>
        public void Configure(UILayerId targetLayerId, UICanvasDomain targetCanvasDomain, RectTransform targetContentRoot)
        {
            layerId = targetLayerId;
            canvasDomain = targetCanvasDomain;
            contentRoot = targetContentRoot != null ? targetContentRoot : transform as RectTransform;
        }
    }
}
