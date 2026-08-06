using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class ConversionResolverTests
    {
        private const int BoardRadius = 4;
        private const int ActingPlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int ActingUnitId = 1;
        private const int LandingUnitIdA = 20;
        private const int LandingUnitIdB = 21;
        private const int EnemyUnitId = 10;
        private const int FriendlyUnitId = 11;
        private const int FreezeDuration = 1;
        private const int JunkUnitId = 9999;
        private const int JunkConvertedUnitId = 8888;
        private const int JunkArmorStrippedUnitId = 7777;
        private const string SourceCardIdValue = "acid_crawler";
        private const string ArmoredCardIdValue = "bio_phalanx";

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _adjacentCoords = new(1, 0);
        private static readonly HexCoordinates _secondAdjacentCoords = new(0, -1);
        private static readonly HexCoordinates _distantCoords = new(3, 0);
        private static readonly HexCoordinates _outsideGridCoords = new(9, 0);

        private HexGrid _grid;
        private Dictionary<int, GridUnit> _units;
        private List<HexCoordinates> _affectedCoordinates;
        private List<HexCell> _neighborBuffer;
        private HashSet<int> _attemptedUnitIds;
        private List<int> _convertedUnitIds;
        private List<int> _armorStrippedUnitIds;

        [SetUp]
        public void SetUp()
        {
            _grid = new HexGrid(new FakeGridLayout { GridRadius = BoardRadius });
            _units = new Dictionary<int, GridUnit>();
            _affectedCoordinates = new List<HexCoordinates>(2);
            _neighborBuffer = new List<HexCell>(6);
            _attemptedUnitIds = new HashSet<int>();
            _convertedUnitIds = new List<int>();
            _armorStrippedUnitIds = new List<int>();
        }

        [Test]
        public void Resolve_AdjacentStandardEnemy_AddsItToConvertedUnitIds()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_convertedUnitIds, Does.Contain(enemyUnit.UnitId));
        }

        [Test]
        public void Resolve_AdjacentStandardEnemy_FlipsItsOwnershipToTheActingPlayer()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(enemyUnit.PlayerId, Is.EqualTo(ActingPlayerId));
        }

        [Test]
        public void Resolve_AdjacentArmoredEnemy_AddsItToArmorStrippedUnitIds()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords, hasArmor: true);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_armorStrippedUnitIds, Does.Contain(enemyUnit.UnitId));
        }

        [Test]
        public void Resolve_AdjacentArmoredEnemy_LeavesItsArmorFlagFalseAfterward()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords, hasArmor: true);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(enemyUnit.HasArmor, Is.False);
        }

        [Test]
        public void Resolve_AdjacentArmoredEnemy_LeavesItsOwnershipUnchanged()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords, hasArmor: true);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(enemyUnit.PlayerId, Is.EqualTo(RivalPlayerId), "Stripping armor must not also flip ownership; that is a second landing's job.");
        }

        [Test]
        public void Resolve_ArmoredEnemyStrippedByAnEarlierResolution_IsConvertedByTheNextOne()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords, hasArmor: true);
            _affectedCoordinates.Add(_origin);
            Resolve();

            // WHEN
            Resolve();

            // THEN
            Assert.That(_convertedUnitIds, Does.Contain(enemyUnit.UnitId));
        }

        [Test]
        public void Resolve_FrozenEnemy_AppearsInNeitherOutputList()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            enemyUnit.AddStatus(StatusType.Frozen, FreezeDuration);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_convertedUnitIds, Is.Empty);
            Assert.That(_armorStrippedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_FrozenEnemy_KeepsItsOriginalOwner()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            enemyUnit.AddStatus(StatusType.Frozen, FreezeDuration);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(enemyUnit.PlayerId, Is.EqualTo(RivalPlayerId));
        }

        [Test]
        public void Resolve_FrozenArmoredEnemy_KeepsItsArmorIntact()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords, hasArmor: true);
            enemyUnit.AddStatus(StatusType.Frozen, FreezeDuration);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(enemyUnit.HasArmor, Is.True);
        }

        [Test]
        public void Resolve_ArmoredEnemyAdjacentToTwoOccupiedLandings_ReceivesExactlyOneAttempt()
        {
            // GIVEN — regression test: a unit adjacent to both landings of a single resolution must not be
            // stripped by the first coordinate and converted by the second within the same call.
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _origin, hasArmor: true);
            PlaceUnit(LandingUnitIdA, ActingPlayerId, _adjacentCoords);
            PlaceUnit(LandingUnitIdB, ActingPlayerId, _secondAdjacentCoords);
            _affectedCoordinates.Add(_adjacentCoords);
            _affectedCoordinates.Add(_secondAdjacentCoords);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_armorStrippedUnitIds, Is.EqualTo(new[] { enemyUnit.UnitId }));
            Assert.That(_convertedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_EnemyAdjacentOnlyToAVacatedJumpSource_IsUntouched()
        {
            // GIVEN
            PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            PlaceUnit(ActingUnitId, ActingPlayerId, _distantCoords);
            _affectedCoordinates.Add(_origin);
            _affectedCoordinates.Add(_distantCoords);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_convertedUnitIds, Is.Empty);
            Assert.That(_armorStrippedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_FriendlyArmoredNeighbor_IsNeverAttempted()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit friendlyUnit = PlaceUnit(FriendlyUnitId, ActingPlayerId, _adjacentCoords, hasArmor: true);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(friendlyUnit.HasArmor, Is.True);
        }

        [Test]
        public void Resolve_DeadEnemyNeighbor_AppearsInNeitherOutputList()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            enemyUnit.IsAlive = false;
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_convertedUnitIds, Is.Empty);
            Assert.That(_armorStrippedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_LandingWithNoEnemyNeighbors_LeavesBothOutputListsEmpty()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _affectedCoordinates.Add(_origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_convertedUnitIds, Is.Empty);
            Assert.That(_armorStrippedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_CoordinateOutsideGrid_DoesNotThrow()
        {
            // GIVEN
            _affectedCoordinates.Add(_outsideGridCoords);

            // WHEN
            void resolveCall() => Resolve();

            // THEN
            Assert.DoesNotThrow(resolveCall);
        }

        [Test]
        public void Resolve_CoordinateOutsideGrid_LeavesBothOutputListsEmpty()
        {
            // GIVEN
            _affectedCoordinates.Add(_outsideGridCoords);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_convertedUnitIds, Is.Empty);
            Assert.That(_armorStrippedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_BuffersHoldingPreviousContent_ClearsThemBeforeFilling()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _affectedCoordinates.Add(_origin);
            var junkCell = new HexCell(_outsideGridCoords);
            _neighborBuffer.Add(junkCell);
            _attemptedUnitIds.Add(JunkUnitId);
            _convertedUnitIds.Add(JunkConvertedUnitId);
            _armorStrippedUnitIds.Add(JunkArmorStrippedUnitId);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_neighborBuffer, Has.No.Member(junkCell));
            Assert.That(_attemptedUnitIds, Has.No.Member(JunkUnitId));
            Assert.That(_convertedUnitIds, Has.No.Member(JunkConvertedUnitId));
            Assert.That(_armorStrippedUnitIds, Has.No.Member(JunkArmorStrippedUnitId));
        }

        [Test]
        public void Resolve_NullGrid_ThrowsArgumentNullException()
        {
            // GIVEN
            _affectedCoordinates.Add(_origin);

            // WHEN
            void resolveCall() =>
                ConversionResolver.Resolve(
                    null,
                    _units,
                    _affectedCoordinates,
                    ActingPlayerId,
                    _neighborBuffer,
                    _attemptedUnitIds,
                    _convertedUnitIds,
                    _armorStrippedUnitIds
                );

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullUnits_ThrowsArgumentNullException()
        {
            // GIVEN
            _affectedCoordinates.Add(_origin);

            // WHEN
            void resolveCall() =>
                ConversionResolver.Resolve(
                    _grid,
                    null,
                    _affectedCoordinates,
                    ActingPlayerId,
                    _neighborBuffer,
                    _attemptedUnitIds,
                    _convertedUnitIds,
                    _armorStrippedUnitIds
                );

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullAffectedCoordinates_ThrowsArgumentNullException()
        {
            // GIVEN
            // no affected-coordinates buffer to pass

            // WHEN
            void resolveCall() =>
                ConversionResolver.Resolve(_grid, _units, null, ActingPlayerId, _neighborBuffer, _attemptedUnitIds, _convertedUnitIds, _armorStrippedUnitIds);

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullNeighborBuffer_ThrowsArgumentNullException()
        {
            // GIVEN
            _affectedCoordinates.Add(_origin);

            // WHEN
            void resolveCall() =>
                ConversionResolver.Resolve(
                    _grid,
                    _units,
                    _affectedCoordinates,
                    ActingPlayerId,
                    null,
                    _attemptedUnitIds,
                    _convertedUnitIds,
                    _armorStrippedUnitIds
                );

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullAttemptedUnitIds_ThrowsArgumentNullException()
        {
            // GIVEN
            _affectedCoordinates.Add(_origin);

            // WHEN
            void resolveCall() =>
                ConversionResolver.Resolve(
                    _grid,
                    _units,
                    _affectedCoordinates,
                    ActingPlayerId,
                    _neighborBuffer,
                    null,
                    _convertedUnitIds,
                    _armorStrippedUnitIds
                );

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullConvertedUnitIds_ThrowsArgumentNullException()
        {
            // GIVEN
            _affectedCoordinates.Add(_origin);

            // WHEN
            void resolveCall() =>
                ConversionResolver.Resolve(
                    _grid,
                    _units,
                    _affectedCoordinates,
                    ActingPlayerId,
                    _neighborBuffer,
                    _attemptedUnitIds,
                    null,
                    _armorStrippedUnitIds
                );

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullArmorStrippedUnitIds_ThrowsArgumentNullException()
        {
            // GIVEN
            _affectedCoordinates.Add(_origin);

            // WHEN
            void resolveCall() =>
                ConversionResolver.Resolve(_grid, _units, _affectedCoordinates, ActingPlayerId, _neighborBuffer, _attemptedUnitIds, _convertedUnitIds, null);

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        private void Resolve()
        {
            ConversionResolver.Resolve(
                _grid,
                _units,
                _affectedCoordinates,
                ActingPlayerId,
                _neighborBuffer,
                _attemptedUnitIds,
                _convertedUnitIds,
                _armorStrippedUnitIds
            );
        }

        private GridUnit PlaceUnit(int unitId, int playerId, HexCoordinates position, bool hasArmor = false)
        {
            CardId cardId = hasArmor ? new CardId(ArmoredCardIdValue) : new CardId(SourceCardIdValue);
            var unit = new GridUnit(unitId, playerId, cardId, position, hasArmor);
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
    }
}
