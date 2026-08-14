using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Board.Controllers
{
    /// <summary>
    /// Drives the match's fuses: it owns the one <see cref="FuseResolver"/> every system shares, ticks
    /// it once a frame, and removes the units whose fuse ran out. The rules live in the resolver and in
    /// <see cref="GridUnit"/>; this component supplies the clock and performs the removal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A fuse is the one duration in the game that resolves without anybody acting, which is why it needs a
    /// component of its own rather than another branch inside <see cref="AbilityController"/>. Everything else
    /// on the board advances on a deployment boundary; this advances on the frame clock, and the two never meet.
    /// </para>
    /// <para>
    /// <b>The clock is scaled, deliberately.</b> <c>Time.deltaTime</c>, never
    /// <c>Time.unscaledDeltaTime</c> — a paused match sets <c>timeScale</c> to zero and must freeze the fuse with
    /// everything else, or a player loses a unit to a countdown that ran while the board could not be played.
    /// </para>
    /// <para>
    /// <b>Expiry never touches the Energy ledger.</b> It is not a move: nothing is charged for it, and nothing is
    /// refunded. <c>UnitPresenter.ResolveMove</c> owns every charge, and a refund from here would return the cost
    /// of a Jump the player never made.
    /// </para>
    /// <para>
    /// The removal deliberately mirrors <see cref="AbilityController"/>'s step 6 self-cleanup and stops there:
    /// the unit is marked dead, unregistered, and gone. Nothing converts, no impact resolves, and no action
    /// window closes — a fuse that runs out "detonates in place ... and nothing further resolves", which is
    /// exactly what separates it from the Jump that detonates the same unit on purpose. See the Volatile Mass
    /// entry in https://app.notion.com/3b856d55129b81d99ea9fd13ff4187e4.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class FuseController : MonoBehaviour
    {
        // One expiry per armed unit, so this mirrors the fuse system's own armed-roster capacity.
        private const int ExpectedExpiriesPerFrame = 2;

        // Reported for a unit that vanished from the registry between the tick and the removal. Real player ids
        // start at one, so this can never be mistaken for an owner.
        private const int NoOwnerPlayerId = 0;

        private static readonly ProfilerMarker _tickFusesMarker = new("FuseController.TickFuses");

        // PERF: separate from the tick marker because the two answer different questions, and the cheap one is
        // the one that runs every frame: the tick returns on a count test with nothing armed, while this runs
        // only on an expiry and does the expensive work — a registry lookup, a cell release, and a bus dispatch
        // that dirties the view's whole-registry overlay pass.
        private static readonly ProfilerMarker _removeExpiredUnitsMarker = new("FuseController.RemoveExpiredUnits");

        private readonly List<int> _expiredUnitIds = new(ExpectedExpiriesPerFrame);

        private UnitPresenter _unitPresenter;
        private FuseResolver _fuses;
        private bool _hasLoggedPresenterMissing;

        /// <remarks>
        /// Lazy because <c>Construct</c> runs before <c>Awake</c> on every container-resolved component, and the
        /// order in which two of them are constructed is not something either can rely on:
        /// <see cref="AbilityController"/> may ask for this before this component has been injected at all.
        /// Building it here rather than in <c>Construct</c> means whichever asks first gets a resolver, and both
        /// get the <i>same</i> one — which is the whole point, since arming and ticking are two halves of one
        /// list and a second instance would silently do nothing.
        /// <para>
        /// Null until a <see cref="UnitPresenter"/> has been injected, and it heals on the first access after
        /// that. Callers null-check rather than assume, which is also why <see cref="AbilityController"/> reads
        /// this property at the point of use instead of caching the value it saw during injection.
        /// </para>
        /// </remarks>
        internal FuseResolver Fuses => _fuses ??= CreateResolver();

        /// <summary>Supplies the registry whose units carry the fuses and whose cells the expiry releases.</summary>
        /// <param name="unitPresenter">The registry the armed units are looked up in and removed from.</param>
        [Inject]
        public void Construct(UnitPresenter unitPresenter)
        {
            Debug.Assert(unitPresenter != null, BoardLogMessages.UnitPresenterMissing, this);

            _unitPresenter = unitPresenter;
        }

        protected void Update()
        {
            // PERF: the latch also stops Fuses re-entering CreateResolver every frame, where the presenter null
            // check is Unity's overloaded operator and costs a native call.
            if (_hasLoggedPresenterMissing)
            {
                return;
            }

            FuseResolver fuses = Fuses;

            if (fuses == null)
            {
                LogPresenterMissing();
                return;
            }

            using (_tickFusesMarker.Auto())
            {
                _expiredUnitIds.Clear();
                fuses.TickFuses(Time.deltaTime, _expiredUnitIds);
            }

            if (_expiredUnitIds.Count == 0)
            {
                return;
            }

            using (_removeExpiredUnitsMarker.Auto())
            {
                RemoveExpiredUnits();
            }
        }

        private FuseResolver CreateResolver()
        {
            // ActiveUnits, not ActiveUnitValues — see FuseResolver's remarks for why the keyed binding.
            return _unitPresenter != null ? new FuseResolver(_unitPresenter.ActiveUnits) : null;
        }

        private void RemoveExpiredUnits()
        {
            // The resolver holds the registry's backing dictionary, which outlives the presenter component, so a
            // fuse reaching zero during scene teardown would otherwise dereference a destroyed MonoBehaviour.
            if (_unitPresenter == null)
            {
                return;
            }

            for (int i = 0; i < _expiredUnitIds.Count; i++)
            {
                int unitId = _expiredUnitIds[i];
                int ownerPlayerId = NoOwnerPlayerId;

                if (_unitPresenter.ActiveUnits.TryGetValue(unitId, out GridUnit unit) && unit != null)
                {
                    // Read before the removal, and it is the owner *now* rather than whoever deployed the unit:
                    // a fuse survives conversion, so a bomb flipped mid-countdown goes off for its new owner.
                    ownerPlayerId = unit.PlayerId;
                    unit.IsAlive = false;
                }

                // Unregistering is what releases the cell, so the registry and the grid stay in step.
                _unitPresenter.UnregisterUnit(unitId);

                // After the removal, so a subscriber reading the board sees the state the event describes.
                PublishFuseExpired(unitId, ownerPlayerId);
            }
        }

        private void PublishFuseExpired(int unitId, int ownerPlayerId)
        {
            try
            {
                MatchEvents.RaiseFuseExpired(unitId, ownerPlayerId);
            }
            catch (Exception exception)
            {
                // Deliberately broad, and the same dispatch-boundary exception the ability publish makes: this
                // calls into arbitrary subscriber code, so no narrower type exists to name. The unit is already
                // gone by now, and letting a subscriber's throw unwind would strand the *other* expiries of this
                // frame on the board with no fuse left to remove them. Nothing is swallowed — the stack is logged.
                Debug.LogError(BoardLogMessages.FuseExpiredSubscriberFailed, this);
                Debug.LogException(exception, this);
            }
        }

        // PERF: latched. A component that was never injected fails on every frame, not once, so an unlatched
        // message would extract a stack trace and retain a console entry sixty times a second.
        private void LogPresenterMissing()
        {
            if (_hasLoggedPresenterMissing)
            {
                return;
            }

            _hasLoggedPresenterMissing = true;
            Debug.LogError(BoardLogMessages.FuseControllerPresenterMissing, this);
        }
    }
}
