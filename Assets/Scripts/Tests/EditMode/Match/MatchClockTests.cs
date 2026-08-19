using GooGalaxy.Runtime.Match.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Match
{
    [TestFixture]
    public class MatchClockTests
    {
        private MatchClock _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = new MatchClock();
            _clock.Reset(5f);
        }

        [Test]
        public void Tick_WithinDuration_DecreasesRemainingByDeltaTime()
        {
            // GIVEN

            // WHEN
            _clock.Tick(2f);

            // THEN
            Assert.That(_clock.Remaining, Is.EqualTo(3f));
        }

        [Test]
        public void Tick_PastZero_ClampsRemainingAtZero()
        {
            // GIVEN

            // WHEN
            _clock.Tick(10f);

            // THEN
            Assert.That(_clock.Remaining, Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Tick_NonPositiveDeltaTime_DoesNotChangeRemaining(float deltaTime)
        {
            // GIVEN

            // WHEN
            _clock.Tick(deltaTime);

            // THEN
            Assert.That(_clock.Remaining, Is.EqualTo(5f));
        }

        [Test]
        public void TryConsumeExpiry_AfterExpiry_ReturnsTrueOnTheFirstCallAndFalseOnTheSecond()
        {
            // GIVEN
            _clock.Tick(10f);

            // WHEN
            bool firstCall = _clock.TryConsumeExpiry();
            bool secondCall = _clock.TryConsumeExpiry();

            // THEN
            Assert.That((firstCall, secondCall), Is.EqualTo((true, false)));
        }

        [Test]
        public void TryConsumeExpiry_NotConsumedOnTheExpiryFrame_StillAvailableNextFrame()
        {
            // GIVEN — the clock is already drained, so this later Tick is the next frame's no-op.
            _clock.Tick(10f);
            _clock.Tick(0.016f);

            // WHEN
            bool consumed = _clock.TryConsumeExpiry();

            // THEN
            Assert.That(consumed, Is.True);
        }

        [Test]
        public void Reset_WithUnconsumedExpiry_DiscardsIt()
        {
            // GIVEN
            _clock.Tick(10f);

            // WHEN
            _clock.Reset(3f);

            // THEN
            Assert.That(_clock.TryConsumeExpiry(), Is.False);
        }

        [Test]
        public void Reset_ToZeroSeconds_DoesNotReportExpired()
        {
            // GIVEN

            // WHEN
            _clock.Reset(0f);

            // THEN
            Assert.That(_clock.HasExpired, Is.False);
        }

        [Test]
        public void Reset_NegativeSeconds_ClampsRemainingAtZero()
        {
            // GIVEN

            // WHEN
            _clock.Reset(-3f);

            // THEN
            Assert.That(_clock.Remaining, Is.EqualTo(0f));
        }
    }
}
