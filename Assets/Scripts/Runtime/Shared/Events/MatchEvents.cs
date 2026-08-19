using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Shared.Events
{
    /// <summary>
    /// The static bus carrying in-match facts across assembly boundaries.
    /// Every event is raised only through its <c>Raise*</c> method, so a subscriber can never invoke one.
    /// </summary>
    /// <remarks>
    /// Subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c> with a named handler: the bus outlives
    /// every subscriber, so a missed unsubscription survives scene loads. Domain reload is disabled in this
    /// project, which is why <see cref="ResetEvents"/> clears every field on subsystem registration.
    /// </remarks>
    public static class MatchEvents
    {
        /// <summary>Raised once the match configuration is settled and gameplay systems may initialize.</summary>
        public static event Action<MatchConfiguration> MatchStarted;

        /// <summary>Raised after the board has been generated, carrying the grid gameplay systems should read.</summary>
        public static event Action<IHexGrid> GridInitialized;

        /// <summary>
        /// Raised when the match enters a new phase, carrying the phase just entered.
        /// </summary>
        /// <remarks>
        /// Raised only for a transition the orchestrator's own table accepts, so the sequence a subscriber sees
        /// is the legal one and never skips a phase or repeats the phase it is already in. It is published
        /// <b>after</b> the orchestrator has entered the phase, so a subscriber that asks the orchestrator what
        /// phase it is in receives the value this payload carries.
        /// <para>
        /// <see cref="MatchPhase.Standard" /> is the only phase in which a card can be played or discarded, and
        /// the deploy and discard controllers gate on exactly that. A subscriber caching the phase to make its
        /// own decision must handle <see cref="MatchPhase.None" />, which is what an abandoned start publishes.
        /// </para>
        /// </remarks>
        public static event Action<MatchPhase> MatchPhaseChanged;

        /// <summary>
        /// Raised as the current phase's clock counts down, carrying the seconds left in it.
        /// </summary>
        /// <remarks>
        /// Raised at most once per second, not once per frame: the publisher only dispatches when the
        /// whole-second floor of the remaining time changes, and the payload is that floor — which is why it is
        /// an <c>int</c>. A subscriber rendering a countdown therefore formats the value it is handed and never
        /// has to round it.
        /// <para>
        /// The clock is scaled match time, so a paused match stops publishing rather than continuing to count.
        /// Both the pre-match countdown and normal play publish through this — the phase says which one is
        /// running, and this event does not repeat it.
        /// </para>
        /// <para>
        /// Only normal play has a sub-second form to fall back on — see <c>MatchController.RemainingSeconds</c>,
        /// which owns that trap.
        /// </para>
        /// </remarks>
        public static event Action<int> MatchClockTicked;

        /// <summary>
        /// Raised when a player's live unit count changes, carrying the player id and their new count.
        /// </summary>
        /// <remarks>
        /// Raised only when that player's count actually moved. A deployment that converts nothing publishes
        /// nothing, and a recount that lands on the number already cached is silent — so a subscriber may treat
        /// every dispatch as a real change and never has to compare against what it last saw.
        /// <para>
        /// Unit count <i>is</i> the match score, which is what makes this the seam the domination rule hangs
        /// on: a count reaching zero is a player with nothing left on the board, and GOOM-12 ends the match on
        /// exactly that without needing a second pass over the registry.
        /// </para>
        /// <para>
        /// A publish that <i>follows a board resolution</i> — a landing, a Protocol impact, or a fuse expiring —
        /// is deferred to the <b>end</b> of the frame it resolved on, never made from inside its dispatch, so a
        /// subscriber reads a settled board and, unlike the resolution events on this bus, is not running inside
        /// the resolution. <c>MatchController.OnEnable</c> enforces that and states why.
        /// </para>
        /// <para>
        /// The two publishes no board resolution produced are made inline instead, and need no deferral: the
        /// opening score, published while the match is still starting and before anything can move it, and the
        /// recount taken when the clock runs out, which is what decides the winner and must read the board as it
        /// stands at that instant.
        /// </para>
        /// <para>
        /// <b>Ticks stop at the end of the match.</b> The deferred pass is gated on the match still running, so
        /// a fuse expiring or an ability resolving after <see cref="MatchPhase.Ended" /> publishes nothing, and
        /// the last counts a subscriber sees are the ones the outcome on <see cref="MatchEnded" /> was decided
        /// from. A results screen bound to this cannot be moved after the outcome is final.
        /// </para>
        /// <para>
        /// The payload is value types only, so there is nothing for a subscriber to copy.
        /// </para>
        /// </remarks>
        public static event Action<int, int> ScoreChanged;

        /// <summary>
        /// Raised once the match is over, carrying who won and what ended it.
        /// </summary>
        /// <remarks>
        /// Published after the phase has already reached <see cref="MatchPhase.Ended" />, so a subscriber
        /// reading the orchestrator sees the state this event describes. Raised at most once per match; a
        /// rematch publishes its own. See <see cref="MatchOutcome" /> for how a draw is represented.
        /// </remarks>
        public static event Action<MatchOutcome> MatchEnded;

        /// <summary>Raised whenever a player's energy total changes, carrying the player id and the new total.</summary>
        public static event Action<int, float> EnergyChanged;

        /// <summary>
        /// Raised on every spend attempt, carrying the player id, their energy after the attempt, and whether
        /// the spend succeeded. A rejected attempt reports the player's unchanged total.
        /// </summary>
        public static event Action<int, float, bool> EnergySpent;

        /// <summary>
        /// Raised after a move has been applied to the board, carrying the command and the coordinates whose
        /// contents changed. The coordinate list is owned by the publisher and is only valid for the duration
        /// of the callback; subscribers must copy it rather than retain the reference, and must read it with an
        /// indexed <c>for</c> loop — <c>foreach</c> over the interface boxes the backing enumerator, one
        /// allocation per subscriber per move.
        /// </summary>
        public static event Action<MoveCommand, IReadOnlyList<HexCoordinates>> MoveExecuted;

        /// <summary>
        /// Raised after a landing has resolved its conversions, carrying the acting player id and what the
        /// landing did to the units around it. Only raised when at least one unit was converted or lost its
        /// armor. See <see cref="ConversionResult" /> for buffer ownership and iteration.
        /// </summary>
        public static event Action<int, ConversionResult> ConversionResolved;

        /// <summary>
        /// Raised on every executed move once its conversions have resolved, carrying the command and what
        /// those conversions did. Unlike <see cref="ConversionResolved"/> it is raised unconditionally,
        /// including when the landing converted nothing, because an impact ability still has to resolve.
        /// </summary>
        /// <remarks>
        /// This is the hand-off from step 3 to step 4 of the GDD's interaction resolution order: it exists so
        /// landing-impact abilities run <b>after</b> standard conversion by construction, instead of resting on
        /// the order in which two independent subscribers happened to register on <see cref="MoveExecuted"/>.
        /// The command already carries the source and target hexes, which is everything step 4 needs to place
        /// an impact, so no coordinate buffer travels with this event. The payload's second half is what step 3
        /// just did, so step 4 can target the units conversion flipped without re-deriving them; an empty
        /// <see cref="ConversionResult"/> means nobody was converted, which is a normal landing and not an
        /// error — see that type for buffer ownership and iteration.
        /// </remarks>
        public static event Action<MoveCommand, ConversionResult> LandingResolved;

        /// <summary>
        /// Raised after a deployment's impact abilities have resolved, carrying the acting player id and what
        /// the impacts did. Raised once per deployment whose card authors at least one impact, even when the
        /// impacts affected nothing; a card with no impacts publishes nothing at all.
        /// </summary>
        /// <remarks>
        /// Both kinds of deployment publish it. A troop landing places its impacts around the hex its acting
        /// unit landed on. A Protocol has neither an acting unit nor a landing — it resolves on the hexes the
        /// player picked — so nothing in its payload is relative to a unit, and a subscriber must not read one
        /// back from it. See <see cref="AbilityResult" /> for buffer ownership, iteration, and why
        /// <c>DestroyedUnitIds</c> names units that are still registered while the callback runs.
        /// </remarks>
        public static event Action<int, AbilityResult> AbilityResolved;

        /// <summary>
        /// Raised when a unit starts, or restarts, a fuse: the unit id, the id of the player who owns
        /// it at that moment, and how many seconds it has left.
        /// </summary>
        /// <remarks>
        /// Re-arming an already-armed unit raises it again with the refreshed time rather than reporting a
        /// second fuse — a unit carries at most one.
        /// <para>
        /// Raised from inside the deployment that armed the fuse, so a subscriber runs while that deployment is
        /// still resolving. Read the board, do not deploy: a subscriber that resolves a move or a spell is
        /// rejected by the re-entrancy latches rather than served.
        /// </para>
        /// <para>
        /// The payload is value types only and describes state that outlives the callback, so unlike the
        /// buffer-carrying events on this bus there is nothing here a subscriber has to copy.
        /// </para>
        /// </remarks>
        public static event Action<int, int, float> FuseArmed;

        /// <summary>
        /// Raised after a unit's fuse has run out and the unit has been removed from the board, carrying its id
        /// and the id of the player who owned it when it went off.
        /// </summary>
        /// <remarks>
        /// Published <b>after</b> the removal, so a subscriber reading the board sees the state this event
        /// describes: the unit is already gone from the registry and its cell is already free. The id is
        /// therefore the only handle left — a lookup will not find it.
        /// <para>
        /// The owner is the player who owned the unit at the moment it went off, which is not necessarily the
        /// player who deployed it: a fuse survives conversion, so a bomb flipped mid-countdown expires for its
        /// new owner.
        /// </para>
        /// <para>
        /// A unit removed any other way — a Jump detonation, a conversion, ordinary cleanup — does not raise
        /// this. It states that the clock ran out, not that a unit died.
        /// </para>
        /// </remarks>
        public static event Action<int, int> FuseExpired;

        /// <summary>
        /// Raised whenever a player's hand changes — dealt at match start, and rotated whenever a slot is
        /// consumed, by a play the board accepted or by a discard — carrying the player id, the cards now in
        /// hand, and the card that will fill the next freed slot.
        /// </summary>
        /// <remarks>
        /// The hand list is the deck's own storage, not a per-dispatch buffer: the reference stays valid for the
        /// deck's lifetime, and its contents are rewritten in place by every later rotation of that player's hand.
        /// A subscriber that retains it is therefore reading live state rather than the hand it was handed — copy
        /// the entries it needs instead. Read it with an indexed <c>for</c> loop; <c>foreach</c> allocates an
        /// enumerator here at every static type, because a <c>ReadOnlyCollection</c> has no struct enumerator to
        /// bind to.
        /// </remarks>
        public static event Action<int, IReadOnlyList<CardId>, CardId> HandChanged;

        /// <summary>
        /// Raised after a card has been discarded from hand, carrying the player id, the card that left the
        /// hand, and the slot index it left.
        /// </summary>
        /// <remarks>
        /// <see cref="HandChanged"/> is necessarily raised first, from inside the rotation that discarding
        /// reuses, so by the time this arrives the hand already holds the replacement — which is exactly why
        /// this payload carries the outgoing card itself rather than expecting a subscriber to still find it in
        /// the hand. The payload is value types only, so there is nothing for a subscriber to copy.
        /// </remarks>
        public static event Action<int, CardId, int> CardDiscarded;

        /// <summary>Publishes <see cref="MatchStarted"/>.</summary>
        /// <param name="config">The configuration the match runs with.</param>
        public static void RaiseMatchStarted(MatchConfiguration config)
        {
            MatchStarted?.Invoke(config);
        }

        /// <summary>Publishes <see cref="GridInitialized"/>.</summary>
        /// <param name="grid">The freshly generated board.</param>
        public static void RaiseGridInitialized(IHexGrid grid)
        {
            GridInitialized?.Invoke(grid);
        }

        /// <summary>
        /// Publishes <see cref="MatchPhaseChanged"/>. Called only once the orchestrator has entered the phase.
        /// </summary>
        /// <param name="phase">The phase just entered.</param>
        public static void RaiseMatchPhaseChanged(MatchPhase phase)
        {
            MatchPhaseChanged?.Invoke(phase);
        }

        /// <summary>
        /// Publishes <see cref="MatchClockTicked"/>. Called only when the whole-second floor of the remaining
        /// time has changed, never once per frame.
        /// </summary>
        /// <param name="remainingSeconds">Whole seconds left in the current phase, clamped at zero.</param>
        public static void RaiseMatchClockTicked(int remainingSeconds)
        {
            MatchClockTicked?.Invoke(remainingSeconds);
        }

        /// <summary>
        /// Publishes <see cref="ScoreChanged"/>. Called only for a player whose count actually moved, once the
        /// new count is the one the registry holds.
        /// </summary>
        /// <param name="playerId">The player whose count changed.</param>
        /// <param name="unitCount">The number of live units that player now holds.</param>
        public static void RaiseScoreChanged(int playerId, int unitCount)
        {
            ScoreChanged?.Invoke(playerId, unitCount);
        }

        /// <summary>
        /// Publishes <see cref="MatchEnded"/>. Called only once the match has already reached
        /// <see cref="MatchPhase.Ended"/>.
        /// </summary>
        /// <param name="outcome">Who won and what ended the match.</param>
        public static void RaiseMatchEnded(MatchOutcome outcome)
        {
            MatchEnded?.Invoke(outcome);
        }

        /// <summary>Publishes <see cref="EnergyChanged"/>.</summary>
        /// <param name="playerId">The player whose total changed.</param>
        /// <param name="newEnergy">The player's energy after the change.</param>
        public static void RaiseEnergyChanged(int playerId, float newEnergy)
        {
            EnergyChanged?.Invoke(playerId, newEnergy);
        }

        /// <summary>Publishes <see cref="EnergySpent"/>.</summary>
        /// <param name="playerId">The player who attempted the spend.</param>
        /// <param name="newEnergy">The player's energy after the attempt.</param>
        /// <param name="wasSuccessful">Whether the energy was actually deducted.</param>
        public static void RaiseEnergySpent(int playerId, float newEnergy, bool wasSuccessful)
        {
            EnergySpent?.Invoke(playerId, newEnergy, wasSuccessful);
        }

        /// <summary>Publishes <see cref="MoveExecuted"/>. Called only after the board has been mutated.</summary>
        /// <param name="command">The move that was applied.</param>
        /// <param name="affectedCoordinates">
        /// The coordinates whose contents changed. Owned by the caller and only valid for the duration of the
        /// dispatch, so subscribers must copy what they intend to keep.
        /// </param>
        public static void RaiseMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates)
        {
            MoveExecuted?.Invoke(command, affectedCoordinates);
        }

        /// <summary>
        /// Publishes <see cref="ConversionResolved"/>. Called only after every conversion attempt of the
        /// landing has been applied to the units.
        /// </summary>
        /// <param name="actingPlayerId">The player whose landing triggered the attempts.</param>
        /// <param name="result">
        /// The units the landing converted and the armored units it stripped. Its buffers must stay valid for
        /// the whole dispatch — see <see cref="ConversionResult" />.
        /// </param>
        public static void RaiseConversionResolved(int actingPlayerId, ConversionResult result)
        {
            ConversionResolved?.Invoke(actingPlayerId, result);
        }

        /// <summary>
        /// Publishes <see cref="LandingResolved"/>. Called on every executed move once its conversions have
        /// been applied, whether or not any unit was converted.
        /// </summary>
        /// <param name="command">
        /// The move whose landing has finished converting. Its source and target are where an impact places
        /// itself, so no separate coordinate buffer is published.
        /// </param>
        /// <param name="conversions">
        /// What step 3 just did, so a landing impact can target the units it flipped. Empty when nothing was
        /// converted, which is normal. Its buffers must stay valid for the whole dispatch — see
        /// <see cref="ConversionResult" />.
        /// </param>
        public static void RaiseLandingResolved(MoveCommand command, ConversionResult conversions)
        {
            LandingResolved?.Invoke(command, conversions);
        }

        /// <summary>
        /// Publishes <see cref="AbilityResolved"/>. Called only after every impact of the deployment — a troop
        /// landing or a Protocol — has been applied to the units and the board.
        /// </summary>
        /// <param name="actingPlayerId">The player whose deployment resolved the impacts.</param>
        /// <param name="result">
        /// The units and hexes the impacts changed. Its buffers must stay valid for the whole dispatch — see
        /// <see cref="AbilityResult" />.
        /// </param>
        public static void RaiseAbilityResolved(int actingPlayerId, AbilityResult result)
        {
            AbilityResolved?.Invoke(actingPlayerId, result);
        }

        /// <summary>Publishes <see cref="FuseArmed"/>. Called once the unit is actually carrying the fuse.</summary>
        /// <param name="unitId">The unit whose fuse is now running.</param>
        /// <param name="playerId">The player who owns that unit right now.</param>
        /// <param name="remainingSeconds">Seconds of scaled match time left before it goes off.</param>
        public static void RaiseFuseArmed(int unitId, int playerId, float remainingSeconds)
        {
            FuseArmed?.Invoke(unitId, playerId, remainingSeconds);
        }

        /// <summary>
        /// Publishes <see cref="FuseExpired"/>. Called only after the unit has been removed from the registry
        /// and its cell released, so the board already matches what this reports.
        /// </summary>
        /// <param name="unitId">The unit the fuse removed. Already unregistered; the id will not resolve.</param>
        /// <param name="playerId">The player who owned it when the fuse ran out, captured before the removal.</param>
        public static void RaiseFuseExpired(int unitId, int playerId)
        {
            FuseExpired?.Invoke(unitId, playerId);
        }

        /// <summary>Publishes <see cref="HandChanged"/>. Called once the hand already holds what this reports.</summary>
        /// <param name="playerId">The player whose hand changed.</param>
        /// <param name="hand">
        /// The cards now in hand. The publisher's own storage, rewritten in place on every later change, so
        /// subscribers copy the entries they intend to keep rather than retaining the list.
        /// </param>
        /// <param name="nextCard">The card queued for the next freed slot.</param>
        public static void RaiseHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard)
        {
            HandChanged?.Invoke(playerId, hand, nextCard);
        }

        /// <summary>
        /// Publishes <see cref="CardDiscarded"/>. Called only after the hand has already rotated, once
        /// <see cref="HandChanged"/> has been raised for the same discard.
        /// </summary>
        /// <param name="playerId">The player who discarded the card.</param>
        /// <param name="discardedCard">The card that left the hand.</param>
        /// <param name="slotIndex">The zero-based hand slot it left.</param>
        public static void RaiseCardDiscarded(int playerId, CardId discardedCard, int slotIndex)
        {
            CardDiscarded?.Invoke(playerId, discardedCard, slotIndex);
        }

        /// <summary>
        /// Drops every subscriber. Runs automatically on subsystem registration because domain reload is
        /// disabled, and is called by tests to isolate fixtures from one another.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetEvents()
        {
            MatchStarted = null;
            GridInitialized = null;
            MatchPhaseChanged = null;
            MatchClockTicked = null;
            ScoreChanged = null;
            MatchEnded = null;
            EnergyChanged = null;
            EnergySpent = null;
            MoveExecuted = null;
            ConversionResolved = null;
            LandingResolved = null;
            AbilityResolved = null;
            FuseArmed = null;
            FuseExpired = null;
            HandChanged = null;
            CardDiscarded = null;
        }
    }
}
