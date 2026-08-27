using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Models;
using GooGalaxy.Runtime.UI.Views;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.UI.Presenters
{
    /// <summary>
    /// Drives an <see cref="IMatchHudView" /> from the match bus: it owns the screen's state, decides what
    /// every phase shows, and writes typed values into the sink. It enforces no match rule and raises no command.
    /// </summary>
    /// <remarks>
    /// <b>It calls the contract, and holds the component.</b> The serialized reference stays a
    /// <see cref="MatchHudView" /> so a prefab can author it and so the Unity null check runs against a real
    /// <c>UnityEngine.Object</c>; every call afterwards goes through <see cref="IMatchHudView" />, which is what
    /// lets a fixture assert the event-to-setter mapping against a double, with no <c>UIDocument</c> and no
    /// panel behind it. The fixture is still a PlayMode one, because this component is only reachable through
    /// <c>OnEnable</c>: the seam buys a panel-free double, not an EditMode test.
    /// <para>
    /// <b>It subscribes first, then pulls, and is ordered ahead of the orchestrator — all three.</b> The
    /// execution order below places this component before <c>MatchController</c> so no opening event is missed;
    /// the snapshot taken in <c>OnEnable</c> covers the case where the HUD is enabled into a match already
    /// running, which no ordering can fix; and the snapshot alone would still leave a frame of zeroes on a cold
    /// start. The attribute is the guarantee nobody re-reads when the next component is added, so it is not left
    /// to carry the case on its own.
    /// </para>
    /// <para>
    /// <b>The local seat comes from the announced configuration, never from a constant.</b> The home side is
    /// whichever seat is driven by the person holding the device. Both seats can be local (a hot-seat match, in
    /// which case the first seat is home), and neither can be (a machine-versus-machine debug match, which logs
    /// once and renders player one as home rather than throwing).
    /// </para>
    /// <para>
    /// <b>A HUD enabled into a running match resolves its seats too.</b> The announcement settles them on
    /// <c>MatchStarted</c>, and <c>MatchController.Configuration</c> settles them for a component that was not
    /// listening when it went out — which is why the snapshot reads the seats before anything per-seat.
    /// </para>
    /// </remarks>
    // Ahead of MatchController, which sits at the default zero, and behind the lifetime scopes at -5000, so the
    // container has already injected by the time this component subscribes. Written as a literal because an
    // attribute on a type cannot see that type's own constants.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class MatchHudPresenter : MonoBehaviour
    {
        // The seat the HUD falls back to when the configuration names no local player.
        private const int FallbackHomePlayerId = 1;

        private const int FallbackAwayPlayerId = 2;

        // No clock is running, which renders as placeholder glyphs rather than as zero.
        private const int NoTimer = -1;

        // Where the timer turns urgent, in every phase. Orange rather than red: red is reserved for a failed
        // action, and a clock running out is not one.
        private const int UrgentThresholdSeconds = 30;

        private const float OvertimeBannerSeconds = 2f;

        // Energy regenerates in fractional steps, so equality against the cap never lands exactly.
        private const float CapTolerance = 0.01f;

        [SerializeField]
        private MatchHudView _view;

        [Tooltip("Draws the opponent's score in the top bar. Turn it off for a clean playtest capture; nothing else on the HUD changes.")]
        [SerializeField]
        private bool _isOpponentScoreShown = true;

        private readonly HandSlotState[] _handStates = new HandSlotState[HudSelectors.HandSlotCount];
        private readonly bool[] _areHandSlotsAffordable = new bool[HudSelectors.HandSlotCount];

        private IMatchHudView _hud;
        private MatchController _matchController;
        private EnergyPresenter _energyPresenter;
        private DeckPresenter _deckPresenter;
        private CardPresenter _cardPresenter;

        private HandSlotState _nextCardState = HandSlotState.Empty;
        private MatchPhase _phase = MatchPhase.None;
        private string _opponentLabel = HudText.OpponentUnknown;
        private string _outcomeTitle = string.Empty;
        private string _outcomeReason = string.Empty;
        private int _localPlayerId = FallbackHomePlayerId;
        private int _opponentPlayerId = FallbackAwayPlayerId;
        private int _localScore;
        private int _opponentScore;
        private int _timerSeconds = NoTimer;
        private int _countdownSeconds = NoTimer;
        private int _catchUpSeconds;
        private float _energy;
        private float _maxEnergy;
        private bool _isEnergyAtCap;
        private bool _isOvertime;
        private bool _isCatchUpActive;
        private bool _isCountdownShown;
        private bool _isOvertimeBannerShown;
        private bool _isOutcomeShown;
        private bool _isAffordabilityDrawn;
        private bool _isHandOverflowLogged;

        // Standard and Overtime, the two phases the clock runs in and a card can be played in. Affordability is
        // only meaningful inside them, which is why a slot is never dimmed before the first of them opens.
        private bool IsPlayOpen => _phase is MatchPhase.Standard or MatchPhase.Overtime;

        /// <remarks>
        /// The three feature presenters are taken concretely rather than through the interfaces the board uses,
        /// because none of those interfaces carries a read. <c>IEnergyLedger</c> is affordability and payment and
        /// has no <c>GetEnergy</c>; <c>ICardCycle</c> rotates a hand and has no <c>TryGetNextCard</c>. Widening
        /// either to serve a HUD would put screen concerns into contracts the board depends on, and would break
        /// every test double already implementing them. <c>MatchController</c> is taken for the same reason it is
        /// elsewhere: the opening phase, clock and scores exist nowhere else to pull from.
        /// </remarks>
        [Inject]
        public void Construct(MatchController matchController, EnergyPresenter energyPresenter, DeckPresenter deckPresenter, CardPresenter cardPresenter)
        {
            Debug.Assert(matchController != null, UiLogMessages.HudMatchControllerMissing, this);
            Debug.Assert(energyPresenter != null, UiLogMessages.HudEnergyPresenterMissing, this);
            Debug.Assert(deckPresenter != null, UiLogMessages.HudDeckPresenterMissing, this);
            Debug.Assert(cardPresenter != null, UiLogMessages.HudCardPresenterMissing, this);

            _matchController = matchController;
            _energyPresenter = energyPresenter;
            _deckPresenter = deckPresenter;
            _cardPresenter = cardPresenter;
        }

        protected void OnEnable()
        {
            IMatchHudView hud = ResolveSink();

            if (hud != null)
            {
                hud.PanelInitialized += HandlePanelInitialized;
            }

            MatchEvents.MatchStarted += HandleMatchStarted;
            MatchEvents.MatchPhaseChanged += HandleMatchPhaseChanged;
            MatchEvents.MatchClockTicked += HandleMatchClockTicked;
            MatchEvents.ScoreChanged += HandleScoreChanged;
            MatchEvents.MatchEnded += HandleMatchEnded;
            MatchEvents.EnergyChanged += HandleEnergyChanged;
            MatchEvents.CatchUpChanged += HandleCatchUpChanged;
            MatchEvents.HandChanged += HandleHandChanged;

            PullSnapshot();
            PushAll();
        }

        protected void Start()
        {
            if (ResolveSink() != null)
            {
                return;
            }

            // Reported from Start rather than from OnEnable so the message names a fully constructed component:
            // injection and every OnEnable in the scene have run by now, so a HUD still holding no sink is
            // genuinely unwired rather than caught half-built. Any scene or fixture carrying this component is
            // expected to author a view for it, the way one already authors a Kit for DeckPresenter.
            Debug.LogError(UiLogMessages.HudViewMissing, this);
        }

        protected void OnDisable()
        {
            MatchEvents.MatchStarted -= HandleMatchStarted;
            MatchEvents.MatchPhaseChanged -= HandleMatchPhaseChanged;
            MatchEvents.MatchClockTicked -= HandleMatchClockTicked;
            MatchEvents.ScoreChanged -= HandleScoreChanged;
            MatchEvents.MatchEnded -= HandleMatchEnded;
            MatchEvents.EnergyChanged -= HandleEnergyChanged;
            MatchEvents.CatchUpChanged -= HandleCatchUpChanged;
            MatchEvents.HandChanged -= HandleHandChanged;

            IMatchHudView hud = ResolveSink();

            if (hud != null)
            {
                hud.PanelInitialized -= HandlePanelInitialized;
            }
        }

        /// <remarks>
        /// Test-only seam: assigns the sink an Inspector otherwise authors, as the contract rather than as the
        /// component, so a fixture can assert the event-to-setter mapping without a panel. Must run before the
        /// component is enabled, because subscription to the sink happens there. A double survives an enable
        /// cycle: the serialized reference only replaces it when one is actually authored.
        /// </remarks>
        internal void SetViewForTests(IMatchHudView view)
        {
            _hud = view;
        }

        internal void SetOpponentScoreShownForTests(bool isShown)
        {
            _isOpponentScoreShown = isShown;
        }

        private static string ResolveOpponentLabel(PlayerControl control)
        {
            return control switch
            {
                PlayerControl.Machine => HudText.OpponentMachine,
                PlayerControl.RemoteHuman => HudText.OpponentRemote,
                _ => HudText.OpponentUnknown,
            };
        }

        private static string ResolveOutcomeReason(MatchEndReason reason)
        {
            return reason switch
            {
                MatchEndReason.TimeLimit => HudText.ReasonTimeLimit,
                MatchEndReason.Domination => HudText.ReasonDomination,
                MatchEndReason.Draw => HudText.ReasonDraw,
                MatchEndReason.Surrender => HudText.ReasonSurrender,
                _ => HudText.ReasonUnknown,
            };
        }

        private static HandSlotKind ResolveSlotKind(CardType type)
        {
            return type == CardType.Spell ? HandSlotKind.Protocol : HandSlotKind.Specimen;
        }

        private async Awaitable HideOvertimeBannerAsync()
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(OvertimeBannerSeconds, destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Reset before the lifecycle guard, never after it. destroyCancellationToken does not fire on a
            // disable, so a HUD disabled inside the banner window resumes here and would return with the latch
            // still set — and Overtime is entered once per match, so nothing would ever clear it again and every
            // later PushAll would re-show the banner.
            _isOvertimeBannerShown = false;

            if ((this == null) || !isActiveAndEnabled)
            {
                return;
            }

            ResolveSink()?.SetOvertimeBannerVisible(false);
        }

        private string ResolveOutcomeTitle(in MatchOutcome outcome)
        {
            if (outcome.IsDraw)
            {
                return HudText.OutcomeDraw;
            }

            return outcome.WinnerPlayerId == _localPlayerId ? HudText.OutcomeVictory : HudText.OutcomeDefeat;
        }

        private IMatchHudView ResolveSink()
        {
            // Re-derived on every write path rather than resolved once, because the question is lifetime and not
            // presence: a sink backed by a component that has since been destroyed still compares non-null
            // through the interface, since the overloaded equality operator lives on UnityEngine.Object and an
            // interface reference never reaches it. Only the concrete field can answer, and it answers here. The
            // call costs one native alive-check and is idempotent, so every path may take it.
            if (_view != null)
            {
                _hud = _view;
                return _hud;
            }

            // Without this, an authored view that has since been destroyed leaves a sink that throws
            // MissingReferenceException on the next match event and keeps throwing for the rest of the session,
            // because the presenter stays subscribed to a bus that outlives the view. The cast is what makes the
            // discard Unity-aware, and it is load-bearing: a bare `is MatchHudView` would also throw away a view
            // that is alive but reached the sink through SetViewForTests, which never fills the serialized field.
            // A sink no component backs at all is a plain double, and is left alone.
            if ((_hud is MatchHudView authoredView) && (authoredView == null))
            {
                _hud = null;
            }

            return _hud;
        }

        private void ResolveSeats(MatchConfiguration config)
        {
            PlayerSlot one = config.PlayerOne;
            PlayerSlot two = config.PlayerTwo;

            if (one.Control == PlayerControl.LocalHuman)
            {
                ApplySeats(one, two);
                return;
            }

            if (two.Control == PlayerControl.LocalHuman)
            {
                ApplySeats(two, one);
                return;
            }

            ApplySeats(one, two);

            // Once per enable, not once per session: the snapshot resolves seats again every time the HUD is
            // enabled into a running match, so a machine-versus-machine debug session repeats this.
            Debug.LogWarning(string.Format(UiLogMessages.HudLocalSeatUnresolvedFormat, one.Control, two.Control, _localPlayerId), this);
        }

        private void ApplySeats(PlayerSlot home, PlayerSlot away)
        {
            _localPlayerId = home.Id == PlayerSlot.UnassignedId ? FallbackHomePlayerId : home.Id;
            _opponentPlayerId = away.Id == PlayerSlot.UnassignedId ? FallbackAwayPlayerId : away.Id;
            _opponentLabel = ResolveOpponentLabel(away.Control);
        }

        private void PullSnapshot()
        {
            if (_matchController != null)
            {
                // Seats first, because every read below is per-seat: resolving them afterwards would snapshot a
                // HUD that enabled into a running match against the fallback pair. Skipped before the first
                // match of the session, where the configuration is still default and ResolveSeats would report
                // an unresolved seat for a match nobody has configured yet.
                MatchConfiguration configuration = _matchController.Configuration;
                bool hasSeats = (configuration.PlayerOne.Control != PlayerControl.Unassigned) || (configuration.PlayerTwo.Control != PlayerControl.Unassigned);

                if (hasSeats)
                {
                    ResolveSeats(configuration);
                }

                _phase = _matchController.Phase;
                _localScore = _matchController.ScoreOf(_localPlayerId);
                _opponentScore = _matchController.ScoreOf(_opponentPlayerId);
                _timerSeconds = IsPlayOpen ? Mathf.CeilToInt(_matchController.RemainingSeconds) : NoTimer;
                _isCountdownShown = _phase == MatchPhase.Countdown;
            }

            RefreshMaxEnergy();

            if (_energyPresenter != null)
            {
                _energy = _energyPresenter.GetEnergy(_localPlayerId);
                _isEnergyAtCap = IsAtCap(_energy);
            }

            PullHand();
        }

        private void PullHand()
        {
            if (_deckPresenter == null)
            {
                return;
            }

            if (_deckPresenter.TryGetHand(_localPlayerId, out IReadOnlyList<CardId> hand))
            {
                ApplyHand(hand);
            }

            if (_deckPresenter.TryGetNextCard(_localPlayerId, out CardId nextCard))
            {
                _nextCardState = BuildSlotState(nextCard);
            }
        }

        private void ApplyHand(IReadOnlyList<CardId> hand)
        {
            if ((hand.Count > _handStates.Length) && !_isHandOverflowLogged)
            {
                _isHandOverflowLogged = true;
                Debug.LogWarning(string.Format(UiLogMessages.HudHandLongerThanStripFormat, hand.Count, _handStates.Length), this);
            }

            // PERF: indexed, never foreach. The hand is the deck's own storage handed out through IReadOnlyList, and
            // enumerating it through the interface allocates an enumerator at every static type it is read from.
            for (int i = 0; i < _handStates.Length; i++)
            {
                CardId cardId = i < hand.Count ? hand[i] : CardId.Empty;
                _handStates[i] = BuildSlotState(cardId);
            }
        }

        private HandSlotState BuildSlotState(CardId cardId)
        {
            if (cardId == CardId.Empty)
            {
                return HandSlotState.Empty;
            }

            if (_cardPresenter == null)
            {
                return HandSlotState.Empty;
            }

            if (!_cardPresenter.TryGetCard(cardId, out ICardData card))
            {
                Debug.LogWarning(string.Format(UiLogMessages.HudCardDataMissingFormat, cardId.Value), this);
                return HandSlotState.Empty;
            }

            return new HandSlotState(cardId, card.DisplayName, card.EnergyCost, ResolveSlotKind(card.Type), card.Accent);
        }

        private void RefreshMaxEnergy()
        {
            if ((_maxEnergy > 0f) || (_energyPresenter == null))
            {
                return;
            }

            EnergyState state = _energyPresenter.GetState(_localPlayerId);

            if (state == null)
            {
                return;
            }

            // Read off the live state rather than retained: the state object is the ledger's, and only the cap
            // it was configured with is wanted here.
            _maxEnergy = state.Config.MaxEnergy;
        }

        private bool IsAtCap(float energy)
        {
            return (_maxEnergy > 0f) && (energy >= (_maxEnergy - CapTolerance));
        }

        private EnergyGaugeAccent ResolveAccent()
        {
            if (_isOvertime)
            {
                return EnergyGaugeAccent.Overtime;
            }

            if (_isCatchUpActive)
            {
                return EnergyGaugeAccent.CatchUp;
            }

            return _isEnergyAtCap ? EnergyGaugeAccent.AtCap : EnergyGaugeAccent.None;
        }

        private void RefreshAffordability()
        {
            IMatchHudView hud = ResolveSink();

            for (int i = 0; i < _handStates.Length; i++)
            {
                // An empty slot is not unaffordable. Dimming one reads as "you nearly have enough" for a card
                // that is not there, and nothing is dimmed at all before the first played phase opens.
                bool isAffordable = !IsPlayOpen || !_handStates[i].IsFilled || CanAffordSlot(i);

                if (_isAffordabilityDrawn && (isAffordable == _areHandSlotsAffordable[i]))
                {
                    continue;
                }

                _areHandSlotsAffordable[i] = isAffordable;

                hud?.SetHandSlotAffordable(i, isAffordable);
            }

            _isAffordabilityDrawn = true;
        }

        private bool CanAffordSlot(int slotIndex)
        {
            if (_energyPresenter == null)
            {
                return false;
            }

            // A Deploy is priced at exactly the card's authored cost, so the ledger answers affordability with
            // no widening and no second pricing rule living here.
            return _energyPresenter.CanAffordMove(_localPlayerId, MoveType.Deploy, _handStates[slotIndex].EnergyCost);
        }

        private void PushAll()
        {
            IMatchHudView hud = ResolveSink();

            if (hud == null)
            {
                return;
            }

            hud.SetHudVisible(_phase != MatchPhase.Loading);
            hud.SetSeats(_localPlayerId, _opponentPlayerId);
            hud.SetOpponentLabel(_opponentLabel);
            hud.SetOpponentScoreVisible(_isOpponentScoreShown);
            hud.SetLocalScore(_localScore);
            hud.SetOpponentScore(_opponentScore);
            hud.SetCountdownVisible(_isCountdownShown);
            hud.SetOvertimeBannerVisible(_isOvertimeBannerShown);

            // Skipped while unknown rather than pushed as zero. Nothing exposes a running countdown to pull, so
            // a HUD enabled midway through one has no value until the next tick, and the overlay draws every
            // value at or below zero as "0" — which would read as the countdown having finished.
            if (_countdownSeconds >= 0)
            {
                hud.SetCountdownSeconds(_countdownSeconds);
            }

            PushTimer();
            PushEnergy();
            PushCatchUp();
            PushHand();
            PushOutcome();

            _isAffordabilityDrawn = false;
            RefreshAffordability();
        }

        private void PushTimer()
        {
            IMatchHudView hud = ResolveSink();

            if (hud == null)
            {
                return;
            }

            if (_timerSeconds < 0)
            {
                hud.ClearTimer();
                hud.SetTimerUrgent(false);
                return;
            }

            hud.SetTimerSeconds(_timerSeconds);
            hud.SetTimerUrgent(_timerSeconds < UrgentThresholdSeconds);
        }

        private void PushEnergy()
        {
            IMatchHudView hud = ResolveSink();

            if (hud == null)
            {
                return;
            }

            var state = new EnergyGaugeState(
                _maxEnergy > 0f ? _energy / _maxEnergy : 0f,
                Mathf.FloorToInt(_energy),
                Mathf.FloorToInt(_maxEnergy),
                ResolveAccent()
            );

            hud.SetEnergy(in state);
        }

        private void PushCatchUp()
        {
            ResolveSink()?.SetCatchUp(_isCatchUpActive, _catchUpSeconds);
        }

        private void PushHand()
        {
            IMatchHudView hud = ResolveSink();

            if (hud == null)
            {
                return;
            }

            for (int i = 0; i < _handStates.Length; i++)
            {
                hud.SetHandSlot(i, in _handStates[i]);
            }

            hud.SetNextCard(in _nextCardState);
        }

        private void PushOutcome()
        {
            IMatchHudView hud = ResolveSink();

            if (hud == null)
            {
                return;
            }

            if (_isOutcomeShown)
            {
                hud.SetOutcome(_outcomeTitle, _outcomeReason);
                return;
            }

            hud.ClearOutcome();
        }

        private void ClearHandState()
        {
            for (int i = 0; i < _handStates.Length; i++)
            {
                _handStates[i] = HandSlotState.Empty;
            }

            _nextCardState = HandSlotState.Empty;
        }

        private void HandlePanelInitialized()
        {
            PushAll();
        }

        private void HandleMatchStarted(MatchConfiguration config)
        {
            ResolveSeats(config);

            _timerSeconds = Mathf.CeilToInt(config.StandardDurationSeconds);
            _countdownSeconds = NoTimer;
            _isOvertime = false;
            _isCatchUpActive = false;
            _catchUpSeconds = 0;
            _isOvertimeBannerShown = false;
            _isOutcomeShown = false;
            _isHandOverflowLogged = false;
            _maxEnergy = 0f;

            RefreshMaxEnergy();

            if (_energyPresenter != null)
            {
                _energy = _energyPresenter.GetEnergy(_localPlayerId);
                _isEnergyAtCap = IsAtCap(_energy);
            }

            if (_matchController != null)
            {
                _localScore = _matchController.ScoreOf(_localPlayerId);
                _opponentScore = _matchController.ScoreOf(_opponentPlayerId);
            }

            PullHand();
            PushAll();
        }

        private void HandleMatchPhaseChanged(MatchPhase phase)
        {
            _phase = phase;

            switch (phase)
            {
                case MatchPhase.None:
                    // An abandoned start is not a match that ran out of clock, so nothing renders a zero — and
                    // the previous scores under a blank clock and an empty hand describe no state the game is
                    // in, so they go too.
                    _timerSeconds = NoTimer;
                    _countdownSeconds = NoTimer;
                    _localScore = 0;
                    _opponentScore = 0;
                    _isCountdownShown = false;
                    _isOutcomeShown = false;

                    _isOvertime = false;
                    _isOvertimeBannerShown = false;

                    // The gauge is cleared here rather than left to the events that normally move it. Catch-up
                    // does publish a closing CatchUpChanged(id, false, 0) when MatchController.AbandonStart calls
                    // ResetCatchUp, and Energy publishes nothing at all — but relying on the first would make this
                    // branch depend on the controller's raise order for state it clears explicitly two lines
                    // above, and the second would leave the dead match's fill on screen under a blank clock.
                    _isCatchUpActive = false;
                    _catchUpSeconds = 0;
                    _energy = 0f;
                    _isEnergyAtCap = false;

                    // Zeroed with the rest so the next match re-reads the cap off its own ledger: RefreshMaxEnergy
                    // only pulls while this is still zero.
                    _maxEnergy = 0f;

                    ClearHandState();

                    // The one call that empties the whole strip and drops the dimming a slot would otherwise
                    // keep from the hand that was there before the match was given up. PushAll re-states the
                    // empty slots straight after, which costs nothing once per abandoned match.
                    ResolveSink()?.ClearHand();
                    break;
                case MatchPhase.Countdown:
                    _isCountdownShown = true;
                    break;
                case MatchPhase.Standard:
                    _isCountdownShown = false;
                    break;
                case MatchPhase.Overtime:
                    _isOvertime = true;
                    _isOvertimeBannerShown = true;
                    _ = HideOvertimeBannerAsync();
                    break;
                case MatchPhase.Results:
                    _isOutcomeShown = false;
                    break;
                default:
                    // Loading hides the panel and OvertimeCheck freezes the last played frame, both of which
                    // PushAll already expresses from the phase itself. Ended waits for the outcome event, which
                    // is published after the phase has already changed.
                    break;
            }

            PushAll();
        }

        private void HandleMatchClockTicked(int remainingSeconds)
        {
            // The tick does not say which phase it belongs to, so it is read against the last phase announced.
            if (_phase == MatchPhase.Countdown)
            {
                _countdownSeconds = remainingSeconds;

                ResolveSink()?.SetCountdownSeconds(remainingSeconds);

                return;
            }

            if (!IsPlayOpen)
            {
                return;
            }

            _timerSeconds = remainingSeconds;
            PushTimer();
        }

        private void HandleScoreChanged(int playerId, int unitCount)
        {
            if (playerId == _localPlayerId)
            {
                _localScore = unitCount;

                ResolveSink()?.SetLocalScore(unitCount);

                return;
            }

            if (playerId != _opponentPlayerId)
            {
                return;
            }

            _opponentScore = unitCount;

            ResolveSink()?.SetOpponentScore(unitCount);
        }

        private void HandleMatchEnded(MatchOutcome outcome)
        {
            _outcomeTitle = ResolveOutcomeTitle(outcome);
            _outcomeReason = ResolveOutcomeReason(outcome.Reason);
            _isOutcomeShown = true;

            PushOutcome();
        }

        private void HandleEnergyChanged(int playerId, float newEnergy)
        {
            if (playerId != _localPlayerId)
            {
                return;
            }

            RefreshMaxEnergy();

            _energy = newEnergy;
            _isEnergyAtCap = IsAtCap(newEnergy);

            PushEnergy();
            RefreshAffordability();
        }

        private void HandleCatchUpChanged(int playerId, bool isActive, float remainingSeconds)
        {
            if (playerId != _localPlayerId)
            {
                return;
            }

            _isCatchUpActive = isActive;
            _catchUpSeconds = isActive ? Mathf.CeilToInt(remainingSeconds) : 0;

            PushCatchUp();
            PushEnergy();
        }

        private void HandleHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard)
        {
            if (playerId != _localPlayerId)
            {
                return;
            }

            ApplyHand(hand);
            _nextCardState = BuildSlotState(nextCard);

            PushHand();
            RefreshAffordability();
        }
    }
}
