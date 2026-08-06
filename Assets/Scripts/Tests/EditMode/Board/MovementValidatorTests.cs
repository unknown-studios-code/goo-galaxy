using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class MovementValidatorTests
    {
        private const int BoardRadius = 4;
        private const int ActingPlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int ActingUnitId = 1;
        private const int RivalUnitId = 2;
        private const int UnknownUnitId = 99;
        private const int FreezeDuration = 1;

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _adjacentCoords = new(1, 0);
        private static readonly HexCoordinates _distantCoords = new(2, 0);
        private static readonly HexCoordinates _secondAdjacentCoords = new(0, 1);
        private static readonly HexCoordinates _secondDistantCoords = new(0, 2);
        private static readonly HexCoordinates _cornerCoords = new(4, -4);
        private static readonly HexCoordinates _outsideAdjacentCoords = new(5, -5);
        private static readonly HexCoordinates _outsideDistantCoords = new(6, -6);
        private static readonly HexCoordinates _outsideGridCoords = new(9, 0);
        private static readonly HexCoordinates _vacantCoords = new(-3, 0);

        private static readonly IMoveCapable _fullCapability = new FakeMoveCapability(canClone: true, canJump: true);
        private static readonly IMoveCapable _jumpOnlyCapability = new FakeMoveCapability(canClone: false, canJump: true);
        private static readonly IMoveCapable _cloneOnlyCapability = new FakeMoveCapability(canClone: true, canJump: false);

        private HexGrid _grid;
        private Dictionary<int, GridUnit> _units;

        [SetUp]
        public void SetUp()
        {
            _grid = new HexGrid(new FakeGridLayout { GridRadius = BoardRadius });
            _units = new Dictionary<int, GridUnit>();
        }

        [Test]
        public void ValidateClone_AdjacentVacantTarget_ReturnsSuccess()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success));
        }

        [Test]
        public void ValidateClone_TargetTwoHexesAway_ReturnsOutOfRange()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.OutOfRange));
        }

        [Test]
        public void ValidateJump_TargetTwoHexesAwayAndVacant_ReturnsSuccess()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success));
        }

        [Test]
        public void ValidateJump_AdjacentTarget_ReturnsOutOfRange()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.OutOfRange));
        }

        [Test]
        public void ValidateClone_SourceEqualsTarget_ReturnsOutOfRange()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _origin, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.OutOfRange));
        }

        [Test]
        public void ValidateJump_SourceEqualsTarget_ReturnsOutOfRange()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _origin, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.OutOfRange));
        }

        [Test]
        public void ValidateClone_TargetOutsideGridRadius_ReturnsTargetBlocked()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _cornerCoords);
            var command = new MoveCommand(MoveType.Clone, _cornerCoords, _outsideAdjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.TargetBlocked));
        }

        [Test]
        public void ValidateJump_TargetOutsideGridRadius_ReturnsTargetBlocked()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _cornerCoords);
            var command = new MoveCommand(MoveType.Jump, _cornerCoords, _outsideDistantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.TargetBlocked));
        }

        [Test]
        public void ValidateClone_TargetCellIsBlocked_ReturnsTargetBlocked()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            BlockCell(_adjacentCoords);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.TargetBlocked));
        }

        [Test]
        public void ValidateJump_TargetCellIsBlocked_ReturnsTargetBlocked()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            BlockCell(_distantCoords);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.TargetBlocked));
        }

        [Test]
        public void ValidateClone_TargetOccupiedByAnotherUnit_ReturnsTargetOccupied()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(RivalUnitId, RivalPlayerId, _adjacentCoords);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.TargetOccupied));
        }

        [Test]
        public void ValidateJump_TargetOccupiedByAnotherUnit_ReturnsTargetOccupied()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(RivalUnitId, RivalPlayerId, _distantCoords);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.TargetOccupied));
        }

        [Test]
        public void ValidateClone_SourceCellHasNoOccupant_ReturnsSourceEmpty()
        {
            // GIVEN
            var command = new MoveCommand(MoveType.Clone, _vacantCoords, _origin, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceEmpty));
        }

        [Test]
        public void ValidateJump_SourceCellHasNoOccupant_ReturnsSourceEmpty()
        {
            // GIVEN
            var command = new MoveCommand(MoveType.Jump, _vacantCoords, _secondDistantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceEmpty));
        }

        [Test]
        public void ValidateClone_SourceOutsideGridRadius_ReturnsSourceEmpty()
        {
            // GIVEN
            var command = new MoveCommand(MoveType.Clone, _outsideGridCoords, _cornerCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceEmpty));
        }

        [Test]
        public void ValidateClone_SourceUnitOwnedByAnotherPlayer_ReturnsSourceNotOwned()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, RivalPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceNotOwned));
        }

        [Test]
        public void ValidateJump_SourceUnitOwnedByAnotherPlayer_ReturnsSourceNotOwned()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, RivalPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceNotOwned));
        }

        [Test]
        public void ValidateClone_SourceOccupantDiffersFromCommandedUnit_ReturnsUnitNotFound()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, UnknownUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.UnitNotFound));
        }

        [Test]
        public void ValidateJump_SourceOccupantDiffersFromCommandedUnit_ReturnsUnitNotFound()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, UnknownUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.UnitNotFound));
        }

        [Test]
        public void ValidateClone_OccupantMissingFromRegistry_ReturnsUnitNotFound()
        {
            // GIVEN
            OccupyCell(_origin, ActingUnitId);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.UnitNotFound));
        }

        [Test]
        public void ValidateClone_CapabilityCannotClone_ReturnsCapabilityMissing()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _jumpOnlyCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.CapabilityMissing));
        }

        [Test]
        public void ValidateJump_CapabilityCannotJump_ReturnsCapabilityMissing()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _cloneOnlyCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.CapabilityMissing));
        }

        [Test]
        public void ValidateClone_NullCapability_ReturnsCapabilityMissing()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, null);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.CapabilityMissing));
        }

        [Test]
        public void ValidateJump_NullCapability_ReturnsCapabilityMissing()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, null);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.CapabilityMissing));
        }

        [Test]
        public void ValidateClone_FrozenSourceUnit_ReturnsSourceFrozen()
        {
            // GIVEN
            GridUnit unit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            unit.AddStatus(StatusType.Frozen, FreezeDuration);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceFrozen));
        }

        [Test]
        public void ValidateJump_FrozenSourceUnit_ReturnsSourceFrozen()
        {
            // GIVEN
            GridUnit unit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            unit.AddStatus(StatusType.Frozen, FreezeDuration);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateJump(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceFrozen));
        }

        [Test]
        public void ValidateClone_SourceCarryingOnlyANonFreezingStatus_ReturnsSuccess()
        {
            // GIVEN
            GridUnit unit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            unit.AddStatus(StatusType.Rooted, 2);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success));
        }

        [Test]
        public void ValidateClone_AfterTheFrozenStatusIsRemoved_ReturnsSuccess()
        {
            // GIVEN
            GridUnit unit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            unit.AddStatus(StatusType.Frozen, FreezeDuration);
            unit.RemoveStatus(StatusType.Frozen);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success));
        }

        [Test]
        public void ValidateClone_FrozenSourceMissingCloneCapability_ReturnsSourceFrozenBeforeCapabilityMissing()
        {
            // GIVEN
            GridUnit unit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            unit.AddStatus(StatusType.Frozen, FreezeDuration);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _jumpOnlyCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceFrozen));
        }

        [Test]
        public void ValidateClone_UnownedFrozenSource_ReturnsSourceNotOwnedBeforeSourceFrozen()
        {
            // GIVEN
            GridUnit unit = PlaceUnit(ActingUnitId, RivalPlayerId, _origin);
            unit.AddStatus(StatusType.Frozen, FreezeDuration);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceNotOwned));
        }

        [Test]
        public void ValidateClone_EmptySourceAndEveryOtherRuleBroken_ReturnsSourceEmpty()
        {
            // GIVEN
            var command = new MoveCommand(MoveType.Clone, _vacantCoords, _outsideGridCoords, RivalPlayerId, UnknownUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, null);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceEmpty));
        }

        [Test]
        public void ValidateClone_UnknownUnitOnUnownedSource_ReturnsUnitNotFoundBeforeSourceNotOwned()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, RivalPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, UnknownUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.UnitNotFound));
        }

        [Test]
        public void ValidateClone_UnownedSourceAndTargetOutOfRange_ReturnsSourceNotOwned()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, RivalPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceNotOwned));
        }

        [Test]
        public void ValidateClone_MissingCapabilityAndBlockedTarget_ReturnsCapabilityMissing()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            BlockCell(_adjacentCoords);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _jumpOnlyCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.CapabilityMissing));
        }

        [Test]
        public void ValidateClone_OutOfRangeAndBlockedTarget_ReturnsOutOfRange()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            BlockCell(_distantCoords);
            var command = new MoveCommand(MoveType.Clone, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.OutOfRange));
        }

        [Test]
        public void ValidateClone_BlockedAndOccupiedTarget_ReturnsTargetBlocked()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(RivalUnitId, RivalPlayerId, _secondAdjacentCoords);
            BlockCell(_secondAdjacentCoords);
            var command = new MoveCommand(MoveType.Clone, _origin, _secondAdjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementValidator.ValidateClone(_grid, _units, command, _fullCapability);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.TargetBlocked));
        }

        [Test]
        public void ValidateClone_RepeatedCalls_AllocatesNoManagedMemory()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            IReadOnlyDictionary<int, GridUnit> units = _units;
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);
            _ = MovementValidator.ValidateClone(_grid, units, command, _fullCapability); // Warm-up to exclude JIT allocation from the measurement.

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                _ = MovementValidator.ValidateClone(_grid, units, command, _fullCapability);
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(allocatedAfter - allocatedBefore, Is.EqualTo(0), "ValidateClone allocated memory on a hot path!");
        }

        private GridUnit PlaceUnit(int unitId, int playerId, HexCoordinates position)
        {
            var unit = new GridUnit(unitId, playerId, new CardId("acid_crawler"), position);
            _units[unitId] = unit;
            OccupyCell(position, unitId);

            return unit;
        }

        private void OccupyCell(HexCoordinates coordinates, int unitId)
        {
            Assert.That(_grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test setup expects {coordinates} to exist on the grid.");
            cell.SetOccupant(unitId);
        }

        private void BlockCell(HexCoordinates coordinates)
        {
            Assert.That(_grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test setup expects {coordinates} to exist on the grid.");
            cell.IsBlocked = true;
        }

        private sealed class FakeGridLayout : IGridLayout
        {
            public int GridRadius { get; set; } = BoardRadius;

            public IReadOnlySet<HexCoordinates> BlockedCoordinates { get; set; } = new ReadOnlySet<HexCoordinates>(new HashSet<HexCoordinates>());
        }

        private sealed class FakeMoveCapability : IMoveCapable
        {
            public FakeMoveCapability(bool canClone, bool canJump)
            {
                CanClone = canClone;
                CanJump = canJump;
            }

            public bool CanClone { get; }

            public bool CanJump { get; }
        }
    }
}
