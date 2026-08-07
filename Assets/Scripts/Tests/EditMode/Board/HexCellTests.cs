using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class HexCellTests
    {
        [Test]
        public void Constructor_Default_IsNotBlocked()
        {
            // GIVEN
            var coords = new HexCoordinates(2, -1);

            // WHEN
            var cell = new HexCell(coords);

            // THEN
            Assert.That(cell.Coordinates, Is.EqualTo(coords));
            Assert.That(cell.IsBlocked, Is.False);
        }

        [Test]
        public void Constructor_Blocked_IsBlockedTrue()
        {
            // GIVEN
            var coords = new HexCoordinates(1, 1);

            // WHEN
            var cell = new HexCell(coords, isBlocked: true);

            // THEN
            Assert.That(cell.Coordinates, Is.EqualTo(coords));
            Assert.That(cell.IsBlocked, Is.True);
        }

        [Test]
        public void IsBlocked_Setter_MutatesState()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0), isBlocked: false);
            Assert.That(cell.IsBlocked, Is.False);

            // WHEN
            cell.IsBlocked = true;

            // THEN
            Assert.That(cell.IsBlocked, Is.True);

            // WHEN
            cell.IsBlocked = false;

            // THEN
            Assert.That(cell.IsBlocked, Is.False);
        }

        [Test]
        public void Coordinates_AfterConstruction_ReturnsConstructorValue()
        {
            // GIVEN
            var expected = new HexCoordinates(-3, 4);
            var cell = new HexCell(expected);

            // WHEN
            HexCoordinates actual = cell.Coordinates;

            // THEN
            Assert.That(actual.Q, Is.EqualTo(expected.Q));
            Assert.That(actual.R, Is.EqualTo(expected.R));
        }

        [Test]
        public void Constructor_OriginCoordinates_Works()
        {
            // GIVEN
            var origin = new HexCoordinates(0, 0);

            // WHEN
            var cell = new HexCell(origin);

            // THEN
            Assert.That(cell.Coordinates, Is.EqualTo(origin));
            Assert.That(cell.IsBlocked, Is.False);
        }

        [Test]
        public void Constructor_Default_HasNoOccupant()
        {
            // GIVEN
            var coords = new HexCoordinates(2, -1);

            // WHEN
            var cell = new HexCell(coords);

            // THEN
            Assert.That(cell.OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
            Assert.That(cell.IsOccupied, Is.False);
        }

        [Test]
        public void SetOccupant_WithUnit_AssignsUnitAndMarksCellOccupied()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(1, -1));

            // WHEN
            cell.SetOccupant(7);

            // THEN
            Assert.That(cell.OccupantUnitId, Is.EqualTo(7));
            Assert.That(cell.IsOccupied, Is.True);
        }

        [Test]
        public void ClearOccupant_AfterSetOccupant_RestoresVacantState()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(1, -1));
            cell.SetOccupant(7);

            // WHEN
            cell.ClearOccupant();

            // THEN
            Assert.That(cell.OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
            Assert.That(cell.IsOccupied, Is.False);
        }

        [Test]
        public void SetOccupant_OnBlockedCell_LeavesBlockedFlagUnchanged()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 2), isBlocked: true);

            // WHEN
            cell.SetOccupant(3);

            // THEN
            Assert.That(cell.IsBlocked, Is.True);
            Assert.That(cell.IsOccupied, Is.True);
        }

        [Test]
        public void IsBlocked_Toggled_DoesNotAffectOccupancy()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 2));
            cell.SetOccupant(3);

            // WHEN
            cell.IsBlocked = true;
            cell.IsBlocked = false;

            // THEN
            Assert.That(cell.OccupantUnitId, Is.EqualTo(3));
            Assert.That(cell.IsOccupied, Is.True);
        }

        [Test]
        public void Constructor_Default_HasNoHazard()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));

            // THEN
            Assert.That(cell.HasHazard, Is.False);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void SetHazard_NonPositiveDuration_PlacesNothingAndReturnsFalse(int duration)
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));

            // WHEN
            bool didReplace = cell.SetHazard(ownerPlayerId: 1, duration: duration);

            // THEN
            Assert.That(didReplace, Is.False);
            Assert.That(cell.HasHazard, Is.False);
        }

        [Test]
        public void SetHazard_OnAClearCell_SetsHasHazardTrue()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));

            // WHEN
            cell.SetHazard(ownerPlayerId: 1, duration: 3);

            // THEN
            Assert.That(cell.HasHazard, Is.True);
        }

        [Test]
        public void SetHazard_OnAClearCell_RecordsTheOwnerAndDuration()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));

            // WHEN
            cell.SetHazard(ownerPlayerId: 1, duration: 3);

            // THEN
            Assert.That(cell.Hazard.OwnerPlayerId, Is.EqualTo(1));
            Assert.That(cell.Hazard.RemainingDuration, Is.EqualTo(3));
        }

        [Test]
        public void SetHazard_OnAClearCell_ReturnsFalse()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));

            // WHEN
            bool didReplace = cell.SetHazard(ownerPlayerId: 1, duration: 3);

            // THEN
            Assert.That(didReplace, Is.False);
        }

        [Test]
        public void SetHazard_OverAnActiveHazard_ReturnsTrue()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));
            cell.SetHazard(ownerPlayerId: 1, duration: 3);

            // WHEN
            bool didReplace = cell.SetHazard(ownerPlayerId: 2, duration: 5);

            // THEN
            Assert.That(didReplace, Is.True);
        }

        [Test]
        public void SetHazard_OverAnActiveHazard_ReplacesTheOwnerAndDuration()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));
            cell.SetHazard(ownerPlayerId: 1, duration: 3);

            // WHEN
            cell.SetHazard(ownerPlayerId: 2, duration: 5);

            // THEN
            Assert.That(cell.Hazard.OwnerPlayerId, Is.EqualTo(2));
            Assert.That(cell.Hazard.RemainingDuration, Is.EqualTo(5));
        }

        [Test]
        public void TickHazard_ActiveHazard_DecrementsRemainingDuration()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));
            cell.SetHazard(ownerPlayerId: 1, duration: 3);

            // WHEN
            cell.TickHazard();

            // THEN
            Assert.That(cell.Hazard.RemainingDuration, Is.EqualTo(2));
        }

        [Test]
        public void TickHazard_HazardReachingZero_ClearsTheHazard()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));
            cell.SetHazard(ownerPlayerId: 1, duration: 1);

            // WHEN
            cell.TickHazard();

            // THEN
            Assert.That(cell.HasHazard, Is.False);
        }

        [Test]
        public void TickHazard_CellWithNoHazard_IsANoOp()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));

            // WHEN
            cell.TickHazard();

            // THEN
            Assert.That(cell.HasHazard, Is.False);
        }

        [Test]
        public void ClearHazard_ActiveHazard_SetsHasHazardFalse()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));
            cell.SetHazard(ownerPlayerId: 1, duration: 3);

            // WHEN
            cell.ClearHazard();

            // THEN
            Assert.That(cell.HasHazard, Is.False);
        }

        [Test]
        public void SetHazard_DoesNotChangeOccupancy()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));
            cell.SetOccupant(3);

            // WHEN
            cell.SetHazard(ownerPlayerId: 1, duration: 3);

            // THEN
            Assert.That(cell.IsOccupied, Is.True);
            Assert.That(cell.OccupantUnitId, Is.EqualTo(3));
        }

        [Test]
        public void ClearOccupant_WithAnActiveHazard_LeavesTheHazardIntact()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0));
            cell.SetOccupant(3);
            cell.SetHazard(ownerPlayerId: 1, duration: 3);

            // WHEN
            cell.ClearOccupant();

            // THEN
            Assert.That(cell.HasHazard, Is.True);
        }
    }
}
