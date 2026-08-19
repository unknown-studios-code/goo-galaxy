using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Match
{
    [TestFixture]
    public class MatchStateTests
    {
        private const int PlayerOneId = 1;

        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            _state = new MatchState();
        }

        [Test]
        public void TryTransition_StandardToEnded_ReturnsTrueAndEntersEnded()
        {
            // GIVEN — the domination edge GOOM-12 reaches; declared and legal ahead of the system that raises it.
            _state.TryTransition(MatchPhase.Loading);
            _state.TryTransition(MatchPhase.Countdown);
            _state.TryTransition(MatchPhase.Standard);

            // WHEN
            bool result = _state.TryTransition(MatchPhase.Ended);

            // THEN
            Assert.That((result, _state.Phase), Is.EqualTo((true, MatchPhase.Ended)));
        }

        [Test]
        public void TryTransition_ToTheSamePhaseAlreadyEntered_ReturnsFalse()
        {
            // GIVEN
            _state.TryTransition(MatchPhase.Loading);

            // WHEN
            bool result = _state.TryTransition(MatchPhase.Loading);

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void TryTransition_IllegalTransition_ReturnsFalseAndLeavesPhaseUnmutated()
        {
            // GIVEN — None only leads to Loading, so Standard is illegal directly from it.

            // WHEN
            bool result = _state.TryTransition(MatchPhase.Standard);

            // THEN
            Assert.That((result, _state.Phase), Is.EqualTo((false, MatchPhase.None)));
        }

        [Test]
        public void TrySetScore_FirstCountForAPlayer_ReturnsChangedEvenWhenZero()
        {
            // GIVEN

            // WHEN
            bool changed = _state.TrySetScore(PlayerOneId, 0);

            // THEN
            Assert.That(changed, Is.True);
        }

        [Test]
        public void TrySetScore_RepeatedCount_ReturnsUnchanged()
        {
            // GIVEN
            _state.TrySetScore(PlayerOneId, 3);

            // WHEN
            bool changed = _state.TrySetScore(PlayerOneId, 3);

            // THEN
            Assert.That(changed, Is.False);
        }

        [Test]
        public void Reset_AfterTransitionsAndScores_ReturnsToNoneAndClearsScores()
        {
            // GIVEN
            _state.TryTransition(MatchPhase.Loading);
            _state.TrySetScore(PlayerOneId, 3);

            // WHEN
            _state.Reset();

            // THEN
            Assert.That((_state.Phase, _state.GetScore(PlayerOneId)), Is.EqualTo((MatchPhase.None, 0)));
        }

        [Test]
        public void IsRunning_OnceEnded_ReturnsFalse()
        {
            // GIVEN
            _state.TryTransition(MatchPhase.Loading);
            _state.TryTransition(MatchPhase.Countdown);
            _state.TryTransition(MatchPhase.Standard);
            _state.TryTransition(MatchPhase.Ended);

            // WHEN
            bool isRunning = _state.IsRunning;

            // THEN
            Assert.That(isRunning, Is.False);
        }
    }
}
