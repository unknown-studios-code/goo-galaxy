using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
{
    [TestFixture]
    public class MatchConfigurationTests
    {
        [Test]
        public void DefaultConstruction_Always_YieldsSeedZero()
        {
            // GIVEN

            // WHEN
            var config = default(MatchConfiguration);

            // THEN
            Assert.That(config.Seed, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_WithSeed_RoundTripsTheSeed()
        {
            // GIVEN
            const int seed = 424242;

            // WHEN
            var config = new MatchConfiguration(seed);

            // THEN
            Assert.That(config.Seed, Is.EqualTo(seed));
        }
    }
}
