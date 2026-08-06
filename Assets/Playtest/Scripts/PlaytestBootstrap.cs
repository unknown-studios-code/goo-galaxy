using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("Scene References")]
        [SerializeField]
        private GridPresenter _gridPresenter;

        [SerializeField]
        private UnitPresenter _unitPresenter;

        [SerializeField]
        private CardPresenter _cardPresenter;

        [SerializeField]
        private GridView _gridView;

        [SerializeField]
        private UnitView _unitView;

        [SerializeField]
        private EnergyPresenter _energyPresenter;

        [SerializeField]
        private PlaytestHudView _hudView;

        [Header("Starting Units")]
        [Tooltip("Units placed on the board at startup. Each card id must exist on the CardPresenter roster.")]
        [SerializeField]
        private StartingUnit[] _startingUnits = Array.Empty<StartingUnit>();

        [Header("Hand")]
        [Tooltip("Card ids offered in the hand, in display order. Each must exist on the CardPresenter roster.")]
        [SerializeField]
        private string[] _handCardIds = Array.Empty<string>();

        [Header("Input")]
        [Tooltip("Layer the cell colliders live on. Leave as Everything unless the board shares the scene with other clickable geometry.")]
        [SerializeField]
        private LayerMask _cellLayerMask = ~0;

        private readonly List<HexCoordinates> _seededCoordinates = new();
        private readonly List<Collider2D> _pickResults = new(MaxPickResults);
        private readonly List<int> _unitIdBuffer = new();
        private readonly List<PlaytestHudView.HandCard> _hand = new();
        private readonly Dictionary<string, int> _cardCosts = new();

        private ContactFilter2D _contactFilter;

        private Camera _camera;
        private PlaytestUnitSpawner _spawner;
        private bool _isMatchOver;
        private int _activePlayerId = PlayerOneId;
        private string _selectedCardId;
        private int _selectedUnitId = -1;
        private HexCoordinates _selectedCoordinates;
        private bool _hasSelection;
        private int _lastAffordableCount = -1;

        private void Awake()
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

        private void OnEnable()
        {
            MatchEvents.ConversionResolved += HandleConversionResolved;

            if (_hudView != null)
            {
                _hudView.ResetRequested += HandleResetRequested;
                _hudView.CardSelected += HandleCardSelected;
                _hudView.ActivePlayerToggleRequested += HandleActivePlayerToggleRequested;
            }

            MatchEvents.EnergyChanged += HandleEnergyChanged;
        }

        private void OnDisable()
        {
            MatchEvents.ConversionResolved -= HandleConversionResolved;

            if (_hudView != null)
            {
                _hudView.ResetRequested -= HandleResetRequested;
                _hudView.CardSelected -= HandleCardSelected;
                _hudView.ActivePlayerToggleRequested -= HandleActivePlayerToggleRequested;
            }

            MatchEvents.EnergyChanged -= HandleEnergyChanged;
        }

        private void Start()
        {
            if (_unitPresenter == null || _cardPresenter == null || _gridPresenter == null)
            {
                Debug.LogError("PlaytestBootstrap: GridPresenter, UnitPresenter, and CardPresenter must all be assigned.", this);
                return;
            }

            RebuildVisualGrid();
            StartMatch();
        }

        private void Update()
        {
            if (_isMatchOver)
            {
                return;
            }

            // Pointer rather than Mouse: the Device Simulator disables the mouse device and feeds a simulated
            // Touchscreen instead, so a mouse-only read never fires there — and never would on a real phone.
            Pointer pointer = Pointer.current;

            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            if (!TryPickCell(out HexCoordinates coordinates))
            {
                return;
            }

            HandleCellClicked(coordinates);
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
            ClearSelection();

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

        // Rebuilt per match so a change to the roster or the id list is picked up by Reset without a domain reload.
        private void BuildHand()
        {
            _hand.Clear();
            _cardCosts.Clear();

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
            }
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

            Debug.Log($"PlaytestBootstrap: DOMINATION — player {winnerId} converted every enemy piece.", this);

            if (_hudView != null)
            {
                _hudView.ShowResult($"DOMINATION\nPlayer {winnerId} wins");
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

                if (!_unitPresenter.RegisterUnit(unit, new PlaytestMoveCapability(card)))
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

            if (distance != CloneRange && distance != JumpRange)
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
                _unitPresenter.RegisterUnit(spawnedUnit, new PlaytestMoveCapability(playedCard));
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
            _selectedCardId = cardId;

            if (_hudView != null)
            {
                _hudView.SetSelectedCard(cardId);
            }
        }

        private void HandleActivePlayerToggleRequested()
        {
            _activePlayerId = _activePlayerId == PlayerOneId ? PlayerTwoId : PlayerOneId;

            // Otherwise a unit picked as the previous player would stay selected and move on the new one's turn.
            ClearSelection();

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

        private bool TryPickCell(out HexCoordinates coordinates)
        {
            coordinates = default;

            if (_camera == null)
            {
                return false;
            }

            Pointer pointer = Pointer.current;

            if (pointer == null)
            {
                return false;
            }

            Vector3 worldPoint = _camera.ScreenToWorldPoint(pointer.position.ReadValue());
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
