using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面定义注册表。
    /// AppUIManager 初始化时会重建索引，运行时按 PageId 快速查找 UIPageDefinition。
    /// </summary>
    [CreateAssetMenu(fileName = "UIPageDefinitionRegistry", menuName = "Joi.H AppUI/Page Definition Registry")]
    public sealed class UIPageDefinitionRegistry : ScriptableObject
    {
        [SerializeField]
        private List<UIPageDefinition> m_Pages = new List<UIPageDefinition>();

        private Dictionary<string, UIPageDefinition> pageById;

        /// <summary>当前注册的所有页面定义。</summary>
        public IReadOnlyList<UIPageDefinition> Pages
        {
            get { return m_Pages; }
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

        /// <summary>按 PageId 查找页面定义；索引未初始化时会自动重建。</summary>
        public bool TryGet(string pageId, out UIPageDefinition definition)
        {
            if (pageById == null)
            {
                RebuildIndex();
            }

            if (string.IsNullOrEmpty(pageId))
            {
                definition = null;
                return false;
            }

            return pageById.TryGetValue(pageId, out definition);
        }

        /// <summary>重建 PageId 到 Definition 的索引，重复 PageId 保留第一个并由初始化校验报错。</summary>
        public void RebuildIndex()
        {
            if (pageById == null)
            {
                pageById = new Dictionary<string, UIPageDefinition>(m_Pages.Count);
            }
            else
            {
                pageById.Clear();
            }

            for (int i = 0; i < m_Pages.Count; i++)
            {
                UIPageDefinition page = m_Pages[i];
                if (page == null || string.IsNullOrEmpty(page.PageId))
                {
                    continue;
                }

                if (!pageById.ContainsKey(page.PageId))
                {
                    pageById.Add(page.PageId, page);
                }
            }
        }
    }
}
