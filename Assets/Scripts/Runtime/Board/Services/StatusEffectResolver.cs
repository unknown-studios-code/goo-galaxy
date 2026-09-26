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
    /// <b>An instance, unlike every other <c>*Resolver</c> here, and deliberately so.</b> It holds a
    /// <c>readonly</c> binding to the registry it expires conditions on. Making it static would mean passing that
    /// registry through every call, and the registry is the same object for the whole match — the parameter would
    /// be noise on every call site and a chance to pass the wrong board. Do not "fix" this into a static class.
    /// </para>
    /// <para>
    /// <b>It carries nothing from one call to the next.</b> What it changed is reported into buffers the caller
    /// owns — the same shape <c>AbilityResolver</c> uses for its affected units and hexes — and it only ever
    /// appends to them: clearing, reading and publishing them is the caller's. Its one other field is a scratch list
    /// cleared before every use, which exists only so a tick can learn which conditions a unit dropped without
    /// allocating.
    /// </para>
    /// </remarks>
    internal sealed class StatusEffectResolver
    {
        // A unit carries at most one marker per condition, and there are two conditions to carry.
        private const int MaxStatusesPerUnit = 2;

        private readonly Dictionary<int, GridUnit>.ValueCollection _units;
        private readonly List<StatusType> _expiredStatusScratch = new(MaxStatusesPerUnit);

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
        /// below one is ignored. Records nothing — see the overload that takes a buffer.
        /// </remarks>
        internal void ApplyStatus(GridUnit unit, StatusType type, int duration)
        {
            ApplyStatus(unit, type, duration, StatusChange.NoActingPlayer, null);
        }

        /// <remarks>
        /// The same application, appended to <paramref name="applied" /> as a <see cref="StatusChange" /> against
        /// <paramref name="actingPlayerId" /> — the player whose deployment applied it. Only an application the unit
        /// actually accepted is recorded, so a null or dead unit, <see cref="StatusType.None" />, or a duration below
        /// one records nothing; a refresh of a condition already held is recorded again. The buffer is caller-owned,
        /// never cleared here, and may be null to record nothing.
        /// </remarks>
        internal void ApplyStatus(GridUnit unit, StatusType type, int duration, int actingPlayerId, List<StatusChange> applied)
        {
            if (unit == null || !unit.IsAlive || type == StatusType.None || duration <= 0)
            {
                return;
            }

            unit.AddStatus(type, duration);
            applied?.Add(new StatusChange(unit.UnitId, unit.PlayerId, actingPlayerId, type, duration));
        }

        /// <remarks>
        /// Closes one defender action window for <paramref name="playerId" /> — the player who just completed a
        /// successful deployment — decrementing every condition their units hold and dropping the ones that reach zero.
        /// </remarks>
        internal void TickDurations(int playerId)
        {
            TickDurations(playerId, null, null);
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
            TickDurations(playerId, exemptUnitIds, null);
        }

        /// <remarks>
        /// The same tick, appending one <see cref="StatusChange" /> to <paramref name="expired" /> for every condition
        /// it ran out — owner at the moment of expiry, <see cref="StatusChange.NoActingPlayer" />, zero windows left.
        /// The buffer is caller-owned, never cleared here, and may be null to record nothing.
        /// </remarks>
        internal void TickDurations(int playerId, IReadOnlyList<int> exemptUnitIds, List<StatusChange> expired)
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

                _expiredStatusScratch.Clear();
                unit.TickStatusDurations(_expiredStatusScratch);
                RecordExpiries(unit, expired);
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

        private void RecordExpiries(GridUnit unit, List<StatusChange> expired)
        {
            if (expired == null)
            {
                return;
            }

            for (int i = 0; i < _expiredStatusScratch.Count; i++)
            {
                expired.Add(new StatusChange(unit.UnitId, unit.PlayerId, StatusChange.NoActingPlayer, _expiredStatusScratch[i], 0));
            }
        }
    }
}
