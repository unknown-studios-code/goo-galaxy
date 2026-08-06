using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <summary>
    /// Applies the conversion attempts a landing triggers on the units around it. Stateless and free of any
    /// engine dependency: every buffer is caller-owned, nothing is logged, and the entry point stays internal
    /// to the Board assembly so <c>ConversionPresenter</c> remains the only caller.
    /// </summary>
    /// <remarks>
    /// The unit, not this service, decides what one attempt does: <c>GridUnit.ReceiveConversionAttempt</c>
    /// mutates its own armor and ownership, and this service only sorts the reported outcome into the output
    /// buffers. Writing either field here would double-apply the rule.
    /// <para>
    /// Every unit receives <b>at most one attempt per resolution</b>, which the GDD's armored resolution rule
    /// requires: one landing never both strips a unit's armor and converts it. No current move type can violate
    /// that on its own — a Clone publishes one coordinate, and a Jump's source is already vacated and skipped as
    /// unoccupied, so exactly one landing cell contributes attempts. The guard exists so the rule still holds
    /// unconditionally for a future move that lands on two occupied cells at once, rather than resting on that
    /// geometry. Units of the acting player are skipped by the ownership filter, which is also what keeps the
    /// landing unit itself — adjacent to a Clone's target — out of the attempt set.
    /// </para>
    /// Allocation-free on every non-throwing path once the caller's buffers are sized.
    /// </remarks>
    internal static class ConversionResolver
    {
        /// <summary>
        /// Runs one conversion attempt against every enemy unit adjacent to an affected coordinate, and sorts
        /// the results into the two output buffers. Coordinates that are off-grid or whose cell is now empty —
        /// a Jump's source — contribute nothing, because only a landing converts.
        /// </summary>
        /// <param name="grid">The board to read adjacency and occupancy from.</param>
        /// <param name="units">The registry of live units, keyed by unit id.</param>
        /// <param name="affectedCoordinates">The coordinates the landing changed, as published with the move.</param>
        /// <param name="actingPlayerId">The player whose landing triggers the attempts.</param>
        /// <param name="neighborBuffer">Caller-owned scratch buffer for adjacency lookups. Overwritten per coordinate.</param>
        /// <param name="attemptedUnitIds">
        /// Caller-owned set enforcing one attempt per unit. Cleared on entry, and left holding every unit the
        /// resolution touched — including the ones that were immune or unaffected.
        /// </param>
        /// <param name="convertedUnitIds">Caller-owned buffer receiving the units whose ownership flipped. Cleared on entry.</param>
        /// <param name="armorStrippedUnitIds">Caller-owned buffer receiving the units that spent their armor. Cleared on entry.</param>
        /// <exception cref="ArgumentNullException">The grid, the registry, the coordinate list, or any buffer is null.</exception>
        internal static void Resolve(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            IReadOnlyList<HexCoordinates> affectedCoordinates,
            int actingPlayerId,
            List<HexCell> neighborBuffer,
            HashSet<int> attemptedUnitIds,
            List<int> convertedUnitIds,
            List<int> armorStrippedUnitIds
        )
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            if (affectedCoordinates == null)
            {
                throw new ArgumentNullException(nameof(affectedCoordinates));
            }

            if (neighborBuffer == null)
            {
                throw new ArgumentNullException(nameof(neighborBuffer));
            }

            if (attemptedUnitIds == null)
            {
                throw new ArgumentNullException(nameof(attemptedUnitIds));
            }

            if (convertedUnitIds == null)
            {
                throw new ArgumentNullException(nameof(convertedUnitIds));
            }

            if (armorStrippedUnitIds == null)
            {
                throw new ArgumentNullException(nameof(armorStrippedUnitIds));
            }

            attemptedUnitIds.Clear();
            convertedUnitIds.Clear();
            armorStrippedUnitIds.Clear();

            for (int i = 0; i < affectedCoordinates.Count; i++)
            {
                ResolveLanding(grid, units, affectedCoordinates[i], actingPlayerId, neighborBuffer, attemptedUnitIds, convertedUnitIds, armorStrippedUnitIds);
            }
        }

        private static void ResolveLanding(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            HexCoordinates coordinates,
            int actingPlayerId,
            List<HexCell> neighborBuffer,
            HashSet<int> attemptedUnitIds,
            List<int> convertedUnitIds,
            List<int> armorStrippedUnitIds
        )
        {
            if (!grid.TryGetCell(coordinates, out HexCell landingCell) || !landingCell.IsOccupied)
            {
                return;
            }

            grid.GetNeighbors(coordinates, neighborBuffer);

            for (int i = 0; i < neighborBuffer.Count; i++)
            {
                HexCell neighborCell = neighborBuffer[i];

                if (!neighborCell.IsOccupied || !units.TryGetValue(neighborCell.OccupantUnitId, out GridUnit neighborUnit) || neighborUnit == null)
                {
                    continue;
                }

                if (neighborUnit.PlayerId == actingPlayerId)
                {
                    continue;
                }

                // The set is the dedup rather than a scan of the two output lists, which would only remember
                // units that produced an outcome — an immune or unaffected unit could still be attempted a
                // second time. Membership keeps "one attempt per unit" true for every outcome, and the
                // caller's set is bounded by the neighbourhood size.
                if (!attemptedUnitIds.Add(neighborUnit.UnitId))
                {
                    continue;
                }

                SortOutcome(neighborUnit.ReceiveConversionAttempt(actingPlayerId), neighborUnit.UnitId, convertedUnitIds, armorStrippedUnitIds);
            }
        }

        private static void SortOutcome(ConversionOutcome outcome, int unitId, List<int> convertedUnitIds, List<int> armorStrippedUnitIds)
        {
            switch (outcome)
            {
                case ConversionOutcome.Converted:
                    convertedUnitIds.Add(unitId);
                    break;
                case ConversionOutcome.ArmorStripped:
                    armorStrippedUnitIds.Add(unitId);
                    break;
            }
        }
    }
}
