namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面句柄。
    /// 对外只暴露页面 ID、状态和 Layer，避免业务持有可变 UIPageInstance。
    /// </summary>
    public readonly struct UIPageHandle
    {
        /// <summary>页面 ID。</summary>
        public readonly string PageId;

        /// <summary>页面当前状态快照。</summary>
        public readonly UIPageState State;

        /// <summary>页面所属 Layer。</summary>
        public readonly UILayerId LayerId;

        public UIPageHandle(string pageId, UIPageState state, UILayerId layerId)
        {
            PageId = pageId ?? string.Empty;
            State = state;
            LayerId = layerId;
        }
    }
}
