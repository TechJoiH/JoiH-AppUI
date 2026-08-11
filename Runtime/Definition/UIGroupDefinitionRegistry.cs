using System.Collections.Generic;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Group 定义注册表。
    /// Editor 和运行时可按 GroupId 查找可复用 GroupDefinition。
    /// </summary>
    [CreateAssetMenu(fileName = "UIGroupDefinitionRegistry", menuName = "Joi.H AppUI/Group Definition Registry")]
    public sealed class UIGroupDefinitionRegistry : ScriptableObject
    {
        [SerializeField]
        private List<UIGroupDefinition> m_Groups = new List<UIGroupDefinition>();

        private Dictionary<string, UIGroupDefinition> groupById;

        /// <summary>当前注册的所有 Group 定义。</summary>
        public IReadOnlyList<UIGroupDefinition> Groups
        {
            get { return m_Groups; }
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

        /// <summary>按 GroupId 查找 GroupDefinition。</summary>
        public bool TryGet(string groupId, out UIGroupDefinition definition)
        {
            if (groupById == null)
            {
                RebuildIndex();
            }

            if (string.IsNullOrEmpty(groupId))
            {
                definition = null;
                return false;
            }

            return groupById.TryGetValue(groupId, out definition);
        }

        /// <summary>重建 GroupId 到 Definition 的索引，重复 GroupId 保留第一个。</summary>
        public void RebuildIndex()
        {
            if (groupById == null)
            {
                groupById = new Dictionary<string, UIGroupDefinition>(m_Groups.Count);
            }
            else
            {
                groupById.Clear();
            }

            for (int i = 0; i < m_Groups.Count; i++)
            {
                UIGroupDefinition group = m_Groups[i];
                if (group == null || string.IsNullOrEmpty(group.GroupId))
                {
                    continue;
                }

                if (!groupById.ContainsKey(group.GroupId))
                {
                    groupById.Add(group.GroupId, group);
                }
            }
        }
    }
}
