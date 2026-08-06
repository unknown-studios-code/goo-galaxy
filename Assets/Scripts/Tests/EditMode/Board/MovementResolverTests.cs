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
    public class MovementResolverTests
    {
        private const int BoardRadius = 4;
        private const int ActingPlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int ActingUnitId = 1;
        private const int RivalUnitId = 2;
        private const int UnknownUnitId = 99;
        private const int FirstSpawnedUnitId = 100;
        private const string SourceCardIdValue = "acid_crawler";

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _adjacentCoords = new(1, 0);
        private static readonly HexCoordinates _distantCoords = new(2, 0);
        private static readonly HexCoordinates _farCoords = new(4, 0);
        private static readonly HexCoordinates _threeHexCoords = new(3, 0);
        private static readonly HexCoordinates _blockedAdjacentCoords = new(0, 1);
        private static readonly HexCoordinates _outsideGridCoords = new(9, 0);

        private HexGrid _grid;
        private Dictionary<int, GridUnit> _units;
        private List<HexCoordinates> _affectedCoordinates;
        private FakeUnitSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _grid = new HexGrid(new FakeGridLayout { GridRadius = BoardRadius });
            _units = new Dictionary<int, GridUnit>();
            _affectedCoordinates = new List<HexCoordinates>(2);
            _spawner = new FakeUnitSpawner();
        }

        [Test]
        public void Resolve_Clone_LeavesSourceUnitAndCellUntouched()
        {
            // GIVEN
            GridUnit sourceUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(sourceUnit.Position, Is.EqualTo(_origin));
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(ActingUnitId));
        }

        [Test]
        public void Resolve_Clone_ReturnsSuccess()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = Resolve(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success));
        }

        [Test]
        public void Resolve_Clone_OccupiesTargetCellWithSpawnedUnit()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(GetCell(_adjacentCoords).OccupantUnitId, Is.EqualTo(FirstSpawnedUnitId));
        }

        [Test]
        public void Resolve_Clone_AddsSpawnedUnitToRegistry()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(_units.Count, Is.EqualTo(2));
        }

        [Test]
        public void Resolve_Clone_ReportsTheSpawnedUnitThroughTheOutParameter()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResolver.Resolve(_grid, _units, _spawner, command, _affectedCoordinates, out GridUnit spawnedUnit);

            // THEN
            Assert.That(spawnedUnit, Is.SameAs(_units[FirstSpawnedUnitId]));
        }

        [Test]
        public void Resolve_Clone_SpawnedUnitCarriesSourceCardIdAndDistinctUnitId()
        {
            // GIVEN
            GridUnit sourceUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResolver.Resolve(_grid, _units, _spawner, command, _affectedCoordinates, out GridUnit spawnedUnit);

            // THEN
            Assert.That(spawnedUnit.CardId, Is.EqualTo(sourceUnit.CardId));
            Assert.That(spawnedUnit.UnitId, Is.Not.EqualTo(sourceUnit.UnitId));
        }

        [Test]
        public void Resolve_Clone_SpawnedUnitStandsOnTheTargetCoordinate()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResolver.Resolve(_grid, _units, _spawner, command, _affectedCoordinates, out GridUnit spawnedUnit);

            // THEN
            Assert.That(spawnedUnit.Position, Is.EqualTo(_adjacentCoords));
        }

        [Test]
        public void Resolve_Clone_InvokesSpawnerOnceWithCommandArguments()
        {
            // GIVEN
            GridUnit sourceUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(_spawner.SpawnCallCount, Is.EqualTo(1));
            Assert.That(_spawner.LastPlayerId, Is.EqualTo(ActingPlayerId));
            Assert.That(_spawner.LastCardId, Is.EqualTo(sourceUnit.CardId));
            Assert.That(_spawner.LastCoordinates, Is.EqualTo(_adjacentCoords));
        }

        [Test]
        public void Resolve_Clone_SpawnedUnitStartsWithCleanStatusState()
        {
            // GIVEN
            GridUnit sourceUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            sourceUnit.AddStatus(StatusType.Rooted, 2);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResolver.Resolve(_grid, _units, _spawner, command, _affectedCoordinates, out GridUnit spawnedUnit);

            // THEN
            Assert.That(spawnedUnit.ActiveStatuses, Is.Empty);
        }

        [Test]
        public void Resolve_Jump_ReturnsSuccess()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = Resolve(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success));
        }

        [Test]
        public void Resolve_Jump_FreesSourceCellAndOccupiesTargetCell()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
            Assert.That(GetCell(_distantCoords).OccupantUnitId, Is.EqualTo(ActingUnitId));
        }

        [Test]
        public void Resolve_Jump_MovesUnitPreservingIdentity()
        {
            // GIVEN
            GridUnit sourceUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(sourceUnit.Position, Is.EqualTo(_distantCoords));
            Assert.That(sourceUnit.UnitId, Is.EqualTo(ActingUnitId));
            Assert.That(sourceUnit.PlayerId, Is.EqualTo(ActingPlayerId));
        }

        [Test]
        public void Resolve_Jump_CarriesActiveStatusesToTheNewPosition()
        {
            // GIVEN
            GridUnit sourceUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            sourceUnit.AddStatus(StatusType.Rooted, 2);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(sourceUnit.HasStatus(StatusType.Rooted), Is.True);
        }

        [Test]
        public void Resolve_Jump_LeavesRegistryCountUnchanged()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(_units.Count, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_Jump_NeverConsultsTheSpawner()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(_spawner.SpawnCallCount, Is.EqualTo(0));
        }

        [Test]
        public void Resolve_Jump_LeavesTheSpawnedUnitOutParameterNull()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResolver.Resolve(_grid, _units, _spawner, command, _affectedCoordinates, out GridUnit spawnedUnit);

            // THEN
            Assert.That(spawnedUnit, Is.Null);
        }

        [Test]
        public void Resolve_Clone_FillsBufferWithOnlyTheTargetCoordinate()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(_affectedCoordinates, Has.Count.EqualTo(1));
            Assert.That(_affectedCoordinates[0], Is.EqualTo(_adjacentCoords));
        }

        [Test]
        public void Resolve_Jump_FillsBufferWithSourceThenTarget()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(_affectedCoordinates, Has.Count.EqualTo(2));
            Assert.That(_affectedCoordinates[0], Is.EqualTo(_origin));
            Assert.That(_affectedCoordinates[1], Is.EqualTo(_distantCoords));
        }

        [Test]
        public void Resolve_BufferHoldingPreviousContent_ClearsItBeforeFilling()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _affectedCoordinates.Add(_farCoords);
            _affectedCoordinates.Add(_threeHexCoords);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(_affectedCoordinates, Has.Count.EqualTo(1));
            Assert.That(_affectedCoordinates[0], Is.EqualTo(_adjacentCoords));
        }

        [Test]
        public void Resolve_NullBuffer_ThrowsArgumentNullException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            void resolveCall() => MovementResolver.Resolve(_grid, _units, _spawner, command, null, out _);

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_CommandUnitIdDoesNotMatchSourceOccupant_ThrowsInvalidOperationException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, UnknownUnitId);

            // WHEN
            void resolveCall() => Resolve(command);

            // THEN
            Assert.Throws<InvalidOperationException>(resolveCall);
        }

        [Test]
        public void Resolve_SourceOutsideGrid_ThrowsInvalidOperationException()
        {
            // GIVEN
            var command = new MoveCommand(MoveType.Clone, _outsideGridCoords, _farCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            void resolveCall() => Resolve(command);

            // THEN
            Assert.Throws<InvalidOperationException>(resolveCall);
        }

        [Test]
        public void Resolve_OccupantMissingFromRegistry_ThrowsInvalidOperationException()
        {
            // GIVEN
            GetCell(_origin).SetOccupant(ActingUnitId);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            void resolveCall() => Resolve(command);

            // THEN
            Assert.Throws<InvalidOperationException>(resolveCall);
        }

        [Test]
        public void Resolve_TargetCellIsBlocked_ThrowsInvalidOperationException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GetCell(_blockedAdjacentCoords).IsBlocked = true;
            var command = new MoveCommand(MoveType.Clone, _origin, _blockedAdjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            void resolveCall() => Resolve(command);

            // THEN
            Assert.Throws<InvalidOperationException>(resolveCall);
        }

        [Test]
        public void Resolve_TargetCellIsOccupied_ThrowsInvalidOperationException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(RivalUnitId, RivalPlayerId, _adjacentCoords);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            void resolveCall() => Resolve(command);

            // THEN
            Assert.Throws<InvalidOperationException>(resolveCall);
        }

        [Test]
        public void Resolve_DistanceDoesNotMatchMoveType_ThrowsInvalidOperationException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            void resolveCall() => Resolve(command);

            // THEN
            Assert.Throws<InvalidOperationException>(resolveCall);
        }

        [Test]
        public void Resolve_UnsupportedMoveTypeMatchingItsDistance_ThrowsArgumentException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);

            var command = new MoveCommand((MoveType)3, _origin, _threeHexCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            void resolveCall() => Resolve(command);

            // THEN
            Assert.Throws<ArgumentException>(resolveCall);
        }

        [Test]
        public void Resolve_UnownedSource_ResolvesAnyway()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, RivalPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = Resolve(command);

            // THEN
            Assert.That(
                result,
                Is.EqualTo(MovementResult.Success),
                "Ownership is a validator rule; resolution is internal precisely because it does not re-check it."
            );
        }

        [Test]
        public void Resolve_FrozenSource_ResolvesAnyway()
        {
            // GIVEN
            GridUnit sourceUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            sourceUnit.AddStatus(StatusType.Frozen, 1);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = Resolve(command);

            // THEN
            Assert.That(
                result,
                Is.EqualTo(MovementResult.Success),
                "Cryo-Stasis is a validator rule; resolution is internal precisely because it does not re-check it."
            );
        }

        [Test]
        public void Resolve_CloneWithNullSpawner_ReturnsSpawnFailed()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = MovementResolver.Resolve(_grid, _units, null, command, _affectedCoordinates, out _);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SpawnFailed));
            AssertBoardUnchangedAfterFailedClone();
        }

        [Test]
        public void Resolve_CloneWithSpawnerReturningNull_ReturnsSpawnFailed()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _spawner.ReturnsNull = true;
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = Resolve(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SpawnFailed));
            AssertBoardUnchangedAfterFailedClone();
        }

        [Test]
        public void Resolve_CloneWithSpawnerReturningNull_LeavesTheBufferEmpty()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _spawner.ReturnsNull = true;
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(_affectedCoordinates, Is.Empty);
        }

        [Test]
        public void Resolve_CloneWithThrowingSpawner_PropagatesTheException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _spawner.ThrowsOnSpawn = true;
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            void resolveCall() => Resolve(command);

            // THEN
            Assert.Throws<InvalidOperationException>(resolveCall);
        }

        [Test]
        public void Resolve_CloneWithThrowingSpawner_LeavesBoardUnchanged()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _spawner.ThrowsOnSpawn = true;
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Assert.Throws<InvalidOperationException>(() => Resolve(command));

            // THEN
            AssertBoardUnchangedAfterFailedClone();
        }

        [Test]
        public void Resolve_CloneWithSpawnerReturningDuplicateUnitId_ReturnsSpawnFailed()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _spawner.ForcedUnitId = ActingUnitId;
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = Resolve(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SpawnFailed));
            AssertBoardUnchangedAfterFailedClone();
        }

        [Test]
        public void Resolve_CloneWithSpawnerReturningDuplicateUnitId_LeavesTheIncumbentUnitUntouched()
        {
            // GIVEN
            GridUnit incumbentUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _spawner.ForcedUnitId = ActingUnitId;
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            Resolve(command);

            // THEN
            Assert.That(_units[ActingUnitId], Is.SameAs(incumbentUnit), "A colliding spawn must not replace the registered unit.");
            Assert.That(incumbentUnit.Position, Is.EqualTo(_origin));
        }

        [Test]
        public void Resolve_RepeatedJumps_AllocatesNoManagedMemory()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            var outwardCommand = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);
            var returnCommand = new MoveCommand(MoveType.Jump, _distantCoords, _origin, ActingPlayerId, ActingUnitId);
            Resolve(outwardCommand); // Warm-up to exclude JIT allocation from the measurement.
            Resolve(returnCommand);

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 500; i++)
            {
                Resolve(outwardCommand);
                Resolve(returnCommand);
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(allocatedAfter - allocatedBefore, Is.EqualTo(0), "Resolving a Jump allocated memory on a hot path!");
        }

        private MovementResult Resolve(in MoveCommand command)
        {
            return MovementResolver.Resolve(_grid, _units, _spawner, command, _affectedCoordinates, out _);
        }

        private void AssertBoardUnchangedAfterFailedClone()
        {
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(ActingUnitId));
            Assert.That(GetCell(_adjacentCoords).OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
            Assert.That(_units.Count, Is.EqualTo(1));
        }

        private GridUnit PlaceUnit(int unitId, int playerId, HexCoordinates position)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(SourceCardIdValue), position);
            _units[unitId] = unit;
            GetCell(position).SetOccupant(unitId);

            return unit;
        }

        private HexCell GetCell(HexCoordinates coordinates)
        {
            Assert.That(_grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test setup expects {coordinates} to exist on the grid.");

            return cell;
        }

        private sealed class FakeGridLayout : IGridLayout
        {
            public int GridRadius { get; set; } = BoardRadius;

            public IReadOnlySet<HexCoordinates> BlockedCoordinates { get; set; } = new ReadOnlySet<HexCoordinates>(new HashSet<HexCoordinates>());
        }

        private sealed class FakeUnitSpawner : IUnitSpawner
        {
            private int _nextUnitId = FirstSpawnedUnitId;

            public int SpawnCallCount { get; private set; }

            public int LastPlayerId { get; private set; }

            public CardId LastCardId { get; private set; }

            public HexCoordinates LastCoordinates { get; private set; }

            public bool ReturnsNull { get; set; }

            public bool ThrowsOnSpawn { get; set; }

            public int ForcedUnitId { get; set; } = HexCell.NoOccupant;

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                SpawnCallCount++;
                LastPlayerId = playerId;
                LastCardId = cardId;
                LastCoordinates = at;

                if (ThrowsOnSpawn)
                {
                    throw new InvalidOperationException("Fake spawner failure.");
                }

                if (ReturnsNull)
                {
                    return null;
                }

                int unitId = ForcedUnitId != HexCell.NoOccupant ? ForcedUnitId : _nextUnitId++;

                return new GridUnit(unitId, playerId, cardId, at);
            }
        }
    }
}
