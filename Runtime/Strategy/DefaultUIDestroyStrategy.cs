using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 默认 UI 销毁策略。
    /// 直接 Destroy 页面 GameObject，资源句柄释放由 UIPageInstanceReleaser 负责。
    /// </summary>
    public sealed class DefaultUIDestroyStrategy : IUIDestroyStrategy
    {
        /// <summary>默认策略使用空字符串 ID。</summary>
        public string StrategyId
        {
            get { return string.Empty; }
        }

        /// <summary>销毁页面 GameObject。</summary>
        public void Destroy(UIPageInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.GameObject != null)
            {
                Object.Destroy(instance.GameObject);
            }
        }
    }
}
