using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Tests.Utils;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Match
{
    [TestFixture]
    public class MatchOutcomeResolverTests
    {
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;
        private const int AllocationIterations = 1000;

        [TestCase(0, 3, PlayerTwoId)]
        [TestCase(3, 0, PlayerOneId)]
        public void TryResolveDomination_OneSideWipedOtherHoldsUnits_ResolvesDominationToTheSurvivor(
            int playerOneUnits,
            int playerTwoUnits,
            int expectedWinnerId
        )
        {
            // GIVEN

            // WHEN
            bool isDomination = MatchOutcomeResolver.TryResolveDomination(playerOneUnits, playerTwoUnits, PlayerOneId, PlayerTwoId, out int winnerId);

            // THEN
            Assert.That((isDomination, winnerId), Is.EqualTo((true, expectedWinnerId)));
        }

        [Test]
        public void TryResolveDomination_BothSidesAtZero_ResolvesAsNoDominationAndLeavesWinnerIdAtNoWinner()
        {
            // GIVEN — a player holding no units eliminated nothing, so this is a draw for the clock, not a
            // domination.

            // WHEN
            bool isDomination = MatchOutcomeResolver.TryResolveDomination(0, 0, PlayerOneId, PlayerTwoId, out int winnerId);

            // THEN
            Assert.That((isDomination, winnerId), Is.EqualTo((false, MatchOutcome.NoWinner)));
        }

        [Test]
        public void TryResolveDomination_BothSidesHoldingUnits_ResolvesAsNoDominationAndLeavesWinnerIdAtNoWinner()
        {
            // GIVEN

            // WHEN
            bool isDomination = MatchOutcomeResolver.TryResolveDomination(2, 3, PlayerOneId, PlayerTwoId, out int winnerId);

            // THEN
            Assert.That((isDomination, winnerId), Is.EqualTo((false, MatchOutcome.NoWinner)));
        }

        [Test]
        [Category("Allocation")]
        public void TryResolveDomination_RepeatedCalls_AllocatesNoManagedMemory()
        {
            // GIVEN — warmed once outside the measured delegate, so the constraint sees only the repeated
            // resolutions it exists to prove are free.
            MatchOutcomeResolver.TryResolveDomination(3, 0, PlayerOneId, PlayerTwoId, out _);
            MatchOutcomeResolver.TryResolveDomination(3, 0, PlayerOneId, PlayerTwoId, out _);

            // WHEN / THEN
            Assert.That(
                () =>
                {
                    for (int i = 0; i < AllocationIterations; i++)
                    {
                        MatchOutcomeResolver.TryResolveDomination(3, 1, PlayerOneId, PlayerTwoId, out _);
                    }
                },
                new AllocatesNothingConstraint()
            );
        }

        [TestCase(5, 3, PlayerOneId)]
        [TestCase(3, 5, PlayerTwoId)]
        public void ResolveByUnitCount_UnequalCounts_ReturnsTimeLimitOutcomeToTheHigherCount(int playerOneUnits, int playerTwoUnits, int expectedWinnerId)
        {
            // GIVEN

            // WHEN
            MatchOutcome outcome = MatchOutcomeResolver.ResolveByUnitCount(playerOneUnits, playerTwoUnits, PlayerOneId, PlayerTwoId);

            // THEN
            Assert.That(outcome, Is.EqualTo(new MatchOutcome(expectedWinnerId, MatchEndReason.TimeLimit)));
        }

        [Test]
        public void ResolveByUnitCount_EqualCounts_ReturnsTheDrawnOutcome()
        {
            // GIVEN

            // WHEN
            MatchOutcome outcome = MatchOutcomeResolver.ResolveByUnitCount(4, 4, PlayerOneId, PlayerTwoId);

            // THEN
            Assert.That(outcome, Is.EqualTo(MatchOutcome.Drawn));
        }
    }
}
