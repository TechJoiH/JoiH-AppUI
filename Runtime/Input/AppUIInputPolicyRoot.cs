using UnityEngine;

namespace Joi.H.AppUI
{
    [DisallowMultipleComponent]
    public sealed class AppUIInputPolicyRoot : MonoBehaviour
    {
        [SerializeField]
        private AppUIInputZoneMode defaultMode = AppUIInputZoneMode.BlockAll;

        [SerializeField]
        private AppUIInputChannelMask defaultPassChannels =
            AppUIInputChannelMask.None;

        public AppUIInputZoneMode DefaultMode
        {
            get { return defaultMode; }
        }

        public AppUIInputChannelMask DefaultPassChannels
        {
            get { return defaultPassChannels; }
        }

        public void SetDefaultPolicy(
            AppUIInputZoneMode mode,
            AppUIInputChannelMask passChannels = AppUIInputChannelMask.None)
        {
            defaultMode = mode;
            defaultPassChannels = passChannels;
        }

        public bool Blocks(AppUIInputChannel channel, bool isInteractiveSelectable)
        {
            return AppUIInputPolicyRules.Blocks(
                defaultMode,
                defaultPassChannels,
                channel,
                isInteractiveSelectable);
        }
    }
}
