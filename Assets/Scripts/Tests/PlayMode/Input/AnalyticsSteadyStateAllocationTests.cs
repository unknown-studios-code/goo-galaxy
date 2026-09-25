using System;
using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Analytics.Controllers;
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
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Tests.PlayMode.Analytics;
using GooGalaxy.Tests.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Input
{
    // Flow-named per Rule 2's PlayMode exception in unity-testing.md: no single public method is under test.
    // Three distinct claims are measured here, so the fixture covers all three. The gesture cycle proves that a
    // subscribed AnalyticsController's mere presence does not make the input path allocate — that gesture
    // cancels and never reaches capture. The card-play cycle proves the opposite direction: that the capture
    // path AnalyticsController.HandleCardPlayAttempted actually runs is allocation-free once it genuinely fires.
    // The move-executed cycle proves the same for AnalyticsController.HandleMoveExecuted, raised directly on the
    // bus with a reused command and coordinate list so only the handler's own capture is measured.
    [TestFixture]
    public class AnalyticsSteadyStateAllocationTests
    {
        private const int BoardRadius = BoardMetrics.DefaultGridRadius;
        private const int HandSize = DeckState.DefaultHandSize;
        private const int LocalPlayerId = 1;
        private const int AnchorUnitId = 10;
        private const int TroopEnergyCost = 2;
        private const string TroopCardIdValue = "analytics_alloc_troop_card";

        private const int WarmUpIterations = 3;
        private const int CaptureWarmUpIterations = 2;
        private const int MeasuredIterations = 500;

        private static readonly HexCoordinates _anchorHex = new(0, 0);
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
        private DeckPresenter _deckPresenter;
        private DeployController _deployController;
        private CardDiscardController _discardController;
        private MatchController _matchController;
        private FakePointerSource _pointerSource;
        private FakeHandGestureSource _handGestureSource;
        private GameObject _presenterGO;
        private MatchInputController _presenter;
        private AnalyticsController _analyticsController;
        private FakeAnalyticsSink _analyticsSink;
        private Vector2 _anchorScreenPosition;
        private MoveCommand _moveExecutedCommand;
        private List<HexCoordinates> _moveExecutedAffectedCoordinates;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BuildCamera();
            BuildBoard();
            PlaceAnchorUnit();
            BuildCardsAndDeck();
            BuildHighlightPresenter();
            BuildMatchControllerAndCardControllers();
            BuildInputSourcesAndPresenter();
            BuildAnalyticsController();

            MatchEvents.RaiseMatchStarted(
                new MatchConfiguration(0, new PlayerSlot(LocalPlayerId, PlayerControl.LocalHuman), new PlayerSlot(2, PlayerControl.Machine), 0f, 0f, 0f)
            );
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            MatchEvents.RaiseGridInitialized(_gridPresenter.HexGrid);

            // MatchInputController resolves its board camera and builds its pointer resolver in Start, which
            // Unity defers to the first frame update following BuildInputSourcesAndPresenter's SetActive(true).
            yield return null;

            _anchorScreenPosition = ScreenPositionForHex(_anchorHex);
        }

        [TearDown]
        public void TearDown()
        {
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
        [Category("Allocation")]
        public void SteadyState_RepeatedPressDragReleaseWithAnalyticsControllerSubscribed_AllocatesNoManagedMemory()
        {
            // GIVEN — the release lands off-grid and cancels, so this cycle never reaches
            // AnalyticsController.HandleCardPlayAttempted; it proves only that a subscribed controller's
            // presence does not make the input path itself allocate. The session-open check confirms the
            // controller is genuinely subscribed rather than inert.
            _pointerSource.RaisePressed(_anchorScreenPosition);
            Assert.That(_presenter.State, Is.Not.EqualTo(InteractionState.Idle), "Test setup expects the press to start a selection.");
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _pointerSource.RaiseReleased(_offGridScreenPosition);
            Assert.That(_analyticsSink.OpenedSessions, Is.Not.Empty, "Test setup expects AnalyticsController.Construct to have opened a session.");

            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunPressDragReleaseCycle();
            }

            // WHEN / THEN
            Assert.That(RunPressDragReleaseCycle, new AllocatesNothingConstraint());
        }

        [Test]
        [Category("Allocation")]
        public void SteadyState_RepeatedRejectedCardPlaysWithAnalyticsControllerSubscribed_AllocatesNoManagedMemory()
        {
            // GIVEN — proof the measured path actually reaches AnalyticsController's capture, so the
            // allocation assertion below is not "zero allocation because nothing captured": one call beyond
            // warm-up already produced a rejected-play record, and nothing has flushed it to the sink yet.
            _deployController.TryPlayCard(LocalPlayerId, 0, Array.Empty<HexCoordinates>());
            Assert.That(_analyticsSink.WrittenRecords, Is.Empty, "Test setup expects the buffer to still be far from a flush.");

            for (int i = 0; i < CaptureWarmUpIterations; i++)
            {
                RunRejectedCardPlayCycle();
            }

            // WHEN / THEN
            Assert.That(RunRejectedCardPlayCycle, new AllocatesNothingConstraint());

            // THEN — the buffer (2048) comfortably holds every record captured above, so nothing auto-flushed
            // mid-measurement; only this explicit Flush() moves them to the sink, at least one per attempt.
            Assert.That(_analyticsSink.WrittenRecords, Is.Empty, "No flush should have happened during the measured run.");
            _analyticsController.Flush();
            Assert.That(_analyticsSink.WrittenRecords.Count, Is.GreaterThanOrEqualTo(MeasuredIterations));
        }

        [Test]
        [Category("Allocation")]
        public void SteadyState_RepeatedMoveExecutedWithAnalyticsControllerSubscribed_AllocatesNoManagedMemory()
        {
            // GIVEN — a reused Jump command and a reused affected-coordinates list, raised directly on the bus so
            // only AnalyticsController.HandleMoveExecuted's own capture is measured, independent of the board and
            // input harness the other two cycles in this fixture exercise.
            _moveExecutedCommand = new MoveCommand(MoveType.Jump, _anchorHex, new HexCoordinates(1, 0), LocalPlayerId, AnchorUnitId);
            _moveExecutedAffectedCoordinates = new List<HexCoordinates> { _anchorHex, new(1, 0) };

            for (int i = 0; i < CaptureWarmUpIterations; i++)
            {
                RunMoveExecutedCycle();
            }

            // Empties the buffer before measuring. Warm-up plus the measured cycle would otherwise sit a few dozen
            // records under capacity, and the records the live match adds on its own — a number that depends on
            // frame timing — could fill it mid-measurement, so the fake sink's growth would be what got measured.
            _analyticsController.Flush();
            int writtenBeforeMeasuring = _analyticsSink.WrittenRecords.Count;

            // WHEN / THEN
            Assert.That(RunMoveExecutedCycle, new AllocatesNothingConstraint());

            // THEN — nothing auto-flushed during the measured run, so the assertion above measured capture alone.
            Assert.That(_analyticsSink.WrittenRecords.Count, Is.EqualTo(writtenBeforeMeasuring), "No flush should have happened during the measured run.");
        }

        private static CardDataSO CreateTroopCard()
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(TroopCardIdValue, TroopCardIdValue, "Test description.", CardType.Troop, TroopEnergyCost, true, true, false, false, 1, null);

            return card;
        }

        // One full gesture: press selects the anchor unit and highlights its targets, the drag carries it off
        // the grid, and the release cancels rather than committing — so the board never changes, no card play is
        // ever attempted, and the same cycle is safe to repeat MeasuredIterations times in a row.
        private void RunPressDragReleaseCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                _pointerSource.RaisePressed(_anchorScreenPosition);
                _pointerSource.RaiseMoved(_offGridScreenPosition);
                _pointerSource.RaiseReleased(_offGridScreenPosition);
            }
        }

        // Slot 0 always holds the same troop card and an empty target list always fails DeployController's
        // own target-count check before anything mutates, so the hand never rotates and the cycle is safe to
        // repeat MeasuredIterations times in a row.
        private void RunRejectedCardPlayCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                _deployController.TryPlayCard(LocalPlayerId, 0, Array.Empty<HexCoordinates>());
            }
        }

        // Raised directly rather than through a real move, since only AnalyticsController's own handler is under
        // measurement here; the command and list are built once in the test and reused every iteration.
        private void RunMoveExecutedCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                MatchEvents.RaiseMoveExecuted(_moveExecutedCommand, _moveExecutedAffectedCoordinates);
            }
        }

        private void BuildCamera()
        {
            _cameraGO = new GameObject("AnalyticsAlloc_Camera_Test");
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

            _boardGO = new GameObject("AnalyticsAlloc_Board_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _unitPresenter.Construct(_gridPresenter, new FakeEnergyLedger());
            FuseController fuseController = _boardGO.AddComponent<FuseController>();
            fuseController.Construct(_unitPresenter);
            AbilityController abilityController = _boardGO.AddComponent<AbilityController>();
            abilityController.Construct(_gridPresenter, _unitPresenter, fuseController);
            _gridPresenter.SetGridLayout(gridLayout);
            _boardGO.SetActive(true);
            _unitPresenter.SetUnitSpawner(new FakeUnitSpawner());
            _spawned.Add(_boardGO);
        }

        private void PlaceAnchorUnit()
        {
            var unit = new GridUnit(AnchorUnitId, LocalPlayerId, CardId.Empty, _anchorHex);
            Assert.That(_unitPresenter.RegisterUnit(unit, new FakeMoveCapable()), Is.True, "Test setup expects the anchor unit to register.");
        }

        private void BuildCardsAndDeck()
        {
            var cardPresenterGO = new GameObject("AnalyticsAlloc_CardPresenter_Test");
            cardPresenterGO.SetActive(false);
            _cardPresenter = cardPresenterGO.AddComponent<CardPresenter>();
            CardDataSO troopCard = CreateTroopCard();
            _cardPresenter.SetAuthoredCards(troopCard);
            cardPresenterGO.SetActive(true);
            _spawned.Add(cardPresenterGO);
            _spawned.Add(troopCard);

            var kitCards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < kitCards.Length; i++)
            {
                kitCards[i] = troopCard;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(kitCards);
            _spawned.Add(kit);

            var deckGO = new GameObject("AnalyticsAlloc_DeckPresenter_Test");
            deckGO.SetActive(false);
            _deckPresenter = deckGO.AddComponent<DeckPresenter>();
            _deckPresenter.SetKit(kit, HandSize);
            deckGO.SetActive(true);
            _deckPresenter.InitializePlayer(LocalPlayerId);
            _spawned.Add(deckGO);
        }

        private void BuildHighlightPresenter()
        {
            var prefabGO = new GameObject("AnalyticsAlloc_CellPrefab_Test");
            prefabGO.AddComponent<SpriteRenderer>();
            CellView cellPrefab = prefabGO.AddComponent<CellView>();
            _spawned.Add(prefabGO);

            _gridViewGO = new GameObject("AnalyticsAlloc_GridView_Test");
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
            var matchControllerGO = new GameObject("AnalyticsAlloc_MatchController_Test");
            matchControllerGO.SetActive(false);
            _matchController = matchControllerGO.AddComponent<MatchController>();
            _matchController.SetPhaseForTests(MatchPhase.Standard);
            _spawned.Add(matchControllerGO);

            AbilityController abilityController = _boardGO.GetComponent<AbilityController>();
            var ledger = new FakeEnergyLedger();

            var deployGO = new GameObject("AnalyticsAlloc_DeployController_Test");
            deployGO.SetActive(false);
            _deployController = deployGO.AddComponent<DeployController>();
            _deployController.Construct(_deckPresenter, _cardPresenter, _unitPresenter, abilityController, ledger);
            _deployController.SetMatchController(_matchController);
            deployGO.SetActive(true);
            _spawned.Add(deployGO);

            var discardGO = new GameObject("AnalyticsAlloc_CardDiscardController_Test");
            discardGO.SetActive(false);
            _discardController = discardGO.AddComponent<CardDiscardController>();
            _discardController.Construct(_deckPresenter, new FakeDiscardLedger(), _deployController);
            _discardController.SetMatchController(_matchController);
            discardGO.SetActive(true);
            _spawned.Add(discardGO);
        }

        private void BuildInputSourcesAndPresenter()
        {
            _pointerSource = new FakePointerSource();
            _handGestureSource = new FakeHandGestureSource();

            _presenterGO = new GameObject("AnalyticsAlloc_MatchInputController_Test");
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
                new FakeEnergyLedger(),
                _pointerSource,
                _handGestureSource
            );
            _presenterGO.SetActive(true);
            _spawned.Add(_presenterGO);
        }

        private void BuildAnalyticsController()
        {
            _analyticsSink = new FakeAnalyticsSink();

            var analyticsGO = new GameObject("AnalyticsAlloc_AnalyticsController_Test");
            analyticsGO.SetActive(false);
            _analyticsController = analyticsGO.AddComponent<AnalyticsController>();
            _analyticsController.Construct(_analyticsSink);
            analyticsGO.SetActive(true);
            _spawned.Add(analyticsGO);
        }

        private Vector2 ScreenPositionForHex(HexCoordinates coordinates)
        {
            Vector3 worldPosition = HexMathUtils.ProjectToWorldSpace(coordinates, _gridView.CellVisualSize);
            Vector3 screenPosition = _camera.WorldToScreenPoint(worldPosition);

            return (Vector2)screenPosition;
        }

        private sealed class FakeMoveCapable : IMoveCapable
        {
            public bool CanClone => true;

            public bool CanJump => true;

            public int CloneDistance => BoardMetrics.DefaultCloneDistance;

            public int JumpDistance => BoardMetrics.DefaultJumpDistance;

            public bool CanIgnoreHazards => false;
        }

        private sealed class FakeEnergyLedger : IEnergyLedger
        {
            public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return true;
            }

            public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
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
            private int _nextUnitId = 2000;

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                return new GridUnit(_nextUnitId++, playerId, cardId, at);
            }
        }
    }
}
