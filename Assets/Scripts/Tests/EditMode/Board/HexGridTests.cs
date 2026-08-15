using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class HexGridTests
    {
        [Test]
        public void HexGrid_Generation_Creates61TilesForRadius4()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };

            // WHEN
            var grid = new HexGrid(mockLayout);

            // THEN
            Assert.That(grid.Cells.Count, Is.EqualTo(61));
        }

        [Test]
        public void Constructor_WithPositiveRadius_ContainsCenterCell()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);

            // WHEN
            bool hasCell = grid.TryGetCell(new HexCoordinates(0, 0), out HexCell centerCell);

            // THEN
            Assert.That(hasCell, Is.True);
            Assert.That(centerCell, Is.Not.Null);
            Assert.That(centerCell.IsBlocked, Is.False);
        }

        [Test]
        public void HexGrid_RadiusBounds_TryGetCellFailsOutside()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);

            // WHEN
            bool insideCoords1 = grid.TryGetCell(new HexCoordinates(4, -4), out _);
            bool insideCoords2 = grid.TryGetCell(new HexCoordinates(-4, 0), out _);
            bool outsideCoords1 = grid.TryGetCell(new HexCoordinates(5, -5), out _);
            bool outsideCoords2 = grid.TryGetCell(new HexCoordinates(0, 5), out _);

            // THEN
            Assert.That(insideCoords1, Is.True);
            Assert.That(insideCoords2, Is.True);
            Assert.That(outsideCoords1, Is.False);
            Assert.That(outsideCoords2, Is.False);
        }

        [Test]
        public void HexGrid_BlockedTiles_AreFlaggedAsBlocked()
        {
            // GIVEN
            var blockedCoords = new HashSet<HexCoordinates> { new(1, 0), new(-2, 2) };
            var mockLayout = new FakeGridLayout { GridRadius = 4, BlockedCoordinates = new ReadOnlySet<HexCoordinates>(blockedCoords) };
            var grid = new HexGrid(mockLayout);

            // WHEN
            grid.TryGetCell(new HexCoordinates(1, 0), out HexCell cell1);
            grid.TryGetCell(new HexCoordinates(-2, 2), out HexCell cell2);
            grid.TryGetCell(new HexCoordinates(0, 0), out HexCell cell3);

            // THEN
            Assert.That(cell1.IsBlocked, Is.True);
            Assert.That(cell2.IsBlocked, Is.True);
            Assert.That(cell3.IsBlocked, Is.False);
        }

        [Test]
        public void GetNeighbors_CenterVSBorder_CountsCorrect()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetNeighbors(new HexCoordinates(0, 0), results);
            int centerNeighborsCount = results.Count;

            grid.GetNeighbors(new HexCoordinates(4, -4), results);
            int cornerNeighborsCount = results.Count;

            grid.GetNeighbors(new HexCoordinates(4, -2), results);
            int edgeNeighborsCount = results.Count;

            // THEN
            Assert.That(centerNeighborsCount, Is.EqualTo(6));
            Assert.That(cornerNeighborsCount, Is.EqualTo(3));
            Assert.That(edgeNeighborsCount, Is.EqualTo(4));
        }

        [Test]
        public void GetRing_Radius4Border_Returns24Tiles()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(0, 0), 4, results);

            // THEN
            Assert.That(results.Count, Is.EqualTo(24));
        }

        [Test]
        public void GetNeighbors_OnRepeatedCalls_DoesNotAllocate()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var center = new HexCoordinates(0, 0);
            var results = new List<HexCell>(6);

            grid.GetNeighbors(center, results);

            // WHEN
            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                grid.GetNeighbors(center, results);
            }

            long endAlloc = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(endAlloc - startAlloc, Is.EqualTo(0), "GetNeighbors allocated memory on hot path!");
        }

        [Test]
        public void GetRingCells_OnRepeatedCalls_DoesNotAllocate()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var center = new HexCoordinates(0, 0);
            var results = new List<HexCell>(24);

            grid.GetRingCells(center, 2, results);

            // WHEN
            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                grid.GetRingCells(center, 2, results);
            }

            long endAlloc = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(endAlloc - startAlloc, Is.EqualTo(0), "GetRingCells allocated memory on hot path!");
        }

        [Test]
        public void GetSpiral_Radius4_Returns61Tiles()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetSpiralCells(new HexCoordinates(0, 0), 4, results);

            // THEN
            Assert.That(results.Count, Is.EqualTo(61));
        }

        [Test]
        public void GetSpiralCells_OnRepeatedCalls_DoesNotAllocate()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var center = new HexCoordinates(0, 0);
            var results = new List<HexCell>(61);

            grid.GetSpiralCells(center, 4, results);

            // WHEN
            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                grid.GetSpiralCells(center, 4, results);
            }

            long endAlloc = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(endAlloc - startAlloc, Is.EqualTo(0), "GetSpiralCells allocated memory on hot path!");
        }

        [Test]
        public void GetRing_Radius0_ReturnsCenterCell()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(0, 0), 0, results);

            // THEN
            Assert.That(results.Count, Is.EqualTo(1), "GetRingCells with radius 0 should return exactly the center cell.");
        }

        [Test]
        public void GetRing_NegativeRadius_ReturnsEmptyList()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(0, 0), -1, results);

            // THEN
            Assert.That(results.Count, Is.EqualTo(0), "GetRingCells with negative radius should return no cells.");
        }

        [Test]
        public void GetSpiral_NegativeRadius_ReturnsEmptyList()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetSpiralCells(new HexCoordinates(0, 0), -1, results);

            // THEN
            Assert.That(results.Count, Is.EqualTo(0), "GetSpiralCells with negative radius should return no cells.");
        }

        private class FakeGridLayout : IGridLayout
        {
            public int GridRadius { get; set; } = 4;
            public IReadOnlySet<HexCoordinates> BlockedCoordinates { get; set; } = new ReadOnlySet<HexCoordinates>(new HashSet<HexCoordinates>());
        }

        [Test]
        public void Constructor_NullGridLayout_ThrowsArgumentNullException()
        {
            // GIVEN
            // WHEN
            // THEN
            Assert.Throws<ArgumentNullException>(() => _ = new HexGrid(null));
        }

        [Test]
        public void GetNeighbors_NullResults_ThrowsArgumentNullException()
        {
            // GIVEN
            var grid = new HexGrid(new FakeGridLayout());

            // WHEN
            // THEN
            Assert.Throws<ArgumentNullException>(() => grid.GetNeighbors(new HexCoordinates(0, 0), null));
        }

        [Test]
        public void GetRingCells_NullResults_ThrowsArgumentNullException()
        {
            // GIVEN
            var grid = new HexGrid(new FakeGridLayout());

            // WHEN
            // THEN
            Assert.Throws<ArgumentNullException>(() => grid.GetRingCells(new HexCoordinates(0, 0), 1, null));
        }

        [Test]
        public void GetSpiralCells_NullResults_ThrowsArgumentNullException()
        {
            // GIVEN
            var grid = new HexGrid(new FakeGridLayout());

            // WHEN
            // THEN
            Assert.Throws<ArgumentNullException>(() => grid.GetSpiralCells(new HexCoordinates(0, 0), 1, null));
        }

        [Test]
        public void HexGrid_Radius0_GeneratesExactlyOneCell()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 0 };

            // WHEN
            var grid = new HexGrid(mockLayout);

            // THEN
            Assert.That(grid.Cells.Count, Is.EqualTo(1));
            Assert.That(grid.TryGetCell(new HexCoordinates(0, 0), out _), Is.True);
        }

        [Test]
        public void GetRingCells_Radius1_ReturnsSixCells()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(0, 0), 1, results);

            // THEN
            Assert.That(results.Count, Is.EqualTo(6));
        }

        [Test]
        public void GetSpiralCells_Radius0_ReturnsCenterOnly()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetSpiralCells(new HexCoordinates(0, 0), 0, results);

            // THEN
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Coordinates, Is.EqualTo(new HexCoordinates(0, 0)));
        }

        [Test]
        public void GetRingCells_OffCenterAtCorner_ReturnsOnlyInBoundsNeighbors()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(4, -4), 1, results);

            // THEN
            Assert.That(results.Count, Is.EqualTo(3));
        }

        [Test]
        public void GetSpiralCells_OffCenterOrigin_ReturnsCorrectCount()
        {
            // GIVEN
            var mockLayout = new FakeGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetSpiralCells(new HexCoordinates(2, -2), 2, results);

            // THEN
            Assert.That(results, Is.Not.Empty, "Spiral from off-center should return at least one cell.");
            Assert.That(results, Is.All.Matches<HexCell>(cell => grid.TryGetCell(cell.Coordinates, out _)), "Every spiral cell must exist in the grid.");
        }
    }
}
