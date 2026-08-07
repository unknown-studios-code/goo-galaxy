using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <summary>
    /// Owns when a condition on a unit expires. Applying a condition is delegated to the unit, which already
    /// refreshes rather than stacks; the value this adds is the expiry rule, which no single unit can decide
    /// because it depends on <i>whose</i> deployment closes the window.
    /// </summary>
    /// <remarks>
    /// Action-window semantics, per the GDD's real-time timing model, in which "turn" always means action
    /// window:
    /// <list type="bullet">
    /// <item>
    /// A <b>defender action window</b> expires when the affected unit's own controller completes their next
    /// successful deployment. Cryo-Stasis's freeze and Plasmic Leaper's root are both of this kind, which is
    /// why <see cref="TickDurations(int)"/> takes the id of the player who just deployed and ticks the
    /// conditions on <i>that player's</i> units.
    /// </item>
    /// <item>
    /// An <b>owner action window</b> expires when the effect's owner completes their next successful
    /// deployment. Acid Crawler's corrosive trail is of this kind; it lives on a hex rather than a unit, so it
    /// is ticked by <c>HexCell.TickHazard</c> rather than here.
    /// </item>
    /// </list>
    /// Engine-free and free of any container dependency: it takes the unit registry as a constructor argument
    /// so an EditMode test can build one over a plain dictionary. Allocation-free on both the apply and the
    /// tick path — the value collection is iterated through its concrete type so the struct enumerator binds
    /// directly, and no temporary list is built per tick.
    /// <para>
    /// <b>An instance, unlike every other <c>*Resolver</c> here, and deliberately so.</b> It holds exactly one
    /// piece of state: a <c>readonly</c> binding to the registry it expires conditions on. Making it static
    /// would mean passing that registry through every call, and the registry is the same object for the whole
    /// match — the parameter would be noise on every call site and a chance to pass the wrong board. The
    /// binding is immutable and there is no per-call state, so the type is still a pure rule in every sense
    /// that matters: same registry and same input, same result. Do not "fix" this into a static class.
    /// </para>
    /// </remarks>
    internal sealed class StatusEffectResolver
    {
        private readonly Dictionary<int, GridUnit>.ValueCollection _units;

        /// <param name="units">
        /// The registry's value collection. It stays bound to the backing dictionary, so units registered or
        /// removed later are picked up without rebinding.
        /// </param>
        /// <exception cref="ArgumentNullException">The value collection is null.</exception>
        internal StatusEffectResolver(Dictionary<int, GridUnit>.ValueCollection units)
        {
            _units = units ?? throw new ArgumentNullException(nameof(units));
        }

        /// <param name="unit">The unit to condition. A null or dead unit is ignored.</param>
        /// <param name="type">The condition to apply. <see cref="StatusType.None"/> is ignored.</param>
        /// <param name="duration">Action windows the condition lasts. A value below one is ignored.</param>
        internal void ApplyStatus(GridUnit unit, StatusType type, int duration)
        {
            if (unit == null || !unit.IsAlive)
            {
                return;
            }

            unit.AddStatus(type, duration);
        }

        /// <summary>
        /// Closes one defender action window for the given player, decrementing every condition their units
        /// hold and dropping the ones that reach zero.
        /// </summary>
        /// <param name="playerId">The player who just completed a successful deployment.</param>
        internal void TickDurations(int playerId)
        {
            TickDurations(playerId, null);
        }

        /// <summary>
        /// Closes one defender action window for the given player, skipping the units named in the exemption
        /// list.
        /// </summary>
        /// <remarks>
        /// The exemption exists because a deployment can condition the deploying player's own units — freezing
        /// your own flank with Cryo-Stasis is a GDD-documented defensive play. Without it, the same deployment
        /// that applied a one-window freeze would immediately close that window, and the condition would never
        /// be observable. A unit in the exemption list keeps every marker it holds for this tick, including any
        /// applied by an earlier window; that is harmless, because re-applying a condition already refreshes
        /// its duration.
        /// </remarks>
        /// <param name="playerId">The player who just completed a successful deployment.</param>
        /// <param name="exemptUnitIds">
        /// Units the deployment just touched, which must not be ticked by it. Null or empty ticks everything
        /// the player owns. Borrowed for the call only and never retained.
        /// </param>
        internal void TickDurations(int playerId, IReadOnlyList<int> exemptUnitIds)
        {
            foreach (GridUnit unit in _units)
            {
                if (unit == null || !unit.IsAlive || unit.PlayerId != playerId)
                {
                    continue;
                }

                if (IsExempt(unit.UnitId, exemptUnitIds))
                {
                    continue;
                }

                unit.TickStatusDurations();
            }
        }

        private static bool IsExempt(int unitId, IReadOnlyList<int> exemptUnitIds)
        {
            if (exemptUnitIds == null)
            {
                return false;
            }

            for (int i = 0; i < exemptUnitIds.Count; i++)
            {
                if (exemptUnitIds[i] == unitId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
