using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Input.Controllers;
using GooGalaxy.Runtime.Input.Presenters;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Input
{
    // Type-named per Rule 2's default in unity-testing.md, since MatchInputController is the type under test.
    // Its test *method* names take the flow's trigger form rather than MethodUnderTest_Scenario_ExpectedOutcome:
    // a gesture is dispatched through the controller's private handlers, which no test here calls directly — the
    // pointer and hand-gesture fakes are what drive it, and the outcome is read from the board, the ledger and
    // the presenter's own internal state together.
    [TestFixture]
    public class MatchInputControllerTests
    {
        private const int BoardRadius = BoardMetrics.DefaultGridRadius;
        private const int HandSize = DeckState.DefaultHandSize;
        private const int LocalPlayerId = 1;
        private const int OpponentPlayerId = 2;
        private const int AnchorUnitId = 10;
        private const int ImmobileUnitId = 11;
        private const int EnemyUnitId = 20;
        private const int TroopEnergyCost = 2;
        private const string TroopCardIdValue = "input_troop_card";

        // Far enough past any device's dp-to-pixel threshold that the exact screen resolution never matters.
        private const float DragOffsetInPixels = 2000f;

        private static readonly HexCoordinates _anchorHex = new(0, 0);
        private static readonly HexCoordinates _cloneTargetHex = new(1, 0); // Distance 1 from the anchor: Clone-only.
        private static readonly HexCoordinates _unhighlightedHex = new(4, 0); // On the board, out of Clone/Jump range.
        private static readonly HexCoordinates _immobileUnitHex = new(3, 0);
        private static readonly HexCoordinates _enemyUnitHex = new(-3, 0);
        private static readonly Vector2 _offGridScreenPosition = new(1_000_000f, 1_000_000f);

        private readonly List<Object> _spawned = new();

        private GameObject _cameraGO;
        private Camera _camera;
        private GameObject _boardGO;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private GameObject _gridViewGO;
        private GridView _gridView;
        private TargetHighlightPresenter _highlightPresenter;
        private CardPresenter _cardPresenter;
        private CardDataSO _troopCard;
        private DeckPresenter _deckPresenter;
        private DeployController _deployController;
        private CardDiscardController _discardController;
        private MatchController _matchController;
        private FakeEnergyLedger _energyLedger;
        private FakeDiscardLedger _discardLedger;
        private FakePointerSource _pointerSource;
        private FakeHandGestureSource _handGestureSource;
        private GameObject _presenterGO;
        private MatchInputController _presenter;
        private int _handChangedCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BuildCamera();
            BuildBoard();
            PlaceUnit(AnchorUnitId, LocalPlayerId, _anchorHex, new FakeMoveCapable(canClone: true, canJump: true));
            PlaceUnit(ImmobileUnitId, LocalPlayerId, _immobileUnitHex, new FakeMoveCapable(canClone: false, canJump: false));
            PlaceUnit(EnemyUnitId, OpponentPlayerId, _enemyUnitHex, new FakeMoveCapable(canClone: true, canJump: true));
            BuildCardsAndDeck();
            BuildHighlightPresenter();
            BuildMatchControllerAndCardControllers();
            BuildInputSourcesAndPresenter();

            _handChangedCount = 0;
            MatchEvents.HandChanged += HandleHandChanged;

            MatchEvents.RaiseMatchStarted(
                new MatchConfiguration(
                    0,
                    new PlayerSlot(LocalPlayerId, PlayerControl.LocalHuman),
                    new PlayerSlot(OpponentPlayerId, PlayerControl.Machine),
                    0f,
                    0f,
                    0f
                )
            );

            // MatchInputController resolves its board camera and builds its pointer resolver in Start, which
            // Unity defers to the first frame update following the SetActive(true) above rather than running
            // synchronously with it — a plain synchronous SetUp returns before that frame ever ticks, and every
            // test that presses a screen point would silently resolve no hex at all as a result.
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.HandChanged -= HandleHandChanged;
            MatchEvents.ResetEvents();

            foreach (Object created in _spawned)
            {
                if (created != null)
                {
                    Object.Destroy(created);
                }
            }

            _spawned.Clear();
        }

        [Test]
        public void TapThenTapOnAHighlightedTarget_OwnedUnitSelected_ClonesTheUnitOntoTheTarget()
        {
            // GIVEN — a tap that never travels leaves the selection live, per the design's tap-then-tap path.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            Vector2 targetScreen = ScreenPositionForHex(_cloneTargetHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.UnitSelected), "Test setup expects the tap to leave the unit selected.");

            // WHEN
            _pointerSource.RaisePressed(targetScreen);

            // THEN
            Assert.That((_presenter.State, _unitPresenter.ActiveUnits.Count, GetOccupant(_cloneTargetHex)), Is.EqualTo((InteractionState.Idle, 4, true)));
        }

        [Test]
        public void HandlePointerPressed_SecondTapOnTheSelectedUnitsOwnHex_CancelsWithoutReselecting()
        {
            // GIVEN — deliberate decision #1: a re-tap on the selection's own source cancels rather than being
            // read as a fresh tap on that same unit, which TrySelectUnitAt would otherwise immediately re-select.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.UnitSelected), "Test setup expects the first tap to select the anchor unit.");

            // WHEN
            _pointerSource.RaisePressed(anchorScreen);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
        }

        [Test]
        public void HandleHandSlotPressed_SecondPressOnTheSameSlot_CancelsWithoutReselecting()
        {
            // GIVEN — the hand-slot equivalent of deliberate decision #1.
            Vector2 pressScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(pressScreen);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.CardSelected), "Test setup expects the first press to select hand slot 0.");

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
        }

        [Test]
        public void PressDragRelease_HandCardDraggedOntoALegalHex_DeploysTheCard()
        {
            // GIVEN
            Vector2 pressScreen = ScreenPositionForHex(_anchorHex);
            Vector2 targetScreen = ScreenPositionForHex(_cloneTargetHex);
            _pointerSource.RaisePressed(pressScreen);
            _handGestureSource.RaiseHandSlotPressed(0);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            _pointerSource.RaiseMoved(targetScreen);
            _pointerSource.RaiseReleased(targetScreen);

            // THEN
            Assert.That((GetOccupant(_cloneTargetHex), _handChangedCount - handChangedBaseline), Is.EqualTo((true, 1)));
        }

        [Test]
        public void PressDragRelease_HandCardDraggedIntoTheDiscardZone_DiscardsTheCard()
        {
            // GIVEN
            Vector2 pressScreen = ScreenPositionForHex(_anchorHex);
            Vector2 discardZoneScreenPosition = pressScreen + new Vector2(DragOffsetInPixels, 0f);
            _handGestureSource.DiscardZoneScreenRect = new Rect(discardZoneScreenPosition - (Vector2.one * 10f), Vector2.one * 20f);
            _pointerSource.RaisePressed(pressScreen);
            _handGestureSource.RaiseHandSlotPressed(0);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            _pointerSource.RaiseMoved(discardZoneScreenPosition);
            _pointerSource.RaiseReleased(discardZoneScreenPosition);

            // THEN
            Assert.That((_presenter.State, _handChangedCount - handChangedBaseline), Is.EqualTo((InteractionState.Idle, 1)));
        }

        [Test]
        public void ReleaseOffTheGrid_PastTheDragThreshold_CancelsTheSelectionWithoutCommitting()
        {
            // GIVEN — one continuous press-hold-drag-release gesture, since a second discrete press on the
            // selected unit's own hex would deselect it instead of starting a drag.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.UnitSelected), "Test setup expects the press to select the anchor unit.");

            // WHEN
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            // THEN
            Assert.That((_presenter.State, _unitPresenter.ActiveUnits.Count, _energyLedger.PayCalls.Count), Is.EqualTo((InteractionState.Idle, 3, 0)));
        }

        [Test]
        public void ReleaseOnAnUnhighlightedHex_PastTheDragThreshold_CancelsTheSelectionWithoutCommitting()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            Vector2 unhighlightedScreen = ScreenPositionForHex(_unhighlightedHex);
            _pointerSource.RaisePressed(anchorScreen);

            // WHEN
            _pointerSource.RaiseMoved(unhighlightedScreen);
            _pointerSource.RaiseReleased(unhighlightedScreen);

            // THEN
            Assert.That((_presenter.State, _unitPresenter.ActiveUnits.Count, _energyLedger.PayCalls.Count), Is.EqualTo((InteractionState.Idle, 3, 0)));
        }

        [Test]
        public void TapAnEnemyUnit_NotOwnedByTheLocalPlayer_SelectsNothing()
        {
            // GIVEN
            Vector2 enemyScreen = ScreenPositionForHex(_enemyUnitHex);

            // WHEN
            _pointerSource.RaisePressed(enemyScreen);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
        }

        [Test]
        public void TapAUnitWithNeitherCloneNorJump_HasNoCapability_HighlightsNothing()
        {
            // GIVEN
            Vector2 immobileScreen = ScreenPositionForHex(_immobileUnitHex);

            // WHEN
            _pointerSource.RaisePressed(immobileScreen);

            // THEN
            Assert.That((_presenter.State, _presenter.TargetCount, _highlightPresenter.HighlightedCount), Is.EqualTo((InteractionState.UnitSelected, 0, 0)));
        }

        [Test]
        public void PressAnUnaffordableHandCard_InsufficientEnergy_HighlightsNothingAndCommitsNothing()
        {
            // GIVEN — every Deploy is priced above what the ledger will approve.
            _energyLedger.AffordableCostCeiling = 0;
            Vector2 pressScreen = ScreenPositionForHex(_anchorHex);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            _pointerSource.RaisePressed(pressScreen);
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That((_presenter.TargetCount, _highlightPresenter.HighlightedCount, _handChangedCount - handChangedBaseline), Is.EqualTo((0, 0, 0)));
        }

        [Test]
        public void MatchEnded_WhileDraggingASelection_CancelsAndClearsHighlights()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Dragging), "Test setup expects the drag to be live before the match ends.");

            // WHEN
            MatchEvents.RaiseMatchEnded(MatchOutcome.Drawn);

            // THEN
            Assert.That((_presenter.State, _highlightPresenter.HighlightedCount), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [Test]
        public void MatchPhaseChanged_OutOfPlayWhileDragging_CancelsAndClearsHighlights()
        {
            // GIVEN — asserted through MatchInputController, not TargetHighlightPresenter: CancelSelection is the
            // single path every cancel routes through by design, so TargetHighlightPresenter deliberately has no
            // MatchPhaseChanged subscription of its own to add here.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Dragging), "Test setup expects the drag to be live before the phase changes.");

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // THEN
            Assert.That((_presenter.State, _highlightPresenter.HighlightedCount), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [Test]
        public void HandleLandingResolved_BoardChangedUnderALiveSelection_ReEnumeratesTargets()
        {
            // GIVEN — a live selection whose Clone target is taken by a landing elsewhere on the board; nothing
            // here re-taps or re-drags, so the only thing that can drop the target is a re-enumeration triggered
            // by the board-changed event itself.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            Assert.That(
                _highlightPresenter.IsHighlighted(_cloneTargetHex),
                Is.True,
                "Test setup expects the clone target to be highlighted before the board changes."
            );
            var opponentUnit = new GridUnit(EnemyUnitId + 1, OpponentPlayerId, CardId.Empty, _cloneTargetHex);
            Assert.That(
                _unitPresenter.RegisterUnit(opponentUnit, new FakeMoveCapable(canClone: true, canJump: true)),
                Is.True,
                "Test setup expects the opponent's unit to register onto the clone target."
            );

            // WHEN
            MatchEvents.RaiseLandingResolved(default, new ConversionResult(System.Array.Empty<int>(), System.Array.Empty<int>()));

            // THEN
            Assert.That(_highlightPresenter.IsHighlighted(_cloneTargetHex), Is.False);
        }

        [Test]
        public void HandleLandingResolved_DuringOwnCommit_SuppressesReentrantEnumeration()
        {
            // GIVEN — a MoveExecuted subscriber stands in for ConversionController, which this fixture does not
            // build, so LandingResolved fires synchronously mid-commit exactly as it does in production. Without
            // the _isCommitting latch, the nested call would re-enumerate against the just-landed board and the
            // target count captured below would differ from what it was going into the commit.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            Vector2 targetScreen = ScreenPositionForHex(_cloneTargetHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            int targetCountBeforeCommit = _presenter.TargetCount;
            int? targetCountDuringLanding = null;
            MatchEvents.MoveExecuted += (command, _) =>
                MatchEvents.RaiseLandingResolved(command, new ConversionResult(System.Array.Empty<int>(), System.Array.Empty<int>()));
            MatchEvents.LandingResolved += (_, _) => targetCountDuringLanding ??= _presenter.TargetCount;

            // WHEN
            _pointerSource.RaisePressed(targetScreen);

            // THEN
            Assert.That(targetCountDuringLanding, Is.EqualTo(targetCountBeforeCommit));
        }

        [Test]
        public void HandleEnergyChanged_EnergyFallsBelowTheLastResolve_ReEnumeratesTargets()
        {
            // GIVEN — the falling edge is unconditional: it re-enumerates regardless of ResolveEnergyQuantum and
            // regardless of whether any target was already offered.
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 5f);
            _energyLedger.AffordableCostCeiling = TroopEnergyCost;
            Vector2 pressScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(pressScreen);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.TargetCount, Is.GreaterThan(0), "Test setup expects the affordable card to highlight at least one target.");
            _energyLedger.AffordableCostCeiling = 0;

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 1f);

            // THEN
            Assert.That(_presenter.TargetCount, Is.EqualTo(0));
        }

        [Test]
        public void HandleEnergyChanged_RiseBelowTheQuantumWithNoTargets_DoesNotReEnumerate()
        {
            // GIVEN — a rise this small is deliberately left un-enumerated until it accumulates past
            // ResolveEnergyQuantum; see HandleEnergyChanged's own remarks for why. The ledger is made newly
            // affordable so a missed re-enumeration and a correct one would disagree, rather than both showing zero.
            _energyLedger.AffordableCostCeiling = 0;
            Vector2 pressScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(pressScreen);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.TargetCount, Is.EqualTo(0), "Test setup expects the unaffordable card to highlight nothing.");
            _energyLedger.AffordableCostCeiling = TroopEnergyCost;

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 0.2f);

            // THEN
            Assert.That(_presenter.TargetCount, Is.EqualTo(0));
        }

        [Test]
        public void HandleEnergyChanged_RiseAtOrAboveTheQuantumWithNoTargets_ReEnumerates()
        {
            // GIVEN
            _energyLedger.AffordableCostCeiling = 0;
            Vector2 pressScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(pressScreen);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.TargetCount, Is.EqualTo(0), "Test setup expects the unaffordable card to highlight nothing.");
            _energyLedger.AffordableCostCeiling = TroopEnergyCost;

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 0.25f);

            // THEN
            Assert.That(_presenter.TargetCount, Is.GreaterThan(0));
        }

        private static CardDataSO CreateTroopCard()
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(TroopCardIdValue, TroopCardIdValue, "Test description.", CardType.Troop, TroopEnergyCost, true, true, false, false, 1, null);

            return card;
        }

        private void BuildCamera()
        {
            // Zoomed in far past any framing a player would see, on purpose: it makes even one hex of world-space
            // distance project to hundreds of screen pixels, so every drag in this fixture clears the gesture
            // threshold regardless of the Editor's real Screen.dpi or the test runner's window size. Orthographic
            // projection has no clip against what is "visible", so hexes and off-grid points far outside this
            // tiny frustum still resolve correctly through the same WorldToScreenPoint/ScreenToWorldPoint math.
            _cameraGO = new GameObject("MatchInputController_Camera_Test");
            _camera = _cameraGO.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 0.5f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _cameraGO.tag = "MainCamera";
            _spawned.Add(_cameraGO);
        }

        private void BuildBoard()
        {
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(gridLayout);

            _boardGO = new GameObject("MatchInputController_Board_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _energyLedger = new FakeEnergyLedger();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _unitPresenter.Construct(_gridPresenter, _energyLedger);
            FuseController fuseController = _boardGO.AddComponent<FuseController>();
            fuseController.Construct(_unitPresenter);
            AbilityController abilityController = _boardGO.AddComponent<AbilityController>();
            abilityController.Construct(_gridPresenter, _unitPresenter, fuseController);
            _gridPresenter.SetGridLayout(gridLayout);
            _boardGO.SetActive(true);
            _unitPresenter.SetUnitSpawner(new FakeUnitSpawner());
            _spawned.Add(_boardGO);
        }

        private void PlaceUnit(int unitId, int playerId, HexCoordinates hex, IMoveCapable capability)
        {
            var unit = new GridUnit(unitId, playerId, CardId.Empty, hex);
            Assert.That(_unitPresenter.RegisterUnit(unit, capability), Is.True, $"Test setup expects unit {unitId} to register at {hex}.");
        }

        private void BuildCardsAndDeck()
        {
            var cardPresenterGO = new GameObject("CardPresenter_Test");
            cardPresenterGO.SetActive(false);
            _cardPresenter = cardPresenterGO.AddComponent<CardPresenter>();
            _troopCard = CreateTroopCard();
            _cardPresenter.SetAuthoredCards(_troopCard);
            cardPresenterGO.SetActive(true);
            _spawned.Add(cardPresenterGO);
            _spawned.Add(_troopCard);

            var kitCards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < kitCards.Length; i++)
            {
                kitCards[i] = _troopCard;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(kitCards);
            _spawned.Add(kit);

            var deckGO = new GameObject("DeckPresenter_Test");
            deckGO.SetActive(false);
            _deckPresenter = deckGO.AddComponent<DeckPresenter>();
            _deckPresenter.SetKit(kit, HandSize);
            deckGO.SetActive(true);
            _deckPresenter.InitializePlayer(LocalPlayerId);
            _spawned.Add(deckGO);
        }

        private void BuildHighlightPresenter()
        {
            var prefabGO = new GameObject("CellPrefab_Test");
            prefabGO.AddComponent<SpriteRenderer>();
            CellView cellPrefab = prefabGO.AddComponent<CellView>();
            _spawned.Add(prefabGO);

            _gridViewGO = new GameObject("MatchInputController_GridView_Test");
            _gridViewGO.SetActive(false);
            _gridView = _gridViewGO.AddComponent<GridView>();
            _gridView.SetViewConfiguration(cellPrefab, 1f);
            _highlightPresenter = _gridViewGO.AddComponent<TargetHighlightPresenter>();
            _highlightPresenter.Construct(_gridView);
            _gridViewGO.SetActive(true);
            _spawned.Add(_gridViewGO);
        }

        private void BuildMatchControllerAndCardControllers()
        {
            // Never activated, matching DeployControllerTests.BuildMatchController: SetPhaseForTests mutates
            // MatchState directly, so nothing here needs Awake, OnEnable, or the countdown to have run — and
            // staying inactive is what keeps Start() from attempting a real TryStartMatch with no authored config.
            var matchControllerGO = new GameObject("MatchController_Test");
            matchControllerGO.SetActive(false);
            _matchController = matchControllerGO.AddComponent<MatchController>();
            _matchController.SetPhaseForTests(MatchPhase.Standard);
            _spawned.Add(matchControllerGO);

            AbilityController abilityController = _boardGO.GetComponent<AbilityController>();

            var deployGO = new GameObject("DeployController_Test");
            deployGO.SetActive(false);
            _deployController = deployGO.AddComponent<DeployController>();
            _deployController.Construct(_deckPresenter, _cardPresenter, _unitPresenter, abilityController, _energyLedger);
            _deployController.SetMatchController(_matchController);
            deployGO.SetActive(true);
            _spawned.Add(deployGO);

            _discardLedger = new FakeDiscardLedger();

            var discardGO = new GameObject("CardDiscardController_Test");
            discardGO.SetActive(false);
            _discardController = discardGO.AddComponent<CardDiscardController>();
            _discardController.Construct(_deckPresenter, _discardLedger, _deployController);
            _discardController.SetMatchController(_matchController);
            discardGO.SetActive(true);
            _spawned.Add(discardGO);
        }

        private void BuildInputSourcesAndPresenter()
        {
            _pointerSource = new FakePointerSource();
            _handGestureSource = new FakeHandGestureSource();

            _presenterGO = new GameObject("MatchInputController_Test");
            _presenterGO.SetActive(false);
            _presenter = _presenterGO.AddComponent<MatchInputController>();
            _presenter.Construct(
                _gridPresenter,
                _gridView,
                _unitPresenter,
                _cardPresenter,
                _deployController,
                _discardController,
                _highlightPresenter,
                _deckPresenter,
                _energyLedger,
                _pointerSource,
                _handGestureSource
            );
            _presenterGO.SetActive(true);
            _spawned.Add(_presenterGO);
        }

        private Vector2 ScreenPositionForHex(HexCoordinates coordinates)
        {
            Vector3 worldPosition = HexMathUtils.ProjectToWorldSpace(coordinates, _gridView.CellVisualSize);
            Vector3 screenPosition = _camera.WorldToScreenPoint(worldPosition);

            return (Vector2)screenPosition;
        }

        private bool GetOccupant(HexCoordinates coordinates)
        {
            HexGrid grid = _gridPresenter.HexGrid;

            return grid != null && grid.TryGetCell(coordinates, out HexCell cell) && cell.IsOccupied;
        }

        private void HandleHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard)
        {
            _handChangedCount++;
        }

        private sealed class FakeMoveCapable : IMoveCapable
        {
            public FakeMoveCapable(bool canClone, bool canJump)
            {
                CanClone = canClone;
                CanJump = canJump;
            }

            public bool CanClone { get; }

            public bool CanJump { get; }

            public int CloneDistance => BoardMetrics.DefaultCloneDistance;

            public int JumpDistance => BoardMetrics.DefaultJumpDistance;

            public bool CanIgnoreHazards => false;
        }

        private sealed class FakeEnergyLedger : IEnergyLedger
        {
            public int AffordableCostCeiling { get; set; } = int.MaxValue;

            public List<(int PlayerId, MoveType Type, int UnitEnergyCost)> PayCalls { get; } = new();

            public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return unitEnergyCost <= AffordableCostCeiling;
            }

            public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                if (!CanAffordMove(playerId, moveType, unitEnergyCost))
                {
                    return false;
                }

                PayCalls.Add((playerId, moveType, unitEnergyCost));

                return true;
            }

            public void RefundMove(int playerId, MoveType moveType, int unitEnergyCost) { }
        }

        private sealed class FakeDiscardLedger : IDiscardLedger
        {
            public bool CanAffordDiscard(int playerId)
            {
                return true;
            }

            public bool TryPayForDiscard(int playerId)
            {
                return true;
            }

            public void RefundDiscard(int playerId) { }
        }

        private sealed class FakeUnitSpawner : IUnitSpawner
        {
            private int _nextUnitId = 1000;

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                return new GridUnit(_nextUnitId++, playerId, cardId, at);
            }
        }
    }
}
