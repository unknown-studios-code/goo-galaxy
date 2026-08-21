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
        public void DefaultConstruction_Always_DeclaresNeitherSeatMachineDriven()
        {
            // GIVEN — the load-bearing case. A machine player reads its seat off the announced configuration, so
            // a renumbering of PlayerControl that put Machine on zero would silently hand it both seats of every
            // defaulted match.

            // WHEN
            var config = default(MatchConfiguration);

            // THEN
            Assert.That((config.PlayerOne.Control, config.PlayerTwo.Control), Is.EqualTo((PlayerControl.Unassigned, PlayerControl.Unassigned)));
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
        public void Constructor_WithSeedOnly_LeavesBothSeatIdsUnassigned()
        {
            // GIVEN — DeckPresenterTests and DeckShufflerTests build a re-shuffle from this exact overload, so
            // the seed-only form must keep leaving both seat ids unset rather than inventing one.

            // WHEN
            var config = new MatchConfiguration(seed: 7);

            // THEN
            Assert.That((config.PlayerOne.Id, config.PlayerTwo.Id), Is.EqualTo((PlayerSlot.UnassignedId, PlayerSlot.UnassignedId)));
        }

        [Test]
        public void Constructor_WithSeedOnly_DeclaresNeitherSeatMachineDriven()
        {
            // GIVEN

            // WHEN
            var config = new MatchConfiguration(seed: 7);

            // THEN
            Assert.That((config.PlayerOne.Control, config.PlayerTwo.Control), Is.EqualTo((PlayerControl.Unassigned, PlayerControl.Unassigned)));
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

        [Test]
        public void Constructor_WithAMachineSecondSeat_RoundTripsThatControl()
        {
            // GIVEN
            var playerOne = new PlayerSlot(1, PlayerControl.LocalHuman);
            var playerTwo = new PlayerSlot(2, PlayerControl.Machine);

            // WHEN
            var config = new MatchConfiguration(seed: 7, playerOne, playerTwo, 180f, 3f, 60f);

            // THEN
            Assert.That((config.PlayerOne.Control, config.PlayerTwo.Control), Is.EqualTo((PlayerControl.LocalHuman, PlayerControl.Machine)));
        }

        [Test]
        public void Constructor_WithAMachineFirstSeat_RoundTripsThatControl()
        {
            // GIVEN — nothing in the type names which side the machine takes, so both orders have to survive it.
            var playerOne = new PlayerSlot(1, PlayerControl.Machine);
            var playerTwo = new PlayerSlot(2, PlayerControl.LocalHuman);

            // WHEN
            var config = new MatchConfiguration(seed: 7, playerOne, playerTwo, 180f, 3f, 60f);

            // THEN
            Assert.That((config.PlayerOne.Control, config.PlayerTwo.Control), Is.EqualTo((PlayerControl.Machine, PlayerControl.LocalHuman)));
        }
    }
}
