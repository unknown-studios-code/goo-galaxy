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

        [Test]
        public void Constructor_WithSeedOnly_LeavesPlayerIdsUnassigned()
        {
            // GIVEN — DeckPresenterTests and DeckShufflerTests build a re-shuffle from this exact overload, so
            // the seed-only form must keep leaving the player ids unset rather than inventing one.

            // WHEN
            var config = new MatchConfiguration(seed: 7);

            // THEN
            Assert.That((config.PlayerOneId, config.PlayerTwoId), Is.EqualTo((MatchConfiguration.UnassignedPlayerId, MatchConfiguration.UnassignedPlayerId)));
        }

        [Test]
        public void Constructor_WithSeedOnly_LeavesEveryDurationAtZero()
        {
            // GIVEN

            // WHEN
            var config = new MatchConfiguration(seed: 7);

            // THEN
            Assert.That((config.StandardDurationSeconds, config.CountdownSeconds, config.OvertimeDurationSeconds), Is.EqualTo((0f, 0f, 0f)));
        }
    }
}
