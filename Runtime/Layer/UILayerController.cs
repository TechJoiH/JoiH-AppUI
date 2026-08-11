using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// LayerRoot 索引服务。
    /// 初始化时收集场景中的 UILayerRoot，运行时按 UILayerId 查找页面挂载点。
    /// </summary>
    public sealed class UILayerController
    {
        private readonly Dictionary<UILayerId, UILayerRoot> layerRoots =
            new Dictionary<UILayerId, UILayerRoot>(8);

        /// <summary>用传入 Root 列表重建 LayerRoot 索引，重复 Layer 会记录错误并保留第一个。</summary>
        public void Initialize(UILayerRoot[] roots)
        {
            layerRoots.Clear();
            if (roots == null)
            {
                return;
            }

            for (int i = 0; i < roots.Length; i++)
            {
                UILayerRoot root = roots[i];
                if (root == null)
                {
                    continue;
                }

                if (!layerRoots.ContainsKey(root.LayerId))
                {
                    layerRoots.Add(root.LayerId, root);
                }
                else
                {
                    Debug.LogError("<Joi.H.AppUI> Duplicate UI layer root: " + root.LayerId);
                }
            }
        }

        /// <summary>尝试获取指定 Layer 的 Root。</summary>
        public bool TryGetRoot(UILayerId layerId, out UILayerRoot root)
        {
            return layerRoots.TryGetValue(layerId, out root) && root != null;
        }
    }
}
