using System;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面实例释放服务。
    /// 该类集中执行 Dispose、DestroyStrategy、资源句柄释放、注册表移除和展示状态复位，保证失败清理与正常关闭使用同一套释放协议。
    /// </summary>
    internal sealed class UIPageInstanceReleaser
    {
        private readonly UIPageInstanceRegistry instanceRegistry;
        private readonly Func<string, IUIDestroyStrategy> destroyStrategyResolver;
        private readonly Action<UIPageInstance> presentationStateResetter;

        /// <summary>
        /// 创建释放服务。
        /// registry 用于移除实例；resolver 用于选择 DestroyStrategy；resourceReleaser 负责归还资源句柄；presentationResetter 负责清栈、清焦点、归零暂停和输入状态。
        /// </summary>
        public UIPageInstanceReleaser(
            UIPageInstanceRegistry registry,
            Func<string, IUIDestroyStrategy> resolver,
            Action<UIPageInstance> presentationResetter)
        {
            instanceRegistry = registry;
            destroyStrategyResolver = resolver;
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
        /// 流程顺序为：先复位展示状态，再调用 OnDispose，一定继续 Destroy、资源释放和 registry 移除；任一步异常只记录，不阻断后续清理。
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
            DestroyInstanceSafe(instance, reason);
            ReleaseAssetLeaseSafe(instance.AssetLease, reason);
            instance.AssetLease = null;

            instance.State = UIPageState.Released;
            instance.StackVisible = false;
            RemoveFromRegistrySafe(instance);
            return UIReleaseResult.Dirty;
        }

        /// <summary>
        /// 销毁尚未注册为 UIPageInstance 的临时 prefab，并释放它的资源句柄。
        /// 主要用于加载成功但 Controller 校验失败的路径。
        /// </summary>
        public void DestroyLoadedObject(GameObject pageObject, UIAssetLease lease)
        {
            if (pageObject != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(pageObject);
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                }
            }

            ReleaseAssetLeaseSafe(lease, UIReleaseReason.OpenFailed);
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

        private void DestroyInstanceSafe(UIPageInstance instance, UIReleaseReason reason)
        {
            IUIDestroyStrategy destroyStrategy = null;
            try
            {
                string strategyId = instance.Definition != null ? instance.Definition.DestroyStrategyId : null;
                destroyStrategy = destroyStrategyResolver != null ? destroyStrategyResolver(strategyId) : null;
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }

            if (destroyStrategy == null)
            {
                return;
            }

            try
            {
                destroyStrategy.Destroy(instance);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "<Joi.H.AppUI> Destroy strategy failed. Page=" +
                    instance.PageId +
                    ", Reason=" +
                    reason);
                Debug.LogError(exception);
            }
        }

        private static void ReleaseAssetLeaseSafe(
            UIAssetLease lease,
            UIReleaseReason reason)
        {
            if (lease == null || !lease.IsValid)
            {
                return;
            }

            try
            {
                lease.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "<Joi.H.AppUI> Resource handle release failed. Reason=" +
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
