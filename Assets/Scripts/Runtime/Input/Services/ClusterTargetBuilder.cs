using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Input.Services
{
    /// <summary>
    /// Builds the hex cluster a Protocol is aimed at from where the pointer is: the hex under it as the centre,
    /// then the nearest cells around that centre, nearest to the pointer first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The centre is always written first.</b> <see cref="AbilityTargetValidator.ValidateTargets" /> measures
    /// every other hex against <c>targets[0]</c>, so a cluster emitted in any other order looks legal on screen
    /// and is refused for being out of radius the moment it is cast.
    /// </para>
    /// <para>
    /// <b>Candidates come from the authored radius, not from "the N nearest hexes on the board".</b> Every
    /// candidate is a cell <see cref="HexGrid.GetSpiralCells" /> returns within <see cref="ImpactEffect.Radius" />
    /// of the centre, so each one already satisfies the radius rule by construction; a free nearest-N search can
    /// return two hexes the radius rejects at a board edge. The pointer only decides which of those candidates
    /// win, so the cluster still leans toward wherever inside the centre hex the pointer rests.
    /// </para>
    /// <para>
    /// <b>It re-implements no rule.</b> The shape comes from the first landing impact, exactly as the machine
    /// player's cluster draw does, and the result is then put to <see cref="AbilityTargetValidator" /> for every
    /// impact — the check the board runs before it resolves a Protocol — so a cluster this accepts is one the
    /// board accepts. Occupancy is never read: an empty sector is a legal Protocol target.
    /// </para>
    /// <para>
    /// <b>Deterministic and allocation-free.</b> Every buffer is caller-owned. Distances are compared as squared
    /// board-space lengths against the hex centres <see cref="HexMathUtils.ProjectToWorldSpace" /> projects, which
    /// is the only hex-to-board conversion in the project, and ties keep the spiral order.
    /// </para>
    /// </remarks>
    public static class ClusterTargetBuilder
    {
        // The spiral lists the centre first whenever the centre is on the board, which TryArrange checks before it
        // gathers, so every candidate sits after it.
        private const int FirstCandidateIndex = 1;

        /// <summary>
        /// Arranges the cluster <paramref name="landingEffects" /> asks for around <paramref name="centre" /> and
        /// checks it against every impact.
        /// </summary>
        /// <param name="grid">The board to build on. A null grid builds nothing.</param>
        /// <param name="landingEffects">The Protocol's authored impacts. The first one decides the cluster's size and radius.</param>
        /// <param name="centre">The hex under the pointer.</param>
        /// <param name="pointerBoardPosition">Where the pointer falls on the board plane, in the board's world space.</param>
        /// <param name="cellVisualSize">The size the board was projected at, center to corner vertex.</param>
        /// <param name="cellScratch">Caller-owned scratch for the candidate cells. Overwritten.</param>
        /// <param name="cluster">Caller-owned buffer receiving the cluster, centre first. Cleared on entry.</param>
        /// <returns>
        /// True when <paramref name="cluster" /> holds a cluster every impact accepts. False when it could not be
        /// arranged — see <see cref="TryArrange" /> — or when any impact rejects it; <paramref name="cluster" /> then
        /// holds whatever was arranged and must not be cast.
        /// </returns>
        public static bool TryBuild(
            HexGrid grid,
            IReadOnlyList<ImpactEffect> landingEffects,
            HexCoordinates centre,
            Vector2 pointerBoardPosition,
            float cellVisualSize,
            List<HexCell> cellScratch,
            List<HexCoordinates> cluster
        )
        {
            if (landingEffects == null || landingEffects.Count == 0)
            {
                cluster.Clear();

                return false;
            }

            return TryArrange(grid, landingEffects[0], centre, pointerBoardPosition, cellVisualSize, cellScratch, cluster)
                && AreTargetsValidForEveryImpact(cluster, landingEffects, grid);
        }

        /// <summary>
        /// Writes <paramref name="centre" /> and then the <c>ClusterSize - 1</c> cells within
        /// <see cref="ImpactEffect.Radius" /> of it that lie nearest <paramref name="pointerBoardPosition" />, without
        /// validating the result.
        /// </summary>
        /// <remarks>
        /// Split from <see cref="TryBuild" /> so a caller can learn whether the arrangement changed — which is all a
        /// pointer moving inside one hex can do — before paying for validation and a highlight pass.
        /// </remarks>
        /// <param name="grid">The board to build on. A null grid builds nothing.</param>
        /// <param name="effect">The impact whose cluster size and radius shape the cluster.</param>
        /// <param name="centre">The hex under the pointer.</param>
        /// <param name="pointerBoardPosition">Where the pointer falls on the board plane, in the board's world space.</param>
        /// <param name="cellVisualSize">The size the board was projected at, center to corner vertex.</param>
        /// <param name="cellScratch">Caller-owned scratch for the candidate cells. Overwritten.</param>
        /// <param name="cluster">Caller-owned buffer receiving the cluster, centre first. Cleared on entry.</param>
        /// <returns>
        /// True when the cluster was arranged. False, with <paramref name="cluster" /> empty, for a null grid, a
        /// non-positive cluster size, a centre off the board, or too few cells within the radius.
        /// </returns>
        public static bool TryArrange(
            HexGrid grid,
            ImpactEffect effect,
            HexCoordinates centre,
            Vector2 pointerBoardPosition,
            float cellVisualSize,
            List<HexCell> cellScratch,
            List<HexCoordinates> cluster
        )
        {
            cluster.Clear();

            if (grid == null || effect.ClusterSize <= 0 || !grid.TryGetCell(centre, out _))
            {
                return false;
            }

            cluster.Add(centre);

            int neighboursNeeded = effect.ClusterSize - 1;

            if (neighboursNeeded == 0)
            {
                return true;
            }

            grid.GetSpiralCells(centre, effect.Radius, cellScratch);

            if ((cellScratch.Count - FirstCandidateIndex) < neighboursNeeded)
            {
                cluster.Clear();

                return false;
            }

            SortNearestFirst(cellScratch, FirstCandidateIndex, neighboursNeeded, pointerBoardPosition, cellVisualSize);

            for (int i = 0; i < neighboursNeeded; i++)
            {
                cluster.Add(cellScratch[FirstCandidateIndex + i].Coordinates);
            }

            return true;
        }

        /// <summary>Reports whether every impact accepts <paramref name="cluster" /> as its target set.</summary>
        /// <param name="cluster">The hexes to test, centre first.</param>
        /// <param name="landingEffects">The Protocol's authored impacts. Null or empty is not a valid Protocol.</param>
        /// <param name="grid">The board every hex must be on.</param>
        /// <returns>True when <see cref="AbilityTargetValidator.ValidateTargets" /> accepts the cluster for every impact.</returns>
        public static bool AreTargetsValidForEveryImpact(IReadOnlyList<HexCoordinates> cluster, IReadOnlyList<ImpactEffect> landingEffects, HexGrid grid)
        {
            if (landingEffects == null || landingEffects.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < landingEffects.Count; i++)
            {
                if (!AbilityTargetValidator.ValidateTargets(cluster, landingEffects[i], grid))
                {
                    return false;
                }
            }

            return true;
        }

        // A stable partial selection sort: each slot takes the nearest remaining cell, and the cells it passes over
        // shift up one place instead of being swapped, so the unsorted tail keeps its spiral order. Strictly-less then
        // picks the earliest of equals, which is what makes "ties keep the spiral order" hold for every slot and not
        // only the first — a swap would carry a far cell ahead of its equals. Only the slots the cluster takes are
        // ordered, over a spiral of a few rings, so it stays cheap on a pointer-move path and allocates nothing.
        private static void SortNearestFirst(List<HexCell> cells, int start, int sortedCount, Vector2 pointerBoardPosition, float cellVisualSize)
        {
            for (int slot = start; slot < (start + sortedCount); slot++)
            {
                int nearest = slot;
                float nearestDistance = SquaredDistance(cells[slot].Coordinates, pointerBoardPosition, cellVisualSize);

                for (int i = slot + 1; i < cells.Count; i++)
                {
                    float distance = SquaredDistance(cells[i].Coordinates, pointerBoardPosition, cellVisualSize);

                    if (distance < nearestDistance)
                    {
                        nearest = i;
                        nearestDistance = distance;
                    }
                }

                HexCell nearestCell = cells[nearest];

                for (int i = nearest; i > slot; i--)
                {
                    cells[i] = cells[i - 1];
                }

                cells[slot] = nearestCell;
            }
        }

        private static float SquaredDistance(HexCoordinates coordinates, Vector2 pointerBoardPosition, float cellVisualSize)
        {
            Vector2 centre = HexMathUtils.ProjectToWorldSpace(coordinates, cellVisualSize);

            return (centre - pointerBoardPosition).sqrMagnitude;
        }
    }
}
