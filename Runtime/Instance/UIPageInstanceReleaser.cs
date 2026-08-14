using System;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面实例释放服务。
    /// 该类集中执行 Controller Dispose、Allocation Release、注册表移除和展示状态复位，保证失败清理与正常关闭使用同一套释放协议。
    /// </summary>
    internal sealed class UIPageInstanceReleaser
    {
        private readonly UIPageInstanceRegistry instanceRegistry;
        private readonly Action<UIPageInstance> presentationStateResetter;

        /// <summary>
        /// 创建释放服务。
        /// registry 用于移除实例；allocation 负责成对释放实例与资源所有权；presentationResetter 负责清栈、清焦点、归零暂停和输入状态。
        /// </summary>
        public UIPageInstanceReleaser(
            UIPageInstanceRegistry registry,
            Action<UIPageInstance> presentationResetter)
        {
            instanceRegistry = registry;
            presentationStateResetter = presentationResetter;
        }

        /// <summary>
        /// 清理打开失败产生的半成品实例。
        /// 失败路径不再额外手动调用 OnDispose，而是转入统一 ReleaseInstance，避免 OnDisposeEx 被调用两次。
        /// </summary>
        public UIReleaseResult CleanupFailedInstance(UIPageInstance instance)
        {
            return ReleaseInstance(instance, UIReleaseReason.OpenFailed);
        }

        /// <summary>
        /// 释放页面实例并返回是否需要刷新显示状态。
        /// 流程顺序为：先复位展示状态，再调用 OnDispose，一定继续 Allocation Release 和 registry 移除；任一步异常只记录，不阻断后续清理。
        /// </summary>
        public UIReleaseResult ReleaseInstance(UIPageInstance instance, UIReleaseReason reason)
        {
            if (instance == null)
            {
                return UIReleaseResult.Clean;
            }

            if (instance.State == UIPageState.Released)
            {
                RemoveFromRegistrySafe(instance);
                return UIReleaseResult.Clean;
            }

            // 展示状态必须先归零，避免 Destroy 或 Dispose 异常后页面仍然持有焦点、暂停或输入禁用状态。
            ResetPresentationStateSafe(instance, reason);
            instance.StackVisible = false;
            instance.State = UIPageState.Disposed;

            DisposeControllerSafe(instance, reason);
            ReleaseAllocationSafe(instance, reason);

            instance.State = UIPageState.Released;
            instance.StackVisible = false;
            RemoveFromRegistrySafe(instance);
            return UIReleaseResult.Dirty;
        }

        private void ResetPresentationStateSafe(UIPageInstance instance, UIReleaseReason reason)
        {
            try
            {
                presentationStateResetter?.Invoke(instance);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "<Joi.H.AppUI> Reset presentation state failed. Page=" +
                    instance.PageId +
                    ", Reason=" +
                    reason);
                Debug.LogError(exception);
            }
        }

        private static void DisposeControllerSafe(UIPageInstance instance, UIReleaseReason reason)
        {
            if (instance.Controller == null)
            {
                return;
            }

            try
            {
                instance.Controller.OnDispose();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "<Joi.H.AppUI> Controller dispose failed. Page=" +
                    instance.PageId +
                    ", Reason=" +
                    reason);
                Debug.LogError(exception);
            }
        }

        private static void ReleaseAllocationSafe(
            UIPageInstance instance,
            UIReleaseReason reason)
        {
            UIPageInstanceAllocation allocation = instance.Allocation;
            instance.Allocation = null;
            if (allocation == null)
            {
                return;
            }

            try
            {
                allocation.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "<Joi.H.AppUI> Instance allocation release failed. Page=" +
                    instance.PageId +
                    ", Reason=" +
                    reason);
                Debug.LogError(exception);
            }
        }

        private void RemoveFromRegistrySafe(UIPageInstance instance)
        {
            try
            {
                instanceRegistry?.Remove(instance.PageId);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }
    }
}
