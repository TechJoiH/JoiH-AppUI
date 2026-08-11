using Cysharp.Threading.Tasks;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面控制器基类。
    /// 继承 UIBaseController，并额外持有 UIPanelContext，供页面主动关闭自己或访问 UI 服务。
    /// </summary>
    public abstract class PanelBaseController : UIBaseController
    {
        /// <summary>页面上下文，包含 UI 服务、PageId 和页面定义。</summary>
        protected UIPanelContext Context { get; private set; }

        internal void SetContext(UIPanelContext context)
        {
            Context = context;
        }

        /// <summary>请求关闭当前页面，默认使用 ReleaseOnClose=true。</summary>
        protected void CloseSelf()
        {
            if (Context == null || Context.UI == null)
            {
                return;
            }

            Context.UI.CloseAsync(Context.PageId, UICloseRequest.Default).Forget();
        }

        /// <summary>框架内部关闭授权入口，最终转发给业务可重写的 CanCloseEx。</summary>
        internal bool CanClose(ref UICloseRequest request)
        {
            return CanCloseEx(ref request);
        }

        /// <summary>关闭授权扩展点；返回 false 会拒绝本次 Close/Cancel/BackgroundClick 关闭请求。</summary>
        protected virtual bool CanCloseEx(ref UICloseRequest request)
        {
            return true;
        }
    }
}
