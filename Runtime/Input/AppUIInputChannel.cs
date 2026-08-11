using System;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Provider-neutral pointer input categories used when querying UI passthrough.
    /// Applications map their concrete input actions to these channels.
    /// </summary>
    public enum AppUIInputChannel
    {
        None = 0,
        PrimaryPointer = 1,
        SecondaryPointer = 2,
        PointerMotion = 3,
        ViewportPan = 4,
        ViewportZoom = 5,
        ContextAction = 6,
        Custom1 = 7,
        Custom2 = 8,
    }

    [Flags]
    public enum AppUIInputChannelMask
    {
        None = 0,
        PrimaryPointer = 1 << 0,
        SecondaryPointer = 1 << 1,
        PointerMotion = 1 << 2,
        ViewportPan = 1 << 3,
        ViewportZoom = 1 << 4,
        ContextAction = 1 << 5,
        Custom1 = 1 << 6,
        Custom2 = 1 << 7,
        All = PrimaryPointer |
            SecondaryPointer |
            PointerMotion |
            ViewportPan |
            ViewportZoom |
            ContextAction |
            Custom1 |
            Custom2,
    }

    public static class AppUIInputChannelMaskUtility
    {
        public static AppUIInputChannelMask ToMask(AppUIInputChannel channel)
        {
            switch (channel)
            {
                case AppUIInputChannel.PrimaryPointer:
                    return AppUIInputChannelMask.PrimaryPointer;
                case AppUIInputChannel.SecondaryPointer:
                    return AppUIInputChannelMask.SecondaryPointer;
                case AppUIInputChannel.PointerMotion:
                    return AppUIInputChannelMask.PointerMotion;
                case AppUIInputChannel.ViewportPan:
                    return AppUIInputChannelMask.ViewportPan;
                case AppUIInputChannel.ViewportZoom:
                    return AppUIInputChannelMask.ViewportZoom;
                case AppUIInputChannel.ContextAction:
                    return AppUIInputChannelMask.ContextAction;
                case AppUIInputChannel.Custom1:
                    return AppUIInputChannelMask.Custom1;
                case AppUIInputChannel.Custom2:
                    return AppUIInputChannelMask.Custom2;
                default:
                    return AppUIInputChannelMask.None;
            }
        }

        public static bool Contains(
            AppUIInputChannelMask mask,
            AppUIInputChannel channel)
        {
            AppUIInputChannelMask channelMask = ToMask(channel);
            return channelMask != AppUIInputChannelMask.None &&
                   (mask & channelMask) != 0;
        }
    }
}
