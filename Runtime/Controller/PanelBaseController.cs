using System;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Page controller with self-close access through the injected UI service.
    /// </summary>
    public abstract class PanelBaseController : UIBaseController
    {
        protected UIPanelContext Context { get; private set; }

        internal void SetContext(UIPanelContext context)
        {
            Context = context;
        }

        protected void CloseSelf()
        {
            if (Context == null || Context.UI == null)
            {
                return;
            }

            IUIOperation<UICloseResult> operation = Context.UI.Close(
                Context.PageId,
                UICloseRequest.Default);
            IDisposable subscription = operation.Register(completion =>
            {
                if (completion.Status == AppUIOperationStatus.Failed)
                {
                    Debug.LogError(
                        completion.Exception ??
                        new InvalidOperationException(
                            "CloseSelf failed without an exception."));
                }
            });
            RegisterDisposeAction(subscription.Dispose);
        }

        internal bool CanClose(ref UICloseRequest request)
        {
            return CanCloseEx(ref request);
        }

        protected virtual bool CanCloseEx(ref UICloseRequest request)
        {
            return true;
        }
    }
}
