using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
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
using GooGalaxy.Runtime.Input.Views;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Input
{
    // Flow-named per Rule 2's PlayMode exception in unity-testing.md: the outcome spans the real PointerInputView,
    // MatchInputController and the board, and no one type owns it. MatchInputControllerTests drives the controller
    // through FakePointerSource, which is why it stayed green while aiming a Protocol in the Device Simulator showed
    // nothing — this fixture puts the real Input System devices back underneath the same flow.
    [TestFixture]
    public class SpellAimDeviceFlowTests : InputTestFixture
    {
#if UNITY_EDITOR
        private const string MatchInputAssetPath = "Assets/Settings/Input/MatchInput.inputactions";
#else
        private const string PointerPositionBindingPath = "<Pointer>/position";
        private const string PointerPressBindingPath = "<Pointer>/press";
#endif
        private const int BoardRadius = BoardMetrics.DefaultGridRadius;
        private const int HandSize = DeckState.DefaultHandSize;
        private const int LocalPlayerId = 1;
        private const int OpponentPlayerId = 2;
        private const int SpellEnergyCost = 3;
        private const int SpellRadius = 1;
        private const int SpellClusterSize = 3;
        private const int SpellFreezeDuration = 1;
        private const int CardTapTouchId = 1;
        private const int BoardTouchId = 2;
        private const string SpellCardIdValue = "device_spell_card";

        private static readonly HexCoordinates _anchorHex = new(0, 0);
        private static readonly HexCoordinates _liftHex = new(2, -2);

        // Stands in for the hand strip: a press the board cannot resolve selects nothing of its own, so the hand
        // report raised between its press and release is the only thing that selects the card.
        private static readonly Vector2 _cardScreenPosition = new(1_000_000f, 1_000_000f);

        private readonly List<Object> _spawned = new();

        private Camera _camera;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private GridView _gridView;
        private TargetHighlightPresenter _highlightPresenter;
        private CardPresenter _cardPresenter;
        private CardDataSO _spellCard;
        private DeckPresenter _deckPresenter;
        private DeployController _deployController;
        private CardDiscardController _discardController;
        private MatchController _matchController;
        private FakeEnergyLedger _energyLedger;
        private FakeHandGestureSource _handGestureSource;
        private InputActionAsset _inputActions;
        private GameObject _inputGO;
        private MatchInputController _controller;
        private Mouse _mouse;
        private Touchscreen _touchscreen;
        private CardPlayAttempt? _lastAttempt;

        public override void Setup()
        {
            base.Setup();

            // The PlayMode runner reuses one fixture instance across every test in the class, so every field a test
            // can write is reset here rather than relying on a fresh instance per test.
            _lastAttempt = null;

            _mouse = InputSystem.AddDevice<Mouse>();
            _touchscreen = InputSystem.AddDevice<Touchscreen>();

            BuildCamera();
            BuildBoard();
            BuildCardsAndDeck();
            BuildHighlightPresenter();
            BuildMatchControllerAndCardControllers();
            BuildPointerViewAndController();

            MatchEvents.CardPlayAttempted += HandleCardPlayAttempted;
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
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // Dealt after MatchStarted, as MatchInputControllerTests.BuildSpellHand deals its own, so every slot
            // resolves to the Protocol whatever the announcement does to the deck.
            _deckPresenter.InitializePlayer(LocalPlayerId);
        }

        public override void TearDown()
        {
            MatchEvents.CardPlayAttempted -= HandleCardPlayAttempted;

            // Deactivated before the Input System is restored, so PointerInputView disables its map against the
            // devices it enabled it on; Destroy alone would defer that OnDisable past base.TearDown.
            if (_inputGO != null)
            {
                _inputGO.SetActive(false);
            }

            MatchEvents.ResetEvents();

            foreach (Object created in _spawned)
            {
                if (created != null)
                {
                    Object.Destroy(created);
                }
            }

            _spawned.Clear();

            if (_mouse != null)
            {
                InputSystem.RemoveDevice(_mouse);
            }

            if (_touchscreen != null)
            {
                InputSystem.RemoveDevice(_touchscreen);
            }

            base.TearDown();
        }

        [UnityTest]
        public IEnumerator TouchscreenPress_OnTheBoardWhileASpellIsAimed_PreviewsTheClusterBeforeTheFingerLifts()
        {
            // GIVEN — a touchscreen reports no position until a finger is down, so this press is the only moment the
            // area can first be shown; casting on it is what left the Device Simulator with no preview at all. Every
            // test here first yields one frame, because MatchInputController builds its pointer resolver in Start,
            // and a touch before that frame resolves no hex.
            yield return null;
            TapSpellCardWithTouch();
            Assert.That(_controller.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the card tap to aim the Protocol.");

            // WHEN
            BeginTouch(BoardTouchId, ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(
                (_controller.State, _controller.SpellPreview.Count, _highlightPresenter.IsHighlighted(_anchorHex), _energyLedger.PayCallCount),
                Is.EqualTo((InteractionState.SpellTargeting, SpellClusterSize, true, 0))
            );
        }

        [UnityTest]
        public IEnumerator TouchscreenRelease_AfterDraggingFromABoardPress_CastsWhereTheFingerLifts()
        {
            // GIVEN
            yield return null;
            TapSpellCardWithTouch();
            BeginTouch(BoardTouchId, ScreenPositionForHex(_anchorHex));
            Vector2 liftScreen = ScreenPositionForHex(_liftHex);
            MoveTouch(BoardTouchId, liftScreen);

            // WHEN
            EndTouch(BoardTouchId, liftScreen);

            // THEN
            Assert.That(_energyLedger.PayCallCount, Is.EqualTo(1));
            Assert.That(_lastAttempt.Value.Target, Is.EqualTo(_liftHex));
        }

        [UnityTest]
        public IEnumerator MouseMove_WithNoButtonHeldWhileASpellIsAimed_PreviewsTheClusterUnderTheCursor()
        {
            // GIVEN
            yield return null;
            ClickSpellCardWithMouse();
            Assert.That(_controller.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the card click to aim the Protocol.");

            // WHEN
            MoveMouse(ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(
                (_controller.SpellPreview.Count, _highlightPresenter.IsHighlighted(_anchorHex), _energyLedger.PayCallCount),
                Is.EqualTo((SpellClusterSize, true, 0))
            );
        }

        [UnityTest]
        public IEnumerator TouchscreenTap_PressAndReleaseInOneInputUpdateOnTheBoard_CastsAtTheTap()
        {
            // GIVEN
            yield return null;
            TapSpellCardWithTouch();

            // WHEN
            TapInOneInputUpdate(BoardTouchId, ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(_energyLedger.PayCallCount, Is.EqualTo(1));
            Assert.That(_lastAttempt.Value.Target, Is.EqualTo(_anchorHex));
        }

        [UnityTest]
        public IEnumerator TouchscreenBoardDrag_AfterACardReportThatArrivedAfterTheTap_NeverArmsTheDiscardZone()
        {
            // GIVEN — a tap whose press and release land in one input update is released before UI Toolkit dispatches
            // the card's report, so the report must not leave a card press behind for the board press that follows.
            yield return null;
            TapInOneInputUpdate(CardTapTouchId, _cardScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_controller.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the late report to aim the Protocol.");
            BeginTouch(BoardTouchId, ScreenPositionForHex(_anchorHex));

            // WHEN
            MoveTouch(BoardTouchId, ScreenPositionForHex(_liftHex));

            // THEN
            Assert.That(_handGestureSource.IsDiscardZoneArmed, Is.False);
        }

        [UnityTest]
        public IEnumerator MouseRelease_AfterPressingOnTheBoardWhileASpellIsAimed_CastsOnButtonUp()
        {
            // GIVEN
            yield return null;
            ClickSpellCardWithMouse();
            MoveMouse(ScreenPositionForHex(_anchorHex));
            PressMouseButton();
            Assert.That(_energyLedger.PayCallCount, Is.EqualTo(0), "Test setup expects the button going down to cast nothing.");

            // WHEN
            ReleaseMouseButton();

            // THEN
            Assert.That(_energyLedger.PayCallCount, Is.EqualTo(1));
            Assert.That(_lastAttempt.Value.Target, Is.EqualTo(_anchorHex));
        }

        [UnityTest]
        public IEnumerator DeviceReset_DuringABoardTouchHold_CancelsTheAimWithoutCasting()
        {
            // GIVEN — a reset is how the Input System drops input when the application loses focus, so this stands in
            // for a phone call or the notification shade arriving mid-hold.
            yield return null;
            TapSpellCardWithTouch();
            BeginTouch(BoardTouchId, ScreenPositionForHex(_anchorHex));

            // WHEN
            InputSystem.ResetDevice(_touchscreen);

            // THEN
            Assert.That((_controller.State, _energyLedger.PayCallCount), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [UnityTest]
        public IEnumerator TouchCanceledByTheSystem_DuringABoardHold_CancelsTheAimWithoutCasting()
        {
            // GIVEN
            yield return null;
            TapSpellCardWithTouch();
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            BeginTouch(BoardTouchId, anchorScreen);

            // WHEN
            CancelTouch(BoardTouchId, anchorScreen);

            // THEN
            Assert.That((_controller.State, _energyLedger.PayCallCount), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [UnityTest]
        public IEnumerator DeviceDisabled_DuringABoardMouseHold_CancelsTheAimWithoutCasting()
        {
            // GIVEN — disabling a device resets it first, which is the path a mouse takes when the application goes to
            // the background.
            yield return null;
            ClickSpellCardWithMouse();
            MoveMouse(ScreenPositionForHex(_anchorHex));
            PressMouseButton();

            // WHEN
            InputSystem.DisableDevice(_mouse);

            // THEN
            Assert.That((_controller.State, _energyLedger.PayCallCount), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [UnityTest]
        public IEnumerator DeviceReset_DuringADragOutOfTheHand_CancelsWithoutCastingOrDiscarding()
        {
            // GIVEN
            yield return null;
            BeginTouch(CardTapTouchId, _cardScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            MoveTouch(CardTapTouchId, ScreenPositionForHex(_anchorHex));
            Assert.That(_handGestureSource.IsDiscardZoneArmed, Is.True, "Test setup expects the drag out of the hand to arm the discard zone.");

            // WHEN
            InputSystem.ResetDevice(_touchscreen);

            // THEN
            Assert.That((_controller.State, _energyLedger.PayCallCount, _handGestureSource.IsDiscardZoneArmed), Is.EqualTo((InteractionState.Idle, 0, false)));
        }

        private static CardDataSO CreateSpellCard()
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            ImpactEffectDefinition[] landingEffects = new[]
            {
                new ImpactEffectDefinition(
                    ImpactEffectType.ApplyStatus,
                    StatusType.Frozen,
                    SpellRadius,
                    SpellFreezeDuration,
                    TargetFilter.All,
                    SpellClusterSize
                ),
            };
            card.SetAuthoredData(
                SpellCardIdValue,
                SpellCardIdValue,
                "Test description.",
                CardType.Spell,
                SpellEnergyCost,
                false,
                false,
                false,
                false,
                0,
                landingEffects
            );

            return card;
        }

        // The authored asset in the editor, so the bindings under test are the ones that ship. A player build of this
        // assembly has no AssetDatabase to load it through, so it builds the same map in code from the same names.
        private static InputActionAsset CreateMatchInputActions()
        {
#if UNITY_EDITOR
            InputActionAsset sourceActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(MatchInputAssetPath);
            Assert.That(sourceActions, Is.Not.Null, $"Test setup expects '{MatchInputAssetPath}' to exist and import as an InputActionAsset.");

            return Object.Instantiate(sourceActions);
#else
            InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = actions.AddActionMap(GooGalaxy.Runtime.Input.Constants.InputActionNames.MatchMap);
            map.AddAction(GooGalaxy.Runtime.Input.Constants.InputActionNames.PointerPosition, InputActionType.PassThrough, PointerPositionBindingPath);
            map.AddAction(GooGalaxy.Runtime.Input.Constants.InputActionNames.PointerPress, InputActionType.Button, PointerPressBindingPath);

            return actions;
#endif
        }

        private void BuildCamera()
        {
            // Zoomed in the way MatchInputControllerTests.BuildCamera is, for the same reason: one hex of world space
            // projects to hundreds of pixels, so a drag clears the gesture threshold at any runner resolution.
            var cameraGO = new GameObject("SpellAimDevice_Camera_Test");
            _camera = cameraGO.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 0.5f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraGO.tag = "MainCamera";
            _spawned.Add(cameraGO);
        }

        private void BuildBoard()
        {
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(gridLayout);

            var boardGO = new GameObject("SpellAimDevice_Board_Test");
            boardGO.SetActive(false);
            _gridPresenter = boardGO.AddComponent<GridPresenter>();
            _energyLedger = new FakeEnergyLedger();
            _unitPresenter = boardGO.AddComponent<UnitPresenter>();
            _unitPresenter.Construct(_gridPresenter, _energyLedger);
            FuseController fuseController = boardGO.AddComponent<FuseController>();
            fuseController.Construct(_unitPresenter);
            AbilityController abilityController = boardGO.AddComponent<AbilityController>();
            abilityController.Construct(_gridPresenter, _unitPresenter, fuseController);
            _gridPresenter.SetGridLayout(gridLayout);
            boardGO.SetActive(true);
            _spawned.Add(boardGO);
        }

        private void BuildCardsAndDeck()
        {
            var cardPresenterGO = new GameObject("SpellAimDevice_CardPresenter_Test");
            cardPresenterGO.SetActive(false);
            _cardPresenter = cardPresenterGO.AddComponent<CardPresenter>();
            _spellCard = CreateSpellCard();
            _cardPresenter.SetAuthoredCards(_spellCard);
            cardPresenterGO.SetActive(true);
            _spawned.Add(cardPresenterGO);
            _spawned.Add(_spellCard);

            // A kit of nothing but the Protocol, so every slot — the shuffle included — resolves to it.
            var kitCards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < kitCards.Length; i++)
            {
                kitCards[i] = _spellCard;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(kitCards);
            _spawned.Add(kit);

            var deckGO = new GameObject("SpellAimDevice_DeckPresenter_Test");
            deckGO.SetActive(false);
            _deckPresenter = deckGO.AddComponent<DeckPresenter>();
            _deckPresenter.SetKit(kit, HandSize);
            deckGO.SetActive(true);
            _spawned.Add(deckGO);
        }

        private void BuildHighlightPresenter()
        {
            var prefabGO = new GameObject("SpellAimDevice_CellPrefab_Test");
            prefabGO.AddComponent<SpriteRenderer>();
            CellView cellPrefab = prefabGO.AddComponent<CellView>();
            _spawned.Add(prefabGO);

            var gridViewGO = new GameObject("SpellAimDevice_GridView_Test");
            gridViewGO.SetActive(false);
            _gridView = gridViewGO.AddComponent<GridView>();
            _gridView.SetViewConfiguration(cellPrefab, 1f);
            _highlightPresenter = gridViewGO.AddComponent<TargetHighlightPresenter>();
            _highlightPresenter.Construct(_gridView);
            gridViewGO.SetActive(true);
            _spawned.Add(gridViewGO);
        }

        private void BuildMatchControllerAndCardControllers()
        {
            // Never activated, as in MatchInputControllerTests: SetPhaseForTests mutates MatchState directly, and
            // staying inactive keeps Start from attempting a real match with no authored configuration.
            var matchControllerGO = new GameObject("SpellAimDevice_MatchController_Test");
            matchControllerGO.SetActive(false);
            _matchController = matchControllerGO.AddComponent<MatchController>();
            _matchController.SetPhaseForTests(MatchPhase.Standard);
            _spawned.Add(matchControllerGO);

            AbilityController abilityController = _gridPresenter.GetComponent<AbilityController>();

            var deployGO = new GameObject("SpellAimDevice_DeployController_Test");
            deployGO.SetActive(false);
            _deployController = deployGO.AddComponent<DeployController>();
            _deployController.Construct(_deckPresenter, _cardPresenter, _unitPresenter, abilityController, _energyLedger);
            _deployController.SetMatchController(_matchController);
            deployGO.SetActive(true);
            _spawned.Add(deployGO);

            var discardGO = new GameObject("SpellAimDevice_CardDiscardController_Test");
            discardGO.SetActive(false);
            _discardController = discardGO.AddComponent<CardDiscardController>();
            _discardController.Construct(_deckPresenter, new FakeDiscardLedger(), _deployController);
            _discardController.SetMatchController(_matchController);
            discardGO.SetActive(true);
            _spawned.Add(discardGO);
        }

        // The view and the controller share one GameObject, as MatchRoot.prefab places them, so they are enabled and
        // disabled together — the pairing PointerInputView's own remarks rely on.
        private void BuildPointerViewAndController()
        {
            _inputActions = CreateMatchInputActions();
            _spawned.Add(_inputActions);

            _handGestureSource = new FakeHandGestureSource();

            _inputGO = new GameObject("SpellAimDevice_Input_Test");
            _inputGO.SetActive(false);
            PointerInputView view = _inputGO.AddComponent<PointerInputView>();
            view.SetInputActionsForTests(_inputActions);

            _controller = _inputGO.AddComponent<MatchInputController>();
            _controller.Construct(
                _gridPresenter,
                _gridView,
                _unitPresenter,
                _cardPresenter,
                _deployController,
                _discardController,
                _highlightPresenter,
                _deckPresenter,
                _energyLedger,
                view,
                _handGestureSource
            );
            _inputGO.SetActive(true);
            _spawned.Add(_inputGO);
        }

        // The whole tap a real HUD produces on a touchscreen: the finger's own press and release around the hand
        // strip's report of slot 0.
        private void TapSpellCardWithTouch()
        {
            BeginTouch(CardTapTouchId, _cardScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            EndTouch(CardTapTouchId, _cardScreenPosition);
        }

        // The mouse equivalent: [UnityTest] makes InputTestFixture.Set only queue, so each state is applied by the
        // explicit Update that follows it.
        private void ClickSpellCardWithMouse()
        {
            Set(_mouse.position, _cardScreenPosition);
            Press(_mouse.leftButton);
            InputSystem.Update();
            _handGestureSource.RaiseHandSlotPressed(0);
            Release(_mouse.leftButton);
            InputSystem.Update();
        }

        private void MoveMouse(Vector2 screenPosition)
        {
            Set(_mouse.position, screenPosition);
            InputSystem.Update();
        }

        private void PressMouseButton()
        {
            Press(_mouse.leftButton);
            InputSystem.Update();
        }

        private void ReleaseMouseButton()
        {
            Release(_mouse.leftButton);
            InputSystem.Update();
        }

        // A Device Simulator click or a fast tap on a device: the touch begins and ends inside one input update, so the
        // press and the release both reach the view before anything else in the frame runs.
        private void TapInOneInputUpdate(int touchId, Vector2 screenPosition)
        {
            BeginTouch(touchId, screenPosition, queueEventOnly: true);
            EndTouch(touchId, screenPosition, queueEventOnly: true);
            InputSystem.Update();
        }

        private Vector2 ScreenPositionForHex(HexCoordinates coordinates)
        {
            Vector3 worldPosition = HexMathUtils.ProjectToWorldSpace(coordinates, _gridView.CellVisualSize);

            return _camera.WorldToScreenPoint(worldPosition);
        }

        private void HandleCardPlayAttempted(CardPlayAttempt attempt)
        {
            _lastAttempt = attempt;
        }

        private sealed class FakeEnergyLedger : IEnergyLedger
        {
            public int PayCallCount { get; private set; }

            public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return true;
            }

            public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                PayCallCount++;

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
    }
}
