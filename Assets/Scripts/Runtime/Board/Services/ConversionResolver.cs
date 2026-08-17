using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <remarks>
    /// Applies the conversion attempts a landing triggers on the units around it. Stateless and free of any
    /// engine dependency: every buffer is caller-owned, nothing is logged, and the entry point stays internal
    /// so no assembly outside Board can call it — <c>ConversionController</c> is its only production caller,
    /// and the EditMode suite reaches it through <c>InternalsVisibleTo</c>.
    /// <para>
    /// The unit, not this service, decides what one attempt does: <c>GridUnit.ReceiveConversionAttempt</c>
    /// mutates its own armor and ownership, and this service only sorts the reported outcome into the output
    /// buffers. Writing either field here would double-apply the rule.
    /// </para>
    /// <para>
    /// Every unit receives <b>at most one attempt per resolution</b>, which the GDD's armored resolution rule
    /// requires: one landing never both strips a unit's armor and converts it. No current move type can violate
    /// that on its own — a Clone publishes one coordinate, and a Jump's source is already vacated and skipped as
    /// unoccupied, so exactly one landing cell contributes attempts. The guard exists so the rule still holds
    /// unconditionally for a future move that lands on two occupied cells at once, rather than resting on that
    /// geometry. It matters more at radius 2, where two published coordinates overlap heavily.
    /// </para>
    /// <para>
    /// The reach is the acting card's authored conversion radius, so Volatile Mass converts two rings while
    /// every other card converts one. The spiral used to gather the area includes the landing cell itself; it
    /// needs no explicit exclusion, because its occupant is by definition the unit that just landed and is
    /// therefore owned by the acting player, which the ownership filter drops before any attempt is made. That
    /// same filter is what keeps the landing unit out of the attempt set when it is merely adjacent, as it is
    /// to a Clone's target.
    /// </para>
    /// Allocation-free on every non-throwing path once the caller's buffers are sized.
    /// </remarks>
    internal static class ConversionResolver
    {
        /// <remarks>
        /// Runs one conversion attempt against every enemy unit within <paramref name="conversionRadius" /> — the
        /// acting card's authored value, clamped up to one so a nonsensical radius still converts adjacently rather
        /// than silently converting nothing — of each coordinate in <paramref name="affectedCoordinates" />, the
        /// coordinates the landing changed as published with the move, and sorts the results into
        /// <paramref name="convertedUnitIds" /> and <paramref name="armorStrippedUnitIds" />. A coordinate that is
        /// off-grid or whose cell is now empty (a Jump's source) contributes nothing, because only a landing converts.
        /// <paramref name="areaBuffer" /> is caller-owned scratch overwritten per coordinate.
        /// <paramref name="attemptedUnitIds" /> is a caller-owned set enforcing one attempt per unit, cleared on entry
        /// and left holding every unit the resolution touched, including the immune and the unaffected. All three
        /// output buffers are cleared on entry. Throws <see cref="ArgumentNullException" /> when the grid, the
        /// registry, the coordinate list, or any buffer is null.
        /// </remarks>
        internal static void Resolve(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            IReadOnlyList<HexCoordinates> affectedCoordinates,
            int actingPlayerId,
            int conversionRadius,
            List<HexCell> areaBuffer,
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

            if (areaBuffer == null)
            {
                throw new ArgumentNullException(nameof(areaBuffer));
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

            int effectiveRadius = Math.Max(conversionRadius, BoardMetrics.DefaultConversionRadius);

            for (int i = 0; i < affectedCoordinates.Count; i++)
            {
                ResolveLanding(
                    grid,
                    units,
                    affectedCoordinates[i],
                    actingPlayerId,
                    effectiveRadius,
                    areaBuffer,
                    attemptedUnitIds,
                    convertedUnitIds,
                    armorStrippedUnitIds
                );
            }
        }

        private static void ResolveLanding(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            HexCoordinates coordinates,
            int actingPlayerId,
            int conversionRadius,
            List<HexCell> areaBuffer,
            HashSet<int> attemptedUnitIds,
            List<int> convertedUnitIds,
            List<int> armorStrippedUnitIds
        )
        {
            if (!grid.TryGetCell(coordinates, out HexCell landingCell) || !landingCell.IsOccupied)
            {
                return;
            }

            grid.GetSpiralCells(coordinates, conversionRadius, areaBuffer);

            for (int i = 0; i < areaBuffer.Count; i++)
            {
                HexCell targetCell = areaBuffer[i];

                if (!targetCell.IsOccupied || !units.TryGetValue(targetCell.OccupantUnitId, out GridUnit targetUnit) || targetUnit == null)
                {
                    continue;
                }

                if (targetUnit.PlayerId == actingPlayerId)
                {
                    continue;
                }

                // The set is the dedup rather than a scan of the two output lists, which would only remember
                // units that produced an outcome — an immune or unaffected unit could still be attempted a
                // second time. Membership keeps "one attempt per unit" true for every outcome, and the
                // caller's set is bounded by the widest area two landing coordinates can cover.
                if (!attemptedUnitIds.Add(targetUnit.UnitId))
                {
                    continue;
                }

                SortOutcome(targetUnit.ReceiveConversionAttempt(actingPlayerId), targetUnit.UnitId, convertedUnitIds, armorStrippedUnitIds);
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
