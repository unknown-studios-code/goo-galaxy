using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Input.Interfaces;
using GooGalaxy.Runtime.Input.Models;
using GooGalaxy.Runtime.Input.Presenters;
using GooGalaxy.Runtime.Input.Services;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;
using GooGalaxy.Runtime.UI.Views;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace GooGalaxy.Runtime.Input.Controllers
{
    /// <summary>
    /// Turns the local player's finger into a board command: it owns the live selection, has every legal action
    /// enumerated when one is made, shows those actions as highlights, and commits through the same entry points
    /// the machine player uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It re-implements no rule.</b> Which actions are legal belongs to <see cref="MoveOptionResolver" />,
    /// affordability to <see cref="IEnergyLedger" />, and whether a commit lands to
    /// <c>UnitPresenter.ResolveMove</c> and <see cref="DeployController" />. A human tap and a machine tick go
    /// through the same enumerator, so the two can never disagree about what is playable, and a rule that
    /// changes reaches both for free. Nothing here asks whether a hex is adjacent to one of the player's units —
    /// the enumerator has already answered that.
    /// </para>
    /// <para>
    /// <b>It gates the match phase in two places, and covers a third by cancelling rather than gating.</b>
    /// <see cref="DeployController.TryPlayCard" /> and <see cref="CardDiscardController.TryDiscardCard" /> both
    /// refuse every play outside <see cref="MatchPhase.Standard" /> and <see cref="MatchPhase.Overtime" />
    /// already. Neither gate reaches a board move, though — <c>UnitPresenter.ResolveMove</c> checks no phase at
    /// all, so a Clone or a Jump submitted during Countdown would land unopposed if one were ever submitted.
    /// What actually protects that path is <see cref="HandleMatchPhaseChanged" />: a phase change out of play
    /// cancels the live selection first, so the release that would otherwise commit instead finds
    /// <see cref="InteractionState.Idle" /> and submits nothing. A commit that does reach the board is attempted
    /// regardless of phase and the returned code is read rather than pre-empted.
    /// </para>
    /// <para>
    /// <b>Enumeration happens on three triggers, never per frame.</b> A pointer move re-tests membership of an
    /// already-computed set and enumerates nothing. The set is rebuilt when a selection is first made, when the
    /// board changes under a live selection (<c>MatchEvents.LandingResolved</c>), and when the local player's
    /// Energy crosses an affordability edge — see <see cref="HandleEnergyChanged" /> for exactly what that third
    /// test approximates. The rising half of that edge is quantised to <see cref="ResolveEnergyQuantum" />
    /// rather than tested on every publication, because <c>EnergyPresenter</c> publishes roughly seven times a
    /// second at the authored regen rate; the approximation is safe because a highlight is only ever a hint, and
    /// every commit is re-validated and re-charged by the board regardless of what was shown.
    /// </para>
    /// <para>
    /// <b>Protocols are not offered.</b> Options of kind <see cref="MoveOptionKind.Protocol" /> are dropped
    /// before anything is highlighted, because cluster targeting is out of scope for the MVP —
    /// <see cref="InteractionState.SpellTargeting" /> is the seam it attaches to. That filter is also what makes
    /// retaining <see cref="_options" /> across frames safe despite <see cref="MoveOption.TargetCluster" />'s own
    /// warning against it: <see cref="IsOptionForSource" /> drops every Protocol option before anything reads
    /// one, and a board move carries a null cluster, so the borrowed buffer a re-enumeration may have already
    /// cleared is never dereferenced. Admitting Protocol options here later would make that retention a live bug.
    /// </para>
    /// <para>
    /// <b>Which seat it accepts input for.</b> Read from <c>MatchEvents.MatchStarted</c> through
    /// <see cref="LocalSeatResolver" />, the same resolution the HUD makes, so the side the screen calls home is
    /// the side the finger commands. Nothing hard-codes a player number.
    /// </para>
    /// <para>
    /// <b>Subscriptions are symmetric, and must stay that way.</b> Domain reload is disabled on this project, so
    /// a <c>MatchEvents</c> subscription that outlived its component would keep a destroyed presenter reachable
    /// and fire into it next play session.
    /// </para>
    /// <para>
    /// <b>Allocation-free once every distinct card and buffer has been seen.</b> The state machine, the option
    /// list, the target and hand-lookup buffers, and the card-definition cache are all built once and reused; a
    /// selection allocates only when it names a card this session has not resolved before. After that, a whole
    /// press-drag-release cycle allocates nothing — the guarantee <c>MatchInputSteadyStateAllocationTests</c>
    /// holds this type to.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class MatchInputController : MonoBehaviour
    {
        private const int HandSlotCapacity = DeckState.DefaultHandSize;

        // Derived rather than guessed: each of the board's cells can be the target of at most one Deploy per hand
        // slot, one Clone from each of its six neighbours, and one Jump from each of the twelve cells two rings out.
        // Sized so a full board never grows the list, because the growth would land mid-gesture.
        private const int OptionCapacity =
            BoardMetrics.DefaultBoardCellCount
            * (HandSlotCapacity + BoardMetrics.NeighborsPerCell + (BoardMetrics.NeighborsPerCell * BoardMetrics.DefaultJumpDistance));

        private const int CardDefinitionCapacity = 8;

        // A troop deploys onto exactly one hex, which is the target count DeployController requires.
        private const int TroopTargetCount = 1;

        // Only reached when no GridView was injected, in which case nothing resolves anyway. Matches
        // GridView._cellVisualSize's field initializer so the failure reads as "no board" rather than as a
        // board at zero scale.
        private const float FallbackCellVisualSize = 1f;

        // PERF: the rising edge is quantised, because the balance is published every 0.05 Energy — roughly seven
        // times a second at the authored regen rate — and without this every one of those publications re-enumerated
        // the whole board for as long as a selection with no targets was held. This widens the approximation the
        // falling edge already accepts rather than introducing a new kind of one: the highlight appears at most one
        // quantum late, and the board re-validates and re-charges every commit regardless of what was highlighted.
        private const float ResolveEnergyQuantum = 0.25f;

        [Header("Wiring")]
        [SerializeField]
        private Camera _boardCamera;

        [Tooltip("The HUD's UIDocument, used to tell a tap on the board from a tap on the interface. Leave empty to treat every point as board.")]
        [SerializeField]
        private UIDocument _hudDocument;

        [Header("Gesture")]
        [Tooltip("How far a finger must travel to be a drag rather than a tap, in density-independent pixels. Below 4 a steady tap registers as a drag.")]
        [Min(0f)]
        [SerializeField]
        private float _dragThresholdInDp = 8f;

        private readonly InteractionStateMachine _stateMachine = new();
        private readonly List<MoveOption> _options = new(OptionCapacity);
        private readonly List<CardDefinition> _handCards = new(HandSlotCapacity);
        private readonly List<HexCoordinates> _targets = new(BoardMetrics.DefaultBoardCellCount);
        private readonly List<HexCoordinates> _deployTargets = new(TroopTargetCount);
        private readonly Dictionary<CardId, CardDefinition> _cardDefinitions = new(CardDefinitionCapacity);
        private readonly MoveOptionBuffers _buffers = new(HandSlotCapacity);

        private GridPresenter _gridPresenter;
        private GridView _gridView;
        private UnitPresenter _unitPresenter;
        private CardPresenter _cardPresenter;
        private DeployController _deployController;
        private CardDiscardController _discardController;
        private TargetHighlightPresenter _highlightPresenter;
        private ICardCycle _cardCycle;
        private IEnergyLedger _energyLedger;
        private IPointerSource _pointerSource;
        private IHandGestureSource _handGestureSource;
        private BoardPointerResolver _pointerResolver;
        private Xorshift32 _random;
        private Vector2 _pressOrigin;
        private int _localPlayerId = PlayerSlot.UnassignedId;
        private float _energy;
        private float _energyAtLastResolve;
        private bool _isPointerDown;
        private bool _isCommitting;

        /// <remarks>The phase the live selection is in, exposed so a fixture can assert it without reading the board.</remarks>
        internal InteractionState State => _stateMachine.State;

        /// <remarks>What the live selection was started from, exposed so a fixture can assert it without reading the board.</remarks>
        internal InteractionSource Source => _stateMachine.Source;

        /// <remarks>
        /// The seat this presenter accepts input for, or <see cref="PlayerSlot.UnassignedId" /> before a match
        /// has been announced — in which case nothing here ever selects anything.
        /// </remarks>
        internal int LocalPlayerId => _localPlayerId;

        /// <remarks>How many hexes the live selection currently offers. Zero whenever nothing is selected.</remarks>
        internal int TargetCount => _targets.Count;

        /// <remarks>
        /// The three board and card presenters are taken concretely because none of the interfaces the board
        /// uses carries the read this needs: <see cref="IEnergyLedger" /> prices and pays but cannot enumerate,
        /// and <see cref="ICardCycle" /> rotates a hand but cannot resolve a card's authored data. Every
        /// dependency arrives before <c>Awake</c>, because the container force-resolves a registered component
        /// while the scope wakes — which is what lets <see cref="_gridView" /> be read for the pointer resolver
        /// in <c>Start</c>, once <c>Camera.main</c> is safe to fall back to.
        /// </remarks>
        [Inject]
        public void Construct(
            GridPresenter gridPresenter,
            GridView gridView,
            UnitPresenter unitPresenter,
            CardPresenter cardPresenter,
            DeployController deployController,
            CardDiscardController discardController,
            TargetHighlightPresenter highlightPresenter,
            ICardCycle cardCycle,
            IEnergyLedger energyLedger,
            IPointerSource pointerSource,
            IHandGestureSource handGestureSource
        )
        {
            Debug.Assert(gridPresenter != null, InputLogMessages.MatchInputGridPresenterMissing, this);
            Debug.Assert(gridView != null, InputLogMessages.MatchInputGridViewMissing, this);
            Debug.Assert(unitPresenter != null, InputLogMessages.MatchInputUnitPresenterMissing, this);
            Debug.Assert(cardPresenter != null, InputLogMessages.MatchInputCardPresenterMissing, this);
            Debug.Assert(deployController != null, InputLogMessages.MatchInputDeployControllerMissing, this);
            Debug.Assert(discardController != null, InputLogMessages.MatchInputDiscardControllerMissing, this);
            Debug.Assert(highlightPresenter != null, InputLogMessages.MatchInputHighlightPresenterMissing, this);
            Debug.Assert(cardCycle != null, InputLogMessages.MatchInputCardCycleMissing, this);
            Debug.Assert(energyLedger != null, InputLogMessages.MatchInputEnergyLedgerMissing, this);
            Debug.Assert(pointerSource != null, InputLogMessages.MatchInputPointerSourceMissing, this);
            Debug.Assert(handGestureSource != null, InputLogMessages.MatchInputHandGestureSourceMissing, this);

            _gridPresenter = gridPresenter;
            _gridView = gridView;
            _unitPresenter = unitPresenter;
            _cardPresenter = cardPresenter;
            _deployController = deployController;
            _discardController = discardController;
            _highlightPresenter = highlightPresenter;
            _cardCycle = cardCycle;
            _energyLedger = energyLedger;
            _pointerSource = pointerSource;
            _handGestureSource = handGestureSource;
        }

        protected void OnEnable()
        {
            if (!UnityReference.IsUnavailable(_pointerSource))
            {
                _pointerSource.PointerPressed += HandlePointerPressed;
                _pointerSource.PointerMoved += HandlePointerMoved;
                _pointerSource.PointerReleased += HandlePointerReleased;
            }

            if (!UnityReference.IsUnavailable(_handGestureSource))
            {
                _handGestureSource.HandSlotPressed += HandleHandSlotPressed;
            }

            MatchEvents.MatchStarted += HandleMatchStarted;
            MatchEvents.MatchPhaseChanged += HandleMatchPhaseChanged;
            MatchEvents.MatchEnded += HandleMatchEnded;
            MatchEvents.LandingResolved += HandleLandingResolved;
            MatchEvents.EnergyChanged += HandleEnergyChanged;
        }

        // Camera.main walks the scene by tag, which is a scene-dependent lookup and therefore belongs in Start
        // rather than Awake. Both scenes wire _boardCamera explicitly today, so this fallback and the resolver
        // it feeds are only ever exercised by a scene that omitted the Inspector reference.
        protected void Start()
        {
            if (_boardCamera == null)
            {
                _boardCamera = Camera.main;
            }

            if (_boardCamera == null)
            {
                Debug.LogError(InputLogMessages.BoardCameraMissing, this);
            }

            float cellVisualSize = _gridView != null ? _gridView.CellVisualSize : FallbackCellVisualSize;

            _pointerResolver = new BoardPointerResolver(_boardCamera, cellVisualSize);
        }

        protected void OnDisable()
        {
            if (!UnityReference.IsUnavailable(_pointerSource))
            {
                _pointerSource.PointerPressed -= HandlePointerPressed;
                _pointerSource.PointerMoved -= HandlePointerMoved;
                _pointerSource.PointerReleased -= HandlePointerReleased;
            }

            if (!UnityReference.IsUnavailable(_handGestureSource))
            {
                _handGestureSource.HandSlotPressed -= HandleHandSlotPressed;
            }

            MatchEvents.MatchStarted -= HandleMatchStarted;
            MatchEvents.MatchPhaseChanged -= HandleMatchPhaseChanged;
            MatchEvents.MatchEnded -= HandleMatchEnded;
            MatchEvents.LandingResolved -= HandleLandingResolved;
            MatchEvents.EnergyChanged -= HandleEnergyChanged;

            _isPointerDown = false;

            CancelSelection();
        }

        // The Protocol filter and the per-path filter in one place, so the set that is highlighted and the set a
        // commit is looked up in cannot drift apart.
        private static bool IsOptionForSource(in MoveOption option, in InteractionSource source)
        {
            if (option.Kind != MoveOptionKind.BoardMove)
            {
                return false;
            }

            if (source.Kind == InteractionSourceKind.BoardUnit)
            {
                return option.UnitId == source.UnitId;
            }

            if (source.Kind == InteractionSourceKind.HandSlot)
            {
                return option.MoveType == MoveType.Deploy && option.SlotIndex == source.SlotIndex;
            }

            return false;
        }

        private void CancelSelection()
        {
            _stateMachine.Cancel();
            _targets.Clear();

            if (!UnityReference.IsUnavailable(_handGestureSource))
            {
                _handGestureSource.SetDiscardZoneArmed(false);
            }

            if (_highlightPresenter != null)
            {
                _highlightPresenter.ClearTargets();
            }
        }

        private void ResolveTargets()
        {
            _targets.Clear();
            _energyAtLastResolve = _energy;

            HexGrid grid = GetGrid();
            InteractionSource source = _stateMachine.Source;

            if (
                grid == null
                || _unitPresenter == null
                || source.Kind == InteractionSourceKind.None
                || _localPlayerId == PlayerSlot.UnassignedId
                || UnityReference.IsUnavailable(_energyLedger)
            )
            {
                ApplyTargets();

                return;
            }

            BuildHandLookup();

            MoveOptionResolver.Resolve(
                _localPlayerId,
                grid,
                _unitPresenter.ActiveUnits,
                _unitPresenter.Capabilities,
                _handCards,
                _energyLedger,
                ref _random,
                _buffers,
                _options
            );

            for (int i = 0; i < _options.Count; i++)
            {
                MoveOption option = _options[i];

                if (IsOptionForSource(in option, in source))
                {
                    _targets.Add(option.Target);
                }
            }

            ApplyTargets();
        }

        private void ApplyTargets()
        {
            if (_highlightPresenter == null)
            {
                return;
            }

            _highlightPresenter.SetTargets(_targets);
        }

        private void TrySelectUnitAt(HexCoordinates coordinates)
        {
            HexGrid grid = GetGrid();

            if (grid == null || _unitPresenter == null || !grid.TryGetCell(coordinates, out HexCell cell))
            {
                return;
            }

            if (cell.OccupantUnitId == HexCell.NoOccupant || !_unitPresenter.ActiveUnits.TryGetValue(cell.OccupantUnitId, out GridUnit unit))
            {
                return;
            }

            if (unit == null || !unit.IsAlive || unit.PlayerId != _localPlayerId)
            {
                return;
            }

            if (!_stateMachine.TrySelectBoardUnit(unit.UnitId, coordinates))
            {
                return;
            }

            ResolveTargets();
        }

        // Scanned rather than looked up, because the option set is small and keying it by target would need a
        // dictionary rebuilt on every enumeration. The first match wins, and the enumerator adds a unit's Clone
        // options ahead of its Jump options — so a hex both could reach commits as the Clone. That is this input
        // layer's own tie-break, not one the GDD states a preference on: a Clone nets +1 unit against a Jump's
        // net +0, which is the GDD-backed reason to favor it, but which of two equally legal landings a bare tap
        // should prefer is a choice this layer is making, not one it is reading off the rules.
        private bool TryFindOptionForTarget(HexCoordinates target, out MoveOption option)
        {
            option = default;

            InteractionSource source = _stateMachine.Source;

            for (int i = 0; i < _options.Count; i++)
            {
                MoveOption candidate = _options[i];

                if (candidate.Target != target || !IsOptionForSource(in candidate, in source))
                {
                    continue;
                }

                option = candidate;

                return true;
            }

            return false;
        }

        private void CommitTarget(HexCoordinates target)
        {
            if (!TryFindOptionForTarget(target, out MoveOption option))
            {
                CancelSelection();

                return;
            }

            // Latched across the submission because committing publishes on the bus synchronously, and the
            // landing this presenter is about to hear is its own — re-enumerating from inside it would clear the
            // option list mid-commit for a selection that is being torn down two lines later anyway.
            _isCommitting = true;

            try
            {
                if (option.MoveType == MoveType.Deploy)
                {
                    SubmitCardPlay(in option);

                    return;
                }

                SubmitBoardMove(in option);
            }
            finally
            {
                _isCommitting = false;

                CancelSelection();
            }
        }

        // A refusal is not necessarily a fault: both players act at once, so a sector highlighted as empty can
        // be taken before the finger lifts, and the controller answers ResolverBusy while the other play is
        // mid-resolution. The message names the returned code rather than assuming one, because every other
        // code means the highlight and the board disagree about the rules.
        private void SubmitCardPlay(in MoveOption option)
        {
            if (_deployController == null)
            {
                return;
            }

            _deployTargets.Clear();
            _deployTargets.Add(option.Target);

            CardPlayResult result = _deployController.TryPlayCard(_localPlayerId, option.SlotIndex, _deployTargets);

            if (result != CardPlayResult.Success)
            {
                Debug.Log(string.Format(InputLogMessages.CardPlayRejectedFormat, _localPlayerId, option.SlotIndex, result), this);
            }
        }

        private void SubmitBoardMove(in MoveOption option)
        {
            if (_unitPresenter == null)
            {
                return;
            }

            var command = option.ToMoveCommand(_localPlayerId);
            MovementResult result = _unitPresenter.ResolveMove(in command);

            if (result != MovementResult.Success)
            {
                Debug.Log(string.Format(InputLogMessages.MoveRejectedFormat, _localPlayerId, option.MoveType, result), this);
            }
        }

        private void DiscardSelectedCard(int slotIndex)
        {
            if (_discardController == null)
            {
                CancelSelection();

                return;
            }

            _isCommitting = true;

            try
            {
                CardDiscardResult result = _discardController.TryDiscardCard(_localPlayerId, slotIndex);

                if (result != CardDiscardResult.Success)
                {
                    Debug.Log(string.Format(InputLogMessages.CardDiscardRejectedFormat, _localPlayerId, slotIndex, result), this);
                }
            }
            finally
            {
                _isCommitting = false;

                CancelSelection();
            }
        }

        private void BuildHandLookup()
        {
            _handCards.Clear();

            if (UnityReference.IsUnavailable(_cardCycle) || !_cardCycle.TryGetHand(_localPlayerId, out IReadOnlyList<CardId> hand) || hand == null)
            {
                return;
            }

            for (int i = 0; i < hand.Count; i++)
            {
                _handCards.Add(GetCardDefinition(hand[i]));
            }
        }

        // CardDataSO implements ICardData alone, so the roster cannot answer what a card can do on the board —
        // CardDefinition is the only type carrying the authored data and the capability contracts at once.
        //
        // PERF: memoized, exactly as DeployController memoizes its own. One definition is built the first time
        // each distinct card reaches the hand and reused afterwards, so a selection allocates nothing once the
        // hand has been seen.
        private CardDefinition GetCardDefinition(CardId cardId)
        {
            if (_cardDefinitions.TryGetValue(cardId, out CardDefinition definition))
            {
                return definition;
            }

            if (_cardPresenter == null || !_cardPresenter.TryGetCard(cardId, out ICardData card) || UnityReference.IsUnavailable(card))
            {
                return null;
            }

            definition = new CardDefinition(card);
            _cardDefinitions[cardId] = definition;

            return definition;
        }

        private HexGrid GetGrid()
        {
            return _gridPresenter != null ? _gridPresenter.HexGrid : null;
        }

        private bool TryResolveHex(Vector2 screenPosition, out HexCoordinates coordinates)
        {
            coordinates = default;

            return _pointerResolver != null && _pointerResolver.TryResolveHex(screenPosition, GetGrid(), out coordinates);
        }

        // PERF: the panel pick is the expensive half and the arithmetic is the cheap half, so the cheap half goes
        // first. A point that is not on a highlighted hex is not a commit target whatever the HUD says, so a
        // pointer dragged across empty board never reaches the panel at all — and over the board the pick could
        // only ever return null after walking the whole HUD tree to prove it.
        private bool TryResolveHighlightedHex(Vector2 screenPosition, out HexCoordinates coordinates)
        {
            if (!TryResolveHex(screenPosition, out coordinates))
            {
                return false;
            }

            if (_highlightPresenter == null || !_highlightPresenter.IsHighlighted(coordinates))
            {
                return false;
            }

            return !IsScreenPointOverHud(screenPosition);
        }

        private bool IsScreenPointOverHud(Vector2 screenPosition)
        {
            if (_hudDocument == null)
            {
                return false;
            }

            VisualElement root = _hudDocument.rootVisualElement;

            return root != null && BoardPointerResolver.IsScreenPointOverPanel(root.panel, screenPosition);
        }

        private bool IsScreenPointInDiscardZone(Vector2 screenPosition)
        {
            return !UnityReference.IsUnavailable(_handGestureSource) && _handGestureSource.IsScreenPointInDiscardZone(screenPosition);
        }

        private void HandlePointerPressed(PointerSample sample)
        {
            _pressOrigin = sample.ScreenPosition;
            _isPointerDown = true;

            // A press over the HUD belongs to the hand strip, which reports it through IHandGestureSource. The
            // two arrive in no guaranteed order, so acting on this one as well would cancel the very selection
            // that event is about to start.
            if (IsScreenPointOverHud(_pressOrigin))
            {
                return;
            }

            bool hasHex = TryResolveHex(_pressOrigin, out HexCoordinates coordinates);

            if (_stateMachine.State != InteractionState.Idle)
            {
                if (hasHex && _highlightPresenter != null && _highlightPresenter.IsHighlighted(coordinates))
                {
                    CommitTarget(coordinates);

                    return;
                }

                InteractionSource source = _stateMachine.Source;
                bool isSecondTapOnSource = hasHex && source.Kind == InteractionSourceKind.BoardUnit && coordinates == source.Hex;

                CancelSelection();

                if (isSecondTapOnSource)
                {
                    return;
                }
            }

            if (!hasHex)
            {
                return;
            }

            TrySelectUnitAt(coordinates);
        }

        private void HandlePointerMoved(PointerSample sample)
        {
            if (!_isPointerDown || _stateMachine.State == InteractionState.Idle)
            {
                return;
            }

            Vector2 position = sample.ScreenPosition;

            if (GestureClassifier.ClassifyHold(_pressOrigin, position, _dragThresholdInDp) == PointerGesture.Drag && _stateMachine.TryBeginDrag())
            {
                if (_stateMachine.Source.Kind == InteractionSourceKind.HandSlot && !UnityReference.IsUnavailable(_handGestureSource))
                {
                    _handGestureSource.SetDiscardZoneArmed(true);
                }
            }

            if (_stateMachine.State is not (InteractionState.Dragging or InteractionState.Previewing))
            {
                return;
            }

            // Membership only: the option set was computed when the selection was made and is not rebuilt here.
            bool isOverCommitTarget =
                (_stateMachine.Source.Kind == InteractionSourceKind.HandSlot && IsScreenPointInDiscardZone(position))
                || TryResolveHighlightedHex(position, out _);

            if (isOverCommitTarget)
            {
                _stateMachine.TryBeginPreview();

                return;
            }

            _stateMachine.TryEndPreview();
        }

        private void HandlePointerReleased(PointerSample sample)
        {
            _isPointerDown = false;

            if (_stateMachine.State == InteractionState.Idle)
            {
                return;
            }

            Vector2 position = sample.ScreenPosition;
            InteractionSource source = _stateMachine.Source;

            bool isOverDiscardZone = source.Kind == InteractionSourceKind.HandSlot && IsScreenPointInDiscardZone(position);
            HexCoordinates target = default;
            bool isOverTarget = !isOverDiscardZone && TryResolveHighlightedHex(position, out target);

            PointerGesture gesture = GestureClassifier.ClassifyRelease(_pressOrigin, position, _dragThresholdInDp, isOverDiscardZone || isOverTarget);

            // A release that never travelled settles nothing: it leaves the selection live so the player can tap
            // a highlighted hex next, which is the whole of the tap-then-tap path. Every abandonment the design
            // lists — off the grid, an unhighlighted hex, back over the HUD — is a release that travelled and
            // landed on none of the targets, which is what Cancel below covers.
            if (gesture == PointerGesture.Tap)
            {
                return;
            }

            if (gesture != PointerGesture.Commit)
            {
                CancelSelection();

                return;
            }

            if (isOverDiscardZone)
            {
                DiscardSelectedCard(source.SlotIndex);

                return;
            }

            CommitTarget(target);
        }

        private void HandleHandSlotPressed(int slotIndex)
        {
            InteractionSource source = _stateMachine.Source;
            bool isSecondPressOnSource = source.Kind == InteractionSourceKind.HandSlot && source.SlotIndex == slotIndex;

            CancelSelection();

            if (isSecondPressOnSource || !_stateMachine.TrySelectHandSlot(slotIndex))
            {
                return;
            }

            ResolveTargets();
        }

        private void HandleMatchStarted(MatchConfiguration config)
        {
            CancelSelection();

            // A definition is an immutable copy of an asset a designer can edit between matches, and domain
            // reload is disabled, so a cache kept across a rematch would resolve the previous session's values.
            _cardDefinitions.Clear();
            _energy = 0f;
            _energyAtLastResolve = 0f;

            // Held only because the enumerator takes one. Its single consumer is the Protocol cluster draw, and
            // every Protocol option is filtered out before anything is highlighted, so nothing this generator
            // produces is ever observable — it is seeded from the match seed anyway, so a replay of the same
            // match enumerates identically.
            _random = new Xorshift32(MoveOptionResolver.DeriveSeed(config.Seed));

            bool wasResolved = LocalSeatResolver.TryResolve(in config, out PlayerSlot home, out _);

            _localPlayerId = home.Id;

            if (!wasResolved)
            {
                Debug.LogWarning(
                    string.Format(InputLogMessages.MatchInputLocalSeatUnresolvedFormat, config.PlayerOne.Control, config.PlayerTwo.Control, _localPlayerId),
                    this
                );
            }
        }

        private void HandleMatchPhaseChanged(MatchPhase phase)
        {
            if (phase is MatchPhase.Standard or MatchPhase.Overtime)
            {
                return;
            }

            CancelSelection();
        }

        private void HandleMatchEnded(MatchOutcome outcome)
        {
            CancelSelection();
        }

        // The board changed under a live selection — the player's own landing, or the opponent's — so hexes that
        // were empty may now be taken and units may have changed hands. Re-enumerating is the only way to learn
        // which, and it is cheap because it happens per landing rather than per frame.
        private void HandleLandingResolved(MoveCommand command, ConversionResult conversions)
        {
            if (_isCommitting || _stateMachine.State == InteractionState.Idle)
            {
                return;
            }

            ResolveTargets();
        }

        // The affordability edge, approximated rather than computed: a fall in Energy can only close options and
        // a rise can only open them, so the set is rebuilt when the balance drops below what it was at the last
        // enumeration, and when it rises by at least ResolveEnergyQuantum while that enumeration produced
        // nothing. The case this deliberately misses is a rise that opens a second, dearer action beside one
        // already offered — which costs a highlight that appears one landing late, and never a wrong commit: the
        // board re-validates and re-charges every action regardless of what was highlighted.
        private void HandleEnergyChanged(int playerId, float energy)
        {
            if (playerId != _localPlayerId)
            {
                return;
            }

            _energy = energy;

            if (_isCommitting || _stateMachine.State == InteractionState.Idle)
            {
                return;
            }

            bool hasCrossedAffordabilityEdge =
                energy < _energyAtLastResolve || (_targets.Count == 0 && (energy - _energyAtLastResolve) >= ResolveEnergyQuantum);

            if (!hasCrossedAffordabilityEdge)
            {
                return;
            }

            ResolveTargets();
        }
    }
}
