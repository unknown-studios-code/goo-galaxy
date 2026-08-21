using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <summary>
    /// Stateless legality check for the hexes a Protocol was aimed at, shared by everything that has to agree
    /// on what a legal cluster is.
    /// </summary>
    /// <remarks>
    /// Public rather than internal to Board because the check has two kinds of caller: the resolver, which asks
    /// once about the cluster a player committed to, and a chooser, which asks about many candidate clusters
    /// before committing to any of them. Both must get the same answer from the same code, or a chooser can
    /// offer a target the resolver then refuses.
    /// </remarks>
    public static class AbilityTargetValidator
    {
        /// <summary>
        /// Reports whether <paramref name="targets" /> — the hexes the player picked, centre first — forms the
        /// cluster <paramref name="effect" /> was authored for, against <paramref name="grid" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The GDD describes a Protocol's target as "a 3-hex cluster (1 center hex + 2 adjacent)", and the two
        /// authored fields already say exactly that without a new schema:
        /// <see cref="ImpactEffect.ClusterSize" /> is how many hexes the player picks, and
        /// <see cref="ImpactEffect.Radius" /> is how far each may sit from the centre.
        /// </para>
        /// <para>
        /// <c>targets[0]</c> is the centre by definition, and it is measured against itself, so a distance of
        /// zero always passes. A cluster size of zero fails: on a troop impact zero means "no cap", but a
        /// Protocol with no target count is not authored, and silently accepting any number of hexes for it
        /// would be worse than rejecting it.
        /// </para>
        /// <para>
        /// Occupancy is not among the rules and is never read: an empty sector is a legal Protocol target. A
        /// caller must not treat a passing cluster as one that holds units.
        /// </para>
        /// <para>
        /// Allocation-free. Distinctness is a nested indexed scan rather than a set, because the count is the
        /// authored cluster size — three or four — and a <c>HashSet</c> would cost an allocation to save
        /// nothing.
        /// </para>
        /// </remarks>
        /// <param name="targets">The hexes the player picked, centre first. Borrowed for the call, never retained.</param>
        /// <param name="effect">The authored impact whose cluster size and radius the targets are measured against.</param>
        /// <param name="grid">The board every hex must be on.</param>
        /// <returns>
        /// True when the count matches the authored cluster size, every hex is on the board, no hex repeats, and
        /// every hex is within the authored radius of the first one; false for a null target list, a null grid,
        /// or a non-positive cluster size.
        /// </returns>
        public static bool ValidateTargets(IReadOnlyList<HexCoordinates> targets, ImpactEffect effect, HexGrid grid)
        {
            if (targets == null || grid == null || effect.ClusterSize <= 0 || targets.Count != effect.ClusterSize)
            {
                return false;
            }

            HexCoordinates centre = targets[0];

            for (int i = 0; i < targets.Count; i++)
            {
                HexCoordinates target = targets[i];

                if (!grid.TryGetCell(target, out _) || centre.CalculateDistance(target) > effect.Radius)
                {
                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    if (targets[j] == target)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
