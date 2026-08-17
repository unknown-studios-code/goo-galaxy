using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Models
{
    /// <summary>
    /// The match's hex board: the fixed set of cells generated from an authored <see cref="IGridLayout" />, and the
    /// lookups and area queries every board system reads it through.
    /// </summary>
    /// <remarks>
    /// The coordinate set is immutable once constructed — cells change occupancy and hazard state, but no cell is
    /// added or removed for the life of the grid. Coordinates are axial (<c>q</c>, <c>r</c>); see
    /// <see cref="Utils.HexMathUtils" /> for the projection into world space.
    /// </remarks>
    public class HexGrid : IHexGrid
    {
        private readonly Dictionary<HexCoordinates, HexCell> _cells;

        /// <summary>Generates the board described by an authored layout.</summary>
        /// <param name="gridLayout">The authored radius and blocked coordinates. Must not be null.</param>
        /// <exception cref="ArgumentNullException">The layout is null.</exception>
        public HexGrid(IGridLayout gridLayout)
        {
            if (gridLayout == null)
            {
                throw new ArgumentNullException(nameof(gridLayout));
            }

            GridRadius = gridLayout.GridRadius;
            int expectedCount = (3 * GridRadius * (GridRadius + 1)) + 1;
            _cells = new Dictionary<HexCoordinates, HexCell>(expectedCount);

            GenerateGrid(gridLayout);
        }

        /// <inheritdoc />
        public int GridRadius { get; }

        /// <summary>Every cell on the board, keyed by its axial coordinates.</summary>
        /// <remarks>Iterating this boxes the backing enumerator — use <see cref="CellValues" /> for a whole-board pass.</remarks>
        public IReadOnlyDictionary<HexCoordinates, HexCell> Cells => _cells;

        /// <remarks>
        /// The same cells as <see cref="Cells" />, typed so a whole-board pass binds the struct enumerator directly
        /// instead of boxing one per call through <c>IReadOnlyDictionary</c>. Prefer it whenever the key is unused —
        /// every cell already carries its own <c>Coordinates</c>.
        /// </remarks>
        public Dictionary<HexCoordinates, HexCell>.ValueCollection CellValues => _cells.Values;

        /// <summary>Looks up the cell at the given coordinates.</summary>
        /// <param name="coords">The axial coordinates to look up.</param>
        /// <param name="cell">The cell at those coordinates, or null when they are off the grid.</param>
        /// <returns>True when the coordinates are on the grid.</returns>
        public bool TryGetCell(HexCoordinates coords, out HexCell cell)
        {
            return _cells.TryGetValue(coords, out cell);
        }

        /// <summary>Finds the up-to-six cells adjacent to a coordinate.</summary>
        /// <remarks>
        /// A neighbour off the grid is omitted rather than reported as null, so the result holds fewer than six
        /// entries at the board's edge.
        /// </remarks>
        /// <param name="center">The coordinate whose neighbours are gathered.</param>
        /// <param name="results">Caller-owned buffer receiving the neighbouring cells. Cleared on entry.</param>
        /// <exception cref="ArgumentNullException">The results buffer is null.</exception>
        public void GetNeighbors(HexCoordinates center, List<HexCell> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            foreach (HexCoordinates d in HexDirection.All)
            {
                HexCoordinates neighborCoords = center.GetNeighbor(d);

                if (TryGetCell(neighborCoords, out HexCell cell))
                {
                    results.Add(cell);
                }
            }
        }

        /// <summary>Finds every cell exactly <paramref name="radius" /> hexes from <paramref name="center" />.</summary>
        /// <remarks>
        /// Walks the ring by stepping along each of the six hex directions in turn. A cell off the grid is skipped
        /// rather than reported, so a ring crossing the board's edge returns fewer than <c>6 * radius</c> entries.
        /// A radius of zero returns just the centre cell; a negative radius returns nothing.
        /// </remarks>
        /// <param name="center">The coordinate the ring is centred on.</param>
        /// <param name="radius">Hex rings out from the centre. Negative values return no cells.</param>
        /// <param name="results">Caller-owned buffer receiving the ring's cells. Cleared on entry.</param>
        /// <exception cref="ArgumentNullException">The results buffer is null.</exception>
        public void GetRingCells(HexCoordinates center, int radius, List<HexCell> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (radius < 0)
            {
                return;
            }

            if (radius == 0)
            {
                if (TryGetCell(center, out HexCell cell))
                {
                    results.Add(cell);
                }

                return;
            }

            HexCoordinates swOffset = HexDirection.SW;
            var currentCoords = new HexCoordinates(center.Q + (swOffset.Q * radius), center.R + (swOffset.R * radius));

            for (int direction = 0; direction < 6; direction++)
            {
                for (int step = 0; step < radius; step++)
                {
                    if (TryGetCell(currentCoords, out HexCell cell))
                    {
                        results.Add(cell);
                    }

                    currentCoords = currentCoords.GetNeighbor(HexDirection.All[direction]);
                }
            }
        }

        /// <summary>
        /// Finds every cell from <paramref name="center" /> out to <paramref name="radius" /> hexes, centre first then
        /// ring by ring outward.
        /// </summary>
        /// <remarks>
        /// This is the area query a troop's impact expands from its landing hex. A cell off the grid is skipped rather
        /// than reported. A radius of zero returns just the centre cell; a negative radius returns nothing.
        /// </remarks>
        /// <param name="center">The coordinate the spiral is centred on.</param>
        /// <param name="radius">The outermost ring included. Negative values return no cells.</param>
        /// <param name="results">Caller-owned buffer receiving the spiral's cells, centre first. Cleared on entry.</param>
        /// <exception cref="ArgumentNullException">The results buffer is null.</exception>
        public void GetSpiralCells(HexCoordinates center, int radius, List<HexCell> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (radius < 0)
            {
                return;
            }

            if (TryGetCell(center, out HexCell centerCell))
            {
                results.Add(centerCell);
            }

            HexCoordinates swOffset = HexDirection.SW;

            for (int k = 1; k <= radius; k++)
            {
                var currentCoords = new HexCoordinates(center.Q + (swOffset.Q * k), center.R + (swOffset.R * k));

                for (int direction = 0; direction < 6; direction++)
                {
                    for (int step = 0; step < k; step++)
                    {
                        if (TryGetCell(currentCoords, out HexCell cell))
                        {
                            results.Add(cell);
                        }

                        currentCoords = currentCoords.GetNeighbor(HexDirection.All[direction]);
                    }
                }
            }
        }

        private void GenerateGrid(IGridLayout gridLayout)
        {
            IReadOnlySet<HexCoordinates> blockedCoordinates = gridLayout.BlockedCoordinates;

            for (int q = -GridRadius; q <= GridRadius; q++)
            {
                int r1 = Math.Max(-GridRadius, -q - GridRadius);
                int r2 = Math.Min(GridRadius, -q + GridRadius);

                for (int r = r1; r <= r2; r++)
                {
                    var coords = new HexCoordinates(q, r);
                    bool isBlocked = blockedCoordinates != null && blockedCoordinates.Contains(coords);
                    _cells.Add(coords, new HexCell(coords, isBlocked));
                }
            }
        }
    }
}
