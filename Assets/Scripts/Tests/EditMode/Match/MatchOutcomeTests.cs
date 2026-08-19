using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Match
{
    [TestFixture]
    public class MatchOutcomeTests
    {
        [Test]
        public void IsDraw_WinnerIsNoWinner_ReturnsTrue()
        {
            // GIVEN
            var outcome = new MatchOutcome(MatchOutcome.NoWinner, MatchEndReason.Draw);

            // WHEN
            bool isDraw = outcome.IsDraw;

            // THEN
            Assert.That(isDraw, Is.True);
        }

        [Test]
        public void IsDraw_WithAWinner_ReturnsFalse()
        {
            // GIVEN
            var outcome = new MatchOutcome(1, MatchEndReason.TimeLimit);

            // WHEN
            bool isDraw = outcome.IsDraw;

            // THEN
            Assert.That(isDraw, Is.False);
        }

        [Test]
        public void Drawn_StaticInstance_IsADrawWithTheDrawReason()
        {
            // GIVEN

            // WHEN
            MatchOutcome drawn = MatchOutcome.Drawn;

            // THEN
            Assert.That((drawn.IsDraw, drawn.Reason), Is.EqualTo((true, MatchEndReason.Draw)));
        }

        [Test]
        public void Reason_OnADefaultedOutcome_IsNoneRatherThanARealEnding()
        {
            // GIVEN
            MatchOutcome outcome = default;

            // WHEN
            MatchEndReason reason = outcome.Reason;

            // THEN
            Assert.That(reason, Is.EqualTo(MatchEndReason.None));
        }

        [Test]
        public void Equals_SameWinnerAndReason_ReturnsTrue()
        {
            // GIVEN
            var first = new MatchOutcome(2, MatchEndReason.TimeLimit);
            var second = new MatchOutcome(2, MatchEndReason.TimeLimit);

            // WHEN
            bool areEqual = first.Equals(second);

            // THEN
            Assert.That(areEqual, Is.True);
        }

        [Test]
        public void Equals_DifferentWinner_ReturnsFalse()
        {
            // GIVEN
            var first = new MatchOutcome(1, MatchEndReason.TimeLimit);
            var second = new MatchOutcome(2, MatchEndReason.TimeLimit);

            // WHEN
            bool areEqual = first.Equals(second);

            // THEN
            Assert.That(areEqual, Is.False);
        }

        [Test]
        public void EqualityOperator_SameValues_ReturnsTrue()
        {
            // GIVEN
            var first = new MatchOutcome(1, MatchEndReason.Surrender);
            var second = new MatchOutcome(1, MatchEndReason.Surrender);

            // WHEN
            bool areEqual = first == second;

            // THEN
            Assert.That(areEqual, Is.True);
        }

        [Test]
        public void InequalityOperator_DifferentReason_ReturnsTrue()
        {
            // GIVEN
            var first = new MatchOutcome(1, MatchEndReason.TimeLimit);
            var second = new MatchOutcome(1, MatchEndReason.Domination);

            // WHEN
            bool areNotEqual = first != second;

            // THEN
            Assert.That(areNotEqual, Is.True);
        }

        [Test]
        public void GetHashCode_EqualInstances_ReturnsTheSameValue()
        {
            // GIVEN
            var first = new MatchOutcome(1, MatchEndReason.TimeLimit);
            var second = new MatchOutcome(1, MatchEndReason.TimeLimit);

            // WHEN
            int firstHash = first.GetHashCode();
            int secondHash = second.GetHashCode();

            // THEN
            Assert.That(firstHash, Is.EqualTo(secondHash));
        }
    }
}
