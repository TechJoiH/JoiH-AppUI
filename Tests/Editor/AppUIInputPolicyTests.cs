using NUnit.Framework;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIInputPolicyTests
    {
        [Test]
        public void PassAll_NonInteractiveHit_DoesNotBlock()
        {
            bool blocked = AppUIInputPolicyRules.Blocks(
                AppUIInputZoneMode.PassAll,
                AppUIInputChannelMask.None,
                AppUIInputChannel.PrimaryPointer,
                false);

            Assert.That(blocked, Is.False);
        }

        [Test]
        public void InteractiveSelectable_AlwaysBlocks()
        {
            bool blocked = AppUIInputPolicyRules.Blocks(
                AppUIInputZoneMode.PassAll,
                AppUIInputChannelMask.All,
                AppUIInputChannel.ViewportPan,
                true);

            Assert.That(blocked, Is.True);
        }

        [TestCase(AppUIInputChannel.ViewportPan, false)]
        [TestCase(AppUIInputChannel.ViewportZoom, false)]
        [TestCase(AppUIInputChannel.PrimaryPointer, true)]
        public void PassChannelMask_UsesGenericChannelMapping(
            AppUIInputChannel channel,
            bool expectedBlocked)
        {
            AppUIInputChannelMask passMask =
                AppUIInputChannelMask.ViewportPan |
                AppUIInputChannelMask.ViewportZoom;

            bool blocked = AppUIInputPolicyRules.Blocks(
                AppUIInputZoneMode.PassChannelMask,
                passMask,
                channel,
                false);

            Assert.That(blocked, Is.EqualTo(expectedBlocked));
        }
    }
}
