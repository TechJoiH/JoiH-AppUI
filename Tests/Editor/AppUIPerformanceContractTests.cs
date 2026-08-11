using System;
using NUnit.Framework;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIPerformanceContractTests
    {
        [Test]
        public void InputPolicyEvaluation_HotLoop_DoesNotAllocate()
        {
            AppUIInputChannelMask passMask =
                AppUIInputChannelMask.ViewportPan |
                AppUIInputChannelMask.ViewportZoom;

            for (int i = 0; i < 128; i++)
            {
                AppUIInputPolicyRules.Blocks(
                    AppUIInputZoneMode.PassChannelMask,
                    passMask,
                    AppUIInputChannel.ViewportPan,
                    false);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            bool blocked = false;
            for (int i = 0; i < 100000; i++)
            {
                blocked ^= AppUIInputPolicyRules.Blocks(
                    AppUIInputZoneMode.PassChannelMask,
                    passMask,
                    AppUIInputChannel.ViewportPan,
                    false);
            }

            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(blocked, Is.False);
            Assert.That(allocated, Is.Zero);
        }
    }
}
