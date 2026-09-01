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
    // Flow-named per Rule 2's PlayMode exception in unity-testing.md: no single public method is under test,
    // since the measured behavior is a gesture dispatched through private handlers. MatchInputControllerTests is
    // type-named — Rule 2's default, since MatchInputController is the type under test — and only its test
    // *method* names take the trigger form, for the same reason this fixture is flow-named.
    [TestFixture]
    public class MatchInputSteadyStateAllocationTests
    {
        private const int BoardRadius = BoardMetrics.DefaultGridRadius;
        private const int HandSize = DeckState.DefaultHandSize;
        private const int LocalPlayerId = 1;
        private const int AnchorUnitId = 10;
        private const int TroopEnergyCost = 2;
        private const string TroopCardIdValue = "alloc_troop_card";

        // A press-drag-release cycle that never commits, so the board never changes and the same cycle can
        // repeat any number of times without running out of empty targets to drag toward.
        private const int WarmUpIterations = 3;
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
        private Vector2 _anchorScreenPosition;

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

            MatchEvents.RaiseMatchStarted(
                new MatchConfiguration(0, new PlayerSlot(LocalPlayerId, PlayerControl.LocalHuman), new PlayerSlot(2, PlayerControl.Machine), 0f, 0f, 0f)
            );

            // MatchInputController resolves its board camera and builds its pointer resolver in Start, which
            // Unity defers to the first frame update following BuildInputSourcesAndPresenter's SetActive(true)
            // rather than running synchronously with it. Without this frame, every press below would silently
            // resolve no hex and the measured cycle would allocate nothing for the wrong reason — because
            // TrySelectUnitAt is never reached at all, not because the reached code is allocation-free.
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
        public void SteadyState_RepeatedPressDragRelease_AllocatesNoManagedMemory()
        {
            // GIVEN — warmed up ahead of the measurement so the first ResolveTargets pass (dictionary growth,
            // JIT, the board-wide enumeration buffers) never lands inside the delegate NotAllocatingGCMemory()
            // measures.
            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunPressDragReleaseCycle();
            }

            // WHEN / THEN — the act is the delegate itself, which the constraint both runs and measures.
            Assert.That(RunPressDragReleaseCycle, NotAllocatingGCMemory());
        }

        private static CardDataSO CreateTroopCard()
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(TroopCardIdValue, TroopCardIdValue, "Test description.", CardType.Troop, TroopEnergyCost, true, true, false, false, 1, null);

            return card;
        }

        // Fully qualified rather than reached through a `using UnityEngine.TestTools.Constraints;`, which would
        // shadow NUnit.Framework.Is (used unqualified throughout this fixture) — see
        // MatchHudSteadyStateAllocationTests.NotAllocatingGCMemory for the .ApplyTo() pitfall this static form
        // sidesteps.
        private static UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint NotAllocatingGCMemory()
        {
            return UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not);
        }

        // One full gesture: press selects the anchor unit and highlights its Clone and Jump targets, the drag
        // carries it off the grid, and the release cancels rather than committing — so the board never changes
        // and the same cycle is safe to repeat MeasuredIterations times in a row.
        private void RunPressDragReleaseCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                _pointerSource.RaisePressed(_anchorScreenPosition);
                _pointerSource.RaiseMoved(_offGridScreenPosition);
                _pointerSource.RaiseReleased(_offGridScreenPosition);
            }
        }

        private void BuildCamera()
        {
            // Zoomed in far past any framing a player would see, so one hex of world-space distance safely
            // clears the gesture threshold regardless of the Editor's real Screen.dpi — see
            // MatchInputControllerTests.BuildCamera for the full reasoning.
            _cameraGO = new GameObject("MatchInputController_Alloc_Camera_Test");
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

            _boardGO = new GameObject("MatchInputController_Alloc_Board_Test");
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
            var cardPresenterGO = new GameObject("CardPresenter_Alloc_Test");
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

            var deckGO = new GameObject("DeckPresenter_Alloc_Test");
            deckGO.SetActive(false);
            _deckPresenter = deckGO.AddComponent<DeckPresenter>();
            _deckPresenter.SetKit(kit, HandSize);
            deckGO.SetActive(true);
            _deckPresenter.InitializePlayer(LocalPlayerId);
            _spawned.Add(deckGO);
        }

        private void BuildHighlightPresenter()
        {
            var prefabGO = new GameObject("CellPrefab_Alloc_Test");
            prefabGO.AddComponent<SpriteRenderer>();
            CellView cellPrefab = prefabGO.AddComponent<CellView>();
            _spawned.Add(prefabGO);

            _gridViewGO = new GameObject("MatchInputController_Alloc_GridView_Test");
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
            // Never activated, matching DeployControllerTests.BuildMatchController: staying inactive is what
            // keeps Start() from attempting a real TryStartMatch with no authored config.
            var matchControllerGO = new GameObject("MatchController_Alloc_Test");
            matchControllerGO.SetActive(false);
            _matchController = matchControllerGO.AddComponent<MatchController>();
            _matchController.SetPhaseForTests(MatchPhase.Standard);
            _spawned.Add(matchControllerGO);

            AbilityController abilityController = _boardGO.GetComponent<AbilityController>();
            var ledger = new FakeEnergyLedger();

            var deployGO = new GameObject("DeployController_Alloc_Test");
            deployGO.SetActive(false);
            _deployController = deployGO.AddComponent<DeployController>();
            _deployController.Construct(_deckPresenter, _cardPresenter, _unitPresenter, abilityController, ledger);
            _deployController.SetMatchController(_matchController);
            deployGO.SetActive(true);
            _spawned.Add(deployGO);

            var discardGO = new GameObject("CardDiscardController_Alloc_Test");
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

            _presenterGO = new GameObject("MatchInputController_Alloc_Test");
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
            private int _nextUnitId = 1000;

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                return new GridUnit(_nextUnitId++, playerId, cardId, at);
            }
        }
    }
}
