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
    public class AbilityTargetValidatorTests
    {
        private const int BoardRadius = 4;
        private const int ClusterSize = 3;
        private const int WiderClusterSize = 4;
        private const int ClusterRadius = 1;
        private const int StatusDuration = 1;
        private const int OccupantUnitId = 1;

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _adjacent = new(1, 0);
        private static readonly HexCoordinates _secondAdjacent = new(0, 1);
        private static readonly HexCoordinates _thirdAdjacent = new(-1, 0);
        private static readonly HexCoordinates _twoHexesAway = new(2, 0);
        private static readonly HexCoordinates _offBoard = new(9, 0);

        private HexGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new HexGrid(new FakeGridLayout());
        }

        [Test]
        public void ValidateTargets_AClusterMatchingTheAuthoredSize_ReturnsTrue()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _adjacent, _secondAdjacent };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void ValidateTargets_AWiderClusterMatchingItsAuthoredSize_ReturnsTrue()
        {
            // GIVEN — proves the rule is authored data alone: Sterilization Beam widens the cluster to four and
            // needs no new code, only (4, 1).
            var targets = new List<HexCoordinates> { _origin, _adjacent, _secondAdjacent, _thirdAdjacent };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(WiderClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void ValidateTargets_FewerTargetsThanTheAuthoredClusterSize_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _adjacent };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_MoreTargetsThanTheAuthoredClusterSize_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _adjacent, _secondAdjacent, _thirdAdjacent };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_ATargetBeyondTheAuthoredRadiusOfTheCentre_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _adjacent, _twoHexesAway };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN — the first hex is the centre by definition, and every other is measured against it.
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_ATargetBeyondTheAuthoredRadiusOfTheCentreOnly_ReturnsTrueWhenTheCentreIsBetweenThem()
        {
            // GIVEN — the endpoints are two hexes apart, but each is one hex from the centre listed first.
            var targets = new List<HexCoordinates> { _adjacent, _origin, _twoHexesAway };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void ValidateTargets_ARepeatedTarget_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _adjacent, _adjacent };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_ATargetOffTheBoard_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _adjacent, _offBoard };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, BoardRadius * 2), _grid);

            // THEN — a generous radius, so membership of the board is the only rule left to fail on.
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_ACentreOffTheBoard_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _offBoard, new(8, 0), new(9, -1) };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_EverySectorEmpty_ReturnsTrue()
        {
            // GIVEN — occupancy is not among the rules and is never read. The machine player depends on it: it
            // aims a Protocol at a cluster drawn from the whole board, most of which holds nothing.
            var targets = new List<HexCoordinates> { _origin, _adjacent, _secondAdjacent };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void ValidateTargets_AnOccupiedSectorAmongTheTargets_ReturnsTrue()
        {
            // GIVEN — the same rule read the other way: occupancy neither qualifies nor disqualifies a target.
            OccupyCell(_adjacent);
            var targets = new List<HexCoordinates> { _origin, _adjacent, _secondAdjacent };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void ValidateTargets_AnAuthoredClusterSizeOfZero_ReturnsFalse()
        {
            // GIVEN — zero means "no cap" on a troop impact, but a Protocol with no target count is unauthored,
            // and accepting any number of hexes for it would be worse than refusing it.
            var targets = new List<HexCoordinates> { _origin };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(0, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_NullTargets_ReturnsFalse()
        {
            // GIVEN

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(null, CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_AnEmptyTargetList_ReturnsFalse()
        {
            // GIVEN

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(new List<HexCoordinates>(), CreateEffect(ClusterSize, ClusterRadius), _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_ANullGrid_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _adjacent, _secondAdjacent };

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(targets, CreateEffect(ClusterSize, ClusterRadius), null);

            // THEN
            Assert.That(isValid, Is.False);
        }

        private static ImpactEffect CreateEffect(int clusterSize, int radius)
        {
            return new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, radius, StatusDuration, TargetFilter.All, clusterSize);
        }

        private void OccupyCell(HexCoordinates coordinates)
        {
            Assert.That(_grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test setup expects {coordinates} to exist on the grid.");
            cell.SetOccupant(OccupantUnitId);
        }

        private sealed class FakeGridLayout : IGridLayout
        {
            public int GridRadius { get; } = BoardRadius;

            public IReadOnlySet<HexCoordinates> BlockedCoordinates { get; } = new ReadOnlySet<HexCoordinates>(new HashSet<HexCoordinates>());
        }
    }
}
