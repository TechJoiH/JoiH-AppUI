using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 页面定义资产。
    /// 记录页面的层级、作用域、打开策略、输入阻断、生命周期行为和加载/销毁策略，是运行时打开页面的主要配置来源。
    /// </summary>
    [CreateAssetMenu(fileName = "UIPageDefinition", menuName = "Joi.H AppUI/Page Definition")]
    public sealed class UIPageDefinition : UIDefinitionAssetBase
    {
        /// <summary>页面唯一 ID，沿用基类 DefinitionId。</summary>
        public string PageId
        {
            get { return DefinitionId; }
        }

        /// <summary>页面所属 UI Layer。</summary>
        public UILayerId LayerId = UILayerId.SystemLayer;

        /// <summary>页面所属 CanvasDomain，必须与 LayerRoot 配置一致。</summary>
        public UICanvasDomain CanvasDomain = UICanvasDomain.System;

        /// <summary>页面生命周期作用域，决定场景退出和 Scope 释放边界。</summary>
        public UIPageScope Scope = UIPageScope.SceneScope;

        /// <summary>重复打开策略。</summary>
        public UIOpenPolicy OpenPolicy = UIOpenPolicy.RejectIfOpeningOrOpen;

        /// <summary>同 LayerRoot 内的显示优先级偏移；值越大越靠上。</summary>
        public int DefaultPriorityOffset;

        /// <summary>关键页面配置错误在 Editor/Development 下会中断初始化。</summary>
        public bool IsCritical;

        /// <summary>是否全屏页面；同层全屏页面会参与 StackVisible 隐藏/恢复。</summary>
        public bool IsFullScreen;

        /// <summary>是否阻断下层页面输入，并影响下层页面 PauseDepth。</summary>
        public bool BlockLowerLayerInput;

        /// <summary>打开时是否刷新语言；当前字段供后续本地化流程接入。</summary>
        public bool RefreshLanguageOnOpen;

        /// <summary>统一 Cancel 流程未被 Handler 消费时是否关闭页面。</summary>
        public bool CloseOnCancel;

        /// <summary>Popup/Modal 背景点击时是否尝试关闭当前页面。</summary>
        public bool CloseOnBackgroundClick;

        /// <summary>加载策略 ID；为空时使用默认加载策略。</summary>
        public string LoadStrategyId;

        /// <summary>销毁策略 ID；为空时使用默认销毁策略。</summary>
        public string DestroyStrategyId;

        /// <summary>是否高频 UI，高频页面限制在 HudLayer + Hud CanvasDomain。</summary>
        public bool IsHighFrequency;

        /// <summary>是否要求所在 Canvas 有启用的 GraphicRaycaster。</summary>
        public bool RequiresRaycaster;

        /// <summary>是否启用 OnUpdateEx Tick。</summary>
        public bool EnableUpdate;

        /// <summary>是否启用 OnLateUpdateEx Tick。</summary>
        public bool EnableLateUpdate;

        /// <summary>暂停状态下是否仍允许 Tick。</summary>
        public bool UpdateWhenPaused;
    }
}
