using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Board
{
    [TestFixture]
    public class HexGridTests
    {
        [Test]
        public void HexGrid_Generation_Creates61TilesForRadius4()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };

            // WHEN
            var grid = new HexGrid(mockLayout);

            // THEN
            Assert.AreEqual(61, grid.Cells.Count);
        }

        [Test]
        public void HexGrid_ContainsCenterCell()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);

            // WHEN
            bool hasCell = grid.TryGetCell(new HexCoordinates(0, 0), out HexCell centerCell);

            // THEN
            Assert.IsTrue(hasCell);
            Assert.IsNotNull(centerCell);
            Assert.IsFalse(centerCell.IsBlocked);
        }

        [Test]
        public void HexGrid_RadiusBounds_TryGetCellFailsOutside()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);

            // WHEN
            bool insideCoords1 = grid.TryGetCell(new HexCoordinates(4, -4), out _);
            bool insideCoords2 = grid.TryGetCell(new HexCoordinates(-4, 0), out _);
            bool outsideCoords1 = grid.TryGetCell(new HexCoordinates(5, -5), out _);
            bool outsideCoords2 = grid.TryGetCell(new HexCoordinates(0, 5), out _);

            // THEN
            Assert.IsTrue(insideCoords1);
            Assert.IsTrue(insideCoords2);
            Assert.IsFalse(outsideCoords1);
            Assert.IsFalse(outsideCoords2);
        }

        [Test]
        public void HexGrid_BlockedTiles_AreFlaggedAsBlocked()
        {
            // GIVEN
            var blockedCoords = new HashSet<HexCoordinates> { new(1, 0), new(-2, 2) };
            var mockLayout = new MockGridLayout { GridRadius = 4, BlockedCoordinates = new ReadOnlySet<HexCoordinates>(blockedCoords) };
            var grid = new HexGrid(mockLayout);

            // WHEN
            grid.TryGetCell(new HexCoordinates(1, 0), out HexCell cell1);
            grid.TryGetCell(new HexCoordinates(-2, 2), out HexCell cell2);
            grid.TryGetCell(new HexCoordinates(0, 0), out HexCell cell3);

            // THEN
            Assert.IsTrue(cell1.IsBlocked);
            Assert.IsTrue(cell2.IsBlocked);
            Assert.IsFalse(cell3.IsBlocked);
        }

        [Test]
        public void GetNeighbors_CenterVSBorder_CountsCorrect()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
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
            Assert.AreEqual(6, centerNeighborsCount);
            Assert.AreEqual(3, cornerNeighborsCount);
            Assert.AreEqual(4, edgeNeighborsCount);
        }

        [Test]
        public void GetRing_Radius4Border_Returns24Tiles()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(0, 0), 4, results);

            // THEN
            Assert.AreEqual(24, results.Count);
        }

        [Test]
        public void GetNeighbors_DoesNotAllocate()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
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
            Assert.AreEqual(0, endAlloc - startAlloc, "GetNeighbors allocated memory on hot path!");
        }

        [Test]
        public void GetRing_DoesNotAllocate()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
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
            Assert.AreEqual(0, endAlloc - startAlloc, "GetRingCells allocated memory on hot path!");
        }

        [Test]
        public void GetSpiral_Radius4_Returns61Tiles()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetSpiralCells(new HexCoordinates(0, 0), 4, results);

            // THEN
            Assert.AreEqual(61, results.Count);
        }

        [Test]
        public void GetSpiral_DoesNotAllocate()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
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
            Assert.AreEqual(0, endAlloc - startAlloc, "GetSpiralCells allocated memory on hot path!");
        }

        [Test]
        public void GetRing_Radius0_ReturnsCenterCell()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(0, 0), 0, results);

            // THEN
            Assert.AreEqual(1, results.Count, "GetRingCells with radius 0 should return exactly the center cell.");
        }

        [Test]
        public void GetRing_NegativeRadius_ReturnsEmptyList()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(0, 0), -1, results);

            // THEN
            Assert.AreEqual(0, results.Count, "GetRingCells with negative radius should return no cells.");
        }

        [Test]
        public void GetSpiral_NegativeRadius_ReturnsEmptyList()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetSpiralCells(new HexCoordinates(0, 0), -1, results);

            // THEN
            Assert.AreEqual(0, results.Count, "GetSpiralCells with negative radius should return no cells.");
        }

        private class MockGridLayout : IGridLayout
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
            var grid = new HexGrid(new MockGridLayout());

            // WHEN
            // THEN
            Assert.Throws<ArgumentNullException>(() => grid.GetNeighbors(new HexCoordinates(0, 0), null));
        }

        [Test]
        public void GetRingCells_NullResults_ThrowsArgumentNullException()
        {
            // GIVEN
            var grid = new HexGrid(new MockGridLayout());

            // WHEN
            // THEN
            Assert.Throws<ArgumentNullException>(() => grid.GetRingCells(new HexCoordinates(0, 0), 1, null));
        }

        [Test]
        public void GetSpiralCells_NullResults_ThrowsArgumentNullException()
        {
            // GIVEN
            var grid = new HexGrid(new MockGridLayout());

            // WHEN
            // THEN
            Assert.Throws<ArgumentNullException>(() => grid.GetSpiralCells(new HexCoordinates(0, 0), 1, null));
        }

        [Test]
        public void HexGrid_Radius0_GeneratesExactlyOneCell()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 0 };

            // WHEN
            var grid = new HexGrid(mockLayout);

            // THEN
            Assert.AreEqual(1, grid.Cells.Count);
            Assert.IsTrue(grid.TryGetCell(new HexCoordinates(0, 0), out _));
        }

        [Test]
        public void GetRingCells_Radius1_ReturnsSixCells()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(0, 0), 1, results);

            // THEN
            Assert.AreEqual(6, results.Count);
        }

        [Test]
        public void GetSpiralCells_Radius0_ReturnsCenterOnly()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetSpiralCells(new HexCoordinates(0, 0), 0, results);

            // THEN
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(new HexCoordinates(0, 0), results[0].Coordinates);
        }

        [Test]
        public void GetRingCells_OffCenterAtCorner_ReturnsOnlyInBoundsNeighbors()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetRingCells(new HexCoordinates(4, -4), 1, results);

            // THEN
            Assert.AreEqual(3, results.Count);
        }

        [Test]
        public void GetSpiralCells_OffCenterOrigin_ReturnsCorrectCount()
        {
            // GIVEN
            var mockLayout = new MockGridLayout { GridRadius = 4 };
            var grid = new HexGrid(mockLayout);
            var results = new List<HexCell>();

            // WHEN
            grid.GetSpiralCells(new HexCoordinates(2, -2), 2, results);

            // THEN
            Assert.Greater(results.Count, 0, "Spiral from off-center should return at least one cell.");
            foreach (HexCell cell in results)
            {
                Assert.IsTrue(grid.TryGetCell(cell.Coordinates, out _), $"Cell {cell.Coordinates} from spiral is not in the grid.");
            }
        }
    }
}
