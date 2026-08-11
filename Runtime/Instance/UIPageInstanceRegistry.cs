using System.Collections.Generic;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面实例注册表。
    /// 负责按 PageId 维护当前活跃或隐藏实例，是 Manager 和各 Coordinator 的共享实例索引。
    /// </summary>
    public sealed class UIPageInstanceRegistry
    {
        private readonly Dictionary<string, UIPageInstance> instances =
            new Dictionary<string, UIPageInstance>(16);

        private readonly Dictionary<long, UIPageInstance> instancesByRuntimeId =
            new Dictionary<long, UIPageInstance>(16);

        private readonly List<UIPageInstance> scratchInstances =
            new List<UIPageInstance>(16);

        private long nextRuntimeInstanceId;

        /// <summary>尝试按 PageId 获取实例。</summary>
        public bool TryGet(string pageId, out UIPageInstance instance)
        {
            if (string.IsNullOrEmpty(pageId))
            {
                instance = null;
                return false;
            }

            return instances.TryGetValue(pageId, out instance);
        }

        /// <summary>注册或替换指定 PageId 的实例。</summary>
        public void Register(UIPageInstance instance)
        {
            if (instance == null || string.IsNullOrEmpty(instance.PageId))
            {
                return;
            }

            if (instances.TryGetValue(instance.PageId, out UIPageInstance previous) &&
                previous != null &&
                !ReferenceEquals(previous, instance))
            {
                RemoveRuntimeIdMapping(previous);
            }

            if (instance.RuntimeInstanceId <= 0 ||
                !instancesByRuntimeId.TryGetValue(instance.RuntimeInstanceId, out UIPageInstance registered) ||
                !ReferenceEquals(registered, instance))
            {
                instance.RuntimeInstanceId = AllocateRuntimeInstanceId();
            }

            instances[instance.PageId] = instance;
            instancesByRuntimeId[instance.RuntimeInstanceId] = instance;
        }

        /// <summary>从注册表移除页面实例。</summary>
        public bool Remove(string pageId)
        {
            if (string.IsNullOrEmpty(pageId) ||
                !instances.TryGetValue(pageId, out UIPageInstance instance))
            {
                return false;
            }

            instances.Remove(pageId);
            RemoveRuntimeIdMapping(instance);
            return true;
        }

        /// <summary>
        /// 使用交互句柄解析当前页面实例。
        /// PageId、RuntimeInstanceId、OperationVersion 和注册对象身份必须全部匹配。
        /// </summary>
        internal bool TryResolve(in UIPageInteractionHandle handle, out UIPageInstance instance)
        {
            if (!handle.IsValid ||
                !instancesByRuntimeId.TryGetValue(handle.InstanceId, out instance) ||
                instance == null ||
                instance.RuntimeInstanceId != handle.InstanceId ||
                instance.OperationVersion != handle.OperationVersion ||
                !string.Equals(instance.PageId, handle.PageId, System.StringComparison.Ordinal) ||
                !instances.TryGetValue(handle.PageId, out UIPageInstance pageInstance) ||
                !ReferenceEquals(pageInstance, instance))
            {
                instance = null;
                return false;
            }

            return true;
        }

        /// <summary>为当前已注册实例创建可复验的交互句柄。</summary>
        internal bool TryCreateInteractionHandle(
            UIPageInstance instance,
            out UIPageInteractionHandle handle)
        {
            if (instance == null ||
                instance.RuntimeInstanceId <= 0 ||
                !instancesByRuntimeId.TryGetValue(instance.RuntimeInstanceId, out UIPageInstance registered) ||
                !ReferenceEquals(registered, instance) ||
                !instances.TryGetValue(instance.PageId, out UIPageInstance pageInstance) ||
                !ReferenceEquals(pageInstance, instance))
            {
                handle = default;
                return false;
            }

            handle = instance.ToInteractionHandle();
            return handle.IsValid;
        }

        /// <summary>获取当前实例快照列表；该列表为内部复用列表，调用方不应长期持有。</summary>
        public List<UIPageInstance> GetSnapshot()
        {
            scratchInstances.Clear();
            foreach (KeyValuePair<string, UIPageInstance> pair in instances)
            {
                scratchInstances.Add(pair.Value);
            }

            return scratchInstances;
        }

        /// <summary>清空注册表和临时快照列表。</summary>
        public void Clear()
        {
            foreach (KeyValuePair<string, UIPageInstance> pair in instances)
            {
                if (pair.Value != null)
                {
                    pair.Value.RuntimeInstanceId = 0;
                }
            }

            instances.Clear();
            instancesByRuntimeId.Clear();
            scratchInstances.Clear();
        }

        private long AllocateRuntimeInstanceId()
        {
            if (nextRuntimeInstanceId == long.MaxValue)
            {
                throw new System.InvalidOperationException(
                    "<Joi.H.AppUI> Runtime page instance id exhausted.");
            }

            nextRuntimeInstanceId++;
            return nextRuntimeInstanceId;
        }

        private void RemoveRuntimeIdMapping(UIPageInstance instance)
        {
            if (instance == null || instance.RuntimeInstanceId <= 0)
            {
                return;
            }

            if (instancesByRuntimeId.TryGetValue(
                    instance.RuntimeInstanceId,
                    out UIPageInstance registered) &&
                ReferenceEquals(registered, instance))
            {
                instancesByRuntimeId.Remove(instance.RuntimeInstanceId);
            }

            instance.RuntimeInstanceId = 0;
        }
    }
}
