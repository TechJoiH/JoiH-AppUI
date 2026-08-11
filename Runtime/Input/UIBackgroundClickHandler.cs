using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 运行时透明背景点击处理器。
    /// Shield 会先消费点击防穿透，再按页面 CloseOnBackgroundClick 配置尝试关闭 Popup/Modal 页面。
    /// </summary>
    public sealed class UIBackgroundClickHandler : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private string pageId;

        [SerializeField]
        private UIPageDefinition pageDefinition;

        private IUIService uiService;

        /// <summary>
        /// 初始化背景点击处理器。
        /// service 只使用 IUIService，避免运行时 Shield 依赖具体 Manager 实现。
        /// </summary>
        public void Initialize(IUIService service, string targetPageId)
        {
            Initialize(service, targetPageId, null);
        }

        /// <summary>
        /// 初始化背景点击处理器并携带页面 Definition。
        /// Definition 用于判断 CloseOnBackgroundClick 与 Popup/Modal Layer 约束。
        /// </summary>
        public void Initialize(IUIService service, string targetPageId, UIPageDefinition definition)
        {
            uiService = service;
            pageId = targetPageId ?? string.Empty;
            pageDefinition = definition;
        }

        /// <summary>
        /// 处理背景点击。
        /// 点击会先被 Use 消费，即使 CanClose 或 CloseAsync 失败，也不会继续穿透到下层 UI 或世界输入。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null)
            {
                eventData.Use();
            }

            if (!CanCloseOnBackgroundClick())
            {
                return;
            }

            uiService.CloseAsync(pageId).Forget();
        }

        /// <summary>
        /// 判断当前页面是否允许背景点击关闭。
        /// 文档约束为仅 PopupLayer / ModalLayer 生效，其他层即便配置为 true 也只消费不关闭。
        /// </summary>
        private bool CanCloseOnBackgroundClick()
        {
            return uiService != null &&
                   !string.IsNullOrEmpty(pageId) &&
                   pageDefinition != null &&
                   pageDefinition.CloseOnBackgroundClick &&
                   IsBackgroundClickLayer(pageDefinition.LayerId);
        }

        /// <summary>
        /// 背景点击关闭仅允许 Popup 与 Modal。
        /// </summary>
        private static bool IsBackgroundClickLayer(UILayerId layerId)
        {
            return layerId == UILayerId.PopupLayer || layerId == UILayerId.ModalLayer;
        }
    }
}
