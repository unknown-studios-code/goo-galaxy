using System;
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
using Unity.Profiling;
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
    /// <b>It re-implements no rule.</b> Which board moves and troop deployments are legal belongs to
    /// <see cref="MoveOptionResolver" />, affordability to <see cref="IEnergyLedger" />, and whether a commit lands to
    /// <c>UnitPresenter.ResolveMove</c> and <see cref="DeployController" />. A human tap and a machine tick go
    /// through the same enumerator for those, so the two cannot disagree about them. A Protocol's cluster is the
    /// exception: the human's is arranged by <see cref="ClusterTargetBuilder" />, but it is accepted by the same
    /// <c>AbilityTargetValidator</c> check the board runs, so it cannot pass here and fail there.
    /// </para>
    /// <para>
    /// <b>Board moves have no domain gate, so this controller is the gate.</b>
    /// <see cref="DeployController.TryPlayCard" /> and <see cref="CardDiscardController.TryDiscardCard" /> both
    /// refuse every play outside <see cref="MatchPhase.Standard" /> and <see cref="MatchPhase.Overtime" />
    /// already, but neither check reaches a board move — <c>UnitPresenter.ResolveMove</c> checks no phase at
    /// all. This controller covers that gap on both sides of a selection's life: the private
    /// <c>IsPlayOpen</c> refuses to <b>start</b> one outside <see cref="MatchPhase.Standard" /> and
    /// <see cref="MatchPhase.Overtime" />, tested in <see cref="TrySelectUnitAt" /> and
    /// <see cref="HandleHandSlotPressed" /> before anything highlights, and <see cref="HandleMatchPhaseChanged" />
    /// cancels a selection that is already <b>live</b> when play closes, so a phase boundary crossed mid-gesture
    /// cannot leave a stale selection standing to commit through. A commit that does reach the board is
    /// attempted regardless of phase and the returned code is read rather than pre-empted.
    /// </para>
    /// <para>
    /// <b>Enumeration happens on three triggers, never per frame.</b> A pointer move re-tests membership of an
    /// already-computed set, or — while a Protocol is aimed — rebuilds only its cluster, and enumerates nothing
    /// either way. The set is rebuilt when a selection is first made, when the
    /// board changes under a live selection (<c>MatchEvents.LandingResolved</c>), and when the local player's
    /// Energy crosses an affordability edge — see <see cref="HandleEnergyChanged" /> for exactly what that third
    /// test approximates. The rising half of that edge is quantised to <see cref="ResolveEnergyQuantum" />
    /// rather than tested on every publication, because <c>EnergyPresenter</c> publishes roughly seven times a
    /// second at the authored regen rate; the approximation is safe because a highlight is only ever a hint, and
    /// every commit is re-validated and re-charged by the board regardless of what was shown.
    /// </para>
    /// <para>
    /// <b>Protocols are aimed, not enumerated.</b> Selecting a Protocol the player can afford enters
    /// <see cref="InteractionState.SpellTargeting" />, and from then on the cluster is rebuilt from wherever the
    /// pointer is by <see cref="ClusterTargetBuilder" /> — under a hovering mouse, under a finger or mouse dragged
    /// out of the hand, and under a finger or mouse pressed onto the board, which casts where it lifts rather than
    /// where it lands, since a touchscreen shows no area before the finger is down — and cast through the same
    /// <see cref="DeployController.TryPlayCard" /> a troop uses, which pays the Energy once. A spot with no valid
    /// cluster highlights nothing and cannot be cast. A Protocol the player cannot afford waits in
    /// <see cref="InteractionState.CardSelected" />, <see cref="InteractionState.Dragging" /> or
    /// <see cref="InteractionState.Previewing" /> exactly as a troop does, and is promoted the moment the balance
    /// reaches its cost. Protocol options are still dropped by <see cref="SelectionTargetResolver.IsOptionForSource" />,
    /// which is what makes retaining <see cref="_options" /> across frames safe; the human's cluster lives in
    /// <see cref="_clusterBuffer" />.
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
    /// list, the target, cluster and hand-lookup buffers, and the card-definition cache are all built once and
    /// reused; a selection allocates only when it names a card this session has not resolved before. After that, a
    /// whole press-drag-release cycle allocates nothing, and neither does hover-aiming a Protocol — the guarantee
    /// <c>MatchInputSteadyStateAllocationTests</c> holds this type to.
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

        // Sized to the widest authored Protocol cluster and the widest impact spiral; a card authored wider grows them,
        // as BoardMetrics warns.
        private const int ClusterCapacity = BoardMetrics.MaxSpellClusterSize;
        private const int ClusterCandidateCapacity = BoardMetrics.MaxImpactAreaCells;

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

        private static readonly HexCoordinates[] _noCluster = Array.Empty<HexCoordinates>();

        private static readonly ProfilerMarker _updateSpellPreviewMarker = new("MatchInputController.UpdateSpellPreview");

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
        private readonly List<HexCoordinates> _clusterBuffer = new(ClusterCapacity);
        private readonly List<HexCoordinates> _clusterScratch = new(ClusterCapacity);
        private readonly List<HexCoordinates> _castTargets = new(ClusterCapacity);
        private readonly List<HexCell> _clusterCandidates = new(ClusterCandidateCapacity);

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
        private CardDefinition _spellCard;
        private HexCoordinates _previewCentre;
        private Vector2 _pressOrigin;
        private int _localPlayerId = PlayerSlot.UnassignedId;
        private float _energy;
        private float _energyAtLastResolve;
        private MatchPhase _phase = MatchPhase.None;
        private PressGesture _pressGesture;
        private bool _isPointerDown;
        private bool _isCommitting;
        private bool _hasSpellPreview;
        private bool _isSpellPreviewValid;

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
        /// The cluster a Protocol being aimed would be cast at, centre first. Empty while nothing is aimed, while the
        /// preview is suspended over the HUD or off the board, and whenever <see cref="IsSpellPreviewValid" /> is
        /// false. Owned by this controller and rewritten on the next pointer move — a fixture copies what it keeps.
        /// </remarks>
        internal IReadOnlyList<HexCoordinates> SpellPreview => _isSpellPreviewValid ? _clusterBuffer : _noCluster;

        /// <remarks>Whether the spot under the pointer holds a cluster the aimed Protocol can be cast at right now.</remarks>
        internal bool IsSpellPreviewValid => _isSpellPreviewValid;

        // Board moves have no phase gate of their own (see the class remarks), so this is what stops a selection
        // from being started outside play. The initial value is MatchPhase.None until the first
        // MatchPhaseChanged arrives, so nothing can select before a match phase is even announced.
        private bool IsPlayOpen => _phase is MatchPhase.Standard or MatchPhase.Overtime;

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
                _pointerSource.PointerHovered += HandlePointerHovered;
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
            MatchEvents.HandChanged += HandleHandChanged;
        }

        // Camera.main walks the scene by tag, which is a scene-dependent lookup and therefore belongs in Start
        // rather than Awake. MatchRoot.prefab wires _boardCamera to its own camera, so this fallback only runs
        // for an instance that lost the reference.
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
                _pointerSource.PointerHovered -= HandlePointerHovered;
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
            MatchEvents.HandChanged -= HandleHandChanged;

            _isPointerDown = false;
            _pressGesture = PressGesture.None;

            CancelSelection();
        }

        /// <remarks>
        /// A test seam, so a PlayMode fixture can hand the controller a HUD without the editor-only
        /// <c>SerializedObject</c>. The scene wires <see cref="_hudDocument" /> in the prefab; nothing at runtime
        /// calls this.
        /// </remarks>
        internal void SetHudDocumentForTests(UIDocument document)
        {
            _hudDocument = document;
        }

        private static bool HasSameSequence(List<HexCoordinates> left, List<HexCoordinates> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void CopySequence(List<HexCoordinates> source, List<HexCoordinates> destination)
        {
            destination.Clear();

            for (int i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }

        private void CancelSelection()
        {
            _stateMachine.Cancel();
            _targets.Clear();
            _spellCard = null;
            ResetSpellPreview();

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

            SelectionTargetResolver.CollectTargets(_options, in source, _targets);

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
            if (!IsPlayOpen)
            {
                return;
            }

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

        private void CommitTarget(HexCoordinates target)
        {
            InteractionSource source = _stateMachine.Source;

            if (!SelectionTargetResolver.TryFindOptionForTarget(_options, in source, target, out MoveOption option))
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
                LogCardPlayRejected(option.SlotIndex, result);
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
                LogMoveRejected(option.MoveType, result);
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
                    LogCardDiscardRejected(slotIndex, result);
                }
            }
            finally
            {
                _isCommitting = false;

                CancelSelection();
            }
        }

        // PERF: each formats and logs only in the Editor or a development build. Debug.Log boxes its enum and
        // int arguments, builds a string and captures a stack trace on every call, so a match played at speed —
        // where ResolverBusy and TargetOccupied are ordinary contention, not faults — would otherwise pay that
        // cost in a release build for a line nobody can read.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogCardPlayRejected(int slotIndex, CardPlayResult result)
        {
            Debug.Log(string.Format(InputLogMessages.CardPlayRejectedFormat, _localPlayerId, slotIndex, result), this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogMoveRejected(MoveType moveType, MovementResult result)
        {
            Debug.Log(string.Format(InputLogMessages.MoveRejectedFormat, _localPlayerId, moveType, result), this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogCardDiscardRejected(int slotIndex, CardDiscardResult result)
        {
            Debug.Log(string.Format(InputLogMessages.CardDiscardRejectedFormat, _localPlayerId, slotIndex, result), this);
        }

        private void SelectSpell(int slotIndex, CardDefinition spellCard)
        {
            bool isSelected = CanAffordSpell(spellCard) ? _stateMachine.TrySelectSpellSlot(slotIndex) : _stateMachine.TrySelectHandSlot(slotIndex);

            if (!isSelected)
            {
                return;
            }

            _spellCard = spellCard;
        }

        private bool TryGetSpellCard(int slotIndex, out CardDefinition spellCard)
        {
            spellCard = null;

            if (_localPlayerId == PlayerSlot.UnassignedId || UnityReference.IsUnavailable(_cardCycle))
            {
                return false;
            }

            if (!_cardCycle.TryGetSlot(_localPlayerId, slotIndex, out CardId cardId))
            {
                return false;
            }

            CardDefinition definition = GetCardDefinition(cardId);

            if (definition == null || definition.Type != CardType.Spell)
            {
                return false;
            }

            spellCard = definition;

            return true;
        }

        // Priced as a Deploy, which is what DeployController charges a Protocol at. Exact rather than quantised like
        // the troop edge, because this asks one ledger question instead of re-enumerating the board.
        private bool CanAffordSpell(CardDefinition spellCard)
        {
            return spellCard != null
                && _localPlayerId != PlayerSlot.UnassignedId
                && !UnityReference.IsUnavailable(_energyLedger)
                && _energyLedger.CanAffordMove(_localPlayerId, MoveType.Deploy, spellCard.EnergyCost);
        }

        // A Protocol selected while unaffordable waits in CardSelected, Dragging or Previewing like a troop, and is
        // promoted here the moment it can be paid for. The live press keeps its gesture across both edges, so a card
        // dragged out of the hand carries on as a drag-aim and a press held on the board carries on as a board aim
        // that casts where it lifts. The fall back to waiting only happens if the balance drops under a live aim,
        // which no opponent action can cause.
        private void RefreshSpellAffordability()
        {
            if (CanAffordSpell(_spellCard))
            {
                if (!_stateMachine.TryBeginSpellTargeting())
                {
                    return;
                }

                RefreshSpellPreviewAtPointer();

                return;
            }

            if (_stateMachine.State != InteractionState.SpellTargeting)
            {
                return;
            }

            SuspendSpellPreview();
            _stateMachine.TryEndSpellTargeting();
        }

        // A mouse resting over the board raises no hover until it moves, so an aim that just became possible reads
        // the pointer once rather than waiting for the next event.
        private void RefreshSpellPreviewAtPointer()
        {
            if (UnityReference.IsUnavailable(_pointerSource))
            {
                return;
            }

            Vector2 position = _pointerSource.CurrentScreenPosition;

            if (_isPointerDown)
            {
                MoveSpellAim(position);

                return;
            }

            UpdateSpellPreview(position);
        }

        // A press on the HUD that is not on a card aims nothing, so a finger dragged from there onto the board shows no
        // cluster its release could never cast.
        private void MoveSpellAim(Vector2 position)
        {
            switch (_pressGesture)
            {
                case PressGesture.Hand:
                case PressGesture.HandDrag:
                    DragSpellAimFromHand(position);

                    return;

                case PressGesture.Board:
                    UpdateSpellPreview(position);

                    return;

                default:
                    SuspendSpellPreview();

                    return;
            }
        }

        // A press on the HUD that is not on a card settles nothing: it aimed nothing on the way there.
        private void ReleaseSpellAim(Vector2 position, PressGesture gesture)
        {
            if (gesture is PressGesture.Hand or PressGesture.HandDrag)
            {
                ReleaseSpellAimFromHand(position, gesture);

                return;
            }

            if (gesture == PressGesture.Board)
            {
                ReleaseBoardSpellAim(position);
            }
        }

        private void DragSpellAimFromHand(Vector2 position)
        {
            if (_pressGesture == PressGesture.Hand && GestureClassifier.ClassifyHold(_pressOrigin, position, _dragThresholdInDp) == PointerGesture.Drag)
            {
                _pressGesture = PressGesture.HandDrag;
                SetDiscardZoneArmed(true);
            }

            if (_pressGesture == PressGesture.HandDrag && IsScreenPointInDiscardZone(position))
            {
                SuspendSpellPreview();

                return;
            }

            UpdateSpellPreview(position);
        }

        // A touchscreen reports no position until a finger is down, so this press is the first moment a finger-aimed
        // Protocol can show its area at all: it previews under the finger, follows it through MoveSpellAim, and casts
        // only when the finger lifts. Casting on the press instead lands the Protocol on a hex whose area the player
        // never saw. A mouse takes the same path — a click is a press and a release at one spot — so the controller
        // needs no knowledge of the device. Off the grid still cancels outright, as a troop's tap there does.
        private void BeginBoardSpellAim(Vector2 position)
        {
            if (!TryResolveBoardPoint(position, out HexCoordinates centre, out Vector2 boardPosition))
            {
                CancelSelection();

                return;
            }

            UpdateSpellPreviewAt(centre, boardPosition);
        }

        // Lifting over the HUD keeps the aim with nothing cast, since the finger cannot mean a hex it cannot see; lifting
        // on an on-grid spot with no valid cluster keeps it too, as a refused tap does; lifting on empty space off the
        // grid cancels. PERF: the arithmetic hit test runs before the panel pick, but the pick still runs on both of its
        // sides — off the board it is what tells the hand strip from empty space.
        private void ReleaseBoardSpellAim(Vector2 position)
        {
            bool isOnBoard = TryResolveBoardPoint(position, out HexCoordinates centre, out Vector2 boardPosition);

            if (IsScreenPointOverHud(position))
            {
                SuspendSpellPreview();

                return;
            }

            if (!isOnBoard)
            {
                CancelSelection();

                return;
            }

            if (UpdateSpellPreviewAt(centre, boardPosition))
            {
                CastSpell();
            }
        }

        // Settles a press on a card. One that never left it is the tap that selected the Protocol, which settles
        // nothing and leaves the aim live — the whole of the tap-then-tap path. One that did is a drag out of the hand.
        private void ReleaseSpellAimFromHand(Vector2 position, PressGesture gesture)
        {
            bool isDragRelease =
                gesture == PressGesture.HandDrag || GestureClassifier.ClassifyHold(_pressOrigin, position, _dragThresholdInDp) == PointerGesture.Drag;

            if (!isDragRelease)
            {
                return;
            }

            // Read before disarming: the zone only reports a hit while armed, so the reverse order never discards.
            bool isOverDiscardZone = IsScreenPointInDiscardZone(position);
            SetDiscardZoneArmed(false);

            if (isOverDiscardZone)
            {
                DiscardSelectedCard(_stateMachine.Source.SlotIndex);

                return;
            }

            if (IsScreenPointOverHud(position))
            {
                CancelSelection();

                return;
            }

            CastDraggedSpellAt(position);
        }

        // A drag out of the hand that lands anywhere it cannot cast — off the grid, or on a spot with no valid
        // cluster — is an abandonment and cancels, unlike a press held on the board, whose release keeps the aim live.
        private void CastDraggedSpellAt(Vector2 screenPosition)
        {
            if (TryResolveBoardPoint(screenPosition, out HexCoordinates centre, out Vector2 boardPosition) && UpdateSpellPreviewAt(centre, boardPosition))
            {
                CastSpell();

                return;
            }

            CancelSelection();
        }

        // A refusal keeps the aim and the preview, because the usual causes — ResolverBusy while another play is
        // resolving, or a balance that moved — are ones the player can simply retry. The message names the code, as
        // a troop's does. An exception tears the aim down, since the board's state after it is unknown.
        private void CastSpell()
        {
            if (_deployController == null)
            {
                return;
            }

            int slotIndex = _stateMachine.Source.SlotIndex;
            CardPlayResult result = CardPlayResult.BoardUnavailable;
            bool isRefused = false;

            // Handed over as a copy, the way a troop commit hands over _deployTargets rather than _targets: the play
            // dispatches synchronously, and a phase change or match end heard mid-cast cancels the selection, which
            // clears the preview buffer the board would otherwise still be reading.
            CopySequence(_clusterBuffer, _castTargets);

            // Latched for the same reason CommitTarget latches: the play publishes Energy and hand changes
            // synchronously, and those are this cast's own.
            _isCommitting = true;

            try
            {
                result = _deployController.TryPlayCard(_localPlayerId, slotIndex, _castTargets);
                isRefused = result != CardPlayResult.Success;
            }
            finally
            {
                _isCommitting = false;

                if (!isRefused)
                {
                    CancelSelection();
                }
            }

            if (isRefused)
            {
                LogCardPlayRejected(slotIndex, result);
            }
        }

        // PERF: the arithmetic hit test runs first and the HUD panel pick only for a point that is on the board, the
        // same ordering TryResolveHighlightedHex uses. That still means one panel pick per on-board pointer move while
        // a Protocol is aimed — a tree walk the troop path only pays over a highlighted hex. It is accepted for now
        // because it is allocation-free, bounded to the aiming gesture, and cannot be cached per hex: the HUD can
        // cover part of one. Knowing which hexes the HUD can reach at all would remove it, and needs the HUD's help.
        private void UpdateSpellPreview(Vector2 screenPosition)
        {
            using (_updateSpellPreviewMarker.Auto())
            {
                if (!TryResolveBoardPoint(screenPosition, out HexCoordinates centre, out Vector2 boardPosition) || IsScreenPointOverHud(screenPosition))
                {
                    SuspendSpellPreview();

                    return;
                }

                UpdateSpellPreviewAt(centre, boardPosition);
            }
        }

        // PERF: the arrangement is recomputed on every move because it is the only way to learn whether the move
        // changed it, but it is a spiral of a few cells and a partial sort into owned buffers. Validation and the
        // highlight pass — the part that writes renderers — run only when the hovered hex or the neighbour order
        // actually changed, which a pointer drifting inside one hex rarely does.
        private bool UpdateSpellPreviewAt(HexCoordinates centre, Vector2 boardPosition)
        {
            HexGrid grid = GetGrid();
            IReadOnlyList<ImpactEffect> landingEffects = _spellCard?.LandingEffects;

            if (grid == null || _pointerResolver == null || landingEffects == null || landingEffects.Count == 0)
            {
                SuspendSpellPreview();

                return false;
            }

            bool isArranged = ClusterTargetBuilder.TryArrange(
                grid,
                landingEffects[0],
                centre,
                boardPosition,
                _pointerResolver.CellVisualSize,
                _clusterCandidates,
                _clusterScratch
            );

            if (_hasSpellPreview && centre == _previewCentre && HasSameSequence(_clusterScratch, _clusterBuffer))
            {
                return _isSpellPreviewValid;
            }

            _hasSpellPreview = true;
            _previewCentre = centre;
            CopySequence(_clusterScratch, _clusterBuffer);
            _isSpellPreviewValid = isArranged && ClusterTargetBuilder.AreTargetsValidForEveryImpact(_clusterBuffer, landingEffects, grid);

            ApplySpellPreview();

            return _isSpellPreviewValid;
        }

        private void ApplySpellPreview()
        {
            if (_highlightPresenter == null)
            {
                return;
            }

            if (_isSpellPreviewValid)
            {
                _highlightPresenter.SetTargets(_clusterBuffer);

                return;
            }

            _highlightPresenter.ClearTargets();
        }

        // Keeps the aim and drops only what is drawn. The cache goes with it, or coming back onto the hex the preview
        // was suspended on would read as "nothing changed" and leave the board dark.
        private void SuspendSpellPreview()
        {
            if (!_hasSpellPreview)
            {
                return;
            }

            ResetSpellPreview();

            if (_highlightPresenter != null)
            {
                _highlightPresenter.ClearTargets();
            }
        }

        private void ResetSpellPreview()
        {
            _hasSpellPreview = false;
            _isSpellPreviewValid = false;
            _clusterBuffer.Clear();
            _clusterScratch.Clear();
        }

        private void SetDiscardZoneArmed(bool isArmed)
        {
            if (UnityReference.IsUnavailable(_handGestureSource))
            {
                return;
            }

            _handGestureSource.SetDiscardZoneArmed(isArmed);
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

        private bool TryResolveBoardPoint(Vector2 screenPosition, out HexCoordinates coordinates, out Vector2 boardPosition)
        {
            coordinates = default;
            boardPosition = default;

            return _pointerResolver != null && _pointerResolver.TryResolveBoardPoint(screenPosition, GetGrid(), out coordinates, out boardPosition);
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

            // A press over the HUD belongs to the hand strip, which reports it through IHandGestureSource once UI
            // Toolkit dispatches it — after this callback, so acting on this press as well would cancel the very
            // selection that report is about to start. Every press sets the gesture afresh, so nothing a previous
            // press left behind can reach this one.
            if (IsScreenPointOverHud(_pressOrigin))
            {
                _pressGesture = PressGesture.Hud;

                return;
            }

            _pressGesture = PressGesture.Board;

            if (_stateMachine.State == InteractionState.SpellTargeting)
            {
                BeginBoardSpellAim(_pressOrigin);

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

            if (_stateMachine.State == InteractionState.SpellTargeting)
            {
                MoveSpellAim(position);

                return;
            }

            // A card is only dragged by a press that is on it. A Protocol that fell below its cost while held on the
            // board is still a hand-slot selection, and without this it would turn into a drag toward the discard zone.
            if (_stateMachine.Source.Kind == InteractionSourceKind.HandSlot && _pressGesture is not (PressGesture.Hand or PressGesture.HandDrag))
            {
                return;
            }

            if (GestureClassifier.ClassifyHold(_pressOrigin, position, _dragThresholdInDp) == PointerGesture.Drag && _stateMachine.TryBeginDrag())
            {
                if (_stateMachine.Source.Kind == InteractionSourceKind.HandSlot)
                {
                    _pressGesture = PressGesture.HandDrag;
                    SetDiscardZoneArmed(true);
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

        // Only a mouse hovers, and only a Protocol reads it: a troop's targets are shown whole on selection, so there
        // is nothing under the pointer for it to follow.
        private void HandlePointerHovered(PointerSample sample)
        {
            if (_isPointerDown || _stateMachine.State != InteractionState.SpellTargeting)
            {
                return;
            }

            UpdateSpellPreview(sample.ScreenPosition);
        }

        private void HandlePointerReleased(PointerSample sample)
        {
            _isPointerDown = false;
            PressGesture pressGesture = _pressGesture;
            _pressGesture = PressGesture.None;

            if (_stateMachine.State == InteractionState.Idle)
            {
                return;
            }

            // A press the system took away — focus lost to a call or the notification shade, a device reset, a touch
            // the OS cancelled — was never lifted by the player, so where it was last seen is not a choice to commit.
            if (sample.Phase == PointerPhase.Canceled)
            {
                CancelSelection();

                return;
            }

            Vector2 position = sample.ScreenPosition;

            if (_stateMachine.State == InteractionState.SpellTargeting)
            {
                ReleaseSpellAim(position, pressGesture);

                return;
            }

            // A Protocol that fell below its cost under a press held on the board keeps waiting for the balance, as a
            // tap on its card does, rather than being settled by the troop rules below.
            if (_spellCard != null && pressGesture == PressGesture.Board)
            {
                return;
            }

            InteractionSource source = _stateMachine.Source;

            bool isOverDiscardZone = source.Kind == InteractionSourceKind.HandSlot && IsScreenPointInDiscardZone(position);
            HexCoordinates target = default;
            bool isOverTarget = !isOverDiscardZone && TryResolveHighlightedHex(position, out target);

            PointerGesture gesture = GestureClassifier.ClassifyRelease(_pressOrigin, position, _dragThresholdInDp, isOverDiscardZone || isOverTarget);

            // A tap off the grid is cancelled on its press, in HandlePointerPressed. A release that travelled and
            // landed on no target is cancelled below, by the general Cancel branch. What lands here is a release
            // that never travelled past the threshold: while the selection is still a bare tap-select
            // (CardSelected or UnitSelected) that settles nothing, leaving it live so the player can tap a
            // highlighted hex next — the whole of the tap-then-tap path. Once a drag has begun, though, the same
            // short release is a drag that returned home rather than a tap-then-tap step, and the zone or preview
            // it armed on the way out must be torn down the same as any other abandonment.
            if (gesture == PointerGesture.Tap)
            {
                if (_stateMachine.State is InteractionState.CardSelected or InteractionState.UnitSelected)
                {
                    return;
                }

                CancelSelection();

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

        // Marks the live press as one on a card, which is what lets it be dragged out of the hand. The report is
        // trusted over the controller's own HUD pick, which sees no hand at all when no HUD is wired. A report with no
        // press live is a tap whose press and release both landed in one input update before UI Toolkit dispatched
        // it; it still selects, but leaves no gesture behind for the next press to inherit.
        private void HandleHandSlotPressed(int slotIndex)
        {
            if (_isPointerDown)
            {
                _pressGesture = PressGesture.Hand;
            }

            if (!IsPlayOpen)
            {
                return;
            }

            InteractionSource source = _stateMachine.Source;
            bool isSecondPressOnSource = source.Kind == InteractionSourceKind.HandSlot && source.SlotIndex == slotIndex;

            CancelSelection();

            if (isSecondPressOnSource)
            {
                return;
            }

            if (TryGetSpellCard(slotIndex, out CardDefinition spellCard))
            {
                SelectSpell(slotIndex, spellCard);

                return;
            }

            if (!_stateMachine.TrySelectHandSlot(slotIndex))
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
            _phase = phase;

            if (IsPlayOpen)
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
            // A Protocol's cluster reads no occupancy, so nothing a landing changes can make it legal or illegal.
            if (_isCommitting || _stateMachine.State == InteractionState.Idle || _spellCard != null)
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

            if (_spellCard != null)
            {
                RefreshSpellAffordability();

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

        // The aimed card left its slot without this controller casting it. The local player's own plays and discards
        // already cancel, so this is the backstop for any other rotation; the opponent's hand is not ours to read.
        private void HandleHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard)
        {
            if (_isCommitting || _spellCard == null || playerId != _localPlayerId)
            {
                return;
            }

            int slotIndex = _stateMachine.Source.SlotIndex;

            if (hand != null && slotIndex >= 0 && slotIndex < hand.Count && hand[slotIndex] == _spellCard.CardId)
            {
                return;
            }

            CancelSelection();
        }

        // The live press, by where it started and — for a card — whether it has left it. One value rather than a set of
        // flags, so a press cannot be on the board and on a card at once, and nothing a gesture decided can outlive it:
        // every press sets it, and every release, cancelled or not, clears it.
        private enum PressGesture
        {
            // No press is live.
            None = 0,

            // Pressed off the HUD. While a Protocol is aimed, it previews under the pointer and casts where it lifts.
            Board = 1,

            // Pressed on the HUD but not on a card. Aims nothing and settles nothing.
            Hud = 2,

            // Pressed on a card and not yet dragged past the threshold. Its release is the tap that selected the card.
            Hand = 3,

            // A card press dragged past the threshold. Arms the discard zone, and its release settles the drag.
            HandDrag = 4,
        }
    }
}
