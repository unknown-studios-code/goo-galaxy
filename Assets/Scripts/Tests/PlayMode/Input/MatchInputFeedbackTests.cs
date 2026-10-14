using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    // Sibling of MatchInputControllerTests, split out per Rule 2 of unity-testing.md: that fixture already owns the
    // state-machine outcomes of each gesture, and this one owns only what the controller tells its listeners about
    // them through FeedbackRaised. It builds the same world on purpose — every PlayMode input fixture here owns its
    // own, as MatchInputSteadyStateAllocationTests does — and reads the list of values raised during the act step.
    // Test names take the trigger form of the flow they drive, as the sibling does.
    [TestFixture]
    public class MatchInputFeedbackTests
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
        private const string TroopCardIdValue = "feedback_troop_card";
        private const string SpellCardIdValue = "feedback_spell_card";
        private const string UncastableSpellCardIdValue = "feedback_uncastable_spell_card";

        // Far enough past any device's dp-to-pixel threshold that the exact screen resolution never matters.
        private const float DragOffsetInPixels = 2000f;

        // Frames budgeted for the HUD UIDocument's panel to attach and Yoga to resolve layout against it.
        private const int HudLayoutSettleFrameBudget = 10;

        // A percentage of the panel, not pixels, so the strip covers the same share of the screen at any scale.
        private const float HudStripSizePercent = 15f;

        private const int FeedbackCapacity = 8;

        private static readonly HexCoordinates _anchorHex = new(0, 0);
        private static readonly HexCoordinates _cloneTargetHex = new(1, 0); // Distance 1 from the anchor: Clone-only.
        private static readonly HexCoordinates _unhighlightedHex = new(4, 0); // On the board, out of Clone/Jump range.
        private static readonly HexCoordinates _immobileUnitHex = new(3, 0);
        private static readonly HexCoordinates _enemyUnitHex = new(-3, 0);
        private static readonly HexCoordinates _secondSpellCentreHex = new(2, -2); // Empty, and far from every placed unit.
        private static readonly Vector2 _offGridScreenPosition = new(1_000_000f, 1_000_000f);

        // Near the screen's own bottom-left corner, which always lands inside a panel region anchored to that corner.
        private static readonly Vector2 _hudScreenPosition = new(1f, 1f);

        private readonly List<Object> _spawned = new();
        private readonly List<InteractionFeedback> _feedback = new(FeedbackCapacity);

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

            _feedback.Clear();
            _presenter.FeedbackRaised += HandleFeedbackRaised;

            MatchEvents.RaiseMatchStarted(BuildMatchConfiguration());
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // MatchInputController resolves its board camera and builds its pointer resolver in Start, which Unity
            // defers to the first frame update after the SetActive(true) in BuildInputSourcesAndPresenter.
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null)
            {
                _presenter.FeedbackRaised -= HandleFeedbackRaised;
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
        }

        [Test]
        public void HandlePointerPressed_OwnedUnit_RaisesSelectedOnce()
        {
            // GIVEN

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Selected }));
        }

        [Test]
        public void HandlePointerPressed_UnitTheLocalPlayerDoesNotOwn_RaisesNothing()
        {
            // GIVEN

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_enemyUnitHex));

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerPressed_AnotherOwnedUnitWhileOneIsSelected_RaisesOnlySelected()
        {
            // GIVEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseReleased(ScreenPositionForHex(_anchorHex));
            _feedback.Clear();

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_immobileUnitHex));

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Selected }));
        }

        [Test]
        public void HandleHandSlotPressed_DifferentSlotWhileACardIsSelected_RaisesOnlySelected()
        {
            // GIVEN
            _handGestureSource.RaiseHandSlotPressed(0);
            _feedback.Clear();

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(1);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Selected }));
        }

        [Test]
        public void HandlePointerPressed_OwnedUnitBeforePlayOpens_RaisesNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandleHandSlotPressed_AffordableTroopCard_RaisesSelectedOnce()
        {
            // GIVEN

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Selected }));
        }

        [Test]
        public void HandleHandSlotPressed_AffordableProtocol_RaisesSelectedOnce()
        {
            // GIVEN
            BuildSpellHand();

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Selected }));
        }

        [Test]
        public void HandleHandSlotPressed_UnaffordableProtocol_RaisesSelectedOnce()
        {
            // GIVEN
            BuildSpellHand();
            _energyLedger.AffordableCostCeiling = 0;

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Selected }));
        }

        [Test]
        public void HandleHandSlotPressed_BeforePlayOpens_RaisesNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerMoved_SelectedUnitPastTheDragThreshold_RaisesDragStarted()
        {
            // GIVEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseMoved(_offGridScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DragStarted }));
        }

        [Test]
        public void HandlePointerMoved_DragKeepsMoving_RaisesDragStartedOnlyOnce()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _pointerSource.RaiseMoved(ScreenPositionForHex(_cloneTargetHex));
            _pointerSource.RaiseMoved(_offGridScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DragStarted }));
        }

        [Test]
        public void HandlePointerMoved_SelectedUnitWithoutLeavingTheThreshold_RaisesNothing()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseMoved(anchorScreen);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerMoved_TroopCardPastTheDragThreshold_RaisesDragStarted()
        {
            // GIVEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _handGestureSource.RaiseHandSlotPressed(0);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseMoved(_offGridScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DragStarted }));
        }

        [Test]
        public void HandlePointerMoved_ProtocolDraggedOutOfTheHand_RaisesDragStarted()
        {
            // GIVEN
            BuildSpellHand();
            _pointerSource.RaisePressed(_offGridScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseMoved(ScreenPositionForHex(_anchorHex));

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DragStarted }));
        }

        [Test]
        public void HandlePointerMoved_ProtocolDragKeepsMoving_RaisesDragStartedOnlyOnce()
        {
            // GIVEN
            BuildSpellHand();
            _pointerSource.RaisePressed(_offGridScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseMoved(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseMoved(ScreenPositionForHex(_secondSpellCentreHex));

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DragStarted }));
        }

        [Test]
        public void HandlePointerMoved_BoardPressAimingAProtocol_RaisesNothing()
        {
            // GIVEN
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseMoved(ScreenPositionForHex(_secondSpellCentreHex));

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerPressed_HighlightedHexTheOptionsDoNotContain_RaisesDropRejected()
        {
            // GIVEN — the highlight and the option list are two views of one set, so the only way to make them disagree
            // is to repaint the highlight behind the controller's back.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            _highlightPresenter.SetTargets(new List<HexCoordinates> { _unhighlightedHex });
            Assert.That(_highlightPresenter.IsHighlighted(_unhighlightedHex), Is.True, "Test setup expects the repainted hex to read as highlighted.");
            _feedback.Clear();

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_unhighlightedHex));

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DropRejected }));
        }

        [Test]
        public void HandlePointerReleased_UnitDraggedOffTheGrid_RaisesDropRejected()
        {
            // GIVEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DropRejected }));
        }

        [Test]
        public void HandlePointerReleased_UnitDraggedOntoAnUnhighlightedHex_RaisesDropRejected()
        {
            // GIVEN
            Vector2 unhighlightedScreen = ScreenPositionForHex(_unhighlightedHex);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseMoved(unhighlightedScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(unhighlightedScreen);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DropRejected }));
        }

        [Test]
        public void HandlePointerReleased_TroopCardDraggedOffTheGrid_RaisesDropRejected()
        {
            // GIVEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DropRejected }));
        }

        [Test]
        public void HandlePointerReleased_ProtocolDraggedOutOfTheHandOffTheGrid_RaisesDropRejected()
        {
            // GIVEN
            BuildSpellHand();
            _pointerSource.RaisePressed(_offGridScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseMoved(ScreenPositionForHex(_anchorHex));
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DropRejected }));
        }

        [Test]
        public void HandlePointerReleased_ProtocolDraggedOntoASpotNoImpactAccepts_RaisesDropRejected()
        {
            // GIVEN — the second authored impact's zero radius rejects every cluster the first can arrange, so this is
            // an on-grid spot with a resolvable centre that still cannot be cast.
            BuildUncastableSpellHand();
            _pointerSource.RaisePressed(_offGridScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaiseMoved(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DropRejected }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandlePointerReleased_ProtocolDraggedBackOntoTheHud_RaisesDropRejected()
        {
            // GIVEN
            yield return BuildHudAsync();
            BuildSpellHand();
            _pointerSource.RaisePressed(_hudScreenPosition);
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseMoved(ScreenPositionForHex(_anchorHex));
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(_hudScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DropRejected }));
        }

        [Test]
        public void HandlePointerReleased_BoardPressedProtocolLiftedOffTheGrid_RaisesDropRejected()
        {
            // GIVEN
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(_offGridScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.DropRejected }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandlePointerReleased_BoardPressedProtocolLiftedOverTheHud_RaisesNothing()
        {
            // GIVEN — the aim is kept for another try, so there is no drop to refuse.
            yield return BuildHudAsync();
            BuildSpellHand();
            TapSpellCardInHand(_hudScreenPosition);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _pointerSource.RaiseMoved(_hudScreenPosition);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(_hudScreenPosition);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerReleased_BoardPressedProtocolLiftedOnASpotNoImpactAccepts_RaisesNothing()
        {
            // GIVEN — a refused tap keeps the aim, as the board refuses it rather than the player abandoning it.
            BuildUncastableSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandleHandSlotPressed_SecondPressOnTheSameTroopSlot_RaisesCleared()
        {
            // GIVEN
            _handGestureSource.RaiseHandSlotPressed(0);
            _feedback.Clear();

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Cleared }));
        }

        [Test]
        public void HandleHandSlotPressed_SecondPressOnTheSameProtocolSlot_RaisesCleared()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _feedback.Clear();

            // WHEN
            _handGestureSource.RaiseHandSlotPressed(0);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Cleared }));
        }

        [Test]
        public void HandlePointerPressed_SecondTapOnTheSelectedUnit_RaisesCleared()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaisePressed(anchorScreen);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Cleared }));
        }

        [Test]
        public void HandlePointerPressed_EmptyUnhighlightedHexWithAUnitSelected_RaisesCleared()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_unhighlightedHex));

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Cleared }));
        }

        [Test]
        public void HandlePointerPressed_UnitTheLocalPlayerDoesNotOwnWithAUnitSelected_RaisesCleared()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_enemyUnitHex));

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Cleared }));
        }

        [Test]
        public void HandlePointerPressed_OffTheGridWithAUnitSelected_RaisesCleared()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaisePressed(_offGridScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Cleared }));
        }

        [Test]
        public void HandlePointerPressed_OffTheGridWhileAimingAProtocol_RaisesCleared()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaisePressed(_offGridScreenPosition);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Cleared }));
        }

        [Test]
        public void HandlePointerReleased_UnitDraggedBackToTheOrigin_RaisesCleared()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _pointerSource.RaiseMoved(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That(_feedback, Is.EqualTo(new[] { InteractionFeedback.Cleared }));
        }

        [Test]
        public void HandlePointerReleased_TapThatLeavesTheSelectionLive_RaisesNothing()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void OnDisable_WithALiveSelection_RaisesNothing()
        {
            // GIVEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _feedback.Clear();

            // WHEN
            _presenterGO.SetActive(false);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandleMatchStarted_WithALiveSelection_RaisesNothing()
        {
            // GIVEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _feedback.Clear();

            // WHEN
            MatchEvents.RaiseMatchStarted(BuildMatchConfiguration());

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandleMatchPhaseChanged_OutOfPlayWithALiveSelection_RaisesNothing()
        {
            // GIVEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _feedback.Clear();

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandleMatchEnded_WithALiveSelection_RaisesNothing()
        {
            // GIVEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _feedback.Clear();

            // WHEN
            MatchEvents.RaiseMatchEnded(MatchOutcome.Drawn);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandleHandChanged_AimedCardLeavingItsSlot_RaisesNothing()
        {
            // GIVEN
            BuildSpellHand();
            _handGestureSource.RaiseHandSlotPressed(0);
            var rotatedHand = new List<CardId> { _troopCard.CardId, _spellCard.CardId, _spellCard.CardId, _spellCard.CardId };
            _feedback.Clear();

            // WHEN
            MatchEvents.RaiseHandChanged(LocalPlayerId, rotatedHand, default);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerReleased_CanceledDuringADrag_RaisesNothing()
        {
            // GIVEN — a canceled release is focus lost to a call or the notification shade, never a choice of the player.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseMoved(_offGridScreenPosition);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseCanceled(anchorScreen);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerPressed_CommittingACloneOntoAHighlightedHex_RaisesNothing()
        {
            // GIVEN
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_cloneTargetHex));

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerPressed_CommitTheBoardRefuses_RaisesNothing()
        {
            // GIVEN — the ledger turns the move down after the highlight promised it; the board's refusal is not the player's mistake.
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _pointerSource.RaiseReleased(anchorScreen);
            _energyLedger.AffordableCostCeiling = -1;
            _feedback.Clear();

            // WHEN
            _pointerSource.RaisePressed(ScreenPositionForHex(_cloneTargetHex));

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerReleased_TroopCardDraggedOntoALegalHex_RaisesNothing()
        {
            // GIVEN
            Vector2 targetScreen = ScreenPositionForHex(_cloneTargetHex);
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseMoved(targetScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(targetScreen);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerReleased_CardDraggedIntoTheDiscardZone_RaisesNothing()
        {
            // GIVEN
            Vector2 pressScreen = ScreenPositionForHex(_anchorHex);
            Vector2 discardZoneScreenPosition = pressScreen + new Vector2(DragOffsetInPixels, 0f);
            _handGestureSource.DiscardZoneScreenRect = new Rect(discardZoneScreenPosition - (Vector2.one * 10f), Vector2.one * 20f);
            _pointerSource.RaisePressed(pressScreen);
            _handGestureSource.RaiseHandSlotPressed(0);
            _pointerSource.RaiseMoved(discardZoneScreenPosition);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(discardZoneScreenPosition);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerReleased_BoardPressedProtocolLiftedOnAValidSpot_RaisesNothing()
        {
            // GIVEN
            BuildSpellHand();
            TapSpellCardInHand(_offGridScreenPosition);
            Vector2 anchorScreen = ScreenPositionForHex(_anchorHex);
            _pointerSource.RaisePressed(anchorScreen);
            _feedback.Clear();

            // WHEN
            _pointerSource.RaiseReleased(anchorScreen);

            // THEN
            Assert.That(_feedback, Is.Empty);
        }

        [Test]
        public void HandlePointerMoved_DragStartedSubscriberThrows_StillBeginsTheDrag()
        {
            // GIVEN
            _presenter.FeedbackRaised += ThrowOnDragStarted;
            LogAssert.Expect(LogType.Warning, InputLogMessages.FeedbackSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _handGestureSource.RaiseHandSlotPressed(0);

            // WHEN
            _pointerSource.RaiseMoved(_offGridScreenPosition);

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Dragging));
        }

        [Test]
        public void HandlePointerMoved_DragStartedSubscriberThrowsOverALegalHex_StillPreviewsTheDrop()
        {
            // GIVEN
            _presenter.FeedbackRaised += ThrowOnDragStarted;
            LogAssert.Expect(LogType.Warning, InputLogMessages.FeedbackSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _handGestureSource.RaiseHandSlotPressed(0);

            // WHEN
            _pointerSource.RaiseMoved(ScreenPositionForHex(_cloneTargetHex));

            // THEN
            Assert.That(_presenter.State, Is.EqualTo(InteractionState.Previewing));
        }

        [Test]
        public void HandlePointerMoved_DragStartedSubscriberThrows_LogsTheWarningAndTheException()
        {
            // GIVEN
            _presenter.FeedbackRaised += ThrowOnDragStarted;
            LogAssert.Expect(LogType.Warning, InputLogMessages.FeedbackSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));
            _pointerSource.RaisePressed(ScreenPositionForHex(_anchorHex));
            _handGestureSource.RaiseHandSlotPressed(0);

            // WHEN
            _pointerSource.RaiseMoved(_offGridScreenPosition);

            // THEN
            LogAssert.NoUnexpectedReceived();
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

        private static MatchConfiguration BuildMatchConfiguration()
        {
            return new MatchConfiguration(
                0,
                new PlayerSlot(LocalPlayerId, PlayerControl.LocalHuman),
                new PlayerSlot(OpponentPlayerId, PlayerControl.Machine),
                0f,
                0f,
                0f
            );
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

        private void HandleFeedbackRaised(InteractionFeedback feedback)
        {
            _feedback.Add(feedback);
        }

        private void ThrowOnDragStarted(InteractionFeedback feedback)
        {
            if (feedback == InteractionFeedback.DragStarted)
            {
                throw new InvalidOperationException("Simulated FeedbackRaised subscriber failure.");
            }
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

            public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return unitEnergyCost <= AffordableCostCeiling;
            }

            public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return CanAffordMove(playerId, moveType, unitEnergyCost);
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
