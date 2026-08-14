using System;
using System.Collections.Generic;
using System.Text;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace GooGalaxy.Playtest
{
    /// <summary>
    /// Throwaway harness that makes the board playable by hand: it seeds the GDD's opening position, wires the
    /// Clone spawner, turns pointer taps into move commands, spends energy for the card being played, and calls
    /// the match on Domination. Not a game mode — there is no deck or cycle, no turn timer, and no match clock,
    /// and the two sides are driven hot-seat through an active-player switch rather than by turn order.
    /// </summary>
    /// <remarks>
    /// This exists because nothing yet drives the board: no system calls <c>UnitPresenter.ResolveMove</c>, none
    /// registers the starting units, and no <c>IUnitSpawner</c> ships in runtime code, which makes a Clone
    /// fail with <c>SpawnFailed</c>. Delete this whole assembly once the real match bootstrap lands.
    /// <para>
    /// Its log text is deliberately exempt from the "log messages live as consts in a centralized class" rule.
    /// That rule exists so a message can be found, asserted on by a test, and kept stable across the releases
    /// people triage against; none of the three applies to a harness that is scheduled for deletion and whose
    /// lines are read live, next to the board, by the person who just tapped. Extracting twenty throwaway
    /// strings into a <c>PlaytestLogMessages</c> class would move them away from the code that explains them
    /// and outlive neither. Runtime assemblies keep the rule.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class PlaytestBootstrap : MonoBehaviour
    {
        private const int FirstClonedUnitId = 1000;
        private const int CloneRange = 1;
        private const int JumpRange = 2;
        private const int MaxPickResults = 4;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;

        // The widest cluster any authored Protocol picks. Only a capacity hint.
        private const int MaxSpellTargets = BoardMetrics.MaxSpellClusterSize;

        // Printed when a status marker carries a type this build has no name for, which can only happen if
        // StatusType gained a value and the table below was not extended with it.
        private const string UnknownStatusName = "Status?";

        // PERF: indexed by StatusType's own value. Enum.ToString() allocates and resolves through IL2CPP's enum
        // metadata, and the readout below runs inside the AbilityResolved dispatch, which is budgeted at zero
        // bytes. Keep this in step with StatusType or a new value prints as UnknownStatusName.
        private static readonly string[] _statusNames = { "None", "Frozen", "Rooted" };

        [Header("Starting Units")]
        [Tooltip("Units placed on the board at startup. Each card id must exist on the CardPresenter roster.")]
        [SerializeField]
        private StartingUnit[] _startingUnits = Array.Empty<StartingUnit>();

        [Header("Hand")]
        [Tooltip("Card ids offered in the hand, in display order. Each must exist on the CardPresenter roster.")]
        [SerializeField]
        private string[] _handCardIds = Array.Empty<string>();

        [Header("Aiming")]
        [Tooltip("Distance from a hex center to its corner vertex, in world units. Must match GridView and UnitView or the preview snaps to wrong hexes.")]
        [SerializeField]
        private float _cellVisualSize = 1.0f;

        [Header("Input")]
        [Tooltip("Layer the cell colliders live on. Leave as Everything unless the board shares the scene with other clickable geometry.")]
        [SerializeField]
        private LayerMask _cellLayerMask = ~0;

        [Header("Diagnostics")]
        [Tooltip(
            "Prints what each impact touched, and the whole statused roster after every deployment. "
                + "Off by default because it runs inside the AbilityResolved dispatch: one StringBuilder pass over the touched units "
                + "plus a console entry per deployment, on a path that is otherwise allocation-free. "
                + "Turn it on to watch a Cryo-Stasis freeze apply and expire."
        )]
        [SerializeField]
        private bool _isStatusReadoutEnabled;

        private readonly List<HexCoordinates> _seededCoordinates = new();
        private readonly List<Collider2D> _pickResults = new(MaxPickResults);
        private readonly List<int> _unitIdBuffer = new();
        private readonly List<PlaytestHudView.HandCard> _hand = new();
        private readonly Dictionary<string, int> _cardCosts = new();

        // One capability per hand card, built with the hand. The type is stateless — a single readonly card
        // reference — so a fresh instance per cast and per card-driven Clone bought nothing but garbage.
        private readonly Dictionary<string, PlaytestMoveCapability> _cardCapabilities = new();

        private readonly List<HexCoordinates> _spellTargets = new(MaxSpellTargets);
        private readonly List<HexCoordinates> _previewBuffer = new(MaxSpellTargets);
        private readonly List<HexCell> _neighborBuffer = new(BoardMetrics.NeighborsPerCell);
        private readonly StringBuilder _statusReport = new();

        private ContactFilter2D _contactFilter;

        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private CardPresenter _cardPresenter;
        private GridView _gridView;
        private UnitView _unitView;
        private EnergyPresenter _energyPresenter;
        private AbilityController _abilityController;
        private PlaytestHudView _hudView;
        private Camera _camera;
        private PlaytestUnitSpawner _spawner;
        private bool _isMatchOver;
        private int _activePlayerId = PlayerOneId;
        private string _selectedCardId;
        private int _selectedUnitId = -1;
        private HexCoordinates _selectedCoordinates;
        private bool _hasSelection;
        private int _lastAffordableCount = -1;
        private bool _isPreviewValid;
        private bool _isDraggingSpellAim;
        private bool _hasLoggedInvalidCentre;
        private HexCoordinates _lastInvalidCentre;
        private ICardData _selectedSpellCard;

        // ICardData is an interface, so a null test on _selectedSpellCard is a plain reference compare that
        // never calls UnityEngine.Object's destroyed-object overload — this bool is the real "is a Protocol
        // selected" test everywhere the field would otherwise be null-checked.
        private bool _isSpellSelected;
        private int _selectedSpellClusterSize;
        private Vector2 _lastPreviewPointerPosition;
        private bool _hasPreviewPointerPosition;

        /// <remarks>
        /// The runtime systems come from <c>GameLifetimeScope</c> and the HUD from
        /// <see cref="PlaytestLifetimeScope" />, the child scope that registers what this assembly owns.
        /// </remarks>
        [Inject]
        public void Construct(
            GridPresenter gridPresenter,
            UnitPresenter unitPresenter,
            CardPresenter cardPresenter,
            GridView gridView,
            UnitView unitView,
            EnergyPresenter energyPresenter,
            AbilityController abilityController,
            PlaytestHudView hudView
        )
        {
            _gridPresenter = gridPresenter;
            _unitPresenter = unitPresenter;
            _cardPresenter = cardPresenter;
            _gridView = gridView;
            _unitView = unitView;
            _energyPresenter = energyPresenter;
            _abilityController = abilityController;
            _hudView = hudView;
        }

        protected void Awake()
        {
            _camera = Camera.main;

            // The cell colliders are triggers, which the default filter would skip.
            _contactFilter = default;
            _contactFilter.useTriggers = true;
            _contactFilter.SetLayerMask(_cellLayerMask);

            if (_camera == null)
            {
                Debug.LogError("PlaytestBootstrap: no camera tagged MainCamera. Clicks cannot be resolved to cells.", this);
            }
        }

        protected void OnEnable()
        {
            MatchEvents.ConversionResolved += HandleConversionResolved;
            MatchEvents.AbilityResolved += HandleAbilityResolved;

            if (_hudView != null)
            {
                _hudView.ResetRequested += HandleResetRequested;
                _hudView.CardSelected += HandleCardSelected;
                _hudView.ActivePlayerToggleRequested += HandleActivePlayerToggleRequested;
            }

            MatchEvents.EnergyChanged += HandleEnergyChanged;
        }

        protected void Start()
        {
            if (_unitPresenter == null || _cardPresenter == null || _gridPresenter == null)
            {
                Debug.LogError("PlaytestBootstrap: GridPresenter, UnitPresenter, and CardPresenter must all be assigned.", this);
                return;
            }

            RebuildVisualGrid();
            StartMatch();
        }

        protected void Update()
        {
            if (_isMatchOver)
            {
                return;
            }

            // Pointer rather than Mouse: the Device Simulator disables the mouse device and feeds a simulated
            // Touchscreen instead, so a mouse-only read never fires there — and never would on a real phone.
            Pointer pointer = Pointer.current;

            if (pointer == null)
            {
                return;
            }

            // The HUD is a UI Toolkit overlay drawn on top of the board, so any point under it must never reach
            // the physics pick below: Physics2D.OverlapPoint knows nothing about UI and would otherwise hit the
            // cell behind a hand card on the same click that selects it. The guard samples the pointer position
            // rather than reacting to CardSelected, and it samples it on the very frame the press lands, which
            // is what makes it hold regardless of whether UI Toolkit's click dispatch for that card ran before
            // or after this Update — the pointer has not moved between the two, so the position is the same
            // either way. Frames where the answer cannot matter are not sampled at all: IsPointerOverUI walks
            // the visual tree and recomputes world clip rects, and on the common frame — no Protocol aimed, no
            // press — nothing would read the result.
            if (_isSpellSelected)
            {
                UpdateSpellAim(_selectedSpellCard, pointer, IsPointerOverHud(pointer));
                return;
            }

            if (!pointer.press.wasPressedThisFrame)
            {
                return;
            }

            if (IsPointerOverHud(pointer))
            {
                return;
            }

            if (!TryPickCell(out HexCoordinates coordinates))
            {
                return;
            }

            HandleCellClicked(coordinates);
        }

        protected void OnDisable()
        {
            MatchEvents.ConversionResolved -= HandleConversionResolved;
            MatchEvents.AbilityResolved -= HandleAbilityResolved;

            if (_hudView != null)
            {
                _hudView.ResetRequested -= HandleResetRequested;
                _hudView.CardSelected -= HandleCardSelected;
                _hudView.ActivePlayerToggleRequested -= HandleActivePlayerToggleRequested;
            }

            MatchEvents.EnergyChanged -= HandleEnergyChanged;
        }

        /// <summary>
        /// Puts the board back to the GDD's opening position and starts a fresh match: every unit cleared and
        /// re-seeded, energy back to its starting values, and the result banner hidden.
        /// </summary>
        public void StartMatch()
        {
            // Public and reachable from the Reset button, so it cannot lean on Start's guard having run.
            if (_unitPresenter == null || _cardPresenter == null || _gridPresenter == null)
            {
                Debug.LogError("PlaytestBootstrap: cannot start a match without GridPresenter, UnitPresenter, and CardPresenter.", this);
                return;
            }

            BuildHand();
            ClearAllUnits();

            // A new spawner rather than a reset one: clone ids restart from the same base every match, so a
            // stale id can never collide with a freshly seeded unit.
            _spawner = new PlaytestUnitSpawner(_cardPresenter, FirstClonedUnitId);
            _unitPresenter.SetUnitSpawner(_spawner);

            SeedStartingUnits();

            if (_unitView != null)
            {
                _unitView.SyncUnitVisuals();
            }

            if (_energyPresenter != null)
            {
                _energyPresenter.InitializeMatch();
            }

            _isMatchOver = false;
            _selectedCardId = null;
            _selectedSpellCard = null;
            _isSpellSelected = false;
            _selectedSpellClusterSize = 0;
            ClearSelection();
            ClearSpellAim();

            if (_hudView != null)
            {
                _hudView.ClearResult();
                _hudView.SetHand(_hand);
                _hudView.SetSelectedCard(null);
                _hudView.SetActivePlayer(_activePlayerId);
            }

            PublishTroopCounts();

            // A fresh match rebuilds the hand, so the cached count was measured against the previous card costs.
            _lastAffordableCount = -1;
            PublishAffordability();
        }

        // GridPresenter raises GridInitialized from Awake, but a subscriber only registers in OnEnable, so on a
        // scene load GridView is still deaf when the event goes out and never builds a cell. Re-raising from
        // Start — after every OnEnable has run — is the harness working around an ordering gap that the real
        // match bootstrap will have to solve properly, most likely by owning the initialization order itself.
        private void RebuildVisualGrid()
        {
            HexGrid grid = _gridPresenter.HexGrid;

            if (grid == null)
            {
                Debug.LogError("PlaytestBootstrap: GridPresenter built no grid. Check that its Grid Layout asset is assigned.", this);
                return;
            }

            MatchEvents.RaiseGridInitialized(grid);
        }

        // Rebuilt per match so a change to the roster or the id list is picked up by Reset without a domain reload.
        private void BuildHand()
        {
            _hand.Clear();
            _cardCosts.Clear();
            _cardCapabilities.Clear();

            for (int i = 0; i < _handCardIds.Length; i++)
            {
                string cardId = _handCardIds[i];

                if (!_cardPresenter.TryGetCard(new CardId(cardId), out ICardData card))
                {
                    Debug.LogError($"PlaytestBootstrap: hand card '{cardId}' is not on the CardPresenter roster.", this);
                    continue;
                }

                _hand.Add(new PlaytestHudView.HandCard(cardId, card.DisplayName, card.EnergyCost));
                _cardCosts[cardId] = card.EnergyCost;
                _cardCapabilities[cardId] = new PlaytestMoveCapability(card);
            }
        }

        // The hand covers every card a cast or a card-driven Clone can name, so the fallback only builds an
        // instance for a starting unit whose card was never dealt.
        private PlaytestMoveCapability ResolveMoveCapability(ICardData card)
        {
            if (_cardCapabilities.TryGetValue(card.CardId.Value, out PlaytestMoveCapability capability))
            {
                return capability;
            }

            return new PlaytestMoveCapability(card);
        }

        private void ClearAllUnits()
        {
            _unitIdBuffer.Clear();

            foreach (int unitId in _unitPresenter.ActiveUnits.Keys)
            {
                _unitIdBuffer.Add(unitId);
            }

            for (int i = 0; i < _unitIdBuffer.Count; i++)
            {
                _unitPresenter.UnregisterUnit(_unitIdBuffer[i]);
            }
        }

        private void HandleResetRequested()
        {
            StartMatch();
        }

        // Domination, per GDD 01: converting every enemy piece ends the match immediately. Conversion is the
        // only way a player can lose their last unit today, so this is the only place it needs checking.
        private void HandleConversionResolved(int actingPlayerId, ConversionResult result)
        {
            CountTroops(out int playerOneTroops, out int playerTwoTroops);

            if (_hudView != null)
            {
                _hudView.SetTroopCounts(playerOneTroops, playerTwoTroops);
            }

            if (_isMatchOver)
            {
                return;
            }

            if (playerOneTroops > 0 && playerTwoTroops > 0)
            {
                return;
            }

            int winnerId = playerOneTroops > 0 ? PlayerOneId : PlayerTwoId;
            _isMatchOver = true;
            ClearSelection();
            ClearSpellAim();

            Debug.Log($"PlaytestBootstrap: DOMINATION — player {winnerId} converted every enemy piece.", this);

            if (_hudView != null)
            {
                _hudView.ShowResult($"DOMINATION\nPlayer {winnerId} wins");
            }
        }

        // UnitView draws a frozen overlay, so the board says *that* a unit is frozen — but not which impact
        // touched it, whose it is, or how many action windows are left. Those only exist in the console, which
        // is why this readout stays. It runs inside the AbilityResolved dispatch, a path budgeted at zero
        // bytes, so it is opt-in and returns before touching anything when it is off. Reads the payload during
        // the dispatch and retains nothing from it.
        private void HandleAbilityResolved(int actingPlayerId, AbilityResult result)
        {
            if (!_isStatusReadoutEnabled)
            {
                return;
            }

            IReadOnlyList<int> affectedUnitIds = result.AffectedUnitIds;

            if (affectedUnitIds == null || affectedUnitIds.Count == 0)
            {
                return;
            }

            _statusReport.Clear();
            _statusReport.Append("PlaytestBootstrap: P").Append(actingPlayerId).Append(" impact touched ").Append(affectedUnitIds.Count).Append(" unit(s) —");

            for (int i = 0; i < affectedUnitIds.Count; i++)
            {
                if (_unitPresenter.ActiveUnits.TryGetValue(affectedUnitIds[i], out GridUnit unit) && unit != null)
                {
                    AppendUnitStatus(unit);
                }
            }

            Debug.Log(_statusReport.ToString(), this);
        }

        // Called after each resolved deployment, which is exactly when a status can have been applied or ticked
        // away. Printing the whole statused roster rather than diffing it means expiry shows up for free: a
        // marker that was listed last deployment and is gone now expired, and no extra subsystem tracks that.
        private void LogActiveStatuses()
        {
            if (!_isStatusReadoutEnabled)
            {
                return;
            }

            _statusReport.Clear();
            int statusedCount = 0;

            foreach (GridUnit unit in _unitPresenter.ActiveUnitValues)
            {
                if (unit == null || !unit.IsAlive || unit.ActiveStatuses.Count == 0)
                {
                    continue;
                }

                AppendUnitStatus(unit);
                statusedCount++;
            }

            if (statusedCount == 0)
            {
                Debug.Log("PlaytestBootstrap: no unit carries a status.", this);
                return;
            }

            Debug.Log($"PlaytestBootstrap: {statusedCount} unit(s) carry a status —{_statusReport}", this);
        }

        // The coordinates are appended component by component rather than through HexCoordinates.ToString(),
        // which is an interpolated "(Q, R)" and so lowers to string.Format with an object[2] and two boxed ints
        // — four allocations per unit, on a path that must not have any.
        private void AppendUnitStatus(GridUnit unit)
        {
            _statusReport
                .Append(" #")
                .Append(unit.UnitId)
                .Append(" P")
                .Append(unit.PlayerId)
                .Append(" (")
                .Append(unit.Position.Q)
                .Append(", ")
                .Append(unit.Position.R)
                .Append(')');

            IReadOnlyList<StatusMarker> statuses = unit.ActiveStatuses;

            if (statuses.Count == 0)
            {
                _statusReport.Append(" [no status]");
                return;
            }

            for (int i = 0; i < statuses.Count; i++)
            {
                int typeIndex = (int)statuses[i].Type;
                string typeName = typeIndex >= 0 && typeIndex < _statusNames.Length ? _statusNames[typeIndex] : UnknownStatusName;

                _statusReport.Append(" [").Append(typeName).Append(' ').Append(statuses[i].RemainingDuration).Append(']');
            }
        }

        private void PublishTroopCounts()
        {
            if (_hudView == null)
            {
                return;
            }

            CountTroops(out int playerOneTroops, out int playerTwoTroops);
            _hudView.SetTroopCounts(playerOneTroops, playerTwoTroops);
        }

        private void CountTroops(out int playerOneTroops, out int playerTwoTroops)
        {
            playerOneTroops = 0;
            playerTwoTroops = 0;

            // ActiveUnitValues, not ActiveUnits.Values: the interface-typed collection boxes its enumerator.
            foreach (GridUnit unit in _unitPresenter.ActiveUnitValues)
            {
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (unit.PlayerId == PlayerOneId)
                {
                    playerOneTroops++;
                }
                else if (unit.PlayerId == PlayerTwoId)
                {
                    playerTwoTroops++;
                }
            }
        }

        private void SeedStartingUnits()
        {
            _seededCoordinates.Clear();

            for (int i = 0; i < _startingUnits.Length; i++)
            {
                StartingUnit seed = _startingUnits[i];
                var cardId = new CardId(seed.CardId);

                if (!_cardPresenter.TryGetCard(cardId, out ICardData card))
                {
                    Debug.LogError($"PlaytestBootstrap: starting unit {i} references card '{seed.CardId}', which is not on the CardPresenter roster.", this);
                    continue;
                }

                var coordinates = new HexCoordinates(seed.Q, seed.R);
                var unit = new GridUnit(seed.UnitId, seed.PlayerId, cardId, coordinates, card.HasArmor);

                if (!_unitPresenter.RegisterUnit(unit, ResolveMoveCapability(card)))
                {
                    continue;
                }

                _seededCoordinates.Add(coordinates);
            }

            Debug.Log($"PlaytestBootstrap: seeded {_seededCoordinates.Count} of {_startingUnits.Length} starting units.", this);
        }

        private void HandleCellClicked(HexCoordinates coordinates)
        {
            if (!_hasSelection)
            {
                TrySelectUnitAt(coordinates);
                return;
            }

            if (coordinates == _selectedCoordinates)
            {
                ClearSelection();
                return;
            }

            TryMoveSelectedTo(coordinates);
        }

        // Hover is a desktop metaphor: a Mouse reports position on every frame, so the preview can track it
        // continuously and a click casts wherever it rests. A Touchscreen has no such thing — Pointer.position
        // only updates while a touch is down — so aiming there has to be press-and-drag, with the cast firing
        // on release instead of on press. The Device Simulator models this exactly: it disables the Mouse
        // device and drives Pointer.current from a simulated Touchscreen, which is also what a real phone
        // reports, so the touch path below is what ships on device. Chosen by capability
        // (Mouse.current.enabled) rather than a platform define, and re-evaluated every frame rather than
        // cached in Awake, because the Simulator flips the Mouse device at runtime. Do not "simplify" this
        // back to hover-only — that breaks aiming on every touch device, including the real phone build.
        private void UpdateSpellAim(ICardData spellCard, Pointer pointer, bool isPointerOverHud)
        {
            if (IsMouseAimModeActive())
            {
                UpdateHoverAim(spellCard, pointer, isPointerOverHud);
                return;
            }

            UpdateTouchDragAim(spellCard, pointer, isPointerOverHud);
        }

        private bool IsMouseAimModeActive()
        {
            return Mouse.current != null && Mouse.current.enabled;
        }

        // Desktop path, unchanged: the preview follows the pointer every frame and a press casts it.
        private void UpdateHoverAim(ICardData spellCard, Pointer pointer, bool isPointerOverHud)
        {
            if (isPointerOverHud)
            {
                // Pointer rests on the HUD (e.g. the card that was just picked) rather than the board.
                // Drop the stale preview instead of leaving it lit on the last aimed cluster while the
                // player works the hand, and do not resolve a cast from whatever lies behind the HUD.
                ClearSpellAim();
                return;
            }

            UpdateSpellPreview(pointer);

            if (pointer.press.wasPressedThisFrame)
            {
                TryCastPreviewedSpell(spellCard);
            }
        }

        // Touch path: press starts the drag, movement while held updates the preview, release casts it. A press
        // that is still held when this runs only aims. A press the Input System reports as pressed *and*
        // released on the same frame — a Device Simulator click, and a fast tap on device, both of which drive
        // the button 0→1→0 inside one input update — is a completed tap, so it aims and casts in one pass:
        // there is no finger left to end a drag it might have started.
        private void UpdateTouchDragAim(ICardData spellCard, Pointer pointer, bool isPointerOverHud)
        {
            if (!_isDraggingSpellAim)
            {
                if (isPointerOverHud || !pointer.press.wasPressedThisFrame)
                {
                    return;
                }

                UpdateSpellPreview(pointer);

                if (!pointer.press.wasReleasedThisFrame)
                {
                    _isDraggingSpellAim = true;
                    return;
                }

                TryCastPreviewedSpell(spellCard);
                return;
            }

            bool isReleased = pointer.press.wasReleasedThisFrame;

            if (isPointerOverHud)
            {
                // Dragged onto the HUD: the preview cannot land on a hex it cannot see, so the highlight is
                // dropped without ending the drag — the finger can still come back onto the board before
                // release. A release here cancels outright rather than casting at the last board position.
                SuspendSpellPreview();

                if (isReleased)
                {
                    _isDraggingSpellAim = false;
                }

                return;
            }

            // A release lost to a focus change or a cancelled touch never sets wasReleasedThisFrame, so without
            // this the drag latches for the rest of the match and the player's *next* tap casts the moment it
            // lands. The button's own state is the authority on whether a finger is still down.
            if (!isReleased && !pointer.press.isPressed)
            {
                _isDraggingSpellAim = false;
                SuspendSpellPreview();
                return;
            }

            UpdateSpellPreview(pointer);

            if (!isReleased)
            {
                return;
            }

            _isDraggingSpellAim = false;
            TryCastPreviewedSpell(spellCard);
        }

        private void SuspendSpellPreview()
        {
            ClearHighlightedTargets();
            _isPreviewValid = false;

            // The pointer gate has to go with the highlights, or coming back onto the board at the hex the
            // preview was suspended on would be read as "nothing moved" and leave it dark.
            _hasPreviewPointerPosition = false;
        }

        // PERF: deliberate harness debt. The rebuild below runs a Physics2D.OverlapPoint, which is exactly the
        // per-frame query the performance rules ban. In hover mode it is reachable on every frame a spell card
        // is picked; in touch mode UpdateTouchDragAim only calls it while a drag is in flight, so an idle
        // touchscreen with a Protocol selected costs nothing. The real drag-and-drop deployment flow will
        // replace it outright rather than inherit it.
        // TODO (GOOM-7): replace the per-frame OverlapPoint when drag-and-drop deployment lands.
        private void UpdateSpellPreview(Pointer pointer)
        {
            // Aiming and unit selection are mutually exclusive; entering one drops the other.
            ClearSelection();

            Vector2 pointerPosition = pointer.position.ReadValue();

            // The cluster is a pure function of the pointer position, so a resting pointer cannot change it.
            // The gate sits in front of the rebuild rather than after it because the rebuild is where the
            // OverlapPoint, the ScreenToWorldPoint, and the neighbour sort live — checked afterwards, a still
            // pointer paid all three every frame to learn nothing had moved.
            if (_hasPreviewPointerPosition && pointerPosition == _lastPreviewPointerPosition)
            {
                return;
            }

            _hasPreviewPointerPosition = true;
            _lastPreviewPointerPosition = pointerPosition;

            bool isValid = TryBuildPreviewCluster(pointerPosition);

            // A pointer that moved within one hex rebuilds the same cluster, so this still earns its place: it
            // skips the highlight churn the rebuild would otherwise repeat on identical cells.
            if (isValid && IsPreviewUnchanged())
            {
                return;
            }

            ClearHighlightedTargets();
            _isPreviewValid = isValid;

            if (!isValid)
            {
                LogInvalidPreviewOnce();
                return;
            }

            _hasLoggedInvalidCentre = false;

            for (int i = 0; i < _previewBuffer.Count; i++)
            {
                _spellTargets.Add(_previewBuffer[i]);
                SetCellHighlight(_previewBuffer[i], true);
            }
        }

        // Neighbours rather than "the N nearest hexes overall", because every neighbour is a hex step from the
        // centre by construction, and that is precisely what AbilityResolver.ValidateTargets checks. A free
        // nearest-N search can return two hexes that are two steps apart near a board edge and would be
        // rejected after the player had already committed the tap. The centroid still tracks the pointer the
        // same way, so it costs nothing in feel.
        private bool TryBuildPreviewCluster(Vector2 pointerPosition)
        {
            _previewBuffer.Clear();

            if (!TryGetBoardPoint(pointerPosition, out Vector3 worldPoint) || !TryPickCellAt(worldPoint, out HexCoordinates centre))
            {
                return false;
            }

            // The centre is emitted first. ValidateTargets measures every other target's distance against
            // targets[0], so an aiming UI that reorders these produces a legal-looking cluster that is rejected
            // for being out of radius.
            _previewBuffer.Add(centre);

            int neighborsNeeded = _selectedSpellClusterSize - 1;

            if (neighborsNeeded <= 0)
            {
                return neighborsNeeded == 0;
            }

            _gridPresenter.HexGrid.GetNeighbors(centre, _neighborBuffer);

            if (_neighborBuffer.Count < neighborsNeeded)
            {
                return false;
            }

            SortNeighborsByPointerDistance(worldPoint, neighborsNeeded);

            for (int i = 0; i < neighborsNeeded; i++)
            {
                _previewBuffer.Add(_neighborBuffer[i].Coordinates);
            }

            return true;
        }

        // Partial selection sort: only the first few slots are ordered, and the list is at most six entries, so
        // this stays well inside what a per-frame harness pass can afford and allocates nothing.
        private void SortNeighborsByPointerDistance(Vector3 worldPoint, int sortedCount)
        {
            for (int slot = 0; slot < sortedCount; slot++)
            {
                int nearest = slot;
                float nearestDistance = (ProjectToBoard(_neighborBuffer[slot].Coordinates) - worldPoint).sqrMagnitude;

                for (int i = slot + 1; i < _neighborBuffer.Count; i++)
                {
                    float distance = (ProjectToBoard(_neighborBuffer[i].Coordinates) - worldPoint).sqrMagnitude;

                    if (distance < nearestDistance)
                    {
                        nearest = i;
                        nearestDistance = distance;
                    }
                }

                (_neighborBuffer[slot], _neighborBuffer[nearest]) = (_neighborBuffer[nearest], _neighborBuffer[slot]);
            }
        }

        private bool IsPreviewUnchanged()
        {
            if (_spellTargets.Count != _previewBuffer.Count)
            {
                return false;
            }

            for (int i = 0; i < _previewBuffer.Count; i++)
            {
                if (_spellTargets[i] != _previewBuffer[i])
                {
                    return false;
                }
            }

            return true;
        }

        // Latched on the centre hex, because an invalid preview is a hover state: without this it would print
        // every frame the pointer rests on a board edge.
        private void LogInvalidPreviewOnce()
        {
            if (_previewBuffer.Count == 0)
            {
                return;
            }

            HexCoordinates centre = _previewBuffer[0];

            if (_hasLoggedInvalidCentre && _lastInvalidCentre == centre)
            {
                return;
            }

            _hasLoggedInvalidCentre = true;
            _lastInvalidCentre = centre;

            Debug.Log($"PlaytestBootstrap: {centre} has too few neighbours for a full cluster — move off the board edge to cast.", this);
        }

        private void TryCastPreviewedSpell(ICardData card)
        {
            if (_selectedSpellClusterSize <= 0)
            {
                // CardDataSO warns about this while authoring, but the harness must not lock up if a card ships
                // that way — refuse the cast and leave the player able to pick a different card.
                Debug.LogError(
                    $"PlaytestBootstrap: '{card.DisplayName}' is a Spell with no usable Cluster Size, so it has no targets to pick. Fix the card asset.",
                    this
                );

                return;
            }

            if (!_isPreviewValid)
            {
                Debug.Log($"PlaytestBootstrap: cannot cast {card.DisplayName} here — the cluster does not fit on the board.", this);
                return;
            }

            CastSpell(card);
        }

        private Vector3 ProjectToBoard(HexCoordinates coordinates)
        {
            return HexMathUtils.ProjectToWorldSpace(coordinates, _cellVisualSize);
        }

        // Step 1 of the GDD chain is "validation and payment", and payment lands last here for the same reason
        // CompleteCardPlay defers it on a move: a rejected deployment must cost nothing. The aim survives every
        // rejection, so a mistimed tap never throws away the hexes the player already picked.
        private void CastSpell(ICardData card)
        {
            if (_abilityController == null)
            {
                Debug.LogError("PlaytestBootstrap: no AbilityController assigned — Protocols cannot be cast.", this);
                return;
            }

            if (_energyPresenter != null && _energyPresenter.GetEnergy(_activePlayerId) < card.EnergyCost)
            {
                Debug.Log($"PlaytestBootstrap: P{_activePlayerId} cannot afford {card.DisplayName} ({card.EnergyCost} energy). Targets kept.", this);
                return;
            }

            var command = new SpellCommand(_activePlayerId, card.CardId, _spellTargets);
            SpellResult result = _abilityController.ResolveSpell(command, ResolveMoveCapability(card));

            Debug.Log($"PlaytestBootstrap: P{_activePlayerId} cast {card.DisplayName} on {_spellTargets.Count} hexes → {result}.", this);

            if (result != SpellResult.Success)
            {
                return;
            }

            if (_energyPresenter != null)
            {
                _energyPresenter.TrySpendEnergy(_activePlayerId, card.EnergyCost);
            }

            SelectCard(null);
            ClearSpellAim();

            LogActiveStatuses();
            PublishTroopCounts();
            PublishAffordability();
        }

        private void ClearSpellAim()
        {
            ClearHighlightedTargets();

            _isPreviewValid = false;
            _hasLoggedInvalidCentre = false;

            // Without this the next aim on the hex the last one ended on would be gated out as unchanged and
            // the preview would never come back.
            _hasPreviewPointerPosition = false;

            // Covers every cancellation path — card change, active-player switch, Reset, match over — so a
            // half-finished drag never survives into whatever the player does next.
            _isDraggingSpellAim = false;
        }

        private void ClearHighlightedTargets()
        {
            // Every previewed hex is un-highlighted, not just the last one: a cluster leaves several lit, and a
            // partial clear would strand highlights on cells nothing is aiming at any more.
            for (int i = 0; i < _spellTargets.Count; i++)
            {
                SetCellHighlight(_spellTargets[i], false);
            }

            _spellTargets.Clear();
        }

        private void TrySelectUnitAt(HexCoordinates coordinates)
        {
            if (!_gridPresenter.HexGrid.TryGetCell(coordinates, out HexCell cell) || !cell.IsOccupied)
            {
                return;
            }

            if (!_unitPresenter.ActiveUnits.TryGetValue(cell.OccupantUnitId, out GridUnit unit) || unit == null)
            {
                return;
            }

            // You command one side at a time, so only the active player's pieces can be picked up. Without this
            // the hand would pay from one player's energy to move the other's unit.
            if (unit.PlayerId != _activePlayerId)
            {
                Debug.Log($"PlaytestBootstrap: unit {unit.UnitId} belongs to P{unit.PlayerId} — switch the active player to command it.", this);
                return;
            }

            _selectedUnitId = unit.UnitId;
            _selectedCoordinates = coordinates;
            _hasSelection = true;

            SetCellHighlight(coordinates, true);

            Debug.Log($"PlaytestBootstrap: selected unit {_selectedUnitId} (P{unit.PlayerId}) at {coordinates}.", this);
        }

        private void TryMoveSelectedTo(HexCoordinates target)
        {
            if (!_unitPresenter.ActiveUnits.TryGetValue(_selectedUnitId, out GridUnit unit))
            {
                ClearSelection();
                return;
            }

            int distance = _selectedCoordinates.CalculateDistance(target);

            if (distance is not CloneRange and not JumpRange)
            {
                Debug.Log($"PlaytestBootstrap: {target} is {distance} hexes away — only Clone (1) and Jump (2) exist.", this);
                return;
            }

            MoveType moveType = distance == CloneRange ? MoveType.Clone : MoveType.Jump;

            if (!TryBeginCardPlay(moveType, out ICardData playedCard))
            {
                return;
            }

            var command = new MoveCommand(moveType, _selectedCoordinates, target, unit.PlayerId, _selectedUnitId);
            MovementResult result = _unitPresenter.ResolveMove(command);

            Debug.Log($"PlaytestBootstrap: {moveType} from {_selectedCoordinates} to {target} → {result}.", this);

            CompleteCardPlay(playedCard, result);

            if (result == MovementResult.Success)
            {
                // A troop deployment closes an action window too, so this is where a Frozen from an earlier
                // Cryo-Stasis visibly ticks away.
                LogActiveStatuses();
            }

            PublishTroopCounts();
            PublishAffordability();
            ClearSelection();
        }

        /// <summary>
        /// Prepares a card-driven Clone: checks the card is playable and tells the spawner what to build.
        /// Energy is only spent once the move resolves, so an illegal move never costs anything.
        /// </summary>
        /// <returns>False when the play is rejected and the move must not be attempted.</returns>
        private bool TryBeginCardPlay(MoveType moveType, out ICardData playedCard)
        {
            playedCard = null;

            if (string.IsNullOrEmpty(_selectedCardId))
            {
                return true;
            }

            // A Jump relocates an existing unit and creates nothing, so there is no identity for a card to
            // decide. Rather than silently charging for it, the card is ignored and the plain move goes through.
            if (moveType != MoveType.Clone)
            {
                Debug.Log("PlaytestBootstrap: cards apply to Clone only — the Jump resolves without spending the card.", this);
                return true;
            }

            if (!_cardPresenter.TryGetCard(new CardId(_selectedCardId), out playedCard))
            {
                Debug.LogError($"PlaytestBootstrap: card '{_selectedCardId}' is not on the CardPresenter roster.", this);
                return false;
            }

            if (_energyPresenter != null && _energyPresenter.GetEnergy(_activePlayerId) < playedCard.EnergyCost)
            {
                Debug.Log($"PlaytestBootstrap: P{_activePlayerId} cannot afford {playedCard.DisplayName} ({playedCard.EnergyCost} energy).", this);
                playedCard = null;
                return false;
            }

            _spawner.PendingCardId = playedCard.CardId;

            return true;
        }

        // Charges for the card and gives the spawned unit the card's own capability. UnitPresenter copies the
        // source unit's capability onto a clone, which would let a cloned Volatile Mass keep cloning.
        private void CompleteCardPlay(ICardData playedCard, MovementResult result)
        {
            _spawner.PendingCardId = default;

            if (playedCard == null)
            {
                return;
            }

            if (result != MovementResult.Success)
            {
                return;
            }

            if (_energyPresenter != null)
            {
                _energyPresenter.TrySpendEnergy(_activePlayerId, playedCard.EnergyCost);
            }

            if (_unitPresenter.ActiveUnits.TryGetValue(_spawner.LastSpawnedUnitId, out GridUnit spawnedUnit))
            {
                _unitPresenter.RegisterUnit(spawnedUnit, ResolveMoveCapability(playedCard));
            }

            // The card has been spent, so the pick is over. A rejected move keeps its card selected instead, so
            // a mistimed tap does not cost the player the choice they already made.
            SelectCard(null);
        }

        private void HandleCardSelected(string cardId)
        {
            SelectCard(cardId);
        }

        private void SelectCard(string cardId)
        {
            // Changing the pick abandons whatever cluster was half-built for the previous one, since the target
            // count and radius belong to that card's impact rather than to the board.
            if (_selectedCardId != cardId)
            {
                ClearSpellAim();
            }

            _selectedCardId = cardId;
            CacheSelectedSpell(cardId);

            if (_hudView != null)
            {
                _hudView.SetSelectedCard(cardId);
            }
        }

        // The roster probe happens once per pick rather than once per frame: Update would otherwise build a
        // CardId, hash its string, and probe the card dictionary on every frame of the match — including the
        // common one where no card is picked and nothing was pressed, where nobody reads the answer.
        private void CacheSelectedSpell(string cardId)
        {
            _selectedSpellCard = null;
            _isSpellSelected = false;
            _selectedSpellClusterSize = 0;

            if (string.IsNullOrEmpty(cardId) || !_cardPresenter.TryGetCard(new CardId(cardId), out ICardData card) || card.Type != CardType.Spell)
            {
                return;
            }

            _selectedSpellCard = card;
            _isSpellSelected = true;

            // On a Protocol the first impact's Cluster Size is the number of hexes the player picks, so it is
            // the preview's shape rather than a cap on how many units are hit.
            IReadOnlyList<ImpactEffect> landingEffects = card.LandingEffects;
            _selectedSpellClusterSize = landingEffects == null || landingEffects.Count == 0 ? 0 : landingEffects[0].ClusterSize;
        }

        private void HandleActivePlayerToggleRequested()
        {
            _activePlayerId = _activePlayerId == PlayerOneId ? PlayerTwoId : PlayerOneId;

            // Otherwise a unit picked as the previous player would stay selected and move on the new one's turn,
            // and a half-aimed cluster would resolve against the wrong player's energy and target filters.
            ClearSelection();
            ClearSpellAim();

            if (_hudView != null)
            {
                _hudView.SetActivePlayer(_activePlayerId);
            }

            // The new player has their own energy, so the cached count describes the wrong player until it is cleared.
            _lastAffordableCount = -1;
            PublishAffordability();
        }

        // PERF: EnergyChanged fires every frame, but affordability flips only when energy crosses a card cost.
        // Affordability is monotone in energy, so the count of payable cards identifies the payable set exactly.
        private void HandleEnergyChanged(int playerId, float newEnergy)
        {
            if (playerId != _activePlayerId)
            {
                return;
            }

            int affordableCount = 0;

            foreach (int cost in _cardCosts.Values)
            {
                if (newEnergy >= cost)
                {
                    affordableCount++;
                }
            }

            if (affordableCount == _lastAffordableCount)
            {
                return;
            }

            _lastAffordableCount = affordableCount;
            PublishAffordability();
        }

        private void PublishAffordability()
        {
            if (_hudView == null || _energyPresenter == null)
            {
                return;
            }

            _hudView.SetAffordability(_energyPresenter.GetEnergy(_activePlayerId), _cardCosts);
        }

        private void ClearSelection()
        {
            if (_hasSelection)
            {
                SetCellHighlight(_selectedCoordinates, false);
            }

            _hasSelection = false;
            _selectedUnitId = -1;
        }

        private void SetCellHighlight(HexCoordinates coordinates, bool isActive)
        {
            if (_gridView == null || !_gridView.CellViews.TryGetValue(coordinates, out CellView cellView) || cellView == null)
            {
                return;
            }

            cellView.SetHighlightState(isActive);
        }

        private bool IsPointerOverHud(Pointer pointer)
        {
            return _hudView != null && _hudView.IsPointerOverUI(pointer.position.ReadValue());
        }

        private bool TryGetBoardPoint(Vector2 screenPosition, out Vector3 worldPoint)
        {
            worldPoint = default;

            if (_camera == null)
            {
                return false;
            }

            worldPoint = _camera.ScreenToWorldPoint(screenPosition);
            worldPoint.z = 0f;

            return true;
        }

        // The click path reads the pointer itself; the aim path converts once and threads the world point
        // through to TryPickCellAt, so a preview frame does not pay for two identical ScreenToWorldPoints.
        private bool TryPickCell(out HexCoordinates coordinates)
        {
            coordinates = default;

            Pointer pointer = Pointer.current;

            if (pointer == null || !TryGetBoardPoint(pointer.position.ReadValue(), out Vector3 worldPoint))
            {
                return false;
            }

            return TryPickCellAt(worldPoint, out coordinates);
        }

        private bool TryPickCellAt(Vector3 worldPoint, out HexCoordinates coordinates)
        {
            coordinates = default;

            int hitCount = Physics2D.OverlapPoint(worldPoint, _contactFilter, _pickResults);

            if (hitCount == 0)
            {
                return false;
            }

            CellView cellView = null;

            for (int i = 0; i < hitCount; i++)
            {
                if (_pickResults[i] != null && _pickResults[i].TryGetComponent(out cellView))
                {
                    break;
                }

                cellView = null;
            }

            if (cellView == null)
            {
                return false;
            }

            coordinates = cellView.CellCoordinates;

            return true;
        }

        /// <summary>One hand-placed unit: which card it is, who owns it, and where it starts.</summary>
        [Serializable]
        private struct StartingUnit
        {
            [Tooltip("Must match the Card Id authored on a CardDataSO registered with the CardPresenter.")]
            public string CardId;

            [Tooltip("Unique across starting units. Keep these below 1000 — clones are numbered from there up.")]
            public int UnitId;

            [Tooltip("1 renders Electric Cyan, 2 renders Hot Magenta.")]
            public int PlayerId;

            public int Q;

            public int R;
        }
    }
}
