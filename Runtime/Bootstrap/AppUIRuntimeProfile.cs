using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Reusable configuration consumed by AppUIRuntimeHost.
    /// Runtime ownership, scene persistence, and EventSystem creation remain the
    /// responsibility of the integrating application.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AppUIRuntimeProfile",
        menuName = "Joi.H AppUI/Runtime Profile")]
    public sealed class AppUIRuntimeProfile : ScriptableObject
    {
        [SerializeField]
        private UIPageDefinitionRegistry pageRegistry;

        [SerializeField]
        private UILayerSettings layerSettings;

        [SerializeField]
        private AppUINoticeSettings noticeSettings =
            AppUINoticeSettings.CreateDefault();

        public UIPageDefinitionRegistry PageRegistry
        {
            get { return pageRegistry; }
        }

        public UILayerSettings LayerSettings
        {
            get { return layerSettings; }
        }

        public AppUINoticeSettings NoticeSettings
        {
            get
            {
                return noticeSettings ?? AppUINoticeSettings.CreateDefault();
            }
        }

        public bool ValidateForRuntime(out string error)
        {
            if (pageRegistry == null)
            {
                error =
                    "AppUIRuntimeProfile is missing UIPageDefinitionRegistry.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
