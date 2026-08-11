namespace Joi.H.AppUI
{
    internal static class AppUIInputPolicyRules
    {
        public static bool Blocks(
            AppUIInputZoneMode mode,
            AppUIInputChannelMask passChannels,
            AppUIInputChannel channel,
            bool isInteractiveSelectable)
        {
            if (isInteractiveSelectable)
            {
                return true;
            }

            switch (mode)
            {
                case AppUIInputZoneMode.PassAll:
                case AppUIInputZoneMode.BlockInteractiveOnly:
                    return false;
                case AppUIInputZoneMode.PassChannelMask:
                    return !AppUIInputChannelMaskUtility.Contains(
                        passChannels,
                        channel);
                case AppUIInputZoneMode.Inherit:
                case AppUIInputZoneMode.BlockAll:
                default:
                    return true;
            }
        }
    }
}
