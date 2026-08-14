using System.Threading;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 关闭页面请求。
    /// Close 通过该结构决定关闭后隐藏还是释放，并携带取消 token 与 SceneScopeId。
    /// </summary>
    public struct UICloseRequest
    {
        /// <summary>true 表示关闭后释放实例和资源；false 表示仅隐藏并保留实例。</summary>
        public bool ReleaseOnClose;

        /// <summary>关闭流程取消 token；开始改变状态后仍会尽量完成对应清理。</summary>
        public CancellationToken CancellationToken;

        /// <summary>请求所属场景作用域；非空时必须与实例 SceneScopeId 匹配。</summary>
        public string SceneScopeId;

        internal UISceneScopeStamp SceneScopeStamp;

        /// <summary>默认关闭请求：释放页面，不携带取消 token 和 SceneScopeId。</summary>
        public static UICloseRequest Default
        {
            get
            {
                return new UICloseRequest
                {
                    ReleaseOnClose = true,
                    CancellationToken = System.Threading.CancellationToken.None,
                    SceneScopeId = string.Empty,
                    SceneScopeStamp = UISceneScopeStamp.Unstamped(string.Empty),
                };
            }
        }

        /// <summary>设置取消 token 并返回当前请求副本。</summary>
        public UICloseRequest WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        /// <summary>设置 SceneScopeId 并返回当前请求副本。</summary>
        public UICloseRequest WithSceneScopeId(string sceneScopeId)
        {
            SceneScopeId = sceneScopeId ?? string.Empty;
            SceneScopeStamp = UISceneScopeStamp.Unstamped(SceneScopeId);
            return this;
        }

        internal UICloseRequest WithSceneScopeStamp(UISceneScopeStamp stamp)
        {
            SceneScopeStamp = stamp;
            SceneScopeId = stamp.SceneScopeId ?? string.Empty;
            return this;
        }
    }
}
