using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面运行时实例。
    /// 该对象只由框架内部维护，记录 Definition、Controller、资源句柄、OperationVersion、栈可见性、暂停和输入阻断状态。
    /// </summary>
    public sealed class UIPageInstance
    {
        /// <summary>页面 ID。</summary>
        public string PageId;

        /// <summary>页面定义。</summary>
        public UIPageDefinition Definition;

        /// <summary>页面所属 Layer。</summary>
        public UILayerId LayerId;

        /// <summary>页面所属 SceneScopeId；GlobalScope 页面固定为空。</summary>
        public string SceneScopeId;

        /// <summary>当前修改该实例的 OperationVersion，用于 await 后防止过期操作提交状态。</summary>
        public int OperationVersion;

        /// <summary>
        /// 当前运行时页面实例的稳定 ID。
        /// 由 UIPageInstanceRegistry 在首次注册时分配；Hidden/Reopen 保留，Release 后重新实例化会获得新值。
        /// </summary>
        public long RuntimeInstanceId { get; internal set; }

        /// <summary>实例化出的页面 GameObject。</summary>
        public GameObject GameObject;

        /// <summary>页面 RectTransform 缓存。</summary>
        public RectTransform RectTransform;

        /// <summary>页面 Controller。</summary>
        public PanelBaseController Controller;

        /// <summary>Optional asset lease disposed when the page is released.</summary>
        public UIAssetLease AssetLease;

        /// <summary>页面生命周期状态。</summary>
        public UIPageState State;

        /// <summary>栈遮挡可见性；同层全屏页遮挡时保持 State=Open 但 StackVisible=false。</summary>
        public bool StackVisible = true;

        /// <summary>同 LayerRoot 内排序序号，配合 DefaultPriorityOffset 计算 sibling 顺序。</summary>
        public int LayerSortSequence;

        /// <summary>Hidden/Initializing 时暂存的刷新数据。</summary>
        public object PendingRefreshData;

        /// <summary>是否存在暂存刷新数据。</summary>
        public bool HasPendingRefreshData;

        /// <summary>暂停深度；大于 0 表示被更高阻断页面暂停。</summary>
        public int PauseDepth;

        /// <summary>输入阻断深度；大于 0 表示下层页面输入被禁用。</summary>
        public int InputBlockDepth;

        /// <summary>当前是否处于暂停状态。</summary>
        public bool IsPaused
        {
            get { return PauseDepth > 0; }
        }

        /// <summary>当前是否被输入阻断。</summary>
        public bool IsInputBlocked
        {
            get { return InputBlockDepth > 0; }
        }

        /// <summary>页面是否处于 Open 且未被栈遮挡。</summary>
        public bool IsOpenAndStackVisible
        {
            get { return State == UIPageState.Open && StackVisible; }
        }

        /// <summary>创建对外只读句柄快照。</summary>
        public UIPageHandle ToHandle()
        {
            return new UIPageHandle(PageId, State, LayerId);
        }

        /// <summary>创建供交互快照和后续焦点请求使用的值类型句柄。</summary>
        internal UIPageInteractionHandle ToInteractionHandle()
        {
            return new UIPageInteractionHandle(PageId, RuntimeInstanceId, OperationVersion);
        }
    }
}
