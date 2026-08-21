using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.AI.Data;
using GooGalaxy.Runtime.AI.Interfaces;
using GooGalaxy.Runtime.AI.Models;
using GooGalaxy.Runtime.AI.Services;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.AI.Controllers
{
    /// <summary>
    /// Drives the machine-held seat of a match: on a think interval it reads the live board, has every legal
    /// action enumerated, has one of them chosen, and commits it through the same entry points a human tap uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It decides nothing itself.</b> Which actions are legal belongs to <see cref="MoveOptionResolver" />,
    /// which action to take belongs to <see cref="IMoveStrategy" />, and both are plain C# with no engine
    /// dependency. What is left here is the clock, the subscriptions, the reusable buffers and the submission —
    /// the humble half — so the whole brain is exercised in EditMode without entering play mode, and a smarter
    /// opponent replaces one interface implementation rather than this component.
    /// </para>
    /// <para>
    /// <b>The design vocabulary calls this the Lab Assistant; the code never does.</b> That name is player-facing
    /// only — UI copy, cards, the GDD — and must not appear in any identifier here or in any assembly that reaches
    /// this one. Code names the mechanism (<c>AiController</c>, <see cref="PlayerControl.Machine" />) so a rename of
    /// the flavour costs a string table rather than a refactor, and so a reader of the type never has to know the
    /// fiction to know what it does.
    /// </para>
    /// <para>
    /// <b>It is a client of the rules, never a second copy of them.</b> It never writes a <c>HexCell</c>, never
    /// spawns a <c>GridUnit</c> and never debits a ledger: a Deploy and a Protocol go through
    /// <see cref="DeployController" />, a Clone and a Jump through <c>UnitPresenter.ResolveMove</c>, and a
    /// discard through <see cref="CardDiscardController" />. A rule that changes therefore reaches it for free,
    /// and any bug it hits is a bug a human can hit.
    /// </para>
    /// <para>
    /// <b>Which seat it takes.</b> Read from <c>MatchEvents.MatchStarted</c>: whichever
    /// <see cref="PlayerSlot" /> reports <see cref="PlayerControl.Machine" />. Nothing hard-codes a player
    /// number, so this same component sits inert in a scene where both seats are human — which is what lets the
    /// PvP scene and the PvE scene share every other part of the match.
    /// </para>
    /// <para>
    /// <b>Subscriptions are symmetric, and must stay that way.</b> Domain reload is disabled on this project, so
    /// a <c>MatchEvents</c> subscription that outlived its component would keep a destroyed controller reachable
    /// and fire into it next play session — visible as a second machine player acting on a board that has one.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class AiController : MonoBehaviour
    {
        // The loop wakes at most this often while waiting. The wait is sliced rather than taken in one call so
        // crossing the Energy ceiling can cut it short without a second cancellation token, and the slice is
        // coarse because nothing here needs frame accuracy.
        private const float ThinkPollStepSeconds = 0.25f;

        // The floor every slice is clamped up to, so a slice can never be zero and the wait can never complete
        // without suspending. The Inspector already holds each authored interval at or above 0.1 through [Min],
        // but AiConfig is a plain struct and SetAuthoredData bypasses that, so the guarantee is taken here
        // rather than assumed of the caller — see WaitForThinkIntervalAsync for what a non-suspending wait costs.
        private const float MinimumThinkStepSeconds = 0.01f;

        // The interval is drawn as whole hundredths of a second, because the generator yields indices rather
        // than floats. Finer than a player can perceive and coarse enough that the draw stays a small range.
        private const int ThinkIntervalTicksPerSecond = 100;

        // Sized for a typical mid-match option set. A larger one grows the list once and never again.
        private const int OptionCapacity = 512;

        private const int HandSlotCapacity = 4;

        private const int CardDefinitionCapacity = 8;

        private const int TroopTargetCount = 1;

        [Tooltip("Without it the think loop never starts, rather than falling back to timings nobody authored.")]
        [SerializeField]
        private AiConfigSO _config;

        private readonly List<MoveOption> _options = new(OptionCapacity);
        private readonly List<CardDefinition> _handCards = new(HandSlotCapacity);
        private readonly List<HexCoordinates> _deployTargets = new(TroopTargetCount);
        private readonly Dictionary<CardId, CardDefinition> _cardDefinitions = new(CardDefinitionCapacity);
        private readonly Dictionary<int, IMoveCapable> _capabilities = new(BoardMetrics.DefaultBoardCellCount);
        private readonly MoveOptionBuffers _buffers = new(HandSlotCapacity);

        private IMoveStrategy _strategy;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private CardPresenter _cardPresenter;
        private DeployController _deployController;
        private CardDiscardController _discardController;
        private MatchController _matchController;
        private ICardCycle _cardCycle;
        private IEnergyLedger _energyLedger;
        private IDiscardLedger _discardLedger;
        private Xorshift32 _random;
        private AiConfig _tuning;
        private int _playerId = PlayerSlot.UnassignedId;
        private int _thinkGeneration;
        private float _energy;
        private bool _isConfigured;
        private bool _isThinking;
        private bool _isStrategyOverridden;

        /// <remarks>
        /// The seat this controller plays, or <see cref="PlayerSlot.UnassignedId" /> when no seat of the running
        /// match reported <see cref="PlayerControl.Machine" /> — in which case nothing here ever acts.
        /// </remarks>
        internal int PlayerId => _playerId;

        internal bool IsThinking => _isThinking;

        [Inject]
        public void Construct(
            GridPresenter gridPresenter,
            UnitPresenter unitPresenter,
            CardPresenter cardPresenter,
            DeployController deployController,
            CardDiscardController discardController,
            MatchController matchController,
            ICardCycle cardCycle,
            IEnergyLedger energyLedger,
            IDiscardLedger discardLedger
        )
        {
            Debug.Assert(gridPresenter != null, AiLogMessages.AiGridPresenterMissing, this);
            Debug.Assert(unitPresenter != null, AiLogMessages.AiUnitPresenterMissing, this);
            Debug.Assert(cardPresenter != null, AiLogMessages.AiCardPresenterMissing, this);
            Debug.Assert(deployController != null, AiLogMessages.AiDeployControllerMissing, this);
            Debug.Assert(discardController != null, AiLogMessages.AiDiscardControllerMissing, this);
            Debug.Assert(matchController != null, AiLogMessages.AiMatchControllerMissing, this);
            Debug.Assert(cardCycle != null, AiLogMessages.AiCardCycleMissing, this);
            Debug.Assert(energyLedger != null, AiLogMessages.AiEnergyLedgerMissing, this);
            Debug.Assert(discardLedger != null, AiLogMessages.AiDiscardLedgerMissing, this);

            _gridPresenter = gridPresenter;
            _unitPresenter = unitPresenter;
            _cardPresenter = cardPresenter;
            _deployController = deployController;
            _discardController = discardController;
            _matchController = matchController;
            _cardCycle = cardCycle;
            _energyLedger = energyLedger;
            _discardLedger = discardLedger;
        }

        protected void Awake()
        {
            _isConfigured = _config != null;

            if (!_isConfigured)
            {
                Debug.LogError(AiLogMessages.AiConfigMissing, this);
            }
        }

        protected void OnEnable()
        {
            MatchEvents.MatchStarted += HandleMatchStarted;
            MatchEvents.MatchPhaseChanged += HandleMatchPhaseChanged;
            MatchEvents.MatchEnded += HandleMatchEnded;
            MatchEvents.EnergyChanged += HandleEnergyChanged;
        }

        protected void OnDisable()
        {
            MatchEvents.MatchStarted -= HandleMatchStarted;
            MatchEvents.MatchPhaseChanged -= HandleMatchPhaseChanged;
            MatchEvents.MatchEnded -= HandleMatchEnded;
            MatchEvents.EnergyChanged -= HandleEnergyChanged;

            StopThinking();
        }

        /// <remarks>
        /// Replaces the strategy the next match start would otherwise install, and suppresses that replacement
        /// for the rest of this component's life. The seam a fixture drives a deterministic choice through; pass
        /// null to hand the seat back to the seeded random strategy.
        /// </remarks>
        internal void SetStrategy(IMoveStrategy strategy)
        {
            _strategy = strategy;
            _isStrategyOverridden = strategy != null;
        }

        /// <remarks>
        /// Runs exactly one think tick — enumerate, choose, commit — without the loop or its wait, so a fixture
        /// can observe a single decision instead of racing a timer. Does nothing until a match has started and
        /// named a machine seat.
        /// </remarks>
        internal void ProcessTickForTests()
        {
            ProcessThinkTick();
        }

        private static int FindMachineSeat(in MatchConfiguration config)
        {
            if (config.PlayerOne.Control == PlayerControl.Machine)
            {
                return config.PlayerOne.Id;
            }

            if (config.PlayerTwo.Control == PlayerControl.Machine)
            {
                return config.PlayerTwo.Id;
            }

            return PlayerSlot.UnassignedId;
        }

        private static string DescribeAction(in MoveOption option)
        {
            return option.Kind == MoveOptionKind.Protocol ? AiLogMessages.ProtocolActionName : option.MoveType.ToString();
        }

        private void StartThinking()
        {
            if (_isThinking || !_isConfigured || _playerId == PlayerSlot.UnassignedId || !isActiveAndEnabled)
            {
                return;
            }

            _isThinking = true;
            _thinkGeneration++;

            _ = RunThinkLoopAsync(_thinkGeneration);
        }

        // Bumping the generation is what retires a loop that is still suspended in its wait. Clearing the flag
        // alone would not: play can close and reopen inside one frame — Standard, OvertimeCheck, Overtime — and
        // the reopening would start a second loop while the first was still waiting to notice the first change.
        private void StopThinking()
        {
            _isThinking = false;
            _thinkGeneration++;
        }

        private async Awaitable RunThinkLoopAsync(int generation)
        {
            try
            {
                while (_isThinking && generation == _thinkGeneration)
                {
                    await WaitForThinkIntervalAsync();

                    if (this == null || !isActiveAndEnabled || !_isThinking || generation != _thinkGeneration)
                    {
                        return;
                    }

                    ProcessThinkTick();
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled with the component. This is how the loop normally ends, so there is nothing to
                // report and nothing to clean up.
            }
            catch (Exception exception)
            {
                // Deliberately broad, and the same dispatch-boundary catch MatchController and FuseController
                // make: submitting runs the board's own publishes into arbitrary subscriber code, and the
                // caller discards this Awaitable, so an escaping throw would vanish. The opponent would then
                // stop moving for the rest of the match against a silent console, which reads as a bug in the
                // enumerator rather than in whoever threw. Nothing is swallowed — the stack is logged.
                Debug.LogError(AiLogMessages.AiThinkLoopFailed, this);
                Debug.LogException(exception, this);
            }
            finally
            {
                if (generation == _thinkGeneration)
                {
                    _isThinking = false;
                }
            }
        }

        // The Energy ceiling truncates the wait and does nothing else: the loop stays "wait, enumerate, act" on
        // every path. Acting out of cycle would need its own guard against DeployController's in-flight latch,
        // and would buy no behaviour that arriving at the next tick early does not.
        private async Awaitable WaitForThinkIntervalAsync()
        {
            float interval = DrawThinkIntervalSeconds();
            float waited = 0f;

            // A do-while, and the ceiling is read only after a slice has been awaited. Both are load-bearing:
            // testing the ceiling first let this method complete without ever suspending whenever Energy was
            // already at or above the threshold, and the caller re-enters it immediately, so the pair span the
            // same frame forever unless the tick between them spends. A tick spends nothing when it enumerates
            // no option and declines the discard — discard disabled, an empty hand, or a price it cannot
            // afford — which is precisely the locked late-game board where Energy sits at the cap. That is a
            // hang, not a busy loop, so the suspend is made structural rather than left to the tick to earn.
            do
            {
                float step = Mathf.Clamp(interval - waited, MinimumThinkStepSeconds, ThinkPollStepSeconds);

                await Awaitable.WaitForSecondsAsync(step, destroyCancellationToken);

                if (this == null || !isActiveAndEnabled)
                {
                    return;
                }

                waited += step;

                if (_energy >= _tuning.EnergyCeilingThreshold)
                {
                    return;
                }
            } while (waited < interval);
        }

        private float DrawThinkIntervalSeconds()
        {
            int minTicks = Mathf.Max(Mathf.RoundToInt(_tuning.MinThinkSeconds * ThinkIntervalTicksPerSecond), 0);
            int maxTicks = Mathf.Max(Mathf.RoundToInt(_tuning.MaxThinkSeconds * ThinkIntervalTicksPerSecond), minTicks);
            int drawnTicks = minTicks + _random.NextIndex(maxTicks - minTicks + 1);

            return drawnTicks / (float)ThinkIntervalTicksPerSecond;
        }

        private void ProcessThinkTick()
        {
            if (!IsBoardReachable() || !IsMatchInPlay())
            {
                return;
            }

            HexGrid grid = _gridPresenter.HexGrid;

            if (grid == null)
            {
                return;
            }

            BuildCapabilityLookup();
            BuildHandLookup();

            MoveOptionResolver.Resolve(_playerId, grid, _unitPresenter.ActiveUnits, _capabilities, _handCards, _energyLedger, ref _random, _buffers, _options);

            if (_options.Count == 0)
            {
                TryDiscardDeadHand();

                return;
            }

            if (!_strategy.TrySelect(_options, out MoveOption selected))
            {
                return;
            }

            // Read one last time. UnitPresenter.ResolveMove does not gate on the match phase — only the two card
            // controllers do — so a Clone chosen just before the clock expired would otherwise still land and
            // change a score that had already been decided.
            if (!IsMatchInPlay())
            {
                return;
            }

            Submit(selected);
        }

        private bool IsBoardReachable()
        {
            return _isConfigured
                && _playerId != PlayerSlot.UnassignedId
                && _strategy != null
                && _gridPresenter != null
                && _unitPresenter != null
                && _cardPresenter != null
                && _deployController != null
                && _matchController != null
                && !UnityReference.IsUnavailable(_cardCycle)
                && !UnityReference.IsUnavailable(_energyLedger);
        }

        private bool IsMatchInPlay()
        {
            return _matchController != null && _matchController.Phase is MatchPhase.Standard or MatchPhase.Overtime;
        }

        // Keyed by unit id and filled from the board's own registry rather than from card data, because that
        // registry is what UnitPresenter will price and validate the move against. Filtering to the acting
        // player here is a shortcut; the resolver refuses another player's unit again on its own.
        private void BuildCapabilityLookup()
        {
            _capabilities.Clear();

            foreach (GridUnit unit in _unitPresenter.ActiveUnitValues)
            {
                if (unit.PlayerId != _playerId)
                {
                    continue;
                }

                if (_unitPresenter.TryGetCapability(unit.UnitId, out IMoveCapable capability) && capability != null)
                {
                    _capabilities[unit.UnitId] = capability;
                }
            }
        }

        private void BuildHandLookup()
        {
            _handCards.Clear();

            if (!_cardCycle.TryGetHand(_playerId, out IReadOnlyList<CardId> hand) || hand == null)
            {
                return;
            }

            for (int i = 0; i < hand.Count; i++)
            {
                _handCards.Add(GetCardDefinition(hand[i]));
            }
        }

        // CardDataSO implements ICardData alone, so the roster cannot answer what a card can do on the board —
        // CardDefinition is the only type carrying the authored data and the capability contracts at once, which
        // is why one is built here rather than the registry entry being cast.
        //
        // PERF: memoized, exactly as DeployController memoizes its own. One definition is built the first time
        // each distinct card reaches the hand and reused afterwards, so a think tick allocates nothing once the
        // hand has been seen.
        private CardDefinition GetCardDefinition(CardId cardId)
        {
            if (_cardDefinitions.TryGetValue(cardId, out CardDefinition definition))
            {
                return definition;
            }

            if (!_cardPresenter.TryGetCard(cardId, out ICardData card) || UnityReference.IsUnavailable(card))
            {
                return null;
            }

            definition = new CardDefinition(card);
            _cardDefinitions[cardId] = definition;

            return definition;
        }

        private void Submit(in MoveOption option)
        {
            if (option.Kind == MoveOptionKind.Protocol)
            {
                SubmitCardPlay(in option, option.TargetCluster);

                return;
            }

            if (option.MoveType == MoveType.Deploy)
            {
                _deployTargets.Clear();
                _deployTargets.Add(option.Target);

                SubmitCardPlay(in option, _deployTargets);

                return;
            }

            var command = option.ToMoveCommand(_playerId);
            MovementResult result = _unitPresenter.ResolveMove(in command);

            if (result != MovementResult.Success)
            {
                Debug.Log(string.Format(AiLogMessages.AiActionRejectedFormat, _playerId, DescribeAction(in option), result), this);
            }
        }

        // A refusal on contention is normal rather than a fault: the human plays at the same time, so a sector
        // enumerated as empty can be taken before this commits, and DeployController answers ResolverBusy while
        // their play is mid-resolution. Both drop the tick — retrying in place would spin against a board that
        // only the next frame can change. Every other CardPlayResult reaching here is a fault, which is why the
        // message names the reason rather than assuming one.
        private void SubmitCardPlay(in MoveOption option, IReadOnlyList<HexCoordinates> targets)
        {
            CardPlayResult result = _deployController.TryPlayCard(_playerId, option.SlotIndex, targets);

            if (result != CardPlayResult.Success)
            {
                Debug.Log(string.Format(AiLogMessages.AiActionRejectedFormat, _playerId, DescribeAction(in option), result), this);
            }
        }

        // Reached only when the enumeration came back empty, which is precisely when nothing in hand is
        // affordable and no unit can move — so no Energy floor is needed on top of the ledger's own price. At
        // most one card per tick, and only when the asset authors it.
        private void TryDiscardDeadHand()
        {
            if (!_tuning.IsDiscardEnabled || _discardController == null || UnityReference.IsUnavailable(_discardLedger))
            {
                return;
            }

            if (_handCards.Count == 0 || !_discardLedger.CanAffordDiscard(_playerId))
            {
                return;
            }

            int slotIndex = _random.NextIndex(_handCards.Count);
            CardDiscardResult result = _discardController.TryDiscardCard(_playerId, slotIndex);

            if (result != CardDiscardResult.Success)
            {
                Debug.Log(string.Format(AiLogMessages.AiDiscardRejectedFormat, _playerId, slotIndex, result), this);
            }
        }

        private void HandleMatchStarted(MatchConfiguration config)
        {
            StopThinking();

            // A definition is an immutable copy of an asset a designer can edit between matches, and domain
            // reload is disabled, so a cache kept across a rematch would resolve the previous session's values.
            _cardDefinitions.Clear();
            _energy = 0f;
            _playerId = FindMachineSeat(in config);

            if (!_isConfigured || _playerId == PlayerSlot.UnassignedId)
            {
                return;
            }

            _tuning = _config.Config;

            int seed = _tuning.Seed != AiConfig.DerivedSeed ? _tuning.Seed : config.Seed;

            _random = new Xorshift32(MoveOptionResolver.DeriveSeed(seed));

            if (!_isStrategyOverridden)
            {
                _strategy = new RandomMoveStrategy(RandomMoveStrategy.DeriveSeed(seed));
            }
        }

        private void HandleMatchPhaseChanged(MatchPhase phase)
        {
            if (phase is MatchPhase.Standard or MatchPhase.Overtime)
            {
                StartThinking();

                return;
            }

            StopThinking();
        }

        private void HandleMatchEnded(MatchOutcome outcome)
        {
            StopThinking();
        }

        private void HandleEnergyChanged(int playerId, float energy)
        {
            if (playerId != _playerId)
            {
                return;
            }

            _energy = energy;
        }
    }
}
