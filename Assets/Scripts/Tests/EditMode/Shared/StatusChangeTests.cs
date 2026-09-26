using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
{
    [TestFixture]
    public class StatusChangeTests
    {
        private const int UnitId = 5;
        private const int OwnerPlayerId = 1;
        private const int ActingPlayerId = 2;
        private const int RemainingWindows = 3;

        [Test]
        public void Equals_IdenticalArguments_ReturnsTrue()
        {
            // GIVEN
            var first = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);
            var second = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);

            // WHEN
            bool result = first.Equals(second);

            // THEN
            Assert.That(result, Is.True);
        }

        [TestCase(6, 1, 2, StatusType.Frozen, 3, "UnitId")]
        [TestCase(5, 2, 2, StatusType.Frozen, 3, "OwnerPlayerId")]
        [TestCase(5, 1, 3, StatusType.Frozen, 3, "ActingPlayerId")]
        [TestCase(5, 1, 2, StatusType.Rooted, 3, "Status")]
        [TestCase(5, 1, 2, StatusType.Frozen, 4, "RemainingWindows")]
        public void Equals_OneMemberDiffers_ReturnsFalse(
            int unitId,
            int ownerPlayerId,
            int actingPlayerId,
            StatusType status,
            int remainingWindows,
            string changedMember
        )
        {
            // GIVEN
            var baseline = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);
            var other = new StatusChange(unitId, ownerPlayerId, actingPlayerId, status, remainingWindows);

            // WHEN
            bool result = baseline.Equals(other);

            // THEN
            Assert.That(result, Is.False, $"Expected a differing {changedMember} to break equality.");
        }

        [Test]
        public void Equals_BoxedObjectOfAnotherType_ReturnsFalse()
        {
            // GIVEN
            var change = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);

            // WHEN
            bool result = change.Equals("not a StatusChange");

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void Equals_Null_ReturnsFalse()
        {
            // GIVEN
            var change = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);

            // WHEN
            bool result = change.Equals(null);

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetHashCode_IdenticalArguments_ReturnsSameHash()
        {
            // GIVEN
            var first = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);
            var second = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);

            // WHEN
            int firstHash = first.GetHashCode();
            int secondHash = second.GetHashCode();

            // THEN
            Assert.That(secondHash, Is.EqualTo(firstHash));
        }

        [Test]
        public void EqualityOperator_EqualChanges_AgreesWithEquals()
        {
            // GIVEN
            var first = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);
            var second = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);

            // WHEN
            bool operatorResult = first == second;
            bool equalsResult = first.Equals(second);

            // THEN
            Assert.That(operatorResult, Is.EqualTo(equalsResult));
        }

        [Test]
        public void InequalityOperator_DifferentChanges_AgreesWithEquals()
        {
            // GIVEN
            var first = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Frozen, RemainingWindows);
            var second = new StatusChange(UnitId, OwnerPlayerId, ActingPlayerId, StatusType.Rooted, RemainingWindows);

            // WHEN
            bool operatorResult = first != second;
            bool equalsResult = !first.Equals(second);

            // THEN
            Assert.That(operatorResult, Is.EqualTo(equalsResult));
        }

        [Test]
        public void NoActingPlayer_Always_MatchesTheUnassignedPlayerSlotId()
        {
            // THEN
            Assert.That(StatusChange.NoActingPlayer, Is.EqualTo(PlayerSlot.UnassignedId));
        }
    }
}
