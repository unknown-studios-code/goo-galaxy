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
using UnityEngine.UIElements;
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
        private const int SpellEnergyCost = 3;
        private const int SpellRadius = 1;
        private const int SpellClusterSize = 3;
        private const int SpellFreezeDuration = 1;
        private const string TroopCardIdValue = "input_troop_card";
        private const string SpellCardIdValue = "input_spell_card";
        private const string UncastableSpellCardIdValue = "input_uncastable_spell_card";

        // Far enough past any device's dp-to-pixel threshold that the exact screen resolution never matters.
        private const float DragOffsetInPixels = 2000f;

        // Frames budgeted for the HUD UIDocument's panel to attach and Yoga to resolve layout against it.
        private const int HudLayoutSettleFrameBudget = 10;

        // A percentage of the panel, not pixels — see BuildHudAsync for why.
        private const float HudStripSizePercent = 15f;

        private static readonly HexCoordinates _anchorHex = new(0, 0);
        private static readonly HexCoordinates _cloneTargetHex = new(1, 0); // Distance 1 from the anchor: Clone-only.
        private static readonly HexCoordinates _unhighlightedHex = new(4, 0); // On the board, out of Clone/Jump range.
        private static readonly HexCoordinates _immobileUnitHex = new(3, 0);
        private static readonly HexCoordinates _enemyUnitHex = new(-3, 0);
        private static readonly HexCoordinates _secondSpellCentreHex = new(2, -2); // Empty, and far from every placed unit.
        private static readonly Vector2 _offGridScreenPosition = new(1_000_000f, 1_000_000f);

        // Near the screen's own bottom-left corner, which — after BoardPointerResolver's screen-to-panel Y-flip
        // and whatever uniform scale the runner's PanelSettings resolves to — always lands inside a panel region
        // anchored to that same corner, regardless of DPI. Never a board hex: the extreme camera zoom projects
        // every hex used in this fixture close to screen centre.
        private static readonly Vector2 _hudScreenPosition = new(1f, 1f);

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
        private CardDataSO _spellCard;
        private CardDataSO _uncastableSpellCard;
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
        private GameObject _hudGO;
        private PanelSettings _hudPanelSettings;
        private UIDocument _hudDocument;
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

            // MatchController.SetPhaseForTests only mutates MatchState, so nothing here raises this event on its
            // own — and without it, _phase stays MatchPhase.None and IsPlayOpen refuses every selection this
            // fixture starts.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

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
        public void HandlePointerPressed_OnAValidSpotWhileAiming_PreviewsTheClusterWithoutCasting()
        {
            // GIVEN — the Device Simulator regression: a touchscreen reports no position until a finger is down, so
            // this press is the first moment the area can be shown at all, and casting on it landed a Protocol whose
            // area the player never saw.
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(
                (_presenter.State, _presenter.SpellPreview.Count, _highlightPresenter.IsHighlighted(_anchorHex), _energyLedger.PayCalls.Count),
                Is.EqualTo((InteractionState.SpellTargeting, SpellClusterSize, true, 0))
            );
        }

        [Test]
        public void HandlePointerMoved_AfterABoardPressWhileAiming_MovesThePreviewUnderThePointer()
        {
            // GIVEN
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            Assert.That(_highlightPresenter.IsHighlighted(_anchorHex), Is.True, "Test setup expects the press to preview the anchor hex.");

            // WHEN
            _pointerSource.RaiseMoved(ScreenPositionForHex(_secondSpellCentreHex));

            // THEN
            Assert.That((_highlightPresenter.IsHighlighted(_anchorHex), _highlightPresenter.IsHighlighted(_secondSpellCentreHex)), Is.EqualTo((false, true)));
        }

        [Test]
        public void HandlePointerReleased_BoardPressLiftedOnAValidSpot_CastsOnceWithTheCentreFirst()
        {
            // GIVEN — the same cluster of three validates in any order, so asserting only that a cast happened
            // would pass even if the cast's own centre were wrong; CardPlayAttempted's Target is read as the
            // cluster's first hex, so it proves which one actually landed there.
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            int handChangedBaseline = _handChangedCount;
            CardPlayAttempt? lastAttempt = null;
            void handleCardPlayAttempted(CardPlayAttempt attempt) => lastAttempt = attempt;
            MatchEvents.CardPlayAttempted += handleCardPlayAttempted;

            // WHEN
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(1));
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(1));
            Assert.That(lastAttempt.Value.Target, Is.EqualTo(_anchorHex));
        }

        [Test]
        public void HandlePointerReleased_BoardPressDraggedToAnotherHex_CastsWhereThePointerLifts()
        {
            // GIVEN
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            Vector2 liftScreen = ScreenPositionForHex(_secondSpellCentreHex);
            _pointerSource.RaiseMoved(liftScreen);
            CardPlayAttempt? lastAttempt = null;
            void handleCardPlayAttempted(CardPlayAttempt attempt) => lastAttempt = attempt;
            MatchEvents.CardPlayAttempted += handleCardPlayAttempted;

            // WHEN
            _pointerSource.RaiseReleased(liftScreen);

            // THEN
            Assert.That(lastAttempt.Value.Target, Is.EqualTo(_secondSpellCentreHex));
        }

        [UnityTest]
        public IEnumerator HandlePointerReleased_BoardPressSlidOntoTheHud_KeepsAimingWithNoCast()
        {
            // GIVEN
            yield return BuildHudAsync();
            BuildSpellHand();
            TapSpellCardInHand(_hudScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseMoved(_hudScreenPosition);

            // WHEN
            _pointerSource.RaiseReleased(_hudScreenPosition);

            // THEN
            Assert.That((_presenter.State, _energyLedger.PayCalls.Count), Is.EqualTo((InteractionState.SpellTargeting, 0)));
        }

        [Test]
        public void HandlePointerReleased_BoardPressLiftedOnAnInvalidOnGridSpot_KeepsAimingWithoutCasting()
        {
            // GIVEN — the second authored impact's zero radius rejects every cluster the first impact can ever
            // arrange, on any hex, so this is an on-grid spot with a resolvable centre that is still not castable
            // — the case the too-few-neighbours guard cannot reach on this board (see ClusterTargetBuilderTests).
            BuildUncastableSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);

            // WHEN
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting));
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator HandlePointerReleased_BoardPressLiftedOverTheDiscardZone_KeepsAimingWithoutDiscarding()
        {
            // GIVEN — only a press on a card arms the zone, so a finger that started on the board and slid onto it has
            // nothing to discard into.
            yield return BuildHudAsync();
            BuildSpellHand();
            _handGestureSource.DiscardZoneScreenRect = new Rect(_hudScreenPosition - (Vector2.one * 10f), Vector2.one * 20f);
            TapSpellCardInHand(_hudScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseMoved(_hudScreenPosition);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            _pointerSource.RaiseReleased(_hudScreenPosition);

            // THEN
            Assert.That(
                (_presenter.State, _handChangedCount - handChangedBaseline, _handGestureSource.IsDiscardZoneArmed),
                Is.EqualTo((InteractionState.SpellTargeting, 0, false))
            );
        }

        [UnityTest]
        public IEnumerator HandleHandSlotPressed_ReportedAfterTheTapWasReleased_NeverArmsTheDiscardZoneForTheNextBoardDrag()
        {
            // GIVEN — a Device Simulator click or a fast tap moves the button 0 to 1 to 0 inside one input update, so the
            // pointer's release is heard before UI Toolkit dispatches the card's report; the report must not leave a
            // card press behind for the board press that follows.
            yield return BuildHudAsync();
            BuildSpellHand();
            ReportCardAfterTheTapWasReleased();
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));

            // WHEN
            _pointerSource.RaiseMoved(ScreenPositionForHex(_secondSpellCentreHex));

            // THEN
            Assert.That(_handGestureSource.IsDiscardZoneArmed, Is.False);
        }

        [UnityTest]
        public IEnumerator HandlePointerReleased_BoardDragLiftedOverTheHudAfterALateCardReport_CastsNothing()
        {
            // GIVEN
            yield return BuildHudAsync();
            BuildSpellHand();
            ReportCardAfterTheTapWasReleased();
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseMoved(ScreenPositionForHex(_secondSpellCentreHex));

            // WHEN
            _pointerSource.RaiseReleased(_hudScreenPosition);

            // THEN
            Assert.That(
                (_presenter.State, _energyLedger.PayCalls.Count, _handGestureSource.IsDiscardZoneArmed),
                Is.EqualTo((InteractionState.SpellTargeting, 0, false))
            );
        }

        [UnityTest]
        public IEnumerator HandlePointerReleased_HudPressDraggedOntoAValidHexAfterALateCardReport_CastsNothing()
        {
            // GIVEN — a press on the HUD that no card reports is not a drag out of the hand, however late the previous
            // tap's report arrived.
            yield return BuildHudAsync();
            BuildSpellHand();
            ReportCardAfterTheTapWasReleased();
            _pointerSource.RaisePressed(_hudScreenPosition);
            Vector2 targetScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaiseMoved(targetScreen);

            // WHEN
            _pointerSource.RaiseReleased(targetScreen);

            // THEN
            Assert.That((_presenter.State, _energyLedger.PayCalls.Count), Is.EqualTo((InteractionState.SpellTargeting, 0)));
        }

        [UnityTest]
        public IEnumerator HandlePointerMoved_HudPressDraggedOntoTheBoardWhileAiming_ShowsNoPreview()
        {
            // GIVEN — its release casts nothing, so a cluster shown under it would be a promise the lift cannot keep.
            yield return BuildHudAsync();
            BuildSpellHand();
            TapSpellCardInHand(_hudScreenPosition);
            _pointerSource.RaisePressed(_hudScreenPosition);

            // WHEN
            _pointerSource.RaiseMoved(ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That((_presenter.IsSpellPreviewValid, _highlightPresenter.HighlightedCount), Is.EqualTo((false, 0)));
        }

        [Test]
        public void HandleHandChanged_DuringABoardHold_LeavesTheLiftNothingToCast()
        {
            // GIVEN
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            var rotatedHand = new List<CardId> { _troopCard.CardId, _spellCard.CardId, _spellCard.CardId, _spellCard.CardId };
            MatchEvents.RaiseHandChanged(LocalPlayerId, rotatedHand, default);
            Vector2 liftScreen = ScreenPositionForHex(_secondSpellCentreHex);
            _pointerSource.RaiseMoved(liftScreen);

            // WHEN
            _pointerSource.RaiseReleased(liftScreen);

            // THEN
            Assert.That((_presenter.State, _energyLedger.PayCalls.Count), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [Test]
        public void HandleMatchPhaseChanged_OutOfPlayDuringABoardHold_LeavesTheLiftNothingToCast()
        {
            // GIVEN
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);
            Vector2 liftScreen = ScreenPositionForHex(_secondSpellCentreHex);
            _pointerSource.RaiseMoved(liftScreen);

            // WHEN
            _pointerSource.RaiseReleased(liftScreen);

            // THEN
            Assert.That((_presenter.State, _energyLedger.PayCalls.Count), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [Test]
        public void HandleMatchEnded_DuringABoardHold_LeavesTheLiftNothingToCast()
        {
            // GIVEN
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            MatchEvents.RaiseMatchEnded(MatchOutcome.Drawn);
            Vector2 liftScreen = ScreenPositionForHex(_secondSpellCentreHex);
            _pointerSource.RaiseMoved(liftScreen);

            // WHEN
            _pointerSource.RaiseReleased(liftScreen);

            // THEN
            Assert.That((_presenter.State, _energyLedger.PayCalls.Count), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [Test]
        public void HandlePointerReleased_BoardTapAfterACancelledBoardHold_CastsOnce()
        {
            // GIVEN — the cancelled hold's gesture must end with its own release, so the next aim starts clean.
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            var rotatedHand = new List<CardId> { _troopCard.CardId, _spellCard.CardId, _spellCard.CardId, _spellCard.CardId };
            MatchEvents.RaiseHandChanged(LocalPlayerId, rotatedHand, default);
            _pointerSource.RaiseReleased(anchorScreen);
            TapSpellCardInHand(_offGridScreenPosition);
            _pointerSource.RaisePressed(anchorScreen);

            // WHEN
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(1));
        }

        [Test]
        public void HandlePointerReleased_CanceledDuringABoardHold_CancelsTheAimWithoutCasting()
        {
            // GIVEN — a canceled release is focus lost to a call or the notification shade, never a lift.
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);

            // WHEN
            _pointerSource.RaiseCanceled(anchorScreen);

            // THEN
            Assert.That((_presenter.State, _energyLedger.PayCalls.Count), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [UnityTest]
        public IEnumerator HandlePointerReleased_CanceledDuringADragOutOfTheHand_CancelsWithoutCastingOrDiscarding()
        {
            // GIVEN
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaiseMoved(anchorScreen);
            Assert.That(_handGestureSource.IsDiscardZoneArmed, Is.True, "Test setup expects the drag out of the hand to arm the discard zone.");
            int handChangedBaseline = _handChangedCount;

            // WHEN
            _pointerSource.RaiseCanceled(anchorScreen);

            // THEN
            Assert.That(
                (_presenter.State, _energyLedger.PayCalls.Count, _handChangedCount - handChangedBaseline, _handGestureSource.IsDiscardZoneArmed),
                Is.EqualTo((InteractionState.Idle, 0, 0, false))
            );
        }

        [Test]
        public void HandlePointerPressed_OffTheGrid_CancelsTheAim()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");

            // WHEN
            _pointerSource.RaisePressed(_offGridScreenPosition);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
        }

        [Test]
        public void HandlePointerPressed_SecondTapOnTheSelectedUnitsOwnHex_CancelsWithoutReselecting()
        {
            // GIVEN — a re-tap on the selection's own source cancels rather than being read as a fresh tap on
            // that same unit, which TrySelectUnitAt would otherwise immediately re-select.
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
            // GIVEN — the hand-slot equivalent: a re-tap on the selection's own hand slot cancels rather than
            // being read as a fresh press that would re-select it.
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
        public void HandleHandSlotPressed_UnaffordableSpell_WaitsInCardSelected()
        {
            // GIVEN
            BuildSpellHand();
            _energyLedger.AffordableCostCeiling = 0;

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.CardSelected));
        }

        [Test]
        public void HandleHandSlotPressed_SecondPressOnTheAimedProtocolsOwnSlot_Cancels()
        {
            // GIVEN — the Protocol equivalent of the troop's second-press-cancels regression above.
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the first press to aim the Protocol.");

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
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.UnitSelected), "Test setup expects the press to select the anchor unit.");

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

            // THEN — CardSelected proves the press reached ResolveTargets rather than having been blocked
            // earlier (by the phase gate, for instance), so the zero counts are the ledger's refusal and not a
            // selection that never started.
            Assert.That(
                (_presenter.State, _presenter.TargetCount, _highlightPresenter.HighlightedCount, _handChangedCount - handChangedBaseline),
                Is.EqualTo((InteractionState.CardSelected, 0, 0, 0))
            );
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
        public void MatchEnded_WhileAimingASpell_CancelsAndClearsHighlights()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));
            Assert.That(_highlightPresenter.HighlightedCount, Is.EqualTo(SpellClusterSize), "Test setup expects the hover to have highlighted the cluster.");

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
        public void MatchPhaseChanged_OutOfPlayWhileAimingASpell_CancelsAndClearsHighlights()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));
            Assert.That(_highlightPresenter.HighlightedCount, Is.EqualTo(SpellClusterSize), "Test setup expects the hover to have highlighted the cluster.");

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
        public void HandleLandingResolved_WhileAimingAProtocol_LeavesThePreviewAndStateAlone()
        {
            // GIVEN — a Protocol's cluster reads no occupancy, so a landing elsewhere on the board — the human's
            // or the machine's — must not re-enumerate or otherwise disturb a live aim.
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));
            Assert.That(_highlightPresenter.HighlightedCount, Is.EqualTo(SpellClusterSize), "Test setup expects the hover to have highlighted the cluster.");

            // WHEN
            MatchEvents.RaiseLandingResolved(default, new ConversionResult(System.Array.Empty<int>(), System.Array.Empty<int>()));

            // THEN
            Assert.That((_presenter.State, _highlightPresenter.HighlightedCount), Is.EqualTo((InteractionState.SpellTargeting, SpellClusterSize)));
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

        [Test]
        public void HandleEnergyChanged_RisePastTheSpellCost_PromotesTheWaitingCardToSpellTargeting()
        {
            // GIVEN
            BuildSpellHand();
            _energyLedger.AffordableCostCeiling = 0;
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.CardSelected), "Test setup expects the unaffordable Protocol to wait in CardSelected.");
            _energyLedger.AffordableCostCeiling = SpellEnergyCost;

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, SpellEnergyCost);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting));
        }

        [Test]
        public void HandleEnergyChanged_FallsBelowTheSpellCostWhileAiming_DemotesToCardSelectedAndClearsThePreview()
        {
            // GIVEN — the mirror of the promotion above: an aim already live loses its cost mid-gesture.
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));
            Assert.That(_presenter.IsSpellPreviewValid, Is.True, "Test setup expects the hover to have built a valid preview.");
            _energyLedger.AffordableCostCeiling = 0;

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 0f);

            // THEN
            Assert.That((_presenter.State, _highlightPresenter.HighlightedCount), Is.EqualTo((InteractionState.CardSelected, 0)));
        }

        [UnityTest]
        public IEnumerator HandleEnergyChanged_RisePastTheSpellCostWhileDragging_KeepsTheDragAimAndCastsOnRelease()
        {
            // GIVEN — the Protocol is unaffordable when the press starts, so the drag is a plain card drag until
            // the balance crosses its cost mid-gesture; RefreshSpellAffordability must promote it into
            // SpellTargeting without dropping the drag it was already carrying.
            yield return BuildHudAsync();
            BuildSpellHand();
            _energyLedger.AffordableCostCeiling = 0;
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.CardSelected), "Test setup expects the unaffordable Protocol to wait in CardSelected.");
            _pointerSource.RaiseMoved(ScreenPositionForHex(_secondSpellCentreHex));
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Dragging), "Test setup expects the drag past the threshold to begin.");
            _energyLedger.AffordableCostCeiling = SpellEnergyCost;
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, SpellEnergyCost);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the promotion to have kept aiming live.");
            Vector2 targetScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaiseMoved(targetScreen);

            // WHEN
            _pointerSource.RaiseReleased(targetScreen);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HandleEnergyChanged_FallsBelowTheCostDuringABoardHold_ReleaseOverTheDiscardZoneDiscardsNothing()
        {
            // GIVEN — the demoted Protocol is a hand-slot selection again, but the press behind it is on the board, so it
            // must not turn into a drag toward the discard zone.
            yield return BuildHudAsync();
            BuildSpellHand();
            _handGestureSource.DiscardZoneScreenRect = new Rect(_hudScreenPosition - (Vector2.one * 10f), Vector2.one * 20f);
            TapSpellCardInHand(_hudScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _energyLedger.AffordableCostCeiling = 0;
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 0f);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.CardSelected), "Test setup expects the aim to fall back to waiting.");
            _pointerSource.RaiseMoved(_hudScreenPosition);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            _pointerSource.RaiseReleased(_hudScreenPosition);

            // THEN
            Assert.That(
                (_presenter.State, _handChangedCount - handChangedBaseline, _handGestureSource.IsDiscardZoneArmed),
                Is.EqualTo((InteractionState.CardSelected, 0, false))
            );
        }

        [Test]
        public void HandleEnergyChanged_RisesBackPastTheCostDuringABoardHold_CastsWhereThePointerLifts()
        {
            // GIVEN
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _energyLedger.AffordableCostCeiling = 0;
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 0f);
            _energyLedger.AffordableCostCeiling = SpellEnergyCost;
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, SpellEnergyCost);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the board hold to be aiming again.");
            Vector2 liftScreen = ScreenPositionForHex(_secondSpellCentreHex);
            _pointerSource.RaiseMoved(liftScreen);
            CardPlayAttempt? lastAttempt = null;
            void handleCardPlayAttempted(CardPlayAttempt attempt) => lastAttempt = attempt;
            MatchEvents.CardPlayAttempted += handleCardPlayAttempted;

            // WHEN
            _pointerSource.RaiseReleased(liftScreen);

            // THEN
            Assert.That(lastAttempt.Value.Target, Is.EqualTo(_secondSpellCentreHex));
        }

        [Test]
        public void HandlePointerPressed_TappingAnOwnedUnitDuringCountdown_SelectsNothing()
        {
            // GIVEN — regression: board moves have no phase gate of their own (UnitPresenter.ResolveMove checks
            // no phase at all), so this controller must refuse to start a selection outside Standard and Overtime.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);

            // WHEN
            _pointerSource.RaisePressed(anchorScreen);

            // THEN
            Assert.That((_presenter.State, _highlightPresenter.HighlightedCount), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [Test]
        public void TapThenTapOnACloneTarget_DuringCountdown_LeavesTheBoardUnchanged()
        {
            // GIVEN — the same regression, read from the board rather than from the presenter's own state: even
            // a tap-then-tap sequence that would clone the unit in Standard must leave nothing changed here.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            Vector2 targetScreen = ScreenPositionForHex(_cloneTargetHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);

            // WHEN
            _pointerSource.RaisePressed(targetScreen);

            // THEN
            Assert.That((_unitPresenter.ActiveUnits.Count, GetOccupant(_cloneTargetHex)), Is.EqualTo((3, false)));
        }

        [Test]
        public void HandlePointerPressed_TappingAnOwnedUnitAfterMatchEnded_SelectsNothing()
        {
            // GIVEN — the same phase gate, on the other side of a match: MatchEnded alone does not move _phase,
            // so this asserts the presenter reads MatchPhaseChanged(Ended) rather than the outcome event.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Ended);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);

            // WHEN
            _pointerSource.RaisePressed(anchorScreen);

            // THEN
            Assert.That((_presenter.State, _highlightPresenter.HighlightedCount), Is.EqualTo((InteractionState.Idle, 0)));
        }

        [Test]
        public void HandleHandSlotPressed_DuringCountdown_SelectsNothing()
        {
            // GIVEN — the hand-slot equivalent of the phase-gate regression above.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
        }

        [Test]
        public void HandleHandSlotPressed_DraggedPastThresholdAndBackToTheOrigin_CancelsAndDisarmsTheDiscardZone()
        {
            // GIVEN — regression for the stuck-drag fix: before it, a release that travelled out past the
            // threshold and back within it left the state at Dragging with the discard zone still armed, instead
            // of tearing down what the drag had armed on the way out.
            Vector2 pressScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(pressScreen);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.CardSelected), "Test setup expects the hand slot press to select a card.");
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            Assert.That(_handGestureSource.IsDiscardZoneArmed, Is.True, "Test setup expects the drag past the threshold to arm the discard zone.");

            // WHEN
            _pointerSource.RaiseMoved(pressScreen);
            _pointerSource.RaiseReleased(pressScreen);

            // THEN
            Assert.That((_presenter.State, _handGestureSource.IsDiscardZoneArmed), Is.EqualTo((InteractionState.Idle, false)));
        }

        [Test]
        public void HandlePointerPressed_DraggedPastThresholdAndBackToTheOrigin_CancelsToIdle()
        {
            // GIVEN — the board-unit equivalent of the stuck-drag fix above.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.UnitSelected), "Test setup expects the press to select the anchor unit.");
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Dragging), "Test setup expects the drag past the threshold to begin.");

            // WHEN
            _pointerSource.RaiseMoved(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
        }

        [Test]
        public void HandlePointerHovered_OverABoardHex_BuildsAThreeHexPreviewWithTheCentreHighlighted()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");

            // WHEN
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(
                (_presenter.SpellPreview.Count, _highlightPresenter.HighlightedCount, _highlightPresenter.IsHighlighted(_anchorHex)),
                Is.EqualTo((SpellClusterSize, SpellClusterSize, true))
            );
        }

        [Test]
        public void HandlePointerHovered_MovingToADifferentHex_UpdatesThePreview()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));
            Assert.That(_highlightPresenter.IsHighlighted(_anchorHex), Is.True, "Test setup expects the first hover to highlight the anchor hex.");

            // WHEN
            _pointerSource.RaiseHovered(ScreenPositionForHex(_secondSpellCentreHex));

            // THEN
            Assert.That((_highlightPresenter.IsHighlighted(_anchorHex), _highlightPresenter.IsHighlighted(_secondSpellCentreHex)), Is.EqualTo((false, true)));
        }

        [UnityTest]
        public IEnumerator HandlePointerHovered_OverTheHudWhileAiming_SuspendsThePreview()
        {
            // GIVEN
            yield return BuildHudAsync();
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));
            Assert.That(_highlightPresenter.HighlightedCount, Is.EqualTo(SpellClusterSize), "Test setup expects the hover to have highlighted the cluster.");

            // WHEN
            _pointerSource.RaiseHovered(_hudScreenPosition);

            // THEN
            Assert.That(_presenter.IsSpellPreviewValid, Is.False);
            Assert.That(_highlightPresenter.HighlightedCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator HandlePointerHovered_ReturningToTheBoardAfterTheHud_ResumesThePreview()
        {
            // GIVEN
            yield return BuildHudAsync();
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseHovered(_hudScreenPosition);
            Assert.That(_highlightPresenter.HighlightedCount, Is.EqualTo(0), "Test setup expects the hover over the HUD to have suspended the preview.");

            // WHEN
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(_presenter.IsSpellPreviewValid, Is.True);
            Assert.That(_highlightPresenter.HighlightedCount, Is.EqualTo(SpellClusterSize));
        }

        [UnityTest]
        public IEnumerator HandlePointerHovered_RaisedWhilePointerIsDown_ChangesNothing()
        {
            // GIVEN — the pointer being down means a drag or a tap is already live, so a hover received while it
            // is down must be ignored rather than building or moving a preview underneath the gesture in progress.
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");

            // WHEN
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(_presenter.IsSpellPreviewValid, Is.False);
            Assert.That(_highlightPresenter.HighlightedCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator DragSpellAim_DraggedOntoAnotherHex_MovesThePreviewThere()
        {
            // GIVEN — the press must land on the hand, or the drag is never armed as an aim from the hand; see
            // HandleHandSlotPressed's own remarks.
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");

            // WHEN
            _pointerSource.RaiseMoved(ScreenPositionForHex(_secondSpellCentreHex));

            // THEN
            Assert.That(_highlightPresenter.IsHighlighted(_secondSpellCentreHex), Is.True);
        }

        [UnityTest]
        public IEnumerator DragSpellAim_DraggedBackOntoTheHud_SuspendsThePreview()
        {
            // GIVEN — the same suspend the hover path exercises, reached instead through a still-live drag: the
            // pointer stays down, so this goes through MoveSpellAim rather than HandlePointerHovered.
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseMoved(ScreenPositionForHex(_anchorHex));
            Assert.That(
                _highlightPresenter.HighlightedCount,
                Is.EqualTo(SpellClusterSize),
                "Test setup expects the drag onto the board to have highlighted the cluster."
            );

            // WHEN
            _pointerSource.RaiseMoved(_hudScreenPosition);

            // THEN
            Assert.That(_presenter.IsSpellPreviewValid, Is.False);
            Assert.That(_highlightPresenter.HighlightedCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator DragSpellAim_PressOriginatedOffTheHandStrip_NeverArmsTheDrag()
        {
            // GIVEN — a press on some other HUD element (not the hand strip) never reports through
            // IHandGestureSource, so _isPressOnHand stays false and a drag that follows it must not be read as
            // an aim dragged out of the hand, even though it crosses the board onto a perfectly castable hex.
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseReleased(_hudScreenPosition);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the tap-then-tap release to leave the aim live.");

            // WHEN
            _pointerSource.RaisePressed(_hudScreenPosition);
            Vector2 targetScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaiseMoved(targetScreen);
            _pointerSource.RaiseReleased(targetScreen);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting));
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ReleaseSpellAim_DraggedFromTheHandOntoAValidHex_CastsOnceWithTheCentreFirst()
        {
            // GIVEN — the press must land on the hand, or the drag is never armed as an aim from the hand; see
            // HandleHandSlotPressed's own remarks. The same cluster of three validates in any order, so
            // CardPlayAttempted's Target is what proves the centre — not merely a member — landed first.
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");
            _pointerSource.RaiseMoved(ScreenPositionForHex(_secondSpellCentreHex));
            int handChangedBaseline = _handChangedCount;
            CardPlayAttempt? lastAttempt = null;
            void handleCardPlayAttempted(CardPlayAttempt attempt) => lastAttempt = attempt;
            MatchEvents.CardPlayAttempted += handleCardPlayAttempted;

            // WHEN
            Vector2 targetScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaiseMoved(targetScreen);
            _pointerSource.RaiseReleased(targetScreen);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(1));
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(1));
            Assert.That(lastAttempt.Value.Target, Is.EqualTo(_anchorHex));
        }

        [UnityTest]
        public IEnumerator ReleaseSpellAim_DraggedBackOntoTheHud_CancelsWithNoCastAndNoEnergySpent()
        {
            // GIVEN
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseMoved(ScreenPositionForHex(_anchorHex));
            Assert.That(_presenter.IsSpellPreviewValid, Is.True, "Test setup expects the drag onto the board to have built a valid preview.");

            // WHEN
            _pointerSource.RaiseReleased(_hudScreenPosition);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ReleaseSpellAim_DraggedOffTheGrid_CancelsWithNoCastAndNoEnergySpent()
        {
            // GIVEN
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);

            // WHEN
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ReleaseSpellAim_DraggedOntoASpotNoImpactAccepts_CancelsWithNoCastAndNoEnergySpent()
        {
            // GIVEN — the second authored impact's zero radius rejects every cluster the first impact can ever
            // arrange, on any hex, so this is an on-grid spot with a resolvable centre that is still not castable
            // — the case the too-few-neighbours guard cannot reach on this board (see ClusterTargetBuilderTests).
            yield return BuildHudAsync();
            BuildUncastableSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the affordable Protocol to enter SpellTargeting.");

            // WHEN
            Vector2 targetScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaiseMoved(targetScreen);
            _pointerSource.RaiseReleased(targetScreen);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ReleaseSpellAim_DraggedIntoTheDiscardZone_DiscardsTheProtocol()
        {
            // GIVEN
            yield return BuildHudAsync();
            BuildSpellHand();
            Vector2 discardZoneScreenPosition = _hudScreenPosition + new Vector2(DragOffsetInPixels, 0f);
            _handGestureSource.DiscardZoneScreenRect = new Rect(discardZoneScreenPosition - (Vector2.one * 10f), Vector2.one * 20f);
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            _pointerSource.RaiseMoved(discardZoneScreenPosition);
            _pointerSource.RaiseReleased(discardZoneScreenPosition);

            // THEN — PayCalls stays empty because a discard never reaches the energy ledger at all; asserting it
            // is what tells a discard apart from a cast that happened to land on a castable hex at the same point.
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(1));
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(0));
        }

        [Test]
        public void CastSpell_RefusedByTheLedger_KeepsStateAndPreview()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));
            Assert.That(_presenter.IsSpellPreviewValid, Is.True, "Test setup expects the hover to have built a valid preview.");
            _energyLedger.AffordableCostCeiling = 0;
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);

            // WHEN
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That((_presenter.State, _presenter.SpellPreview.Count), Is.EqualTo((InteractionState.SpellTargeting, SpellClusterSize)));
        }

        [Test]
        public void CastSpell_MatchPhaseChangedDuringTheCast_CommitsTheCopiedTargetsWithoutThrowing()
        {
            // GIVEN — a subscriber that reacts to the cast's own CardPlayAttempted by moving the match out of
            // play, standing in for a countdown starting mid-resolution. CastSpell must already have copied the
            // preview into its own buffer before this fires, or the nested CancelSelection clearing the shared
            // cluster buffer would leave TryPlayCard resolving against an emptied cluster.
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseHovered(ScreenPositionForHex(_anchorHex));
            Assert.That(_presenter.IsSpellPreviewValid, Is.True, "Test setup expects the hover to have built a valid preview.");
            void handleCardPlayAttempted(CardPlayAttempt attempt) => MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);
            MatchEvents.CardPlayAttempted += handleCardPlayAttempted;
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            void releaseCall() => _pointerSource.RaiseReleased(anchorScreen);

            // WHEN
            Assert.DoesNotThrow(releaseCall);

            // THEN — the cast must have gone through with the copy CastSpell took before the reentrant cancel
            // cleared the shared preview buffer, or a countdown starting mid-resolution would silently swallow
            // the play.
            Assert.That(_energyLedger.PayCalls.Count, Is.EqualTo(1));
        }

        [Test]
        public void HandleHandChanged_AimedCardLeavingItsSlot_Cancels()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the Protocol to be aimed.");
            var rotatedHand = new List<CardId> { _troopCard.CardId, _spellCard.CardId, _spellCard.CardId, _spellCard.CardId };

            // WHEN
            MatchEvents.RaiseHandChanged(LocalPlayerId, rotatedHand, default);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Idle));
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

        // A second impact whose radius no cluster the first impact arranges can ever satisfy, so
        // AreTargetsValidForEveryImpact rejects the drag no matter where it lands — the on-grid, resolvable-centre
        // shape of "invalid spot" that a too-few-neighbours board edge cannot produce at this board's radius.
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
            _spellCard = CreateSpellCard();
            _uncastableSpellCard = CreateUncastableSpellCard();
            _cardPresenter.SetAuthoredCards(_troopCard, _spellCard, _uncastableSpellCard);
            cardPresenterGO.SetActive(true);
            _spawned.Add(cardPresenterGO);
            _spawned.Add(_troopCard);
            _spawned.Add(_spellCard);
            _spawned.Add(_uncastableSpellCard);

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

        // Re-deals the local player's hand from a kit of nothing but the spell card, so every slot — the shuffle
        // included, since a shuffle of identical entries has nothing to scramble — resolves to the same Protocol
        // deterministically. Called from a spell test's own GIVEN rather than from SetUp, so the troop-hand tests
        // above stay on the deck SetUp already builds for them.
        private void BuildSpellHand()
        {
            var kitCards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < kitCards.Length; i++)
            {
                kitCards[i] = _spellCard;
            }

            KitDataSO spellKit = ScriptableObject.CreateInstance<KitDataSO>();
            spellKit.SetAuthoredCards(kitCards);
            _spawned.Add(spellKit);

            _deckPresenter.SetKit(spellKit, HandSize);
            _deckPresenter.InitializePlayer(LocalPlayerId);
        }

        // The same re-deal as BuildSpellHand, from a kit of nothing but the uncastable spell card.
        private void BuildUncastableSpellHand()
        {
            var kitCards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < kitCards.Length; i++)
            {
                kitCards[i] = _uncastableSpellCard;
            }

            KitDataSO uncastableKit = ScriptableObject.CreateInstance<KitDataSO>();
            uncastableKit.SetAuthoredCards(kitCards);
            _spawned.Add(uncastableKit);

            _deckPresenter.SetKit(uncastableKit, HandSize);
            _deckPresenter.InitializePlayer(LocalPlayerId);
        }

        // The whole tap a real HUD produces when a card is picked: the pointer's own press and release around the
        // hand strip's report of slot 0. Raising the report alone would leave the controller believing the finger is
        // still on the card, and a later drag on the board would be read as a drag out of the hand.
        private void TapSpellCardInHand(Vector2 cardScreenPosition)
        {
            _pointerSource.RaisePressed(cardScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseReleased(cardScreenPosition);
        }

        // The same tap in the order a single input update produces it: the pointer's press and release are both heard
        // before UI Toolkit dispatches the card's report.
        private void ReportCardAfterTheTapWasReleased()
        {
            _pointerSource.RaisePressed(_hudScreenPosition);
            _pointerSource.RaiseReleased(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.SpellTargeting), "Test setup expects the late report to aim the Protocol.");
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

        // Wires a real UIDocument onto the already-built presenter through the internal test seam
        // SetHudDocumentForTests, exactly as the scene's prefab wires the serialized field in the Inspector —
        // Construct carries no HUD parameter, since the field is Inspector-only wiring rather than an injected
        // dependency.
        //
        // The root is picking-mode Ignore, matching a real HUD's structural host (see unity-ui-toolkit.md Rule 7),
        // and one child element stands in for the hand strip: anchored to the panel's bottom-left corner and
        // sized as a percentage of it, so _hudScreenPosition — near the screen's own bottom-left corner — always
        // falls inside it after the screen-to-panel Y-flip, whatever uniform scale the runner's PanelSettings
        // resolves to (measured here at roughly 4.8:1 against Screen, matching MatchHudViewPointerTests's own
        // measurement) — a fixed pixel count would read as "a corner" in screen terms but cover most of the
        // panel once divided by that scale, which is what let it swallow the centre hex on the first attempt.
        // Every board hex this fixture presses or drags to projects close to screen centre, on the opposite side
        // of the panel, so the two never collide.
        private IEnumerator BuildHudAsync()
        {
            _hudGO = new GameObject("MatchInputController_Hud_Test");
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

            // WORKAROUND: the root has no in-flow content of its own — the hand strip below is position:absolute,
            // which Yoga excludes from a parent's automatic content-based sizing — so its height resolves to 0
            // without an explicit size, and a bottom-anchored absolute child then measures "bottom" against that
            // zero height instead of the panel's real one, landing it off the top of the screen instead of at the
            // bottom. 100% of the panel is what the root would have sized itself to anyway had it held any
            // in-flow content, so this changes nothing about what the root visually covers.
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

            // Neither creating the root nor attaching a child to it resolves Yoga layout by itself — both the
            // root's own size and the hand strip's position against it stay NaN for a frame or two after this,
            // the same settle MatchHudViewPointerTests budgets for, so a Pick against it right away finds nothing.
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
