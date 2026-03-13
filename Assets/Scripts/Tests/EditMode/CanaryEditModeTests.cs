using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode
{
    public sealed class CanaryEditModeTests : EditModeTestBase
    {
        [Test]
        public void CanaryTestPasses()
        {
            Assert.IsTrue(true);
        }

        [Test]
        public void PrimaryBoardCoordinateHelperBuildsExpectedHexCount()
        {
            Assert.That(PrimaryBoardCoordinates, Has.Count.EqualTo(61));
        }
    }
}
