using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 场景 UI 绑定组件。
    /// 场景 owner 可通过它构建 SceneUIBindingData，并在进入/退出时委托 IUIService 处理 UI 生命周期。
    /// </summary>
    public class SceneUIBinding : MonoBehaviour
    {
        [SerializeField]
        private SceneUIBindingData bindingData = new SceneUIBindingData();

        /// <summary>返回当前序列化的场景 UI 绑定数据。</summary>
        public SceneUIBindingData BuildBindingData()
        {
            return bindingData;
        }

        /// <summary>执行场景进入 UI 绑定。</summary>
        public async UniTask BindAsync(IUIService ui)
        {
            if (ui == null || bindingData == null)
            {
                return;
            }

            await ui.BindSceneAsync(bindingData);
        }

        /// <summary>执行场景退出 UI 解绑。</summary>
        public UniTask<UISceneExitResult> UnbindAsync(IUIService ui)
        {
            if (ui == null || bindingData == null)
            {
                return UniTask.FromResult(UISceneExitResult.FromResults(string.Empty, string.Empty, null));
            }

            return ui.UnbindSceneAsync(bindingData);
        }
    }
}
