using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class HexMathUtilsTests
    {
        private const float Size = 1f;
        private const float Tolerance = 0.0001f;

        // The board radius the round-trip and out-of-bounds cases below are checked against, matching
        // BoardMetrics.DefaultGridRadius.
        private const int TestBoardRadius = 4;

        [Test]
        public void ProjectToWorldSpace_Origin_ReturnsZero()
        {
            // GIVEN
            var origin = new HexCoordinates(0, 0);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(origin, Size);

            // THEN
            Assert.That(result, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ProjectToWorldSpace_ZCoordinate_IsAlwaysZero()
        {
            // GIVEN
            var coords = new HexCoordinates(2, -1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(coords, Size);

            // THEN
            Assert.That(result.z, Is.EqualTo(0f).Within(Tolerance), "Z axis should always be 0 (XY plane layout).");
        }

        [Test]
        public void ProjectToWorldSpace_East_CorrectPosition()
        {
            // GIVEN
            var east = new HexCoordinates(1, 0);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(east, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(1.5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(Mathf.Sqrt(3f) * 0.5f).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_West_CorrectPosition()
        {
            // GIVEN
            var west = new HexCoordinates(-1, 0);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(west, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(-1.5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(-Mathf.Sqrt(3f) * 0.5f).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_SouthEast_CorrectPosition()
        {
            // GIVEN
            var se = new HexCoordinates(0, 1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(se, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(Mathf.Sqrt(3f)).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_NorthEast_CorrectPosition()
        {
            // GIVEN
            var ne = new HexCoordinates(1, -1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(ne, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(1.5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(-Mathf.Sqrt(3f) * 0.5f).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_NorthWest_CorrectPosition()
        {
            // GIVEN
            var nw = new HexCoordinates(0, -1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(nw, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(-Mathf.Sqrt(3f)).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_SouthWest_CorrectPosition()
        {
            // GIVEN
            var sw = new HexCoordinates(-1, 1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(sw, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(-1.5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(Mathf.Sqrt(3f) * 0.5f).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_SizeZero_ReturnsZero()
        {
            // GIVEN
            var coords = new HexCoordinates(3, -1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(coords, 0f);

            // THEN
            Assert.That(result, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ProjectToWorldSpace_WithLargerCellSize_ScalesPositionProportionally()
        {
            // GIVEN
            var east = new HexCoordinates(1, 0);
            float size2 = 2f;

            // WHEN
            Vector3 result1 = HexMathUtils.ProjectToWorldSpace(east, Size);
            Vector3 result2 = HexMathUtils.ProjectToWorldSpace(east, size2);

            // THEN
            Assert.That(result2.x, Is.EqualTo(result1.x * size2).Within(Tolerance));
            Assert.That(result2.y, Is.EqualTo(result1.y * size2).Within(Tolerance));
        }

        [Test]
        public void ProjectToAxial_NegativeSize_ReturnsZero()
        {
            // GIVEN
            var worldPosition = new Vector3(3f, 2f, 0f);

            // WHEN
            Vector2 result = HexMathUtils.ProjectToAxial(worldPosition, -1f);

            // THEN
            Assert.That(result, Is.EqualTo(Vector2.zero));
        }

        [TestCaseSource(nameof(Radius4BoardCoordinates))]
        public void ProjectToAxial_ThenRoundToAxial_RoundTripsEveryRadius4CellBackToItself(HexCoordinates coordinate)
        {
            // GIVEN
            Vector3 worldPosition = HexMathUtils.ProjectToWorldSpace(coordinate, Size);

            // WHEN
            Vector2 axial = HexMathUtils.ProjectToAxial(worldPosition, Size);
            HexCoordinates result = HexMathUtils.RoundToAxial(axial.x, axial.y);

            // THEN
            Assert.That(result, Is.EqualTo(coordinate));
        }

        // (0, 0), (1, 0) and (0, 1) are mutually adjacent — see HexDirection.E and HexDirection.SE — so their
        // shared corner sits at the axial centroid (1/3, 1/3). Nudging away from that centroid along one axis
        // only, rather than along the diagonal, breaks the q/r tie asymmetrically and forces RoundToAxial's
        // largest-delta correction to run: a per-axis rounding with no cube constraint would answer (0, 0) for
        // both, since neither q nor r alone crosses a whole-number boundary this close to the centroid.
        [TestCase(0.4333f, 0.3333f, 1, 0)]
        [TestCase(0.3333f, 0.4333f, 0, 1)]
        public void RoundToAxial_NudgedTowardASharedVertex_ReturnsTheNeighborTheNudgeLeansToward(float q, float r, int expectedQ, int expectedR)
        {
            // GIVEN

            // WHEN
            HexCoordinates result = HexMathUtils.RoundToAxial(q, r);

            // THEN
            Assert.That(result, Is.EqualTo(new HexCoordinates(expectedQ, expectedR)));
        }

        [Test]
        public void RoundToAxial_PointWellOutsideRing4_ReturnsACoordinateTheGridRejects()
        {
            // GIVEN
            HexGrid grid = BuildRadius4Grid();
            var farCoordinate = new HexCoordinates(10, 10);
            Vector3 worldPosition = HexMathUtils.ProjectToWorldSpace(farCoordinate, Size);

            // WHEN
            Vector2 axial = HexMathUtils.ProjectToAxial(worldPosition, Size);
            HexCoordinates result = HexMathUtils.RoundToAxial(axial.x, axial.y);

            // THEN
            Assert.That(grid.TryGetCell(result, out _), Is.False);
        }

        // A hex disk of TestBoardRadius rings, generated from the same area formula HexGrid.GenerateGrid builds
        // its board from — reproduced here as known coordinates, not as a re-derivation of the code under test,
        // since HexMathUtils itself never enumerates a board.
        private static IEnumerable<HexCoordinates> Radius4BoardCoordinates()
        {
            for (int q = -TestBoardRadius; q <= TestBoardRadius; q++)
            {
                int r1 = Mathf.Max(-TestBoardRadius, -q - TestBoardRadius);
                int r2 = Mathf.Min(TestBoardRadius, -q + TestBoardRadius);

                for (int r = r1; r <= r2; r++)
                {
                    yield return new HexCoordinates(q, r);
                }
            }
        }

        private static HexGrid BuildRadius4Grid()
        {
            return new HexGrid(new FakeGridLayout(TestBoardRadius));
        }

        private sealed class FakeGridLayout : IGridLayout
        {
            private static readonly IReadOnlySet<HexCoordinates> _noBlockedCoordinates = new ReadOnlySet<HexCoordinates>(new HashSet<HexCoordinates>());

            public FakeGridLayout(int gridRadius)
            {
                GridRadius = gridRadius;
            }

            public int GridRadius { get; }

            public IReadOnlySet<HexCoordinates> BlockedCoordinates => _noBlockedCoordinates;
        }
    }
}
