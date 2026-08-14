using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <remarks>
    /// Owns which units are carrying a fuse and when each one runs out. Arming is delegated to the unit, which
    /// already refreshes rather than stacks; the value this adds is the roster of armed units, which no single
    /// unit can hold because expiry has to be found without walking the whole board every frame.
    /// <para>
    /// A fuse is the GDD's one duration measured in seconds rather than action windows. Every other duration in
    /// the game only advances when somebody deploys, which is why the status system ticks on a deployment and
    /// this ticks on a delta.
    /// </para>
    /// <para>
    /// <b>It reads no clock.</b> The caller passes the elapsed time, so the same code drives a local match from
    /// <c>Time.deltaTime</c>, an EditMode test from a literal, and — later — a networked match from the server's
    /// tick, without a branch. The only engine surface it touches is the console, and only to report a
    /// subscriber that threw. There must be exactly one instance per match; see <c>FuseController.Fuses</c> for
    /// what guarantees that.
    /// </para>
    /// <para>
    /// <see cref="TickFuses"/> is allocation-free: it only removes from the pre-sized roster and appends to the
    /// caller's buffer. <see cref="ArmFuse"/> allocates once if a third fuse is ever armed simultaneously, then
    /// keeps the grown capacity. The registry is bound as a keyed dictionary rather than as the value collection
    /// <see cref="StatusEffectResolver"/> takes, because the access pattern is the opposite one: that resolver
    /// ticks every unit and wants an enumerator, this one needs at most two units by id and wants a lookup.
    /// </para>
    /// </remarks>
    internal sealed class FuseResolver
    {
        // Only Volatile Mass carries a fuse, and at 4 Energy a player rarely has two burning at once. Two covers
        // one per player; a third simply grows the list.
        private const int ExpectedArmedCapacity = 2;

        private readonly IReadOnlyDictionary<int, GridUnit> _units;
        private readonly List<int> _armedUnitIds = new(ExpectedArmedCapacity);

        /// <remarks>
        /// The registry is held by reference, so units registered or removed later are picked up without
        /// rebinding — this never holds a snapshot.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The registry is null.</exception>
        internal FuseResolver(IReadOnlyDictionary<int, GridUnit> units)
        {
            _units = units ?? throw new ArgumentNullException(nameof(units));
        }

        internal int ArmedUnitCount => _armedUnitIds.Count;

        /// <remarks>
        /// The duration is in seconds, on whatever clock the caller later ticks with. Re-arming an already-armed
        /// unit refreshes its remaining time and does <b>not</b> add a second entry, which is what keeps one unit
        /// to one fuse and one expiry. A null unit, a dead unit, and a non-positive duration are all ignored
        /// rather than rejected.
        /// </remarks>
        internal void ArmFuse(GridUnit unit, float durationInSeconds)
        {
            if (unit == null || !unit.IsAlive || durationInSeconds <= 0f)
            {
                return;
            }

            unit.ArmFuse(durationInSeconds);

            // Contains rather than a set: the roster is two entries, so a HashSet would cost an allocation to
            // save a comparison. The guard is what keeps one unit to one id, and therefore to one expiry.
            if (!_armedUnitIds.Contains(unit.UnitId))
            {
                _armedUnitIds.Add(unit.UnitId);
            }

            PublishFuseArmed(unit);
        }

        /// <remarks>
        /// The buffer is caller-owned. Expired ids are <b>appended</b> — never cleared here, so a caller batching several ticks
        /// keeps what earlier ones found. An id whose unit has left the registry, gone null, or died is dropped
        /// silently: those units are already off the board, so there is nothing to remove and nothing to report.
        /// <para>
        /// Reporting only, exactly as <c>AbilityResolver</c> reports a self-destruct: this never removes a unit,
        /// never touches the grid, and never touches the Energy ledger. Expiry is not a move, so it is neither
        /// charged nor refunded.
        /// </para>
        /// Allocation-free, and returns on a single count test when nothing is armed.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The buffer is null.</exception>
        internal void TickFuses(float deltaSeconds, List<int> expiredUnitIds)
        {
            if (expiredUnitIds == null)
            {
                throw new ArgumentNullException(nameof(expiredUnitIds));
            }

            if (_armedUnitIds.Count == 0)
            {
                return;
            }

            // Backwards, because an expiry and a dropped id both remove from the list being walked.
            for (int i = _armedUnitIds.Count - 1; i >= 0; i--)
            {
                int unitId = _armedUnitIds[i];

                if (!TryGetLiveUnit(unitId, out GridUnit unit))
                {
                    _armedUnitIds.RemoveAt(i);
                    continue;
                }

                if (!unit.TickFuse(deltaSeconds))
                {
                    continue;
                }

                _armedUnitIds.RemoveAt(i);
                expiredUnitIds.Add(unitId);
            }
        }

        /// <remarks>
        /// The deterministic removal path, and it must run for <i>every</i> unit that leaves the board by any
        /// route other than its own fuse. Without it a Jump detonation would leave the id armed, the tick would
        /// find no unit behind it, and the drop would be silent but late — the roster would carry a dead id until
        /// the next tick that happened to look at it. Clearing an id that was never armed does nothing.
        /// <para>
        /// Only the roster entry is dropped: the unit's own <see cref="GridUnit.HasFuse"/> and remaining time are
        /// deliberately left untouched, because every caller is removing the unit in the same step and the roster
        /// is the only thing the ticker reads.
        /// </para>
        /// </remarks>
        internal void ClearFuse(int unitId)
        {
            _armedUnitIds.Remove(unitId);
        }

        // Arming happens in the middle of a live ability resolution, so this dispatches into arbitrary subscriber
        // code while the deployment is still only half applied. Letting a subscriber's throw unwind would abort
        // the impact loop and skip the caller's step 6 self-cleanup, stranding a self-destructed unit on the
        // board — the same failure the ability publish is wrapped against, reached by a different route. Nothing
        // is swallowed: the stack is logged. The unit is already armed and rostered by now, so the fuse itself
        // is unaffected either way.
        private static void PublishFuseArmed(GridUnit unit)
        {
            try
            {
                MatchEvents.RaiseFuseArmed(unit.UnitId, unit.PlayerId, unit.RemainingFuseSeconds);
            }
            catch (Exception exception)
            {
                Debug.LogError(string.Format(BoardLogMessages.FuseArmedSubscriberFailedFormat, unit.UnitId, unit.PlayerId));
                Debug.LogException(exception);
            }
        }

        private bool TryGetLiveUnit(int unitId, out GridUnit unit)
        {
            if (_units.TryGetValue(unitId, out GridUnit candidate) && candidate != null && candidate.IsAlive)
            {
                unit = candidate;

                return true;
            }

            unit = null;

            return false;
        }
    }
}
