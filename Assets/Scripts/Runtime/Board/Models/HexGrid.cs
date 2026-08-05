using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Models
{
    public class HexGrid : IHexGrid
    {
        private readonly Dictionary<HexCoordinates, HexCell> _cells;

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

        public int GridRadius { get; }

        public IReadOnlyDictionary<HexCoordinates, HexCell> Cells => _cells;

        public bool TryGetCell(HexCoordinates coords, out HexCell cell)
        {
            return _cells.TryGetValue(coords, out cell);
        }

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
