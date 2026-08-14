using System;
using System.Threading;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 打开页面参数。
    /// 使用不可变结构承载数据、打开回调、取消 token 和 SceneScopeId，避免异步过程中被外部修改。
    /// </summary>
    public readonly struct UIOpenArgs
    {
        /// <summary>传给页面 OnDataLoadEx 的业务数据。</summary>
        public object Data { get; }

        /// <summary>是否显式传入过数据；用于区分“传了 null”和“未传数据”。</summary>
        public bool HasData { get; }

        /// <summary>打开成功后的回调，仅在 OpenResult.Success 时触发。</summary>
        public Action<UIOpenResult> OnOpened { get; }

        /// <summary>打开流程取消 token；加载接口本身不可取消时，会在 await 返回后进行校验和清理。</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>请求所属场景作用域；空字符串表示兼容旧调用，不做严格 scope 校验。</summary>
        public string SceneScopeId { get; }

        internal UISceneScopeStamp SceneScopeStamp { get; }

        /// <summary>默认打开参数，不携带数据、回调、取消 token 或 SceneScopeId。</summary>
        public static UIOpenArgs None
        {
            get
            {
                return new UIOpenArgs(
                    null,
                    false,
                    null,
                    System.Threading.CancellationToken.None,
                    UISceneScopeStamp.Unstamped(string.Empty));
            }
        }

        /// <summary>创建一个显式携带数据的打开参数，即使 data 为 null 也会标记 HasData=true。</summary>
        public static UIOpenArgs FromExplicit(object data)
        {
            return new UIOpenArgs(
                data,
                true,
                null,
                System.Threading.CancellationToken.None,
                UISceneScopeStamp.Unstamped(string.Empty));
        }

        /// <summary>返回带打开完成回调的新参数。</summary>
        public UIOpenArgs WithOnOpened(Action<UIOpenResult> onOpened)
        {
            return new UIOpenArgs(
                Data,
                HasData,
                onOpened,
                CancellationToken,
                SceneScopeStamp);
        }

        /// <summary>返回带取消 token 的新参数。</summary>
        public UIOpenArgs WithCancellationToken(CancellationToken cancellationToken)
        {
            return new UIOpenArgs(
                Data,
                HasData,
                OnOpened,
                cancellationToken,
                SceneScopeStamp);
        }

        /// <summary>返回带 SceneScopeId 的新参数。</summary>
        public UIOpenArgs WithSceneScopeId(string sceneScopeId)
        {
            return new UIOpenArgs(
                Data,
                HasData,
                OnOpened,
                CancellationToken,
                UISceneScopeStamp.Unstamped(sceneScopeId));
        }

        internal UIOpenArgs WithSceneScopeStamp(UISceneScopeStamp stamp)
        {
            return new UIOpenArgs(
                Data,
                HasData,
                OnOpened,
                CancellationToken,
                stamp);
        }

        private UIOpenArgs(
            object data,
            bool hasData,
            Action<UIOpenResult> onOpened,
            CancellationToken cancellationToken,
            UISceneScopeStamp sceneScopeStamp)
        {
            Data = data;
            HasData = hasData;
            OnOpened = onOpened;
            CancellationToken = cancellationToken;
            SceneScopeStamp = sceneScopeStamp;
            SceneScopeId = sceneScopeStamp.SceneScopeId ?? string.Empty;
        }
    }
}
