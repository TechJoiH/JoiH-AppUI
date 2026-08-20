using System.Reflection;
using NUnit.Framework;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIControllerBoundaryTests
    {
        [Test]
        public void Controller_DoesNotOwnTextTechnologyOrLocalization()
        {
            const BindingFlags StaticPublicDeclared =
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.DeclaredOnly;
            const BindingFlags InstanceNonPublicDeclared =
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            Assert.That(
                typeof(UIBaseController).GetProperty(
                    "LocalizeText",
                    StaticPublicDeclared),
                Is.Null,
                "Controller base must not own a global localization function.");
            Assert.That(
                typeof(UIBaseController).GetMethod(
                    "SetText",
                    InstanceNonPublicDeclared),
                Is.Null,
                "Controller base must not expose a concrete text helper.");
            Assert.That(
                typeof(UIBaseController).GetMethod(
                    "SetTextStr",
                    InstanceNonPublicDeclared),
                Is.Null,
                "Controller base must not expose a concrete string helper.");
        }
    }
}
