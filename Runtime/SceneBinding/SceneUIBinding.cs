using System;
using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Scene-owned bridge that forwards authored binding data to IUIService.
    /// </summary>
    public class SceneUIBinding : MonoBehaviour
    {
        [SerializeField]
        private SceneUIBindingData bindingData = new SceneUIBindingData();

        public SceneUIBindingData BuildBindingData()
        {
            return bindingData;
        }

        public IUIOperation<UISceneBindResult> Bind(IUIService ui)
        {
            if (ui == null)
            {
                throw new ArgumentNullException(nameof(ui));
            }

            if (bindingData == null)
            {
                throw new InvalidOperationException(
                    "Scene UI binding data is missing.");
            }

            return ui.BindScene(bindingData);
        }

        public IUIOperation<UISceneExitResult> Unbind(IUIService ui)
        {
            if (ui == null)
            {
                throw new ArgumentNullException(nameof(ui));
            }

            if (bindingData == null)
            {
                throw new InvalidOperationException(
                    "Scene UI binding data is missing.");
            }

            return ui.UnbindScene(bindingData);
        }
    }
}
