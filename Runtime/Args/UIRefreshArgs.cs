using System.Threading;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 刷新页面参数。
    /// 用于统一承载刷新数据、取消 token 和 SceneScopeId。
    /// </summary>
    public readonly struct UIRefreshArgs
    {
        /// <summary>传给页面 OnDataLoadEx 的刷新数据。</summary>
        public object Data { get; }

        /// <summary>刷新流程取消 token。</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>请求所属场景作用域；空字符串表示不做严格 scope 校验。</summary>
        public string SceneScopeId { get; }

        /// <summary>创建仅携带数据的刷新参数。</summary>
        public UIRefreshArgs(object data)
            : this(data, System.Threading.CancellationToken.None, string.Empty)
        {
        }

        /// <summary>创建完整刷新参数。</summary>
        public UIRefreshArgs(object data, CancellationToken cancellationToken, string sceneScopeId)
        {
            Data = data;
            CancellationToken = cancellationToken;
            SceneScopeId = sceneScopeId ?? string.Empty;
        }

        /// <summary>返回带取消 token 的新参数。</summary>
        public UIRefreshArgs WithCancellationToken(CancellationToken cancellationToken)
        {
            return new UIRefreshArgs(Data, cancellationToken, SceneScopeId);
        }

        /// <summary>返回带 SceneScopeId 的新参数。</summary>
        public UIRefreshArgs WithSceneScopeId(string sceneScopeId)
        {
            return new UIRefreshArgs(Data, CancellationToken, sceneScopeId);
        }
    }
}
