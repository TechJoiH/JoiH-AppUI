using System;
using System.Collections.Generic;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 场景 UI 绑定数据。
    /// 描述场景进入时要打开的页面，以及场景退出时的显式关闭/释放规则。
    /// </summary>
    [Serializable]
    public sealed class SceneUIBindingData
    {
        /// <summary>场景 ID；SceneScopeId 为空时会作为 fallback。</summary>
        public string SceneId;

        /// <summary>场景作用域 ID；用于标记本场景打开的 SceneScope/TemporaryScope 页面。</summary>
        public string SceneScopeId;

        /// <summary>场景 ready 时按 Order 打开的页面规则。</summary>
        public List<SceneUIOpenRule> OpenOnSceneReady = new List<SceneUIOpenRule>();

        /// <summary>场景退出时的显式页面处理规则。</summary>
        public List<SceneUICloseRule> CloseOnSceneExit = new List<SceneUICloseRule>();
    }

    /// <summary>
    /// 场景进入打开页面规则。
    /// </summary>
    [Serializable]
    public sealed class SceneUIOpenRule
    {
        /// <summary>要打开的页面 ID。</summary>
        public string PageId;

        /// <summary>打开顺序，值越小越先执行。</summary>
        public int Order;

        /// <summary>打开参数；BindScene 会额外注入解析后的 SceneScopeId。</summary>
        public UIOpenArgs OpenArgs;
    }

    /// <summary>
    /// 场景退出页面处理规则。
    /// </summary>
    [Serializable]
    public sealed class SceneUICloseRule
    {
        /// <summary>目标页面 ID。</summary>
        public string PageId;

        /// <summary>退出时执行的动作。</summary>
        public UISceneExitAction ExitAction = UISceneExitAction.Release;
    }

    /// <summary>
    /// 场景退出时对页面的显式处理动作。
    /// None 表示保留并排除默认清理；Close 表示隐藏；Release 表示释放。
    /// </summary>
    public enum UISceneExitAction
    {
        None,
        Close,
        Release,
    }
}
