using System.Collections.Generic;
using GooGalaxy.Runtime.AI.Models;
using GooGalaxy.Runtime.AI.Services;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.AI
{
    [TestFixture]
    public class RandomMoveStrategyTests
    {
        private const int OptionCount = 4;
        private const int Seed = 12345;

        // Pinned rather than re-derived: the guarantee is that this seed yields this stream seed on every
        // runtime, and computing the expectation from the production code would only prove self-agreement.
        private const int SeedDerivedForSelectionStream = -1334834426;

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _target = new(1, 0);

        private List<MoveOption> _options;

        [SetUp]
        public void SetUp()
        {
            _options = new List<MoveOption>(OptionCount);

            for (int unitId = 0; unitId < OptionCount; unitId++)
            {
                _options.Add(MoveOption.ForClone(unitId, _origin, _target));
            }
        }

        [Test]
        public void DeriveSeed_ForTheSelectionStream_MatchesThePinnedSeed()
        {
            // GIVEN

            // WHEN
            int derived = RandomMoveStrategy.DeriveSeed(Seed);

            // THEN
            Assert.That(derived, Is.EqualTo(SeedDerivedForSelectionStream));
        }

        [Test]
        public void TrySelect_ANonEmptyList_ReturnsTrue()
        {
            // GIVEN
            var strategy = new RandomMoveStrategy(Seed);

            // WHEN
            bool wasSelected = strategy.TrySelect(_options, out _);

            // THEN
            Assert.That(wasSelected, Is.True);
        }

        [Test]
        public void TrySelect_TwoStrategiesOnTheSameSeedAndList_SelectTheSameOption()
        {
            // GIVEN
            var strategy = new RandomMoveStrategy(Seed);
            var replay = new RandomMoveStrategy(Seed);
            strategy.TrySelect(_options, out MoveOption selected);

            // WHEN
            replay.TrySelect(_options, out MoveOption replayed);

            // THEN
            Assert.That(replayed.UnitId, Is.EqualTo(selected.UnitId));
        }

        // The unit id of each option is its index, so the expected result names the index that seed lands on.
        [TestCase(4, ExpectedResult = 0)]
        [TestCase(1, ExpectedResult = 1)]
        [TestCase(2, ExpectedResult = 2)]
        [TestCase(3, ExpectedResult = 3)]
        public int TrySelect_DifferentSeeds_ReachEveryIndexOfTheList(int seed)
        {
            // GIVEN
            var strategy = new RandomMoveStrategy(seed);

            // WHEN
            strategy.TrySelect(_options, out MoveOption selected);

            // THEN
            return selected.UnitId;
        }

        [Test]
        public void TrySelect_ASingleOption_SelectsThatOption()
        {
            // GIVEN
            var strategy = new RandomMoveStrategy(Seed);
            var single = new List<MoveOption> { MoveOption.ForJump(7, _origin, _target) };

            // WHEN
            strategy.TrySelect(single, out MoveOption selected);

            // THEN
            Assert.That(selected.UnitId, Is.EqualTo(7));
        }

        [Test]
        public void TrySelect_AnEmptyList_ReturnsFalseRatherThanThrowing()
        {
            // GIVEN — the generator refuses an empty range, so an empty option set has to be answered before it
            // is ever drawn against.
            var strategy = new RandomMoveStrategy(Seed);

            // WHEN
            bool wasSelected = strategy.TrySelect(new List<MoveOption>(), out _);

            // THEN
            Assert.That(wasSelected, Is.False);
        }

        [Test]
        public void TrySelect_ANullList_ReturnsFalse()
        {
            // GIVEN
            var strategy = new RandomMoveStrategy(Seed);

            // WHEN
            bool wasSelected = strategy.TrySelect(null, out _);

            // THEN
            Assert.That(wasSelected, Is.False);
        }

        [Test]
        public void SelectionStreamId_Always_SitsBelowEveryPlayerId()
        {
            // THEN — real player ids start at one and the unassigned id is zero, so a negative stream id can
            // never collide with the per-player stream the deck shuffle draws on.
            Assert.That(RandomMoveStrategy.SelectionStreamId, Is.LessThan(PlayerSlot.UnassignedId));
        }

        [Test]
        public void SelectionStreamId_Always_DiffersFromTheTargetStreamId()
        {
            // THEN — a shared id would tie the choice of action to the choice of Protocol cluster, so a repeated
            // opening hand would be answered the same way every match.
            Assert.That(RandomMoveStrategy.SelectionStreamId, Is.Not.EqualTo(MoveOptionResolver.TargetStreamId));
        }
    }
}
