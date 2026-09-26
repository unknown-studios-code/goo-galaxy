using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Input.Services;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Tests.Utils;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Tests.EditMode.Input
{
    [TestFixture]
    public class ClusterTargetBuilderTests
    {
        private const float CellVisualSize = 1f;
        private const int GridRadius = 4;
        private const int FreezeDuration = 1;
        private const int AllocationIterations = 500;

        // Pointer positions near (0,0) at CellVisualSize 1: the East neighbour's projected centre, and a point on
        // East's column leaning toward North-East — written as literals so nothing here re-derives the projection.
        private static readonly Vector2 _eastWorldPosition = new(1.5f, 0.8660254f);
        private static readonly Vector2 _northEastLeaningWorldPosition = new(1.5f, 0.5f);

        // On the West side of the centre, equidistant from South-West and West at CellVisualSize 1 — the mirror
        // of _eastWorldPosition's own pair, used to force a tie the far side of a shift. See
        // TryArrange_TiedNeighbourSurvivesAnEarlierShift_KeepsSpiralOrder for the worked distances.
        private static readonly Vector2 _westLeaningWorldPosition = new(-1f, 0f);

        private static readonly HexCoordinates _centre = new(0, 0);
        private static readonly HexCoordinates _east = new(1, 0);
        private static readonly HexCoordinates _northEast = new(1, -1);
        private static readonly HexCoordinates _southEast = new(0, 1);
        private static readonly HexCoordinates _southWest = new(-1, 1);
        private static readonly HexCoordinates _west = new(-1, 0);
        private static readonly HexCoordinates _offBoard = new(20, 20);

        private readonly List<HexCell> _cellScratch = new();
        private readonly List<HexCoordinates> _cluster = new();

        [Test]
        public void TryBuild_ArrangedClusterValidForItsOwnEffect_ReturnsTrueWithCentreFirst()
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            var effects = new List<ImpactEffect> { new(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 2) };

            // WHEN
            bool wasBuilt = ClusterTargetBuilder.TryBuild(grid, effects, _centre, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasBuilt, Is.True);
            Assert.That(_cluster, Is.EqualTo(new List<HexCoordinates> { _centre, _east }));
        }

        [Test]
        [Category("Allocation")]
        public void TryBuild_RepeatedCallsAtTheWidestAuthoredClusterAndRadius_AllocatesNoManagedMemory()
        {
            // GIVEN — ClusterSize 4 is BoardMetrics.MaxSpellClusterSize and Radius 2 the widest an impact spawns
            // its candidates from, so the scratch buffer is pre-sized to BoardMetrics.MaxImpactAreaCells — the
            // same capacity MatchInputController sizes its own scratch buffer to for this reason — and the
            // measured delegate never grows it. Warmed once outside the measured delegate, so the constraint sees
            // only the repeated calls it exists to prove are free.
            HexGrid grid = BuildGrid();
            var effects = new List<ImpactEffect> { new(ImpactEffectType.ApplyStatus, StatusType.Frozen, 2, FreezeDuration, TargetFilter.All, 4) };
            var cellScratch = new List<HexCell>(BoardMetrics.MaxImpactAreaCells);
            var cluster = new List<HexCoordinates>(BoardMetrics.MaxSpellClusterSize);
            ClusterTargetBuilder.TryBuild(grid, effects, _centre, _eastWorldPosition, CellVisualSize, cellScratch, cluster);

            // WHEN / THEN
            Assert.That(
                () =>
                {
                    for (int i = 0; i < AllocationIterations; i++)
                    {
                        ClusterTargetBuilder.TryBuild(grid, effects, _centre, _eastWorldPosition, CellVisualSize, cellScratch, cluster);
                    }
                },
                new AllocatesNothingConstraint()
            );
        }

        [Test]
        public void TryBuild_ArrangementRejectedByASecondEffectsSmallerRadius_ReturnsFalse()
        {
            // GIVEN — the cluster arranges fine against the first effect's radius of 1, but the second effect's
            // radius of 0 puts the east hex out of range, so the whole build must fail.
            HexGrid grid = BuildGrid();
            var effects = new List<ImpactEffect>
            {
                new(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 2),
                new(ImpactEffectType.ApplyStatus, StatusType.Rooted, 0, FreezeDuration, TargetFilter.All, 2),
            };

            // WHEN
            bool wasBuilt = ClusterTargetBuilder.TryBuild(grid, effects, _centre, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasBuilt, Is.False);
        }

        [Test]
        public void TryBuild_NullLandingEffects_ReturnsFalseAndEmptyCluster()
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            _cluster.Add(_offBoard);

            // WHEN
            bool wasBuilt = ClusterTargetBuilder.TryBuild(grid, null, _centre, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasBuilt, Is.False);
            Assert.That(_cluster, Is.Empty);
        }

        [Test]
        public void TryBuild_EmptyLandingEffects_ReturnsFalseAndEmptyCluster()
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            var effects = new List<ImpactEffect>();

            // WHEN
            bool wasBuilt = ClusterTargetBuilder.TryBuild(grid, effects, _centre, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasBuilt, Is.False);
            Assert.That(_cluster, Is.Empty);
        }

        [Test]
        public void TryArrange_ClusterSizeOne_ReturnsCentreOnly()
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 1);

            // WHEN
            bool wasArranged = ClusterTargetBuilder.TryArrange(grid, effect, _centre, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasArranged, Is.True);
            Assert.That(_cluster, Is.EqualTo(new List<HexCoordinates> { _centre }));
        }

        [Test]
        public void TryArrange_PointerNearestOneNeighbour_PicksThatNeighbourSecond()
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 2);

            // WHEN
            bool wasArranged = ClusterTargetBuilder.TryArrange(grid, effect, _centre, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasArranged, Is.True);
            Assert.That(_cluster, Is.EqualTo(new List<HexCoordinates> { _centre, _east }));
        }

        [Test]
        public void TryArrange_PointerLeaningTowardTwoNeighbours_OrdersThemNearestFirst()
        {
            // GIVEN — the pointer sits closest to East, then North-East, then South-East, so a cluster of four
            // picks those three in that order and never a far-side neighbour.
            HexGrid grid = BuildGrid();
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 4);

            // WHEN
            bool wasArranged = ClusterTargetBuilder.TryArrange(grid, effect, _centre, _northEastLeaningWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasArranged, Is.True);
            Assert.That(_cluster, Is.EqualTo(new List<HexCoordinates> { _centre, _east, _northEast, _southEast }));
        }

        [Test]
        public void TryArrange_PointerEquidistantFromEveryNeighbour_KeepsSpiralOrder()
        {
            // GIVEN — the pointer rests exactly on the centre, which is the same distance from every one of its
            // six neighbours, so the tie-break must fall back to HexGrid.GetSpiralCells's own order.
            HexGrid grid = BuildGrid();
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 3);

            // WHEN
            bool wasArranged = ClusterTargetBuilder.TryArrange(grid, effect, _centre, Vector2.zero, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasArranged, Is.True);
            Assert.That(_cluster, Is.EqualTo(new List<HexCoordinates> { _centre, _southWest, _southEast }));
        }

        [Test]
        public void TryArrange_TiedNeighbourSurvivesAnEarlierShift_KeepsSpiralOrder()
        {
            // GIVEN — at this pointer, South-West and West tie for nearest (South-West wins, found first, with no
            // shift needed since it is already the first candidate). The second pick then has West alone nearest,
            // four spiral places away, which shifts South-East, East and North-East each one place up to make
            // room for it. The third pick ties South-East against North-West — a stable sort must still prefer
            // South-East, because it led North-West in the original spiral, even though South-East has by now
            // been shifted forward twice and no longer sits anywhere near its original index. A swap-based sort
            // would instead have carried North-West ahead of it on the first shift and broken the tie the other way.
            HexGrid grid = BuildGrid();
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 4);

            // WHEN
            bool wasArranged = ClusterTargetBuilder.TryArrange(grid, effect, _centre, _westLeaningWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasArranged, Is.True);
            Assert.That(_cluster, Is.EqualTo(new List<HexCoordinates> { _centre, _southWest, _west, _southEast }));
        }

        [Test]
        public void TryArrange_NullGrid_ReturnsFalseAndClearsAPrePopulatedCluster()
        {
            // GIVEN
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 2);
            _cluster.Add(_offBoard);

            // WHEN
            bool wasArranged = ClusterTargetBuilder.TryArrange(null, effect, _centre, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasArranged, Is.False);
            Assert.That(_cluster, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryArrange_NonPositiveClusterSize_ReturnsFalseAndEmptyCluster(int clusterSize)
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, clusterSize);

            // WHEN
            bool wasArranged = ClusterTargetBuilder.TryArrange(grid, effect, _centre, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasArranged, Is.False);
            Assert.That(_cluster, Is.Empty);
        }

        [Test]
        public void TryArrange_CentreOffBoard_ReturnsFalseAndEmptyCluster()
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 2);

            // WHEN
            bool wasArranged = ClusterTargetBuilder.TryArrange(grid, effect, _offBoard, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasArranged, Is.False);
            Assert.That(_cluster, Is.Empty);
        }

        [Test]
        public void TryArrange_TooFewCellsWithinTheAuthoredRadius_ReturnsFalseAndEmptyCluster()
        {
            // GIVEN — a radius of zero offers only the centre cell, which cannot supply the one neighbour a
            // cluster size of two needs.
            HexGrid grid = BuildGrid();
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, FreezeDuration, TargetFilter.All, 2);

            // WHEN
            bool wasArranged = ClusterTargetBuilder.TryArrange(grid, effect, _centre, _eastWorldPosition, CellVisualSize, _cellScratch, _cluster);

            // THEN
            Assert.That(wasArranged, Is.False);
            Assert.That(_cluster, Is.Empty);
        }

        [Test]
        public void AreTargetsValidForEveryImpact_EveryEffectAccepts_ReturnsTrue()
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            var cluster = new List<HexCoordinates> { _centre, _east };
            var effects = new List<ImpactEffect> { new(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 2) };

            // WHEN
            bool areValid = ClusterTargetBuilder.AreTargetsValidForEveryImpact(cluster, effects, grid);

            // THEN
            Assert.That(areValid, Is.True);
        }

        [Test]
        public void AreTargetsValidForEveryImpact_SecondEffectHasASmallerRadius_ReturnsFalse()
        {
            // GIVEN — the same cluster passes the first effect's radius of 1 but fails the second's radius of 0,
            // since the east hex sits one ring out from the centre.
            HexGrid grid = BuildGrid();
            var cluster = new List<HexCoordinates> { _centre, _east };
            var effects = new List<ImpactEffect>
            {
                new(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 2),
                new(ImpactEffectType.ApplyStatus, StatusType.Rooted, 0, FreezeDuration, TargetFilter.All, 2),
            };

            // WHEN
            bool areValid = ClusterTargetBuilder.AreTargetsValidForEveryImpact(cluster, effects, grid);

            // THEN
            Assert.That(areValid, Is.False);
        }

        [Test]
        public void AreTargetsValidForEveryImpact_NullLandingEffects_ReturnsFalse()
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            var cluster = new List<HexCoordinates> { _centre, _east };

            // WHEN
            bool areValid = ClusterTargetBuilder.AreTargetsValidForEveryImpact(cluster, null, grid);

            // THEN
            Assert.That(areValid, Is.False);
        }

        [Test]
        public void AreTargetsValidForEveryImpact_EmptyLandingEffects_ReturnsFalse()
        {
            // GIVEN
            HexGrid grid = BuildGrid();
            var cluster = new List<HexCoordinates> { _centre, _east };

            // WHEN
            bool areValid = ClusterTargetBuilder.AreTargetsValidForEveryImpact(cluster, new List<ImpactEffect>(), grid);

            // THEN
            Assert.That(areValid, Is.False);
        }

        private static HexGrid BuildGrid()
        {
            return new HexGrid(new FakeGridLayout { GridRadius = GridRadius });
        }

        private sealed class FakeGridLayout : IGridLayout
        {
            public int GridRadius { get; set; } = 4;

            public IReadOnlySet<HexCoordinates> BlockedCoordinates { get; set; } = new ReadOnlySet<HexCoordinates>(new HashSet<HexCoordinates>());
        }
    }
}
