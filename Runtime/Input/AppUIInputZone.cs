using UnityEngine;

namespace Joi.H.AppUI
{
    [DisallowMultipleComponent]
    public sealed class AppUIInputZone : MonoBehaviour
    {
        [SerializeField]
        private AppUIInputZoneMode mode = AppUIInputZoneMode.Inherit;

        [SerializeField]
        private AppUIInputChannelMask passChannels = AppUIInputChannelMask.None;

        public AppUIInputZoneMode Mode
        {
            get { return mode; }
        }

        public AppUIInputChannelMask PassChannels
        {
            get { return passChannels; }
        }

        public void SetPolicy(
            AppUIInputZoneMode zoneMode,
            AppUIInputChannelMask channels)
        {
            mode = zoneMode;
            passChannels = channels;
        }

        public bool Blocks(AppUIInputChannel channel, bool isInteractiveSelectable)
        {
            return AppUIInputPolicyRules.Blocks(
                mode,
                passChannels,
                channel,
                isInteractiveSelectable);
        }
    }
}
