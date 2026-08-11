using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>
    /// 迁移期将 Prefab 上的 Unity 原生导航归一为语义导航配置。
    /// Prefab 完成 Navigation.None 序列化后，页面无需再调用该入口。
    /// </summary>
    public static class AppUIFocusNodeConfiguration
    {
        public static void DisableUnityNavigation(Selectable selectable)
        {
            if (selectable == null)
            {
                return;
            }

            Navigation navigation = selectable.navigation;
            if (navigation.mode == Navigation.Mode.None)
            {
                return;
            }

            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }
    }
}
