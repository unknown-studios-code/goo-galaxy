using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Board.Controllers
{
    /// <summary>
    /// Turns every resolved deployment into the impact abilities its card authored, publishes what they did
    /// through <c>MatchEvents.AbilityResolved</c>, and then performs the deployment's self-cleanup. The rules
    /// live in <see cref="AbilityResolver" /> and <see cref="StatusEffectResolver" />; this component wires them
    /// to the scene's grid and unit registry and owns the buffers it publishes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is steps 4 and 6 of the GDD's interaction resolution order, for both kinds of deployment a player
    /// can make. A troop arrives on its own: this component listens on <c>MatchEvents.LandingResolved</c> rather
    /// than <c>MoveExecuted</c> precisely so it cannot run before step 3's conversions —
    /// <see cref="ConversionController" /> raises that event itself, once its own work is done, so the ordering
    /// is a call sequence rather than a bet on static subscription order. A Protocol is pushed in through
    /// <see cref="ResolveSpell" />, because a spell has no move to ride in on.
    /// </para>
    /// <para>
    /// A card that has impacts publishes exactly once, even when the impacts changed nothing — the event states
    /// that the card's ability resolved, which is a fact regardless of its outcome. A card with no impacts
    /// publishes nothing.
    /// </para>
    /// <para>
    /// Step 6 self-cleanup is unconditional <i>within a deployment that resolves</i>, and the two paths differ
    /// on what counts as one. A troop landing always resolves: the move already happened, so even a card with
    /// no impacts reaches the shared body and closes its action windows — a Subject Alpha thaws a frozen unit
    /// exactly as a Cryo-Stasis does. A Protocol can still be turned away, and every non-Success
    /// <see cref="SpellResult" /> — <see cref="SpellResult.CardHasNoImpacts" /> included — is a rejection
    /// rather than a deployment: it returns before the shared body, leaves the board untouched, and closes no
    /// window. The GDD's "successful deployment" is exactly the set that gets past those checks.
    /// </para>
    /// The published lists are this component's own reusable buffers and are only valid for the duration of the
    /// dispatch.
    /// </remarks>
    [DisallowMultipleComponent]
    public class AbilityController : MonoBehaviour
    {
        // Only Volatile Mass self-destructs, and only the unit that landed can, so one landing marks one unit.
        private const int MaxSelfDestructsPerLanding = 1;

        // A sizing hint, not a rule: the list only holds cells that are actually hazardous, and it grows if it
        // has to. Two live trails per player is the most an authored duration of two leaves standing while both
        // keep deploying, which is four across the two of them; the capacity doubles that so a card authored
        // with a longer trail does not resize the list on a landing.
        private const int MaxTrackedHazards = 8;

        private static readonly ProfilerMarker _resolveAbilitiesMarker = new("AbilityController.ResolveAbilities");

        // Separate from the resolve marker because the two answer different questions: the resolve marker is
        // scoped to one card's impacts and only runs when a card has some, while cleanup runs on every landing
        // and is the widest scan in the feature — a whole-registry status tick plus the hazard passes. Folded
        // together, a cheap card would hide a cleanup regression inside an average nobody reads.
        private static readonly ProfilerMarker _resolveSelfCleanupMarker = new("AbilityController.ResolveSelfCleanup");

        // Separate again, and scoped to the rejection path the other two never reach: a spell turned away at
        // InvalidTargets or CardHasNoImpacts never enters ResolveDeployment, so without this a player pushing
        // illegal clusters shows up as unattributed time. Target validation is pure rule work, so no subscriber
        // dispatch is charged to it.
        private static readonly ProfilerMarker _validateSpellTargetsMarker = new("AbilityController.ValidateSpellTargets");

        private readonly List<HexCell> _areaBuffer = new(BoardMetrics.MaxImpactAreaCells);
        private readonly List<int> _affectedUnitIds = new(BoardMetrics.MaxImpactAreaCells);
        private readonly List<HexCoordinates> _affectedHexes = new(BoardMetrics.MaxImpactAreaCells);
        private readonly List<int> _destroyedUnitIds = new(MaxSelfDestructsPerLanding);
        private readonly List<HexCell> _hazardCells = new(MaxTrackedHazards);

        private ReadOnlyCollection<int> _affectedUnitIdsView;
        private ReadOnlyCollection<HexCoordinates> _affectedHexesView;
        private ReadOnlyCollection<int> _destroyedUnitIdsView;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private StatusEffectResolver _statusEffects;
        private AbilityDiagnostic _loggedDiagnostics;
        private bool _isResolvingAbilities;
        private bool _hasLoggedBoardUnavailable;
        private bool _hasLoggedAbilityReentry;
        private bool _hasLoggedSpellReentry;

        /// <summary>Supplies the board and the unit registry every impact is resolved against.</summary>
        /// <remarks>
        /// The status resolver is built here rather than in <c>Awake</c> because it binds to the registry's value
        /// collection, and injection is the first point at which that registry is known. The collection stays
        /// bound to a <c>readonly</c> field on <see cref="UnitPresenter"/>, so the binding survives every later
        /// mutation of the registry.
        /// </remarks>
        /// <param name="gridPresenter">The board whose cells impacts are placed on.</param>
        /// <param name="unitPresenter">The registry the affected units are looked up in.</param>
        [Inject]
        public void Construct(GridPresenter gridPresenter, UnitPresenter unitPresenter)
        {
            Debug.Assert(gridPresenter != null, BoardLogMessages.GridPresenterMissing, this);
            Debug.Assert(unitPresenter != null, BoardLogMessages.UnitPresenterMissing, this);

            _gridPresenter = gridPresenter;
            _unitPresenter = unitPresenter;

            // Guarded rather than dereferenced outright: the container never injects null, but this method is
            // public and the PlayMode fixtures call it directly, where a null would surface as a
            // NullReferenceException thrown out of LifetimeScope.Build — which Unity swallows and only logs.
            // A null resolver instead reaches the latched AbilityBoardUnavailable path at the use site.
            _statusEffects = unitPresenter != null ? new StatusEffectResolver(unitPresenter.ActiveUnitValues) : null;
        }

        protected void Awake()
        {
            _affectedUnitIdsView = new ReadOnlyCollection<int>(_affectedUnitIds);
            _affectedHexesView = new ReadOnlyCollection<HexCoordinates>(_affectedHexes);
            _destroyedUnitIdsView = new ReadOnlyCollection<int>(_destroyedUnitIds);
        }

        protected void OnEnable()
        {
            MatchEvents.GridInitialized += HandleGridInitialized;
            MatchEvents.LandingResolved += HandleLandingResolved;
        }

        protected void OnDisable()
        {
            MatchEvents.GridInitialized -= HandleGridInitialized;
            MatchEvents.LandingResolved -= HandleLandingResolved;
        }

        /// <summary>
        /// Deploys a Protocol: validates the hexes the player picked, resolves the card's impacts on them,
        /// publishes <c>MatchEvents.AbilityResolved</c>, and closes the deployment's action windows.
        /// </summary>
        /// <remarks>
        /// The Protocol counterpart of <c>UnitPresenter.ResolveMove</c>, and it deliberately mirrors that
        /// method's shape: nothing is published and the board is left untouched for any non-Success result.
        /// <para>
        /// The capability is a parameter rather than a registry lookup because a Protocol puts no unit on the
        /// board, so there is no unit id to look one up by. This is the same reason
        /// <c>MovementValidator.ValidateClone</c> receives an <c>IMoveCapable</c> instead of searching for one,
        /// and it is what keeps the Board assembly free of any reference to Cards.
        /// </para>
        /// <para>
        /// Energy is not spent here. Paying for the card is step 1 of the GDD's resolution order and belongs to
        /// the caller, exactly as it does for a troop move.
        /// </para>
        /// <para>
        /// Checks run in a fixed order, so the returned code is predictable when several would fail at once:
        /// <see cref="SpellResult.ResolverBusy" />, then <see cref="SpellResult.BoardUnavailable" />, then
        /// <see cref="SpellResult.CardHasNoImpacts" />, then <see cref="SpellResult.InvalidTargets" />. Board
        /// availability precedes the card checks because a missing grid makes target validation meaningless,
        /// and the card is inspected before its targets because a card with no impacts has nothing to validate
        /// them against.
        /// </para>
        /// <para>
        /// Every non-Success code is a <b>rejection</b>, not a deployment: the board is untouched and no action
        /// window closes. In particular <see cref="SpellResult.CardHasNoImpacts" /> returns before step 6 runs,
        /// so a card that resolves nothing does not expire anyone's statuses or hazards — the caller is expected
        /// to refund the Energy it had not yet committed.
        /// </para>
        /// </remarks>
        /// <param name="command">
        /// The requested deployment. Its target list is borrowed for the duration of the call and never
        /// retained.
        /// </param>
        /// <param name="capability">
        /// The card's authored impacts, typically its card definition. A null capability or an empty impact
        /// list is reported as <see cref="SpellResult.CardHasNoImpacts" />.
        /// </param>
        /// <returns>Success once the impacts have been applied, or the specific reason the request was rejected.</returns>
        public SpellResult ResolveSpell(in SpellCommand command, IAbilityCapable capability)
        {
            if (_isResolvingAbilities)
            {
                // PERF: latched. Re-entry is not a one-off — the shape that causes it is an AbilityResolved
                // subscriber that deploys, and that subscriber fires on every deployment for the rest of the
                // match. The message is a const so nothing is formatted, but each call still extracts a stack
                // trace into a fresh string and retains a console entry.
                if (!_hasLoggedSpellReentry)
                {
                    _hasLoggedSpellReentry = true;
                    Debug.LogError(BoardLogMessages.SpellResolveReentered, this);
                }

                return SpellResult.ResolverBusy;
            }

            if (!TryGetBoard(out HexGrid grid))
            {
                return SpellResult.BoardUnavailable;
            }

            IReadOnlyList<ImpactEffect> landingEffects = capability?.LandingEffects;

            if (landingEffects == null || landingEffects.Count == 0)
            {
                return SpellResult.CardHasNoImpacts;
            }

            using (_validateSpellTargetsMarker.Auto())
            {
                if (!AreTargetsValid(command.TargetHexes, landingEffects, grid))
                {
                    return SpellResult.InvalidTargets;
                }
            }

            _isResolvingAbilities = true;

            try
            {
                ResolveDeployment(grid, AbilityContext.ForSpell(command.PlayerId, command.TargetHexes), landingEffects);
            }
            finally
            {
                _isResolvingAbilities = false;
            }

            return SpellResult.Success;
        }

        // Every impact has to accept the same target set, because the player picked one and each impact reads
        // it. A card whose impacts disagree on cluster size or radius is an authoring error, and rejecting it
        // here is cheaper than resolving the first impact and silently skipping the rest.
        private static bool AreTargetsValid(IReadOnlyList<HexCoordinates> targets, IReadOnlyList<ImpactEffect> landingEffects, HexGrid grid)
        {
            for (int i = 0; i < landingEffects.Count; i++)
            {
                if (!AbilityResolver.ValidateTargets(targets, landingEffects[i], grid))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExempt(HexCoordinates coordinates, IReadOnlyList<HexCoordinates> exemptHexes)
        {
            if (exemptHexes == null)
            {
                return false;
            }

            for (int i = 0; i < exemptHexes.Count; i++)
            {
                if (exemptHexes[i] == coordinates)
                {
                    return true;
                }
            }

            return false;
        }

        // The tracked cells belong to the grid that is being replaced, and a hazard whose owner never deploys
        // again never ticks out on its own, so without this they would outlive their board. Clearing on the
        // event rather than on the next landing's grid comparison means the stale references are dropped at
        // the moment the old board dies, instead of being held until someone happens to move.
        //
        // The diagnostic and re-entry latches are re-armed alongside them. Domain reload is disabled, so a
        // presenter that reported one in a match would otherwise stay silent about it for every later match in
        // the same session — the failure mode that hides the *next* defect rather than the one already seen.
        // _hasLoggedBoardUnavailable is deliberately not re-armed here: it re-arms inside TryGetBoard, on the
        // first call that actually finds a usable board. A grid arriving is not that moment — the unit registry
        // and the status resolver can still be unset — so clearing it here would re-report the same broken
        // wiring on the next landing.
        private void HandleGridInitialized(IHexGrid grid)
        {
            _hazardCells.Clear();

            _loggedDiagnostics = AbilityDiagnostic.None;
            _hasLoggedAbilityReentry = false;
            _hasLoggedSpellReentry = false;
        }

        private void HandleLandingResolved(MoveCommand command, ConversionResult conversions)
        {
            // Re-entering would clear the buffers the outer AbilityResolved subscribers are still iterating,
            // and would run step 6 cleanup for the inner landing before the outer one had finished step 4.
            if (_isResolvingAbilities)
            {
                // PERF: latched, for the same reason as the spell path — a subscriber that deploys mid-dispatch
                // re-enters on every landing for the rest of the match, and each rejected one would otherwise
                // extract a stack trace and retain a console entry.
                if (!_hasLoggedAbilityReentry)
                {
                    _hasLoggedAbilityReentry = true;
                    Debug.LogError(BoardLogMessages.AbilityResolveReentered, this);
                }

                return;
            }

            if (!TryGetBoard(out HexGrid grid))
            {
                return;
            }

            // The unit standing on the landing hex is the acting one, which is not always the commanded unit:
            // a Clone leaves the commanded unit on its source and puts a brand-new unit on the target.
            bool hasLanded = grid.TryGetCell(command.Target, out HexCell landingCell) && landingCell.IsOccupied;

            _isResolvingAbilities = true;

            try
            {
                var context = AbilityContext.ForLanding(
                    command.PlayerId,
                    hasLanded ? landingCell.OccupantUnitId : AbilityContext.NoActingUnit,
                    command.Target,
                    command.Type == MoveType.Jump,
                    command.Source,
                    conversions
                );

                // An empty or off-grid landing hex means nothing actually landed, so no impact may resolve —
                // but the deployment still happened, so its action windows still close. Passing no impacts
                // rather than returning early is what keeps step 6 unconditional.
                ResolveDeployment(grid, context, hasLanded ? GetLandingEffects(command.UnitId) : null);
            }
            finally
            {
                _isResolvingAbilities = false;
            }
        }

        // The shared body of both deployment paths. Everything a troop landing and a Protocol have in common
        // lives here; everything they do not is already resolved into the context by the time it arrives.
        private void ResolveDeployment(HexGrid grid, in AbilityContext context, IReadOnlyList<ImpactEffect> landingEffects)
        {
            // Cleared here as well as by the resolver, because the cleanup below reads them on the path where
            // no impact resolved and would otherwise act on the previous deployment's contents.
            _affectedUnitIds.Clear();
            _affectedHexes.Clear();
            _destroyedUnitIds.Clear();

            if (landingEffects != null && landingEffects.Count > 0)
            {
                ResolveImpacts(grid, context, landingEffects);
            }

            // Runs for every deployment that gets this far, including a troop landing whose card has no impacts
            // at all: step 6 closes the action windows the deployment itself opened. A Protocol only reaches
            // here once ResolveSpell has accepted it — its rejection codes return earlier and close nothing.
            ResolveSelfCleanup(grid, context.ActingPlayerId);
        }

        private void ResolveImpacts(HexGrid grid, in AbilityContext context, IReadOnlyList<ImpactEffect> landingEffects)
        {
            AbilityDiagnostic diagnostics;

            // Scoped to the rules alone: the publish below runs every subscriber's work, and folding that into
            // this marker would charge a view's effect spawning to the resolver.
            using (_resolveAbilitiesMarker.Auto())
            {
                AbilityResolver.Resolve(
                    grid,
                    _unitPresenter.ActiveUnits,
                    context,
                    landingEffects,
                    _statusEffects,
                    _areaBuffer,
                    _affectedUnitIds,
                    _affectedHexes,
                    _destroyedUnitIds,
                    out diagnostics
                );
            }

            LogDiagnostics(diagnostics);
            PublishAbilityResolved(context.ActingPlayerId);
        }

        private void PublishAbilityResolved(int actingPlayerId)
        {
            var result = new AbilityResult(_affectedUnitIdsView, _affectedHexesView, _destroyedUnitIdsView);

            try
            {
                MatchEvents.RaiseAbilityResolved(actingPlayerId, result);
            }
            catch (Exception exception)
            {
                // Deliberately broad, and the one place the style rule's "no try/catch as flow control" does not
                // apply: this is a dispatch boundary into arbitrary subscriber code, so no narrower type exists
                // to name. The impacts are already committed to the models by now, and letting a subscriber's
                // throw unwind would skip the self-cleanup below and leave a self-destructed unit on the board.
                // Nothing is swallowed — the exception is logged with its stack.
                Debug.LogError(BoardLogMessages.AbilityResolvedSubscriberFailed, this);
                Debug.LogException(exception, this);
            }
        }

        // Step 6 of the GDD's interaction order. Removal comes first, because a destroyed unit must not be
        // ticked. Both ticks then close the window the *previous* deployment opened, never the one this
        // landing just opened, and both express that as an exemption on identity rather than on list
        // membership: a hazard overwritten on an already-tracked hex stays in the tracking list across the
        // overwrite, so "it is not tracked yet" was never a safe proxy for "it is new".
        private void ResolveSelfCleanup(HexGrid grid, int actingPlayerId)
        {
            using (_resolveSelfCleanupMarker.Auto())
            {
                DestroyMarkedUnits();
                TrackSpawnedHazards(grid);
                TickHazards(actingPlayerId, _affectedHexes);
                _statusEffects.TickDurations(actingPlayerId, _affectedUnitIdsView);
            }
        }

        private void DestroyMarkedUnits()
        {
            for (int i = 0; i < _destroyedUnitIds.Count; i++)
            {
                int unitId = _destroyedUnitIds[i];

                if (_unitPresenter.ActiveUnits.TryGetValue(unitId, out GridUnit unit) && unit != null)
                {
                    unit.IsAlive = false;
                }

                // Unregistering is what releases the cell, so the registry and the grid stay in step.
                _unitPresenter.UnregisterUnit(unitId);
            }
        }

        // The exemption list is this landing's affected hexes, which is what keeps a trail spawned now from
        // being consumed by the window it opened — including on a hex that already carried one, where the
        // overwrite leaves HasHazard true and the cell tracked. The list also holds the hexes of units a status
        // impact conditioned; those are normally empty of hazards, and the one case where they are not — a
        // Hover unit standing on a puddle — costs that puddle a single extra window rather than a wrong one.
        private void TickHazards(int ownerPlayerId, IReadOnlyList<HexCoordinates> exemptHexes)
        {
            for (int i = _hazardCells.Count - 1; i >= 0; i--)
            {
                HexCell cell = _hazardCells[i];

                if (!cell.HasHazard)
                {
                    _hazardCells.RemoveAt(i);
                    continue;
                }

                if (cell.Hazard.OwnerPlayerId != ownerPlayerId || IsExempt(cell.Coordinates, exemptHexes))
                {
                    continue;
                }

                cell.TickHazard();

                if (!cell.HasHazard)
                {
                    _hazardCells.RemoveAt(i);
                }
            }
        }

        private void TrackSpawnedHazards(HexGrid grid)
        {
            for (int i = 0; i < _affectedHexes.Count; i++)
            {
                if (!grid.TryGetCell(_affectedHexes[i], out HexCell cell) || !cell.HasHazard || _hazardCells.Contains(cell))
                {
                    continue;
                }

                _hazardCells.Add(cell);
            }
        }

        // PERF: latched per flag, because every diagnostic here is an authoring or state fault that repeats on
        // every landing of the offending card for the rest of the match. The messages are consts so nothing is
        // formatted, but each call still extracts a stack trace and retains a log entry — a per-landing cost
        // for a line the reader already saw.
        private void LogDiagnostics(AbilityDiagnostic diagnostics)
        {
            AbilityDiagnostic unlogged = diagnostics & ~_loggedDiagnostics;

            if (unlogged == AbilityDiagnostic.None)
            {
                return;
            }

            _loggedDiagnostics |= unlogged;

            if ((unlogged & AbilityDiagnostic.HazardOverwritten) != 0)
            {
                Debug.LogWarning(BoardLogMessages.HazardOverwritten, this);
            }

            if ((unlogged & AbilityDiagnostic.SelfDestructOnDeadUnit) != 0)
            {
                Debug.LogWarning(BoardLogMessages.SelfDestructOnDeadUnit, this);
            }

            if ((unlogged & AbilityDiagnostic.UnknownEffectType) != 0)
            {
                Debug.LogError(BoardLogMessages.UnknownImpactEffectType, this);
            }

            if ((unlogged & AbilityDiagnostic.HazardWithoutVacatedHex) != 0)
            {
                Debug.LogWarning(BoardLogMessages.HazardWithoutVacatedHex, this);
            }

            if ((unlogged & AbilityDiagnostic.SelfDestructWithoutActingUnit) != 0)
            {
                Debug.LogWarning(BoardLogMessages.SelfDestructWithoutActingUnit, this);
            }
        }

        private IReadOnlyList<ImpactEffect> GetLandingEffects(int unitId)
        {
            if (!_unitPresenter.TryGetCapability(unitId, out IMoveCapable capability))
            {
                return null;
            }

            return capability is IAbilityCapable abilityCapable ? abilityCapable.LandingEffects : null;
        }

        // Latched: a misconfigured board fails on every deployment for the rest of the match, and one console
        // line naming the cause is more useful than one per deployment burying everything after it.
        private bool TryGetBoard(out HexGrid grid)
        {
            grid = null;

            if (_unitPresenter == null || _statusEffects == null || !TryGetHexGrid(out grid))
            {
                if (!_hasLoggedBoardUnavailable)
                {
                    _hasLoggedBoardUnavailable = true;
                    Debug.LogError(BoardLogMessages.AbilityBoardUnavailable, this);
                }

                return false;
            }

            _hasLoggedBoardUnavailable = false;

            return true;
        }

        private bool TryGetHexGrid(out HexGrid grid)
        {
            grid = _gridPresenter != null ? _gridPresenter.HexGrid : null;

            return grid != null;
        }
    }
}
