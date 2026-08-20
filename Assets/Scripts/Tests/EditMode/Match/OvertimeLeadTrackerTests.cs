using System;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Match
{
    [TestFixture]
    public class OvertimeLeadTrackerTests
    {
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;
        private const float HoldSeconds = 1f;

        private OvertimeLeadTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new OvertimeLeadTracker();
        }

        [Test]
        public void Tick_LeadAppearsThisTick_ReturnsNoWinnerRegardlessOfDeltaTime()
        {
            // GIVEN

            // WHEN — a large delta on the very tick a lead is taken must still accumulate nothing.
            int result = _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 5f);

            // THEN
            Assert.That(result, Is.EqualTo(MatchOutcome.NoWinner));
        }

        [Test]
        public void Tick_UnbrokenLeadReachesTheHoldThreshold_ReturnsTheLeaderOnThatTickAndNotBefore()
        {
            // GIVEN
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.4f);
            int partialResult = _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.4f);

            // WHEN
            int completingResult = _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.6f);

            // THEN
            Assert.That((partialResult, completingResult), Is.EqualTo((MatchOutcome.NoWinner, PlayerOneId)));
        }

        [Test]
        public void Tick_LeaderChangesMidHold_RestartsTheAccumulatorFromZero()
        {
            // GIVEN
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.5f);
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.9f);
            _tracker.Tick(1, 2, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.9f);

            // WHEN — if the old leader's 0.9s residue had survived the switch, this smaller delta would already
            // complete the hold; a fresh accumulator leaves it short.
            int result = _tracker.Tick(1, 2, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.15f);

            // THEN
            Assert.That(result, Is.EqualTo(MatchOutcome.NoWinner));
        }

        [Test]
        public void Tick_CountsBecomeLevel_RestartsTheAccumulator()
        {
            // GIVEN
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.5f);
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.9f);
            _tracker.Tick(1, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 100f);

            // WHEN — the same leader retakes the lead; the first tick after the dip only reports it appeared,
            // and the full hold has to be earned again from there.
            int reappearedResult = _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 100f);
            int completingResult = _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 1f);

            // THEN
            Assert.That((reappearedResult, completingResult), Is.EqualTo((MatchOutcome.NoWinner, PlayerOneId)));
        }

        [TestCase(0)]
        [TestCase(5)]
        public void Tick_LevelUnitCounts_NeverNamesALeader(int units)
        {
            // GIVEN

            // WHEN — a large delta must not be enough either, since level counts are checked before it is used.
            int result = _tracker.Tick(units, units, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 100f);

            // THEN
            Assert.That(result, Is.EqualTo(MatchOutcome.NoWinner));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Tick_ZeroOrNegativeDeltaTime_DoesNotAdvanceTheHold(float nonPositiveDeltaTime)
        {
            // GIVEN
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.5f);
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.9f);

            // WHEN
            int ignoredResult = _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, nonPositiveDeltaTime);
            int followUpResult = _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.1f);

            // THEN — the follow-up alone completes the hold, proving the zero/negative tick added nothing to it.
            Assert.That((ignoredResult, followUpResult), Is.EqualTo((MatchOutcome.NoWinner, PlayerOneId)));
        }

        [Test]
        [Category("Allocation")]
        public void Tick_RepeatedCalls_AllocatesNoManagedMemory()
        {
            // GIVEN
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.01f); // Warm-up: excludes JIT allocation.
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.01f);

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.01f);
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(allocatedAfter - allocatedBefore, Is.EqualTo(0), "Tick allocated memory on a path its own <remarks> documents as allocation-free!");
        }

        [Test]
        public void Reset_AfterAnUnconsumedHold_MeasuresTheNextHoldFromZero()
        {
            // GIVEN
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.5f);
            _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 1f);
            _tracker.Reset();

            // WHEN — without the reset, the same leader's next tick would still be past the threshold.
            int result = _tracker.Tick(2, 1, PlayerOneId, PlayerTwoId, HoldSeconds, deltaTime: 0.1f);

            // THEN
            Assert.That(result, Is.EqualTo(MatchOutcome.NoWinner));
        }
    }
}
