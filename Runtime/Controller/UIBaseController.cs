using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// App UI 控制器基类。
    /// 框架通过 internal 生命周期方法驱动页面，业务侧只重写 OnXXXEx 钩子并使用 RegisterDisposeAction 统一解绑事件。
    /// </summary>
    public abstract class UIBaseController : UIBindingScopeBase
    {
        private List<Action> eventDisposables;
        private bool initialized;
        private bool disposed;

        /// <summary>全局文本本地化委托；SetText 会优先使用该委托转换 localizationKey。</summary>
        public static Func<string, string> LocalizeText { get; set; }

        internal bool IsInitialized
        {
            get { return initialized; }
        }

        internal bool IsDisposed
        {
            get { return disposed; }
        }

        internal void OnCreate(UIControllerContext context)
        {
            disposed = false;
            OnCreateEx(context);
        }

        internal void OnInit()
        {
            if (initialized)
            {
                return;
            }

            OnBindGeneratedFields();
            initialized = true;
            OnInitEx();
        }

        internal void OnDataLoad(object data)
        {
            OnDataLoadEx(data);
        }

        internal void OnRefresh()
        {
            OnRefreshEx();
        }

        internal void OnPause()
        {
            OnPauseEx();
        }

        internal void OnResume()
        {
            OnResumeEx();
        }

        internal void OnTick(float deltaTime, float unscaledDeltaTime)
        {
            OnUpdateEx(deltaTime, unscaledDeltaTime);
        }

        internal void OnLateTick(float deltaTime, float unscaledDeltaTime)
        {
            OnLateUpdateEx(deltaTime, unscaledDeltaTime);
        }

        internal async UniTask ShowAsync()
        {
            // Show 生命周期先激活 GameObject，再执行业务前置钩子、动画和完成钩子，确保动画期间对象可见。
            if (this != null && gameObject != null && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            OnBeforeShowEx();
            await PlayShowAnimationAsync();
            OnShowEx();
        }

        internal async UniTask HideAsync()
        {
            // Hide 生命周期先让业务和动画完成，再关闭 GameObject，避免动画对象被提前隐藏。
            OnBeforeHideEx();
            await PlayHideAnimationAsync();
            OnHideEx();
            if (this != null && gameObject != null && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        internal void OnDispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                OnDisposeEx();
            }
            finally
            {
                // 无论业务 Dispose 是否异常，已注册的事件解绑都必须尽量执行，避免 UI 残留监听。
                DisposeRegisteredActions();
            }
        }

        /// <summary>创建上下文后的业务扩展点，只会在实例创建阶段调用一次。</summary>
        protected virtual void OnCreateEx(UIControllerContext context)
        {
        }

        /// <summary>绑定生成字段后的扩展点，适合校验或整理自动绑定引用。</summary>
        protected virtual void OnBindGeneratedFields()
        {
        }

        /// <summary>初始化扩展点，同一实例只调用一次。</summary>
        protected virtual void OnInitEx()
        {
        }

        /// <summary>数据载入扩展点，Open/Refresh 会把业务数据传到这里。</summary>
        protected virtual void OnDataLoadEx(object data)
        {
        }

        /// <summary>刷新扩展点，通常在 OnDataLoadEx 之后更新界面显示。</summary>
        protected virtual void OnRefreshEx()
        {
        }

        /// <summary>显示动画前扩展点。</summary>
        protected virtual void OnBeforeShowEx()
        {
        }

        /// <summary>显示完成扩展点。</summary>
        protected virtual void OnShowEx()
        {
        }

        /// <summary>隐藏动画前扩展点。</summary>
        protected virtual void OnBeforeHideEx()
        {
        }

        /// <summary>隐藏完成扩展点。</summary>
        protected virtual void OnHideEx()
        {
        }

        /// <summary>页面被更高阻断层暂停时调用；PauseDepth 从 0 变为非 0 时触发。</summary>
        protected virtual void OnPauseEx()
        {
        }

        /// <summary>页面从暂停恢复时调用；PauseDepth 回到 0 时触发。</summary>
        protected virtual void OnResumeEx()
        {
        }

        /// <summary>释放扩展点，只在统一释放协议中调用一次。</summary>
        protected virtual void OnDisposeEx()
        {
        }

        /// <summary>Update 扩展点，由 UIPageDefinition.EnableUpdate 控制是否调用。</summary>
        protected virtual void OnUpdateEx(float deltaTime, float unscaledDeltaTime)
        {
        }

        /// <summary>LateUpdate 扩展点，由 UIPageDefinition.EnableLateUpdate 控制是否调用。</summary>
        protected virtual void OnLateUpdateEx(float deltaTime, float unscaledDeltaTime)
        {
        }

        /// <summary>显示动画异步扩展点；默认无动画并立即完成。</summary>
        protected virtual UniTask PlayShowAnimationAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>隐藏动画异步扩展点；默认无动画并立即完成。</summary>
        protected virtual UniTask PlayHideAnimationAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>注册释放时要执行的清理动作，常用于事件解绑和外部订阅撤销。</summary>
        protected void RegisterDisposeAction(Action disposeAction)
        {
            if (disposeAction == null)
            {
                return;
            }

            if (eventDisposables == null)
            {
                eventDisposables = new List<Action>(4);
            }

            eventDisposables.Add(disposeAction);
        }

        /// <summary>执行并清空已注册的释放动作；按后进先出顺序撤销。</summary>
        protected void DisposeRegisteredActions()
        {
            if (eventDisposables == null)
            {
                return;
            }

            for (int i = eventDisposables.Count - 1; i >= 0; i--)
            {
                try
                {
                    Action disposeAction = eventDisposables[i];
                    if (disposeAction != null)
                    {
                        disposeAction.Invoke();
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                }
            }

            eventDisposables.Clear();
            eventDisposables = null;
        }

        /// <summary>注册 Button 点击事件，并自动在 Dispose 时解绑。</summary>
        protected void RegisterClick(Button button, UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.AddListener(action);
            RegisterDisposeAction(delegate
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(action);
                }
            });
        }

        /// <summary>注册 Toggle 变化事件，并自动在 Dispose 时解绑。</summary>
        protected void RegisterToggle(Toggle toggle, UnityAction<bool> action)
        {
            if (toggle == null || action == null)
            {
                return;
            }

            toggle.onValueChanged.AddListener(action);
            RegisterDisposeAction(delegate
            {
                if (toggle != null)
                {
                    toggle.onValueChanged.RemoveListener(action);
                }
            });
        }

        /// <summary>设置本地化文本；如果 LocalizeText 未设置，则直接显示 localizationKey。</summary>
        protected void SetText(TMP_Text target, string localizationKey)
        {
            if (target == null)
            {
                return;
            }

            Func<string, string> localizer = LocalizeText;
            target.text = localizer != null
                ? localizer.Invoke(localizationKey ?? string.Empty)
                : localizationKey ?? string.Empty;
        }

        /// <summary>设置普通字符串文本，不经过本地化委托。</summary>
        protected void SetTextStr(TMP_Text target, string value)
        {
            if (target == null)
            {
                return;
            }

            target.text = value ?? string.Empty;
        }

        /// <summary>安全切换 GameObject active，目标为空或状态一致时不做额外操作。</summary>
        protected void TryActiveObject(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        /// <summary>安全切换 Component 所在 GameObject active。</summary>
        protected void TryActiveObject(Component target, bool active)
        {
            if (target != null)
            {
                TryActiveObject(target.gameObject, active);
            }
        }
    }
}
