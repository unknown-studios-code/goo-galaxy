using System;
using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Controllers;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Deck
{
    [TestFixture]
    public class CardDiscardControllerTests
    {
        private const int HandSize = 4;
        private const int ActingPlayerId = 1;
        private const int SecondPlayerId = 2;
        private const int UnknownPlayerId = 99;
        private const int BoardRadius = 6;
        private const int SpellEnergyCost = 2;
        private const string HandChangedEventName = "HandChanged";
        private const string CardDiscardedEventName = "CardDiscarded";

        private static readonly HexCoordinates _spellTarget = new(-3, 0);

        private readonly List<Object> _spawned = new();
        private readonly List<string> _eventOrder = new();

        private int _handChangedCount;
        private int _cardDiscardedCount;
        private int _lastDiscardedPlayerId;
        private CardId _lastDiscardedCard;
        private int _lastDiscardedSlot;
        private int _moveExecutedCount;
        private int _landingResolvedCount;
        private int _conversionResolvedCount;

        [SetUp]
        public void SetUp()
        {
            _handChangedCount = 0;
            _cardDiscardedCount = 0;
            _lastDiscardedPlayerId = -1;
            _lastDiscardedCard = default;
            _lastDiscardedSlot = -1;
            _moveExecutedCount = 0;
            _landingResolvedCount = 0;
            _conversionResolvedCount = 0;
            _eventOrder.Clear();

            MatchEvents.HandChanged += HandleHandChanged;
            MatchEvents.CardDiscarded += HandleCardDiscarded;
            MatchEvents.MoveExecuted += HandleMoveExecuted;
            MatchEvents.LandingResolved += HandleLandingResolved;
            MatchEvents.ConversionResolved += HandleConversionResolved;
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.HandChanged -= HandleHandChanged;
            MatchEvents.CardDiscarded -= HandleCardDiscarded;
            MatchEvents.MoveExecuted -= HandleMoveExecuted;
            MatchEvents.LandingResolved -= HandleLandingResolved;
            MatchEvents.ConversionResolved -= HandleConversionResolved;
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
        public void TryDiscardCard_LegalDiscard_ReturnsSuccess()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());

            // WHEN
            CardDiscardResult result = controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            Assert.That(result, Is.EqualTo(CardDiscardResult.Success));
        }

        [Test]
        public void TryDiscardCard_LegalDiscard_RotatesTheSlotToTheQueuedNextCard()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId expectedCard);

            // WHEN
            controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardInSlot);
            Assert.That(cardInSlot, Is.EqualTo(expectedCard));
        }

        [Test]
        public void TryDiscardCard_LegalDiscard_ChargesExactlyOnce()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());

            // WHEN
            controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            Assert.That(ledger.PayCalls.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryDiscardCard_LegalDiscard_RaisesCardDiscardedWithTheDiscardedCardAndSlot()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId expectedCard);

            // WHEN
            controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            Assert.That((_lastDiscardedPlayerId, _lastDiscardedCard, _lastDiscardedSlot), Is.EqualTo((ActingPlayerId, expectedCard, 0)));
        }

        [Test]
        public void TryDiscardCard_LegalDiscard_RaisesCardDiscardedAfterHandChanged()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            _eventOrder.Clear();

            // WHEN
            controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            Assert.That(_eventOrder, Is.EqualTo(new[] { HandChangedEventName, CardDiscardedEventName }));
        }

        [Test]
        public void TryDiscardCard_LegalDiscard_RaisesNoBoardEvents()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());

            // WHEN
            controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            Assert.That((_moveExecutedCount, _landingResolvedCount, _conversionResolvedCount), Is.EqualTo((0, 0, 0)));
        }

        [Test]
        public void TryDiscardCard_CalledTwiceInARow_BothSucceedAndBothCharge()
        {
            // GIVEN — no cooldown and no per-match cap by design.
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());

            // WHEN
            CardDiscardResult firstResult = controller.TryDiscardCard(ActingPlayerId, 0);
            CardDiscardResult secondResult = controller.TryDiscardCard(ActingPlayerId, 1);

            // THEN
            Assert.That((firstResult, secondResult, ledger.PayCalls.Count), Is.EqualTo((CardDiscardResult.Success, CardDiscardResult.Success, 2)));
        }

        [Test]
        public void TryDiscardCard_ZeroCycleDepthDeck_TheDiscardedCardReappearsAsNext()
        {
            // GIVEN — BuildDeckPresenter deals from a kit sized to the hand plus the next slot, the zero
            // cycle-depth case DeckStateTests documents for TryAdvanceSlot; this covers it through the controller.
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId discardedCard);

            // WHEN
            controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId nextCard);
            Assert.That(nextCard, Is.EqualTo(discardedCard));
        }

        [Test]
        public void TryDiscardCard_UnknownPlayer_ReturnsUnknownPlayerAndChargesNothing()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            int handChangedBaseline = _handChangedCount;
            int cardDiscardedBaseline = _cardDiscardedCount;

            // WHEN
            CardDiscardResult result = controller.TryDiscardCard(UnknownPlayerId, 0);

            // THEN
            Assert.That(
                (result, ledger.PayCalls.Count, _handChangedCount - handChangedBaseline, _cardDiscardedCount - cardDiscardedBaseline),
                Is.EqualTo((CardDiscardResult.UnknownPlayer, 0, 0, 0))
            );
        }

        [Test]
        public void TryDiscardCard_OutOfRangeSlot_ReturnsSlotOutOfRangeAndLeavesHandCycleAndEnergyUnchanged()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardBefore);
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId nextBefore);

            // WHEN
            CardDiscardResult result = controller.TryDiscardCard(ActingPlayerId, HandSize);

            // THEN
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardAfter);
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId nextAfter);
            Assert.That((result, cardAfter, nextAfter, ledger.PayCalls.Count), Is.EqualTo((CardDiscardResult.SlotOutOfRange, cardBefore, nextBefore, 0)));
        }

        [Test]
        public void TryDiscardCard_InsufficientEnergy_ReturnsInsufficientEnergyAndLeavesHandCycleAndEnergyUnchanged()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger { NextPaymentSucceeds = false };
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardBefore);
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId nextBefore);

            // WHEN
            CardDiscardResult result = controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN — PayAttempts proves the ledger was actually asked and declined, distinct from never being
            // asked at all; PayCalls staying empty is the balance effect of that decline.
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardAfter);
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId nextAfter);
            Assert.That(
                (result, cardAfter, nextAfter, ledger.PayAttempts.Count, ledger.PayCalls.Count, ledger.RefundCalls.Count),
                Is.EqualTo((CardDiscardResult.InsufficientEnergy, cardBefore, nextBefore, 1, 0, 0))
            );
        }

        [Test]
        public void TryDiscardCard_MissingDeployController_ReturnsDeckUnavailableAndLeavesHandCycleAndEnergyUnchanged()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            LogAssert.Expect(LogType.Assert, DeckLogMessages.DiscardDeployControllerMissing);
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, null);
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardBefore);
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId nextBefore);

            // WHEN
            CardDiscardResult result = controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardAfter);
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId nextAfter);
            Assert.That((result, cardAfter, nextAfter, ledger.PayCalls.Count), Is.EqualTo((CardDiscardResult.DeckUnavailable, cardBefore, nextBefore, 0)));
        }

        [Test]
        public void TryDiscardCard_MissingDeckPresenter_ReturnsDeckUnavailableAndChargesNothing()
        {
            // GIVEN
            var ledger = new FakeDiscardLedger();
            LogAssert.Expect(LogType.Assert, DeckLogMessages.DiscardDeckPresenterMissing);
            CardDiscardController controller = BuildDiscardController(null, ledger, BuildBareDeployController());

            // WHEN
            CardDiscardResult result = controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            Assert.That((result, ledger.PayCalls.Count), Is.EqualTo((CardDiscardResult.DeckUnavailable, 0)));
        }

        [Test]
        public void TryDiscardCard_MissingDiscardLedger_ReturnsDeckUnavailableAndLeavesHandCycleUnchanged()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            LogAssert.Expect(LogType.Assert, DeckLogMessages.DiscardLedgerMissing);
            CardDiscardController controller = BuildDiscardController(deckPresenter, null, BuildBareDeployController());
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardBefore);
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId nextBefore);

            // WHEN
            CardDiscardResult result = controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardAfter);
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId nextAfter);
            Assert.That((result, cardAfter, nextAfter), Is.EqualTo((CardDiscardResult.DeckUnavailable, cardBefore, nextBefore)));
        }

        [Test]
        public void TryDiscardCard_LedgerReDealsDuringPayment_ReturnsDeckUnavailableAndRefundsOnce()
        {
            // GIVEN — TryPayForDiscard re-deals the player with a smaller hand before TryAdvanceSlot runs against
            // the original slot index, reaching the branch where the rotation is refused after the charge landed.
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            CardDataSO[] smallKitCards = { CreateCard("small_kit_0", CardType.Troop, 0, null), CreateCard("small_kit_1", CardType.Troop, 0, null) };
            KitDataSO smallKit = ScriptableObject.CreateInstance<KitDataSO>();
            smallKit.SetAuthoredCards(smallKitCards);
            _spawned.Add(smallKit);
            _spawned.Add(smallKitCards[0]);
            _spawned.Add(smallKitCards[1]);
            var ledger = new FakeReDealingDiscardLedger(deckPresenter, smallKit);
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());

            // WHEN
            CardDiscardResult result = controller.TryDiscardCard(ActingPlayerId, HandSize - 1);

            // THEN
            Assert.That((result, ledger.RefundCalls.Count), Is.EqualTo((CardDiscardResult.DeckUnavailable, 1)));
        }

        [Test]
        public void TryDiscardCard_ReentrantFromHandChangedHandler_ReturnsDeckBusyAndLeavesTheReentrantSlotUnchanged()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            deckPresenter.TryGetSlot(ActingPlayerId, 1, out CardId slotOneBefore);
            CardDiscardResult reentrantResult = CardDiscardResult.Success;
            void handleHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard) => reentrantResult = controller.TryDiscardCard(ActingPlayerId, 1);
            MatchEvents.HandChanged += handleHandChanged;

            // WHEN
            CardDiscardResult outerResult = controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            deckPresenter.TryGetSlot(ActingPlayerId, 1, out CardId slotOneAfter);
            Assert.That(
                (outerResult, reentrantResult, slotOneAfter, ledger.PayCalls.Count),
                Is.EqualTo((CardDiscardResult.Success, CardDiscardResult.DeckBusy, slotOneBefore, 1))
            );
        }

        [Test]
        public void TryDiscardCard_ReentrantFromCardDiscardedHandler_ReturnsDeckBusyAndLeavesTheReentrantSlotUnchanged()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            deckPresenter.TryGetSlot(ActingPlayerId, 1, out CardId slotOneBefore);
            CardDiscardResult reentrantResult = CardDiscardResult.Success;
            void handleCardDiscarded(int playerId, CardId card, int slotIndex) => reentrantResult = controller.TryDiscardCard(ActingPlayerId, 1);
            MatchEvents.CardDiscarded += handleCardDiscarded;

            // WHEN
            CardDiscardResult outerResult = controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            deckPresenter.TryGetSlot(ActingPlayerId, 1, out CardId slotOneAfter);
            Assert.That(
                (outerResult, reentrantResult, slotOneAfter, ledger.PayCalls.Count),
                Is.EqualTo((CardDiscardResult.Success, CardDiscardResult.DeckBusy, slotOneBefore, 1))
            );
        }

        [Test]
        public void TryDiscardCard_ReentrantForASecondPlayerFromHandChangedHandler_ReturnsDeckBusyAndLeavesThatPlayerUnchanged()
        {
            // GIVEN — _isDiscarding is a latch on the controller, not per player, so a second player's discard
            // issued from inside the first player's own HandChanged dispatch must be rejected too.
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            deckPresenter.InitializePlayer(SecondPlayerId);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            deckPresenter.TryGetSlot(SecondPlayerId, 0, out CardId secondPlayerCardBefore);
            deckPresenter.TryGetNextCard(SecondPlayerId, out CardId secondPlayerNextBefore);
            CardDiscardResult reentrantResult = CardDiscardResult.Success;
            void handleHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard) => reentrantResult = controller.TryDiscardCard(SecondPlayerId, 0);
            MatchEvents.HandChanged += handleHandChanged;

            // WHEN
            controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            deckPresenter.TryGetSlot(SecondPlayerId, 0, out CardId secondPlayerCardAfter);
            deckPresenter.TryGetNextCard(SecondPlayerId, out CardId secondPlayerNextAfter);
            Assert.That(
                (reentrantResult, secondPlayerCardAfter, secondPlayerNextAfter, ledger.PayCalls.Count),
                Is.EqualTo((CardDiscardResult.DeckBusy, secondPlayerCardBefore, secondPlayerNextBefore, 1))
            );
        }

        [Test]
        public void TryDiscardCard_HandChangedSubscriberThrows_PropagatesTheExceptionAfterRotatingAndChargingButNotPublishingCardDiscarded()
        {
            // GIVEN — HandChanged is dispatched synchronously from inside the rotation, ahead of CardDiscarded, so
            // a throwing subscriber leaves the discard half-committed. This pins that CURRENT behaviour
            // deliberately, rather than changing it.
            DeckPresenter deckPresenter = BuildDeckPresenter(HandSize);
            var ledger = new FakeDiscardLedger();
            CardDiscardController controller = BuildDiscardController(deckPresenter, ledger, BuildBareDeployController());
            deckPresenter.TryGetNextCard(ActingPlayerId, out CardId expectedNext);
            void handleHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard) => throw new InvalidOperationException();
            MatchEvents.HandChanged += handleHandChanged;

            // WHEN
            void discard() => controller.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            Assert.Throws<InvalidOperationException>(discard);
            deckPresenter.TryGetSlot(ActingPlayerId, 0, out CardId cardAfter);
            Assert.That((cardAfter, ledger.PayCalls.Count, _cardDiscardedCount), Is.EqualTo((expectedNext, 1, 0)));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator TryDiscardCard_WhileDeployControllerIsResolving_ReturnsDeckBusy()
        {
            // GIVEN — a legal Protocol play that reaches AbilityResolved while DeployController is still resolving.
            ReentrancyBoard board = BuildReentrancyBoard();
            board.BoardGO.SetActive(true);
            yield return null;

            (DeployController deployController, CardDiscardController discardController, int spellSlotIndex, int discardSlotIndex) = BuildReentrancyControllers(
                board
            );

            CardDiscardResult reentrantResult = CardDiscardResult.Success;
            void handleAbilityResolved(int playerId, AbilityResult result) =>
                reentrantResult = discardController.TryDiscardCard(ActingPlayerId, discardSlotIndex);
            MatchEvents.AbilityResolved += handleAbilityResolved;

            // WHEN
            CardPlayResult playResult = deployController.TryPlayCard(ActingPlayerId, spellSlotIndex, new List<HexCoordinates> { _spellTarget });

            // THEN
            Assert.That((playResult, reentrantResult), Is.EqualTo((CardPlayResult.Success, CardDiscardResult.DeckBusy)));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator TryDiscardCard_AfterAReentrantPlayIsRejectedAsResolverBusy_StillReturnsDeckBusy()
        {
            // GIVEN — a single AbilityResolved handler drives both reentrant calls, so the assertion does not
            // depend on subscriber registration order.
            ReentrancyBoard board = BuildReentrancyBoard();
            board.BoardGO.SetActive(true);
            yield return null;

            (DeployController deployController, CardDiscardController discardController, int spellSlotIndex, int discardSlotIndex) = BuildReentrancyControllers(
                board
            );

            CardPlayResult reentrantPlayResult = CardPlayResult.Success;
            CardDiscardResult reentrantDiscardResult = CardDiscardResult.Success;

            void handleAbilityResolved(int playerId, AbilityResult result)
            {
                reentrantPlayResult = deployController.TryPlayCard(ActingPlayerId, discardSlotIndex, null);
                reentrantDiscardResult = discardController.TryDiscardCard(ActingPlayerId, discardSlotIndex);
            }

            MatchEvents.AbilityResolved += handleAbilityResolved;

            // WHEN
            CardPlayResult outerPlayResult = deployController.TryPlayCard(ActingPlayerId, spellSlotIndex, new List<HexCoordinates> { _spellTarget });

            // THEN
            Assert.That(
                (outerPlayResult, reentrantPlayResult, reentrantDiscardResult),
                Is.EqualTo((CardPlayResult.Success, CardPlayResult.ResolverBusy, CardDiscardResult.DeckBusy))
            );
        }

        private static int FindSlotHoldingCard(DeckPresenter deckPresenter, CardId cardId)
        {
            for (int i = 0; i < HandSize; i++)
            {
                deckPresenter.TryGetSlot(ActingPlayerId, i, out CardId slotCard);

                if (slotCard.Equals(cardId))
                {
                    return i;
                }
            }

            return -1;
        }

        private static CardDataSO CreateCard(string cardId, CardType type, int energyCost, ImpactEffectDefinition[] landingEffects)
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(cardId, cardId, "Test description.", type, energyCost, false, false, false, false, 1, landingEffects);

            return card;
        }

        // Builds the board and the authored cards a reentrancy scenario needs, inactive so the caller controls
        // when Awake runs. Split from BuildReentrancyControllers because the deck and the controllers it builds
        // depend on the board's components having already run their Awake, which needs a yielded frame the
        // caller supplies between the two calls.
        private ReentrancyBoard BuildReentrancyBoard()
        {
            var cardPresenterGO = new GameObject("CardPresenter_Discard_Test");
            cardPresenterGO.SetActive(false);
            CardPresenter cardPresenter = cardPresenterGO.AddComponent<CardPresenter>();
            CardDataSO discardCard = CreateCard("discard_card", CardType.Troop, 0, null);
            CardDataSO spellCard = CreateCard(
                "spell_card",
                CardType.Spell,
                SpellEnergyCost,
                new[] { new ImpactEffectDefinition(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, 1, TargetFilter.All, 1) }
            );
            cardPresenter.SetAuthoredCards(discardCard, spellCard);
            cardPresenterGO.SetActive(true);
            _spawned.Add(cardPresenterGO);
            _spawned.Add(discardCard);
            _spawned.Add(spellCard);

            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(gridLayout);

            var boardGO = new GameObject("Board_Discard_Test");
            boardGO.SetActive(false);
            GridPresenter gridPresenter = boardGO.AddComponent<GridPresenter>();
            UnitPresenter unitPresenter = boardGO.AddComponent<UnitPresenter>();
            var energyLedger = new FakeEnergyLedger();
            unitPresenter.Construct(gridPresenter, energyLedger);
            FuseController fuseController = boardGO.AddComponent<FuseController>();
            fuseController.Construct(unitPresenter);
            AbilityController abilityController = boardGO.AddComponent<AbilityController>();
            abilityController.Construct(gridPresenter, unitPresenter, fuseController);
            gridPresenter.SetGridLayout(gridLayout);
            _spawned.Add(boardGO);

            return new ReentrancyBoard
            {
                BoardGO = boardGO,
                UnitPresenter = unitPresenter,
                AbilityController = abilityController,
                CardPresenter = cardPresenter,
                DiscardCard = discardCard,
                SpellCard = spellCard,
                EnergyLedger = energyLedger,
            };
        }

        // DeckPresenter shuffles the kit per player before dealing, so the spell card's hand slot has to be read
        // back rather than assumed from kit order.
        private (
            DeployController DeployController,
            CardDiscardController DiscardController,
            int SpellSlotIndex,
            int DiscardSlotIndex
        ) BuildReentrancyControllers(ReentrancyBoard board)
        {
            DeckPresenter deckPresenter = BuildDeckPresenterFromKit(
                new[] { board.DiscardCard, board.SpellCard, board.DiscardCard, board.DiscardCard, board.DiscardCard },
                HandSize
            );
            int spellSlotIndex = FindSlotHoldingCard(deckPresenter, board.SpellCard.CardId);
            int discardSlotIndex = spellSlotIndex == 0 ? 1 : 0;

            var deployGO = new GameObject("DeployController_Discard_Test");
            deployGO.SetActive(false);
            DeployController deployController = deployGO.AddComponent<DeployController>();
            deployController.Construct(deckPresenter, board.CardPresenter, board.UnitPresenter, board.AbilityController, board.EnergyLedger);
            deployGO.SetActive(true);
            _spawned.Add(deployGO);

            var discardLedger = new FakeDiscardLedger();
            CardDiscardController discardController = BuildDiscardController(deckPresenter, discardLedger, deployController);

            return (deployController, discardController, spellSlotIndex, discardSlotIndex);
        }

        private DeckPresenter BuildDeckPresenter(int handSize)
        {
            var cards = new CardDataSO[DeckState.GetMinimumKitSize(handSize)];

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = CreateCard($"slot_{i}", CardType.Troop, 0, null);
                _spawned.Add(cards[i]);
            }

            return BuildDeckPresenterFromKit(cards, handSize);
        }

        private DeckPresenter BuildDeckPresenterFromKit(CardDataSO[] kitCards, int handSize)
        {
            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(kitCards);
            _spawned.Add(kit);

            var go = new GameObject("DeckPresenter_Discard_Test");
            go.SetActive(false);
            DeckPresenter presenter = go.AddComponent<DeckPresenter>();
            presenter.SetKit(kit, handSize);
            go.SetActive(true);
            _spawned.Add(go);

            presenter.InitializePlayer(ActingPlayerId);

            return presenter;
        }

        private CardDiscardController BuildDiscardController(DeckPresenter deckPresenter, IDiscardLedger ledger, DeployController deployController)
        {
            var go = new GameObject("CardDiscardController_Test");
            go.SetActive(false);
            CardDiscardController controller = go.AddComponent<CardDiscardController>();
            controller.Construct(deckPresenter, ledger, deployController);
            go.SetActive(true);
            _spawned.Add(go);

            return controller;
        }

        private DeployController BuildBareDeployController()
        {
            var go = new GameObject("DeployController_Bare_Test");
            go.SetActive(false);
            DeployController controller = go.AddComponent<DeployController>();
            go.SetActive(true);
            _spawned.Add(go);

            return controller;
        }

        private void HandleHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard)
        {
            _handChangedCount++;
            _eventOrder.Add(HandChangedEventName);
        }

        private void HandleCardDiscarded(int playerId, CardId card, int slotIndex)
        {
            _cardDiscardedCount++;
            _lastDiscardedPlayerId = playerId;
            _lastDiscardedCard = card;
            _lastDiscardedSlot = slotIndex;
            _eventOrder.Add(CardDiscardedEventName);
        }

        private void HandleMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affected)
        {
            _moveExecutedCount++;
        }

        private void HandleLandingResolved(MoveCommand command, ConversionResult conversions)
        {
            _landingResolvedCount++;
        }

        private void HandleConversionResolved(int playerId, ConversionResult result)
        {
            _conversionResolvedCount++;
        }

        // The board half of a reentrancy scenario, as a named type rather than a tuple: the members spelled out
        // as an explicit tuple exceed the 160-column limit, and .editorconfig mandates explicit types, so `var`
        // is not available to hide the declaration.
        private sealed class ReentrancyBoard
        {
            public GameObject BoardGO { get; set; }

            public UnitPresenter UnitPresenter { get; set; }

            public AbilityController AbilityController { get; set; }

            public CardPresenter CardPresenter { get; set; }

            public CardDataSO DiscardCard { get; set; }

            public CardDataSO SpellCard { get; set; }

            public FakeEnergyLedger EnergyLedger { get; set; }
        }

        private sealed class FakeDiscardLedger : IDiscardLedger
        {
            // Every attempt, successful or refused, so a test can tell "never called" apart from "called and
            // declined" — PayCalls alone cannot, since a refusal never reaches it.
            public List<int> PayAttempts { get; } = new();

            public List<int> PayCalls { get; } = new();

            public List<int> RefundCalls { get; } = new();

            public bool NextPaymentSucceeds { get; set; } = true;

            public bool CanAffordDiscard(int playerId)
            {
                return NextPaymentSucceeds;
            }

            public bool TryPayForDiscard(int playerId)
            {
                PayAttempts.Add(playerId);

                if (!NextPaymentSucceeds)
                {
                    return false;
                }

                PayCalls.Add(playerId);

                return true;
            }

            public void RefundDiscard(int playerId)
            {
                RefundCalls.Add(playerId);
            }
        }

        // Simulates the deck vanishing mid-discard: accepts the payment as the real ledger would, but re-deals
        // the player a smaller hand from inside the charge itself, so the slot TryAdvanceSlot rotates next no
        // longer exists.
        private sealed class FakeReDealingDiscardLedger : IDiscardLedger
        {
            private readonly DeckPresenter _deckPresenter;
            private readonly KitDataSO _smallKit;

            public FakeReDealingDiscardLedger(DeckPresenter deckPresenter, KitDataSO smallKit)
            {
                _deckPresenter = deckPresenter;
                _smallKit = smallKit;
            }

            public List<int> RefundCalls { get; } = new();

            public bool CanAffordDiscard(int playerId)
            {
                return true;
            }

            public bool TryPayForDiscard(int playerId)
            {
                _deckPresenter.SetKit(_smallKit, DeckState.MinHandSize);
                _deckPresenter.InitializePlayer(playerId);

                return true;
            }

            public void RefundDiscard(int playerId)
            {
                RefundCalls.Add(playerId);
            }
        }

        // Permissive on purpose: this fixture exercises the discard path, never troop Energy pricing, so the
        // reentrancy tests that need a legal play never have to seed a balance for one.
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
    }
}
