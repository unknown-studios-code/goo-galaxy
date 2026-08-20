using System;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Match.Data;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Match.Controllers
{
    /// <summary>
    /// The match orchestrator: it owns the phase sequence and the clock, drives setup through
    /// <see cref="MatchInitializer" />, keeps the score, and decides when the match is over. The phase, the
    /// clock tick, the score and the outcome are published from here and from nowhere else; the two setup
    /// announcements — <c>GridInitialized</c> and <c>MatchStarted</c> — belong to
    /// <see cref="MatchInitializer" />, which raises them in the order its own body fixes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The clock is scaled, deliberately.</b> <c>Time.deltaTime</c>, never <c>Time.unscaledDeltaTime</c> —
    /// the same choice <c>FuseController</c> documents, and it has to be the same one: a fuse and the match
    /// clock that will end the match under it must run off one time base, or a pause desynchronizes them and a
    /// unit is lost to a countdown that ran while the board could not be played.
    /// </para>
    /// <para>
    /// <b>It hands itself to the two card controllers rather than being injected into them.</b>
    /// <see cref="DeployController" /> and <see cref="CardDiscardController" /> gate on the phase, and this
    /// component reads <see cref="DeployController.IsResolving" /> — a mutual dependency the container refuses,
    /// because VContainer walks <c>[Inject]</c> method parameters when it checks for cycles and throws at build
    /// time. Only one direction is therefore registered, the orchestrator's, and <see cref="Construct" /> pushes
    /// the back-reference. Both controllers live in this assembly, so the seam is invisible outside it.
    /// </para>
    /// <para>
    /// <b>Three things end a match, and all three are decided here.</b> The standard clock running out compares
    /// the unit counts, and a level comparison opens <see cref="MatchPhase.Overtime" /> rather than publishing a
    /// draw. Overtime keeps plays open, doubles energy regeneration, and is won by the first player to hold a
    /// unit-count lead unbroken for the authored hold — or, if its own clock runs out first, by whoever is
    /// ahead. Domination is the one ending that waits for no clock: the instant a recount finds one player
    /// holding every live unit, in either played phase, the match is over.
    /// </para>
    /// <para>
    /// <b>The overtime lead is read one frame late, deliberately — but never decided on.</b> The hold is ticked
    /// against the counts <see cref="LateUpdate" /> settled on the previous frame rather than against a fresh
    /// walk of the registry, for the same reason that recount is deferred at all: a count taken mid-resolution
    /// credits units that are about to vanish. One frame against a hold measured in seconds moves no outcome
    /// while the hold is still accumulating. It would move one on the frame the hold completes, so that frame
    /// recounts and re-confirms the lead before ending the match — the cache times the hold, the board settles
    /// it.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class MatchController : MonoBehaviour
    {
        // The two sides of a match. Constants rather than authored values because the rest of the runtime
        // already agrees on them — EnergyPresenter.InitializeMatch configures exactly 1 and 2, and the unit
        // views colour by the same ids. A networked session will hand these down instead of declaring them.
        private const int PlayerOneId = 1;

        private const int PlayerTwoId = 2;

        // No whole second has been published yet. Negative, so the first tick of any phase always publishes.
        private const int NoPublishedSecond = -1;

        // The route SetPhaseForTests walks, in the order MatchPhase declares its members — which is also the
        // one chain of legal edges MatchState's table draws out of None, so every phase is reachable along it.
        // Each hop is still put to the table rather than assumed; this array only says which hop to try next.
        private static readonly MatchPhase[] _phaseWalk = new MatchPhase[]
        {
            MatchPhase.Loading,
            MatchPhase.Countdown,
            MatchPhase.Standard,
            MatchPhase.OvertimeCheck,
            MatchPhase.Overtime,
            MatchPhase.Ended,
            MatchPhase.Results,
        };

        // The recount is a whole-registry walk behind a dirty flag, so without a marker of its own a non-deep
        // profile cannot separate a frame that actually recounted from the far more common one that only tested
        // the flag. Mirrors UnitView's marker over its own gated whole-registry pass.
        private static readonly ProfilerMarker _recountScoresMarker = new("MatchController.RecountScores");

        [Header("Match Setup")]
        [SerializeField]
        private MatchConfigSO _matchConfig;

        [Tooltip(
            "Seed for the deterministic deck shuffle. The same seed reproduces the same hand-cycle order for both players every time a match "
                + "starts — that is the point of it, and it is what makes a bug in a specific opening hand reproducible. Any value is valid, zero included."
        )]
        [SerializeField]
        private int _matchSeed;

        [Tooltip("Starts the match from Start(), which is what makes a scene playable on its own. Turn off when a lobby or a networked session starts it.")]
        [SerializeField]
        private bool _isAutoStartEnabled = true;

        private readonly MatchState _state = new();
        private readonly MatchClock _clock = new();
        private readonly OvertimeLeadTracker _overtimeLeadTracker = new();

        private MatchInitializer _initializer;
        private UnitPresenter _unitPresenter;
        private DeployController _deployController;
        private CardDiscardController _cardDiscardController;
        private EnergyPresenter _energyPresenter;
        private float _standardDurationSeconds;
        private float _overtimeDurationSeconds;
        private float _overtimeLeadHoldSeconds;
        private int _countdownTicks;
        private int _lastPublishedSecond = NoPublishedSecond;
        private bool _isScoreDirty;

        public MatchPhase Phase => _state.Phase;

        /// <summary>
        /// Seconds left on the clock the running phase is counting down — <see cref="MatchPhase.Standard" />,
        /// then <see cref="MatchPhase.Overtime" /> — at frame precision.
        /// </summary>
        /// <remarks>
        /// <b>Outside those two phases it reports whatever the clock was last left holding.</b> That is zero
        /// before a match starts and zero once a phase's clock has run out, but a match ended early — by
        /// domination, or by a lead held through overtime — leaves the seconds that were still on it, because
        /// ending a match stops the clock rather than draining it.
        /// <para>
        /// <b>Not the countdown.</b> <c>MatchEvents.MatchClockTicked</c> is the throttled form of this during
        /// the two played phases only: the pre-match countdown publishes from its own whole-second counter and
        /// never starts this clock, so a reader polling here through <see cref="MatchPhase.Countdown" /> sees
        /// zero while ticks are still going out.
        /// </para>
        /// </remarks>
        public float RemainingSeconds => _clock.Remaining;

        // The two phases the clock runs in, plays are accepted in, and a domination can end. Standard and
        // Overtime differ only in what their expiry resolves to, so every per-frame step but that one is shared.
        private bool IsPlayOpen => _state.Phase is MatchPhase.Standard or MatchPhase.Overtime;

        /// <remarks>
        /// Also completes the wiring in the other direction, handing this component to the two controllers that
        /// gate on the phase. See the class remarks for why that is a push rather than a second registration.
        /// <para>
        /// The energy presenter is taken concretely rather than as <c>IEnergyLedger</c>, which
        /// <see cref="MatchInitializer" /> already does for the same reason: the ledger is about affordability
        /// and payment, and what the orchestrator needs of it — doubling regeneration for overtime — is neither.
        /// Widening the ledger to carry a phase would put match flow into an interface the board depends on.
        /// </para>
        /// </remarks>
        [Inject]
        public void Construct(
            MatchInitializer initializer,
            UnitPresenter unitPresenter,
            DeployController deployController,
            CardDiscardController cardDiscardController,
            EnergyPresenter energyPresenter
        )
        {
            Debug.Assert(initializer != null, MatchLogMessages.MatchInitializerMissing, this);
            Debug.Assert(unitPresenter != null, MatchLogMessages.MatchUnitPresenterMissing, this);
            Debug.Assert(deployController != null, MatchLogMessages.MatchDeployControllerMissing, this);
            Debug.Assert(cardDiscardController != null, MatchLogMessages.MatchDiscardControllerMissing, this);
            Debug.Assert(energyPresenter != null, MatchLogMessages.MatchEnergyPresenterMissing, this);

            _initializer = initializer;
            _unitPresenter = unitPresenter;
            _deployController = deployController;
            _cardDiscardController = cardDiscardController;
            _energyPresenter = energyPresenter;

            if (_deployController != null)
            {
                _deployController.SetMatchController(this);
            }

            if (_cardDiscardController != null)
            {
                _cardDiscardController.SetMatchController(this);
            }
        }

        protected void OnEnable()
        {
            // Three seams rather than one, because a unit count moves in three unrelated ways and no single
            // event covers them. LandingResolved arrives once conversions have flipped units between the
            // players. AbilityResolved covers what a Protocol does, which rides on no move at all. FuseExpired
            // is the only removal nobody deployed.
            //
            // None of the three recounts on the spot: they raise a flag that LateUpdate flushes. AbilityController
            // publishes AbilityResolved and *then* runs its self-cleanup, so a unit an impact destroyed is still
            // registered and still alive while that callback runs — counting there would credit units that are
            // about to vanish. Deferring to the end of the frame is what makes the count read a settled board,
            // and it collapses a deployment's several events into one pass.
            MatchEvents.LandingResolved += HandleLandingResolved;
            MatchEvents.AbilityResolved += HandleAbilityResolved;
            MatchEvents.FuseExpired += HandleFuseExpired;
        }

        protected void Start()
        {
            // Start, not Awake: every OnEnable in the scene has run by now, so the GridInitialized the setup
            // sequence publishes reaches the views that subscribe there. GridPresenter builds the grid in Awake
            // and deliberately announces nothing, which is what leaves this the only announcement.
            if (!_isAutoStartEnabled)
            {
                return;
            }

            TryStartMatch();
        }

        protected void Update()
        {
            // Both played phases tick here. The clock, the elapsed accumulator and the throttled tick are
            // identical in either one; only what the expiry resolves to differs, which is the branch at the end.
            if (!IsPlayOpen)
            {
                return;
            }

            bool isOvertime = _state.Phase == MatchPhase.Overtime;
            float deltaTime = Time.deltaTime;

            _clock.Tick(deltaTime);
            _state.AddElapsed(deltaTime);
            PublishClockTick();

            // Tested before the expiry, so a hold that completes on the very frame the overtime clock drains
            // wins outright rather than being handed back to a comparison it has already survived.
            int leadWinnerId = isOvertime ? TickOvertimeLead(deltaTime) : MatchOutcome.NoWinner;
            bool hasLeadWinner = leadWinnerId != MatchOutcome.NoWinner;

            if (!hasLeadWinner && !_clock.HasExpired)
            {
                return;
            }

            // Deferred rather than dropped, and for both endings below. Scoring the match in the middle of a
            // landing would read a registry whose conversions are committed but not yet applied to every unit,
            // so the expiry is left latched and reclaimed on a later frame. The clock is already at zero either
            // way, and a lead that survives the resolution is still reported next frame — so nothing about
            // either deadline moves, only the moment the transition runs.
            if (_deployController != null && _deployController.IsResolving)
            {
                return;
            }

            if (hasLeadWinner)
            {
                // Recounted rather than trusted, for the same reason both expiries recount, and it matters more
                // here than anywhere: the hold was measured against counts LateUpdate settled on an earlier
                // frame, so a landing resolved earlier in this one has already moved the board without them.
                // Ending on the cache would name a winner the board no longer has, and would swallow that
                // landing's pending publish with it — ScoreChanged stops at match end, and LateUpdate returns on
                // a match that is no longer running.
                RecountScores(out int leadPlayerOneUnits, out int leadPlayerTwoUnits);

                MatchOutcome settled = MatchOutcomeResolver.ResolveByUnitCount(leadPlayerOneUnits, leadPlayerTwoUnits, PlayerOneId, PlayerTwoId);

                // Re-confirmed against the settled board, because a resolution that broke the lead has to break
                // the hold with it. Falling through costs nothing: the tracker reads the counts this recount has
                // just published on its next tick, and restarts the hold itself.
                if (settled.WinnerPlayerId == leadWinnerId)
                {
                    // TimeLimit rather than a reason of its own: what ended the match is a clock running out —
                    // the hold the lead had to survive — and the enum is destined for the wire, so a member is
                    // not added for a distinction a results screen can already draw from the phase it ended in.
                    EndMatch(new MatchOutcome(leadWinnerId, MatchEndReason.TimeLimit));
                }

                return;
            }

            if (isOvertime)
            {
                ResolveOvertimeExpiry();
                return;
            }

            ResolveTimeLimit();
        }

        protected void LateUpdate()
        {
            // PERF: gated on the flag, so an idle frame costs one bool read. The pass itself is a single walk of
            // the unit registry, and it runs on a frame where a landing, a Protocol impact or a fuse resolved —
            // never on the frame clock. A resolution that leaves both counts unchanged still costs the walk,
            // because MatchEvents.LandingResolved is raised on every executed move; TrySetScore is what keeps
            // the event itself silent afterwards.
            if (!_isScoreDirty)
            {
                return;
            }

            // Ticks stop at match end, which MatchEvents.ScoreChanged states as its contract: a fuse expiring or
            // an ability resolving after MatchPhase.Ended would otherwise republish a score behind a results
            // screen bound to it, moving counts the outcome has already been decided on. The flag is left set —
            // PrepareForNewMatch clears it before a rematch reads it.
            if (!_state.IsRunning)
            {
                return;
            }

            RecountScores(out int playerOneUnits, out int playerTwoUnits);

            // Tested after both counts have gone out, because MatchEvents.ScoreChanged stops at match end: the
            // counts the outcome is decided from have to reach a results screen while the match is still
            // running, and ending it first would swallow the very publish that says a player reached zero.
            //
            // Gated on the two played phases because a domination is only an ending in them. Before the board is
            // seeded both counts are zero, which is not a domination but is also not a match; and neither
            // Loading nor Countdown has a legal edge to Ended, so an ungated check that reached one of them
            // would log an illegal transition and abandon the match rather than end it. OvertimeCheck cannot
            // reach here at all — ResolveTimeLimit clears the dirty flag on its own recount and leaves the phase
            // past OvertimeCheck before Update returns — so the gate is what keeps that true rather than
            // incidental.
            if (!IsPlayOpen)
            {
                return;
            }

            if (!MatchOutcomeResolver.TryResolveDomination(playerOneUnits, playerTwoUnits, PlayerOneId, PlayerTwoId, out int winnerId))
            {
                return;
            }

            EndMatch(new MatchOutcome(winnerId, MatchEndReason.Domination));
        }

        protected void OnDisable()
        {
            MatchEvents.LandingResolved -= HandleLandingResolved;
            MatchEvents.AbilityResolved -= HandleAbilityResolved;
            MatchEvents.FuseExpired -= HandleFuseExpired;
        }

        /// <summary>
        /// Starts a match: seeds the board from the authored config, announces it, and runs the countdown into
        /// normal play.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Calling it while a match is running changes nothing.</b> It returns
        /// <see cref="MatchStartResult.AlreadyRunning" /> without re-seeding the board, re-dealing a hand, or
        /// publishing anything — a second call is a no-op, not a restart. A match that has ended is not running,
        /// so a rematch needs no explicit teardown.
        /// </para>
        /// <para>
        /// <b>An abandoned start is recoverable, never terminal.</b> Setup refusing the board, the component
        /// being disabled during the countdown, and any phase transition <see cref="MatchState" />'s table
        /// refuses all return the match to <see cref="MatchPhase.None" /> and publish that phase. None of them
        /// counts as running, so the next call starts a match normally — a lobby is free to switch this object
        /// off mid-countdown and start again later, and a sequencing defect costs the match rather than the
        /// session.
        /// </para>
        /// <para>
        /// Checks run in a fixed order, so the returned code is predictable when several would fail at once: a
        /// match already running, then the authored config, then the injected systems, then whatever setup
        /// decides. Setup is all-or-nothing — see <see cref="MatchStartResult" />.
        /// </para>
        /// <para>
        /// Returns as soon as the countdown has been started, not when it finishes. Normal play opens a few
        /// seconds later, announced by <c>MatchEvents.MatchPhaseChanged</c>.
        /// </para>
        /// </remarks>
        /// <returns>Success once the board is seeded and the countdown is running, or the reason it was refused.</returns>
        public MatchStartResult TryStartMatch()
        {
            if (_state.IsRunning)
            {
                return MatchStartResult.AlreadyRunning;
            }

            if (_matchConfig == null)
            {
                Debug.LogError(MatchLogMessages.MatchConfigMissing, this);
                return MatchStartResult.ConfigMissing;
            }

            if (_initializer == null || _unitPresenter == null)
            {
                Debug.LogError(MatchLogMessages.MatchDomainUnavailable, this);
                return MatchStartResult.DomainUnavailable;
            }

            PrepareForNewMatch();

            // Unreachable while the table stands: Reset leaves the state in None, and None -> Loading is legal.
            // Kept so a future edit to the table cannot quietly start a match that never entered a phase. It
            // abandons like every other refusal, which here only announces the None that Reset already left
            // behind — a subscriber still holding the previous match's Ended would otherwise never be told.
            if (!TryChangePhase(MatchPhase.Loading))
            {
                AbandonStart();

                return MatchStartResult.DomainUnavailable;
            }

            var configuration = new MatchConfiguration(
                _matchSeed,
                PlayerOneId,
                PlayerTwoId,
                _standardDurationSeconds,
                _matchConfig.CountdownSeconds,
                _overtimeDurationSeconds
            );

            MatchStartResult setup = _initializer.InitializeMatch(_matchConfig, configuration);

            if (setup != MatchStartResult.Success)
            {
                if (setup == MatchStartResult.DomainUnavailable)
                {
                    Debug.LogError(MatchLogMessages.MatchDomainUnavailable, this);
                }

                AbandonStart();

                return setup;
            }

            // The opening score, published before anybody can change it, so a HUD binding to a starting match
            // shows the seeded counts rather than waiting for the first deployment to move one. The counts are
            // discarded: the phase is Loading, so a seeded board that somehow held only one player's units is
            // not a domination yet — LateUpdate tests that once play is open.
            RecountScores(out _, out _);

            _ = RunCountdownAsync();

            return MatchStartResult.Success;
        }

        /// <remarks>
        /// Test-only seam: assigns the three fields an Inspector would otherwise author, so a fixture can drive
        /// <see cref="TryStartMatch" /> without a committed asset. Must run before the component is activated —
        /// <see cref="Start" /> reads the auto-start flag once, and a fixture that assigns it afterwards has
        /// already lost the race.
        /// </remarks>
        internal void SetMatchConfigForTests(MatchConfigSO matchConfig, int matchSeed, bool isAutoStartEnabled)
        {
            _matchConfig = matchConfig;
            _matchSeed = matchSeed;
            _isAutoStartEnabled = isAutoStartEnabled;
        }

        /// <remarks>
        /// Test-only seam: walks <see cref="MatchState" />'s own transition table from
        /// <see cref="MatchPhase.None" /> to the phase asked for, without running setup or the countdown, so a
        /// fixture exercising the phase gate on <see cref="DeployController" /> or
        /// <see cref="CardDiscardController" /> does not have to build a full match domain — a
        /// <c>MatchConfigSO</c>, a board, a deck, and an energy ledger — just to reach
        /// <see cref="MatchPhase.Standard" />. Publishes nothing on <c>MatchEvents</c>.
        /// <para>
        /// <b>Every step is checked against the table, and an unreachable phase throws.</b> The walk asserts
        /// each hop rather than assuming it, so an edit to the table that breaks the chain — or a phase member
        /// added without an edge into it — fails the seam loudly instead of leaving the controller in whatever
        /// phase the walk happened to stop in while the caller believes otherwise.
        /// </para>
        /// <para>
        /// <b>The clock it leaves behind is not running.</b> Nothing here calls <see cref="MatchClock.Reset" />,
        /// so a controller parked in <see cref="MatchPhase.Standard" /> this way reports a
        /// <see cref="RemainingSeconds" /> of zero and its <c>Update</c> can never reach the time limit. A
        /// fixture that needs the clock to run has to start a real match.
        /// </para>
        /// </remarks>
        internal void SetPhaseForTests(MatchPhase phase)
        {
            _state.Reset();

            if (phase == MatchPhase.None)
            {
                return;
            }

            for (int i = 0; i < _phaseWalk.Length; i++)
            {
                MatchPhase step = _phaseWalk[i];

                if (!_state.TryTransition(step))
                {
                    throw new InvalidOperationException(string.Format(MatchLogMessages.PhaseWalkRefusedFormat, _state.Phase, step, phase));
                }

                if (step == phase)
                {
                    return;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(phase), phase, MatchLogMessages.PhaseWalkUnreachable);
        }

        private void PrepareForNewMatch()
        {
            _state.Reset();
            _clock.Reset(0f);
            _overtimeLeadTracker.Reset();
            _lastPublishedSecond = NoPublishedSecond;
            _isScoreDirty = false;

            // Captured at start, so an Inspector edit made mid-match changes the next match rather than the
            // running one — the guarantee CardDefinition makes for card data, made here for the clock. Overtime
            // is captured on the same terms even though it is read minutes later, because the alternative is a
            // match whose sudden death lasts whatever the asset happened to say by the time it got there.
            _standardDurationSeconds = _matchConfig.StandardDurationSeconds;
            _overtimeDurationSeconds = _matchConfig.OvertimeDurationSeconds;
            _overtimeLeadHoldSeconds = _matchConfig.OvertimeLeadHoldSeconds;

            // Counted down one whole second at a time, so a fractional authored countdown rounds up to the next
            // whole tick rather than ending early. At the authored 3 seconds this is the GDD's "3 2 1 GO".
            _countdownTicks = Mathf.Max(1, Mathf.CeilToInt(_matchConfig.CountdownSeconds));
        }

        // Reset rather than a transition: MatchState's table has no edge out of Loading or Countdown back to
        // None, because a start that never opened normal play is abandoned rather than finished. The phase is
        // still published, so a subscriber that already saw Loading or Countdown is told the match is not
        // coming.
        private void AbandonStart()
        {
            _state.Reset();
            MatchEvents.RaiseMatchPhaseChanged(_state.Phase);
        }

        private async Awaitable RunCountdownAsync()
        {
            bool wasInterruptedByDisable = false;

            try
            {
                // Inside the guard, not above it: this transition publishes MatchPhaseChanged to arbitrary
                // subscribers exactly as the ticks below publish MatchClockTicked, and a throw here used to
                // escape into the Awaitable the caller discards — leaving the match parked in Countdown, which
                // is the very stranding the catch was written to prevent, one step earlier.
                if (!TryChangePhase(MatchPhase.Countdown))
                {
                    AbandonStart();
                    return;
                }

                for (int remaining = _countdownTicks; remaining > 0; remaining--)
                {
                    MatchEvents.RaiseMatchClockTicked(remaining);

                    await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);

                    if (this == null)
                    {
                        return;
                    }

                    if (!isActiveAndEnabled)
                    {
                        wasInterruptedByDisable = true;
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The scene tore down mid-countdown. Nothing is published: the match never reached normal play,
                // and a phase event raised from a destroyed component would reach subscribers of a scene that
                // no longer exists.
                return;
            }
            catch (Exception exception)
            {
                // Deliberately broad, and the same dispatch-boundary catch FuseController makes: the entry
                // transition and the ticks above both call into arbitrary subscriber code, and the caller
                // discards this Awaitable, so an escaping throw would vanish and leave the match sitting in
                // Countdown forever. Nothing is swallowed — the stack is logged — and play still opens, because
                // a broken HUD must not hang a match.
                Debug.LogError(MatchLogMessages.CountdownSubscriberFailed, this);
                Debug.LogException(exception, this);
            }

            // Abandoned, not merely dropped. destroyCancellationToken fires on destroy and never on disable, so
            // the awaits above keep running while the component is switched off — and simply returning would
            // leave the match parked in Countdown, which MatchState counts as running, so every later
            // TryStartMatch would answer AlreadyRunning for the rest of the session. Resetting is what makes a
            // lobby that toggles this object able to start a match afterwards.
            //
            // Guarded separately from the loop rather than inside it: both exits publish, and this still runs on
            // the tail of an Awaitable nobody awaits, so a throw here would vanish too — but the catch above has
            // to fall through to exactly one of these two, so it cannot also be the thing that catches them. It
            // stays narrow: every statement it spans either transitions the state machine or raises an event, so
            // there is no orchestrator logic inside it for a broad catch to swallow.
            try
            {
                if (wasInterruptedByDisable)
                {
                    AbandonStart();
                    return;
                }

                BeginStandardPhase();
            }
            catch (Exception exception)
            {
                Debug.LogError(MatchLogMessages.PhaseSubscriberFailed, this);
                Debug.LogException(exception, this);
            }
        }

        private void BeginStandardPhase()
        {
            _clock.Reset(_standardDurationSeconds);
            _lastPublishedSecond = NoPublishedSecond;

            // Recoverable rather than terminal, like every other refusal here: the match would otherwise sit in
            // Countdown, which MatchState counts as running, and every later TryStartMatch would answer
            // AlreadyRunning for the rest of the session.
            if (!TryChangePhase(MatchPhase.Standard))
            {
                AbandonStart();
                return;
            }

            // The phase's opening tick, so a HUD renders the full duration on the frame play opens rather than
            // a second later.
            PublishClockTick();
        }

        // PERF: allocation-free and called every frame of both played phases. The comparison is against the last
        // published whole second rather than an accumulated timer, so a long frame that crosses two seconds
        // publishes once and lands on the correct value instead of drifting.
        private void PublishClockTick()
        {
            int wholeSecond = Mathf.FloorToInt(_clock.Remaining);

            if (wholeSecond == _lastPublishedSecond)
            {
                return;
            }

            _lastPublishedSecond = wholeSecond;

            MatchEvents.RaiseMatchClockTicked(wholeSecond);
        }

        // PERF: allocation-free, and the only per-frame work overtime adds — two dictionary reads and an
        // accumulator. The counts come from MatchState's cache rather than a fresh walk of the registry, so a
        // lead is recognised on the frame after the landing that created it; see the class remarks for why one
        // frame of lag is the correct trade against reading a board mid-resolution.
        private int TickOvertimeLead(float deltaTime)
        {
            return _overtimeLeadTracker.Tick(
                _state.GetScore(PlayerOneId),
                _state.GetScore(PlayerTwoId),
                PlayerOneId,
                PlayerTwoId,
                _overtimeLeadHoldSeconds,
                deltaTime
            );
        }

        // The whole of the standard time-limit decision. A clear lead ends the match; level counts are what
        // Overtime exists for, and are the only route into it.
        private void ResolveTimeLimit()
        {
            // Recoverable rather than terminal. The match would otherwise sit in Standard at zero forever — the
            // clock cannot expire twice, so nothing would ever try this transition again, and MatchState counts
            // Standard as running.
            if (!TryChangePhase(MatchPhase.OvertimeCheck))
            {
                AbandonStart();
                return;
            }

            // Consumed only now that the transition acting on the edge has been accepted, rather than by the
            // caller before it could refuse. From here the phase is what makes this run once — Update reaches
            // this branch only from Standard, which the transition above has just left — so the latch is
            // bookkeeping, and MatchClock's "exactly one caller per expiry" contract is honoured rather than
            // spent on a transition that might not happen.
            _ = _clock.TryConsumeExpiry();

            // Recounted rather than trusted: the cached scores are only as fresh as the last deployment, and a
            // fuse that expired on this same frame has not published anything yet.
            RecountScores(out int playerOneUnits, out int playerTwoUnits);

            if (playerOneUnits == playerTwoUnits)
            {
                BeginOvertimePhase();
                return;
            }

            EndMatch(MatchOutcomeResolver.ResolveByUnitCount(playerOneUnits, playerTwoUnits, PlayerOneId, PlayerTwoId));
        }

        // The overtime clock running out, which unlike the standard one has nothing left to break a tie with:
        // whoever is ahead wins, and level counts publish a draw.
        private void ResolveOvertimeExpiry()
        {
            // Consumed here rather than by the caller, on the same terms as ResolveTimeLimit: from Overtime the
            // only transition left is the one this method makes, so the latch is spent on an ending that
            // actually happens.
            _ = _clock.TryConsumeExpiry();

            RecountScores(out int playerOneUnits, out int playerTwoUnits);

            EndMatch(MatchOutcomeResolver.ResolveByUnitCount(playerOneUnits, playerTwoUnits, PlayerOneId, PlayerTwoId));
        }

        private void BeginOvertimePhase()
        {
            _clock.Reset(_overtimeDurationSeconds);
            _lastPublishedSecond = NoPublishedSecond;

            // Cleared rather than assumed clear: the tracker is only reset per match otherwise, and overtime is
            // the one phase that reads it — a hold must start from zero however the previous match left it.
            _overtimeLeadTracker.Reset();

            // Recoverable rather than terminal, like every other refusal here: the match would otherwise sit in
            // OvertimeCheck, which MatchState counts as running, and every later TryStartMatch would answer
            // AlreadyRunning for the rest of the session.
            if (!TryChangePhase(MatchPhase.Overtime))
            {
                AbandonStart();
                return;
            }

            // After the transition, so a start abandoned on the edge above does not leave both players
            // regenerating at double rate with no overtime to spend it in.
            SetEnergyOvertime(true);

            // The phase's opening tick, so a HUD renders the full overtime duration on the frame it opens rather
            // than a second later. The same reset-and-publish BeginStandardPhase makes, for the same reason.
            PublishClockTick();
        }

        private void EndMatch(MatchOutcome outcome)
        {
            // First, and unconditionally, because EnergyPresenter.Update is not phase-gated: a match that
            // reached overtime would otherwise keep regenerating at double rate behind a results screen. It runs
            // ahead of the transition so the refusal path below stops the doubling too.
            SetEnergyOvertime(false);

            // Recoverable rather than terminal: a refusal leaves the match in whichever phase it was ending
            // from — Standard on a domination, Overtime, or OvertimeCheck — every one of which MatchState counts
            // as running, and no outcome is published either way. Abandoning is what lets the session start
            // another match instead of answering AlreadyRunning forever.
            if (!TryChangePhase(MatchPhase.Ended))
            {
                AbandonStart();
                return;
            }

            // After the transition, so a subscriber reading Phase sees the state this event describes.
            MatchEvents.RaiseMatchEnded(outcome);
        }

        private bool TryChangePhase(MatchPhase next)
        {
            if (!_state.TryTransition(next))
            {
                Debug.LogError(string.Format(MatchLogMessages.IllegalPhaseTransitionFormat, _state.Phase, next), this);
                return false;
            }

            MatchEvents.RaiseMatchPhaseChanged(next);

            return true;
        }

        private void SetEnergyOvertime(bool isActive)
        {
            if (_energyPresenter == null)
            {
                return;
            }

            _energyPresenter.SetOvertime(isActive);
        }

        // PERF: hands the counted pair back rather than making the caller read it out of MatchState again, so
        // the domination test and the two expiry comparisons all run on this one walk. Calling the counter a
        // second time would double a whole-registry pass on a path budgeted at zero allocations, and overtime
        // already doubles how often the pass runs.
        private void RecountScores(out int playerOneUnits, out int playerTwoUnits)
        {
            using (_recountScoresMarker.Auto())
            {
                _isScoreDirty = false;

                // PERF: both counts off one walk of the registry rather than one walk per player. They also
                // describe the same board by construction, which matters at the time limit and at a domination,
                // where the pair is what decides the winner.
                MatchScoreCounter.CountLiveUnits(_unitPresenter, PlayerOneId, PlayerTwoId, out playerOneUnits, out playerTwoUnits);

                PublishScore(PlayerOneId, playerOneUnits);
                PublishScore(PlayerTwoId, playerTwoUnits);
            }
        }

        private void PublishScore(int playerId, int unitCount)
        {
            if (!_state.TrySetScore(playerId, unitCount))
            {
                return;
            }

            MatchEvents.RaiseScoreChanged(playerId, unitCount);
        }

        private void HandleLandingResolved(MoveCommand command, ConversionResult conversions)
        {
            _isScoreDirty = true;
        }

        private void HandleAbilityResolved(int actingPlayerId, AbilityResult result)
        {
            _isScoreDirty = true;
        }

        private void HandleFuseExpired(int unitId, int playerId)
        {
            _isScoreDirty = true;
        }
    }
}
