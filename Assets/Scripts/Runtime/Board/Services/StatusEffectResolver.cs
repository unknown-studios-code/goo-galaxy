using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <remarks>
    /// Owns when a condition on a unit expires. Applying a condition is delegated to the unit, which already
    /// refreshes rather than stacks; the value this adds is the expiry rule, which no single unit can decide
    /// because it depends on <i>whose</i> deployment closes the window.
    /// <para>
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
    /// </para>
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

        /// <remarks>
        /// Takes the registry's value collection, which stays bound to the backing dictionary, so units registered or
        /// removed later are picked up without rebinding. Throws <see cref="ArgumentNullException" /> when it is null.
        /// </remarks>
        internal StatusEffectResolver(Dictionary<int, GridUnit>.ValueCollection units)
        {
            _units = units ?? throw new ArgumentNullException(nameof(units));
        }

        /// <remarks>
        /// A null or dead <paramref name="unit" /> is ignored, as is a <paramref name="type" /> of
        /// <see cref="StatusType.None" />. <paramref name="duration" /> is action windows the condition lasts; a value
        /// below one is ignored.
        /// </remarks>
        internal void ApplyStatus(GridUnit unit, StatusType type, int duration)
        {
            if (unit == null || !unit.IsAlive)
            {
                return;
            }

            unit.AddStatus(type, duration);
        }

        /// <remarks>
        /// Closes one defender action window for <paramref name="playerId" /> — the player who just completed a
        /// successful deployment — decrementing every condition their units hold and dropping the ones that reach zero.
        /// </remarks>
        internal void TickDurations(int playerId)
        {
            TickDurations(playerId, null);
        }

        /// <remarks>
        /// Closes one defender action window for <paramref name="playerId" />, skipping the units named in
        /// <paramref name="exemptUnitIds" /> — units the deployment just touched, which must not be ticked by it.
        /// Null or empty ticks everything the player owns; the list is borrowed for the call only and never retained.
        /// <para>
        /// The exemption exists because a deployment can condition the deploying player's own units — freezing
        /// your own flank with Cryo-Stasis is a GDD-documented defensive play. Without it, the same deployment
        /// that applied a one-window freeze would immediately close that window, and the condition would never
        /// be observable. A unit in the exemption list keeps every marker it holds for this tick, including any
        /// applied by an earlier window; that is harmless, because re-applying a condition already refreshes
        /// its duration.
        /// </para>
        /// </remarks>
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
