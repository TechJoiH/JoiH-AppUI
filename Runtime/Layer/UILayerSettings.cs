using System;
using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// UI Layer 配置资产。
    /// 可覆盖内置 CanvasDomain 与 sortingOrder，未配置的 Layer 会走框架默认值。
    /// </summary>
    [CreateAssetMenu(fileName = "UILayerSettings", menuName = "Joi.H AppUI/Layer Settings")]
    public sealed class UILayerSettings : ScriptableObject
    {
        [SerializeField]
        private List<UILayerSetting> m_Layers = new List<UILayerSetting>();

        /// <summary>配置资产中的 Layer 设置列表。</summary>
        public IReadOnlyList<UILayerSetting> Layers
        {
            get { return m_Layers; }
        }

        /// <summary>按 LayerId 查找配置项。</summary>
        public bool TryGetSetting(UILayerId layerId, out UILayerSetting setting)
        {
            if (m_Layers != null)
            {
                for (int i = 0; i < m_Layers.Count; i++)
                {
                    UILayerSetting candidate = m_Layers[i];
                    if (candidate != null && candidate.LayerId == layerId)
                    {
                        setting = candidate;
                        return true;
                    }
                }
            }

            setting = null;
            return false;
        }
    }

    /// <summary>
    /// 单个 Layer 的 CanvasDomain 与 sortingOrder 配置。
    /// </summary>
    [Serializable]
    public sealed class UILayerSetting
    {
        /// <summary>目标 Layer。</summary>
        public UILayerId LayerId;

        /// <summary>目标 CanvasDomain。</summary>
        public UICanvasDomain CanvasDomain;

        /// <summary>拥有该 CanvasDomain 的 Canvas sortingOrder。</summary>
        public int SortingOrder;
    }
}
