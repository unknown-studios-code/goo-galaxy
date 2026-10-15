using GooGalaxy.Runtime.Core.Startup;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Core
{
    [TestFixture]
    public class FrameRateInitializerTests
    {
        [Test]
        public void ResolveTargetFrameRate_MobilePlatform_IsSixty()
        {
            // GIVEN

            // WHEN
            int frameRate = FrameRateInitializer.ResolveTargetFrameRate(isMobilePlatform: true);

            // THEN
            Assert.That(frameRate, Is.EqualTo(60), "Mobile target is not 60 — Android and iOS fall back to 30 FPS, missing the minimum-spec budget.");
        }

        [Test]
        public void ResolveTargetFrameRate_DesktopOrEditor_KeepsThePlatformDefault()
        {
            // GIVEN

            // WHEN
            int frameRate = FrameRateInitializer.ResolveTargetFrameRate(isMobilePlatform: false);

            // THEN
            Assert.That(frameRate, Is.EqualTo(-1), "Desktop and the editor must keep the default so vSync and PlayMode timing are unchanged.");
        }
    }
}
