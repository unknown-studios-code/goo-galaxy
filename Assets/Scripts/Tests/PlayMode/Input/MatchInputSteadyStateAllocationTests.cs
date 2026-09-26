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
using GooGalaxy.Tests.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
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
        private const int SpellEnergyCost = 3;
        private const int SpellRadius = 1;
        private const int SpellClusterSize = 3;
        private const int SpellFreezeDuration = 1;
        private const string TroopCardIdValue = "alloc_troop_card";
        private const string SpellCardIdValue = "alloc_spell_card";
        private const string UncastableSpellCardIdValue = "alloc_uncastable_spell_card";

        // Shared by every cycle below: none of them ever commits, so the board never changes and each is safe to
        // repeat any number of times without running out of empty targets to drag toward.
        private const int WarmUpIterations = 3;
        private const int MeasuredIterations = 500;

        // Frames budgeted for the HUD UIDocument's panel to attach and Yoga to resolve layout against it, matching
        // MatchInputControllerTests.BuildHudAsync.
        private const int HudLayoutSettleFrameBudget = 10;

        // A percentage of the panel, not pixels — see MatchInputControllerTests.BuildHudAsync for why.
        private const float HudStripSizePercent = 15f;

        private static readonly HexCoordinates _anchorHex = new(0, 0);

        // Distance 1 from the anchor, matching MatchInputControllerTests._cloneTargetHex: a legal Clone target
        // for the anchor unit's capability and, since it is empty and adjacent to that unit, a legal Deploy
        // target for the hand-slot cycle too.
        private static readonly HexCoordinates _targetHex = new(1, 0);

        private static readonly Vector2 _offGridScreenPosition = new(1_000_000f, 1_000_000f);

        // Near the screen's own bottom-left corner, matching MatchInputControllerTests._hudScreenPosition — always
        // inside the hand-strip region BuildHudAsync builds there, and never a board hex under this fixture's zoom.
        private static readonly Vector2 _hudScreenPosition = new(1f, 1f);

        // A single pixel: far below the gesture threshold, so a move by this much stays inside the same hex
        // instead of crossing into a neighbour — the "moving within one hex" half of the touch-drift cycles below.
        private static readonly Vector2 _withinHexDriftOffset = new(1f, 0f);

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
        private CardDataSO _spellCard;
        private CardDataSO _uncastableSpellCard;
        private DeckPresenter _deckPresenter;
        private DeployController _deployController;
        private CardDiscardController _discardController;
        private MatchController _matchController;
        private FakePointerSource _pointerSource;
        private FakeHandGestureSource _handGestureSource;
        private GameObject _presenterGO;
        private MatchInputController _presenter;
        private GameObject _hudGO;
        private PanelSettings _hudPanelSettings;
        private UIDocument _hudDocument;
        private Vector2 _anchorScreenPosition;
        private Vector2 _targetScreenPosition;

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

            // MatchController.SetPhaseForTests only mutates MatchState, so nothing here raises this event on its
            // own — and without it, _phase stays MatchPhase.None and IsPlayOpen refuses every press below, which
            // would leave the measured cycle allocation-free for the wrong reason: because it never runs.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // GridView subscribes to this in OnEnable and builds CellViews from it. Without it,
            // TargetHighlightPresenter.SetCellHighlight finds no cell for any coordinate and returns before ever
            // touching a SpriteRenderer, so the measured cycles below would never price the write this component
            // makes on every selection and every preview begin/end.
            MatchEvents.RaiseGridInitialized(_gridPresenter.HexGrid);

            // MatchInputController resolves its board camera and builds its pointer resolver in Start, which
            // Unity defers to the first frame update following BuildInputSourcesAndPresenter's SetActive(true)
            // rather than running synchronously with it. Without this frame, every press below would silently
            // resolve no hex and the measured cycle would allocate nothing for the wrong reason — because
            // TrySelectUnitAt is never reached at all, not because the reached code is allocation-free.
            yield return null;

            _anchorScreenPosition = ScreenPositionForHex(_anchorHex);
            _targetScreenPosition = ScreenPositionForHex(_targetHex);
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
            // GIVEN — proof the cycle actually reaches a selection before anything is warmed up or measured, so
            // a regression that blocks the press earlier (the phase gate, for one) fails this setup check rather
            // than passing the zero-GC assertion below for the wrong reason — an inert path that allocates
            // nothing because it never runs.
            _pointerSource.RaisePressed(_anchorScreenPosition);
            Assert.That(_presenter.State, Is.Not.EqualTo(InteractionState.Idle), "Test setup expects the press to start a selection.");
            Assert.That(_presenter.TargetCount, Is.GreaterThan(0), "Test setup expects the anchor unit to highlight at least one target.");
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunPressDragReleaseCycle();
            }

            // WHEN / THEN
            Assert.That(RunPressDragReleaseCycle, new AllocatesNothingConstraint());
        }

        [Test]
        [Category("Allocation")]
        public void SteadyState_RepeatedPressDragOntoAHighlightedTargetAndOff_AllocatesNoManagedMemory()
        {
            // GIVEN — the target must actually be highlighted, or the drag below would never reach
            // TryBeginPreview/TryEndPreview and this would measure the same inert path as a plain off-grid drag.
            _pointerSource.RaisePressed(_anchorScreenPosition);
            Assert.That(_highlightPresenter.IsHighlighted(_targetHex), Is.True, "Test setup expects the target hex to be a highlighted Clone target.");
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunPressDragOntoTargetAndOffCycle();
            }

            // WHEN / THEN
            Assert.That(RunPressDragOntoTargetAndOffCycle, new AllocatesNothingConstraint());
        }

        [Test]
        [Category("Allocation")]
        public void SteadyState_RepeatedHandSlotPressDragRelease_AllocatesNoManagedMemory()
        {
            // GIVEN — the pointer press lands off-grid first, so it selects nothing of its own and the hand-slot
            // press that follows starts a clean CardSelected selection rather than cancelling one already live.
            _pointerSource.RaisePressed(_offGridScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.CardSelected), "Test setup expects the hand slot press to select a card.");
            _pointerSource.RaiseMoved(_targetScreenPosition);
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunHandSlotDragCycle();
            }

            // WHEN / THEN
            Assert.That(RunHandSlotDragCycle, new AllocatesNothingConstraint());
        }

        [Test]
        [Category("Allocation")]
        public void SteadyState_RepeatedHoverBackAndForthWithASpellSelected_AllocatesNoManagedMemory()
        {
            // GIVEN — the aim must genuinely be live and the preview genuinely valid, or the cycle below would
            // measure an inert path instead of ClusterTargetBuilder actually re-arranging on every hover. No
            // pointer press precedes the hand-slot press: a hover is ignored outright while the pointer is down.
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");
            _pointerSource.RaiseHovered(_anchorScreenPosition);
            Assert.That(_presenter.IsSpellPreviewValid, Is.True, "Test setup expects the hover to have built a valid preview.");

            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunSpellHoverBackAndForthCycle();
            }

            // WHEN / THEN
            Assert.That(RunSpellHoverBackAndForthCycle, new AllocatesNothingConstraint());
        }

        [Test]
        [Category("Allocation")]
        public void SteadyState_RepeatedBoardPressAimWithASpellSelected_AllocatesNoManagedMemory()
        {
            // GIVEN — the press on the board must genuinely preview a valid cluster, or the cycle below would measure
            // the off-grid cancel alone instead of ClusterTargetBuilder arranging under a finger that is down.
            BuildSpellHand();
            TapSpellCardInHand();
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");
            _pointerSource.RaisePressed(_anchorScreenPosition);
            Assert.That(_presenter.IsSpellPreviewValid, Is.True, "Test setup expects the board press to have built a valid preview.");
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunBoardPressAimCycle();
            }

            // WHEN / THEN
            Assert.That(RunBoardPressAimCycle, new AllocatesNothingConstraint());
        }

        [UnityTest]
        [Category("Allocation")]
        public IEnumerator SteadyState_RepeatedBoardPressAimWithAHudWired_AllocatesNoManagedMemory()
        {
            // GIVEN — a real HUD wired through the test seam, so the panel picks on the press, the move onto the hand
            // strip and the release there are all genuinely on the measured path. The release over the HUD keeps the aim,
            // so the cycle repeats without re-selecting and never casts.
            yield return BuildHudAsync();
            BuildSpellHand();
            TapSpellCardInHand();
            _pointerSource.RaisePressed(_anchorScreenPosition);
            Assert.That(_presenter.IsSpellPreviewValid, Is.True, "Test setup expects the board press to have built a valid preview.");
            _pointerSource.RaiseReleased(_hudScreenPosition);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the release over the HUD to keep the aim.");

            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunBoardPressAimOntoTheHudCycle();
            }

            // WHEN / THEN
            Assert.That(RunBoardPressAimOntoTheHudCycle, new AllocatesNothingConstraint());
        }

        [Test]
        [Category("Allocation")]
        public void SteadyState_RepeatedBoardTapOnAnInvalidSpot_AllocatesNoManagedMemory()
        {
            // GIVEN — the uncastable Protocol makes every on-grid spot one with no valid cluster, so each lift keeps the
            // aim instead of casting and the same tap repeats against the same live selection.
            BuildUncastableSpellHand();
            TapSpellCardInHand();
            _pointerSource.RaisePressed(_anchorScreenPosition);
            _pointerSource.RaiseReleased(_anchorScreenPosition);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the invalid lift to keep the aim.");

            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunBoardTapOnAnInvalidSpotCycle();
            }

            // WHEN / THEN
            Assert.That(RunBoardTapOnAnInvalidSpotCycle, new AllocatesNothingConstraint());
        }

        [UnityTest]
        [Category("Allocation")]
        public IEnumerator SteadyState_RepeatedTouchDragAimWithAHudWired_AllocatesNoManagedMemory()
        {
            // GIVEN — a real HUD UIDocument, wired through the test seam, so IsScreenPointOverHud's panel pick
            // is genuinely on the measured path the way it is once a scene's HUD exists; a drag-aim armed from a
            // bare press never reaches it. The press lands on the hand strip so DragSpellAim genuinely arms the
            // drag, and the pointer is never released, so ClusterTargetBuilder re-arranges the aim on every move
            // — between hexes and drifting within one — without either cycle ever committing.
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseMoved(_anchorScreenPosition);
            Assert.That(_presenter.IsSpellPreviewValid, Is.True, "Test setup expects the drag onto the board to have built a valid preview.");

            for (int i = 0; i < WarmUpIterations; i++)
            {
                RunTouchDragAimCycle();
            }

            // WHEN / THEN
            Assert.That(RunTouchDragAimCycle, new AllocatesNothingConstraint());
        }

        private static CardDataSO CreateTroopCard()
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(TroopCardIdValue, TroopCardIdValue, "Test description.", CardType.Troop, TroopEnergyCost, true, true, false, false, 1, null);

            return card;
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

        // A second impact whose zero radius no cluster the first impact arranges can satisfy, matching
        // MatchInputControllerTests.CreateUncastableSpellCard: every spot on the board is an on-grid spot with no valid
        // cluster, which is the keep-the-aim release path.
        private static CardDataSO CreateUncastableSpellCard()
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
                new ImpactEffectDefinition(ImpactEffectType.ApplyStatus, StatusType.Rooted, 0, SpellFreezeDuration, TargetFilter.All, SpellClusterSize),
            };
            card.SetAuthoredData(
                UncastableSpellCardIdValue,
                UncastableSpellCardIdValue,
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

        // One full gesture that drags onto a highlighted Clone target and back off before releasing off-grid, so
        // TargetHighlightPresenter.IsHighlighted is tested true and then false and the preview begins and ends —
        // paths RunPressDragReleaseCycle's straight-off-grid drag never reaches. The release still lands
        // off-grid, so the board never changes and the cycle is safe to repeat.
        private void RunPressDragOntoTargetAndOffCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                _pointerSource.RaisePressed(_anchorScreenPosition);
                _pointerSource.RaiseMoved(_targetScreenPosition);
                _pointerSource.RaiseMoved(_offGridScreenPosition);
                _pointerSource.RaiseReleased(_offGridScreenPosition);
            }
        }

        // One full hand-slot gesture: the press lands off-grid first so it selects nothing of its own, the
        // hand-slot press selects the card cleanly, and the drag onto the target arms the discard zone before
        // the release — off-grid and at the press origin — disarms it again through the ordinary cancel path.
        private void RunHandSlotDragCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                _pointerSource.RaisePressed(_offGridScreenPosition);
                _handGestureSource.RaiseHandSlotPressed(0);
                _pointerSource.RaiseMoved(_targetScreenPosition);
                _pointerSource.RaiseReleased(_offGridScreenPosition);
            }
        }

        // Hovers back and forth between two on-board hexes with a Protocol aimed, then drifts within the anchor
        // hex by a single pixel, so ClusterTargetBuilder re-arranges and TargetHighlightPresenter re-diffs the
        // cluster on a genuine hex change and then re-measures the same arrangement on a move too small to
        // change it — the "same hex, moved anyway" path a finger's natural jitter produces. Neither ever
        // commits, so the same cycle is safe to repeat MeasuredIterations times in a row.
        private void RunSpellHoverBackAndForthCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                _pointerSource.RaiseHovered(_targetScreenPosition);
                _pointerSource.RaiseHovered(_anchorScreenPosition);
                _pointerSource.RaiseHovered(_anchorScreenPosition + _withinHexDriftOffset);
            }
        }

        // The finger-aimed equivalent of the hover cycle above: the card is tapped, a press on the board previews
        // under the finger, the drag re-arranges on a genuine hex change and on a within-hex drift, and the release
        // off the grid cancels rather than casting — so the board never changes and the cycle is safe to repeat.
        private void RunBoardPressAimCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                TapSpellCardInHand();
                _pointerSource.RaisePressed(_anchorScreenPosition);
                _pointerSource.RaiseMoved(_targetScreenPosition);
                _pointerSource.RaiseMoved(_anchorScreenPosition + _withinHexDriftOffset);
                _pointerSource.RaiseReleased(_offGridScreenPosition);
            }
        }

        // The board-press cycle above with the HUD in play: the drag ends on the hand strip and lifts there, which keeps
        // the aim live, so each iteration presses again on the same selection.
        private void RunBoardPressAimOntoTheHudCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                _pointerSource.RaisePressed(_anchorScreenPosition);
                _pointerSource.RaiseMoved(_targetScreenPosition);
                _pointerSource.RaiseMoved(_hudScreenPosition);
                _pointerSource.RaiseReleased(_hudScreenPosition);
            }
        }

        private void RunBoardTapOnAnInvalidSpotCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                _pointerSource.RaisePressed(_anchorScreenPosition);
                _pointerSource.RaiseReleased(_anchorScreenPosition);
            }
        }

        // The pointer's own press and release around the hand strip's report of slot 0, as a real HUD produces them,
        // landing off the grid so the press selects nothing of its own.
        private void TapSpellCardInHand()
        {
            _pointerSource.RaisePressed(_offGridScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseReleased(_offGridScreenPosition);
        }

        // The touch equivalent of the hover cycle above, driven through DragSpellAim instead of
        // HandlePointerHovered: the pointer stays down throughout, alternating a genuine hex change with a
        // within-hex drift, and is never released, so the aim stays live and the board never changes.
        private void RunTouchDragAimCycle()
        {
            for (int i = 0; i < MeasuredIterations; i++)
            {
                _pointerSource.RaiseMoved(_targetScreenPosition);
                _pointerSource.RaiseMoved(_anchorScreenPosition);
                _pointerSource.RaiseMoved(_anchorScreenPosition + _withinHexDriftOffset);
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
            _spellCard = CreateSpellCard();
            _uncastableSpellCard = CreateUncastableSpellCard();
            _cardPresenter.SetAuthoredCards(troopCard, _spellCard, _uncastableSpellCard);
            cardPresenterGO.SetActive(true);
            _spawned.Add(cardPresenterGO);
            _spawned.Add(troopCard);
            _spawned.Add(_spellCard);
            _spawned.Add(_uncastableSpellCard);

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

        private void BuildSpellHand()
        {
            DealHandOf(_spellCard);
        }

        private void BuildUncastableSpellHand()
        {
            DealHandOf(_uncastableSpellCard);
        }

        // Re-deals the local player's hand from a kit of nothing but one card, so every slot resolves to the same
        // Protocol deterministically — a shuffle of identical entries has nothing to scramble.
        private void DealHandOf(CardDataSO card)
        {
            var kitCards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < kitCards.Length; i++)
            {
                kitCards[i] = card;
            }

            KitDataSO spellKit = ScriptableObject.CreateInstance<KitDataSO>();
            spellKit.SetAuthoredCards(kitCards);
            _spawned.Add(spellKit);

            _deckPresenter.SetKit(spellKit, HandSize);
            _deckPresenter.InitializePlayer(LocalPlayerId);
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

        // Wires a real UIDocument onto the already-built presenter through the internal test seam
        // SetHudDocumentForTests, mirroring MatchInputControllerTests.BuildHudAsync — see that method for the
        // full reasoning behind the root's explicit sizing and the hand strip's percentage-based one.
        private IEnumerator BuildHudAsync()
        {
            _hudGO = new GameObject("MatchInputController_Alloc_Hud_Test");
            _hudPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _hudDocument = _hudGO.AddComponent<UIDocument>();
            _hudDocument.panelSettings = _hudPanelSettings;
            _spawned.Add(_hudGO);
            _spawned.Add(_hudPanelSettings);

            int frameBudget = HudLayoutSettleFrameBudget;

            while ((_hudDocument.rootVisualElement == null) && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(_hudDocument.rootVisualElement, Is.Not.Null, "Test setup expects the HUD UIDocument to have created its root within the wait budget.");

            _hudDocument.rootVisualElement.style.width = new Length(100f, LengthUnit.Percent);
            _hudDocument.rootVisualElement.style.height = new Length(100f, LengthUnit.Percent);
            _hudDocument.rootVisualElement.pickingMode = PickingMode.Ignore;

            var handStrip = new VisualElement();
            handStrip.style.position = Position.Absolute;
            handStrip.style.left = 0f;
            handStrip.style.bottom = 0f;
            handStrip.style.width = new Length(HudStripSizePercent, LengthUnit.Percent);
            handStrip.style.height = new Length(HudStripSizePercent, LengthUnit.Percent);
            _hudDocument.rootVisualElement.Add(handStrip);

            int layoutBudget = HudLayoutSettleFrameBudget;

            while (float.IsNaN(handStrip.resolvedStyle.width) && layoutBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(handStrip.resolvedStyle.width, Is.GreaterThan(0f), "Test setup expects the hand strip to have resolved a non-zero layout.");

            _presenter.SetHudDocumentForTests(_hudDocument);
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
