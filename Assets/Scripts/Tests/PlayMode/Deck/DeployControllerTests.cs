using System;
using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Controllers;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
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
    public class DeployControllerTests
    {
        private const int BoardRadius = 6;
        private const int HandSize = 4;
        private const int ActingPlayerId = 1;
        private const int UnknownPlayerId = 99;
        private const int AnchorUnitId = 1;
        private const int OccupantUnitId = 2;
        private const int FirstSpawnedUnitId = 100;
        private const int TroopEnergyCost = 2;
        private const int SpellEnergyCost = 2;
        private const string TroopCardIdValue = "troop_card";
        private const string SpellCardIdValue = "spell_card";
        private const string NoImpactSpellCardIdValue = "no_impact_spell_card";
        private const string UnknownCardIdValue = "unregistered_card";
        private const string OccupantCardIdValue = "occupant_card";

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _deployTarget = new(1, 0);
        private static readonly HexCoordinates _spellTarget = new(-3, 0);
        private static readonly HexCoordinates _secondSpellTarget = new(-3, 1);

        private readonly List<Object> _spawned = new();

        private CardPresenter _cardPresenter;
        private CardDataSO _troopCard;
        private CardDataSO _spellCard;
        private CardDataSO _noImpactSpellCard;
        private CardDataSO _unknownCard;
        private GameObject _boardGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private FuseController _fuseController;
        private AbilityController _abilityController;
        private FakeEnergyLedger _ledger;
        private int _handChangedCount;

        [SetUp]
        public void SetUp()
        {
            _ledger = new FakeEnergyLedger();

            var cardPresenterGO = new GameObject("CardPresenter_Test");
            cardPresenterGO.SetActive(false);
            _cardPresenter = cardPresenterGO.AddComponent<CardPresenter>();
            _troopCard = CreateCard(TroopCardIdValue, CardType.Troop, TroopEnergyCost, null);
            _spellCard = CreateCard(
                SpellCardIdValue,
                CardType.Spell,
                SpellEnergyCost,
                new[] { new ImpactEffectDefinition(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, 1, TargetFilter.All, 1) }
            );
            _noImpactSpellCard = CreateCard(NoImpactSpellCardIdValue, CardType.Spell, SpellEnergyCost, null);
            // Deliberately left off SetAuthoredCards below: CardPresenter must never resolve it.
            _unknownCard = CreateCard(UnknownCardIdValue, CardType.Troop, TroopEnergyCost, null);
            _cardPresenter.SetAuthoredCards(_troopCard, _spellCard, _noImpactSpellCard);
            cardPresenterGO.SetActive(true);
            _spawned.Add(cardPresenterGO);

            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(_gridLayout);

            _boardGO = new GameObject("DeployController_Board_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _unitPresenter.Construct(_gridPresenter, _ledger);
            _fuseController = _boardGO.AddComponent<FuseController>();
            _fuseController.Construct(_unitPresenter);
            _abilityController = _boardGO.AddComponent<AbilityController>();
            _abilityController.Construct(_gridPresenter, _unitPresenter, _fuseController);
            _gridPresenter.SetGridLayout(_gridLayout);
            _spawned.Add(_boardGO);

            _handChangedCount = 0;
            MatchEvents.HandChanged += HandleHandChanged;
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

        [UnityTest]
        public IEnumerator TryPlayCard_LegalTroopPlay_ReturnsSuccess()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceAnchorUnit();
            DeployController deployController = BuildDeployController(_troopCard);

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.Success));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_LegalTroopPlay_PlacesTheUnitOnTheTarget()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceAnchorUnit();
            DeployController deployController = BuildDeployController(_troopCard);

            // WHEN
            deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(2));
            Assert.That(GetCell(_deployTarget).OccupantUnitId, Is.EqualTo(FirstSpawnedUnitId));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_LegalTroopPlay_ChargesExactlyOnce()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceAnchorUnit();
            DeployController deployController = BuildDeployController(_troopCard);

            // WHEN
            deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That(_ledger.PayCalls.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_LegalTroopPlay_AdvancesTheSlot()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceAnchorUnit();
            DeployController deployController = BuildDeployController(_troopCard);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_LegalProtocolPlay_ReturnsSuccess()
        {
            // GIVEN
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_spellCard);

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _spellTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.Success));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_LegalProtocolPlay_ChargesExactlyOnce()
        {
            // GIVEN
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_spellCard);

            // WHEN
            deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _spellTarget });

            // THEN
            Assert.That(_ledger.PayCalls.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_LegalProtocolPlay_AdvancesTheSlot()
        {
            // GIVEN
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_spellCard);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _spellTarget });

            // THEN
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_UnknownPlayer_ReturnsUnknownPlayer()
        {
            // GIVEN
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_troopCard);

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(UnknownPlayerId, 0, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.UnknownPlayer));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_OutOfRangeSlot_ReturnsSlotOutOfRange()
        {
            // GIVEN
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_troopCard);

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, HandSize, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.SlotOutOfRange));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_CardNotRegisteredWithCardPresenter_ReturnsCardNotFound()
        {
            // GIVEN
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_unknownCard);

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.CardNotFound));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_TroopWithInsufficientEnergy_ReturnsInsufficientEnergyAndPlacesNoUnit()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceAnchorUnit();
            DeployController deployController = BuildDeployController(_troopCard);
            _ledger.NextPaymentSucceeds = false;

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.InsufficientEnergy));
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(1), "Only the anchor unit should be on the board.");
        }

        [UnityTest]
        public IEnumerator TryPlayCard_TroopWithTwoTargets_ReturnsInvalidTargetCountAndChangesNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_troopCard);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _deployTarget, _spellTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.InvalidTargetCount));
            Assert.That(_unitPresenter.ActiveUnits, Is.Empty);
            Assert.That(_ledger.PayCalls, Is.Empty);
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_IllegalDeployTarget_LeavesHandBoardAndEnergyUntouched()
        {
            // GIVEN — no anchor unit placed, so the target is legal but adjacent to no owned territory.
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_troopCard);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.IllegalPlacement));
            Assert.That(_unitPresenter.ActiveUnits, Is.Empty);
            Assert.That(_ledger.PayCalls, Is.Empty);
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_ProtocolWithInsufficientEnergy_ReturnsInsufficientEnergyWithoutResolvingTheSpell()
        {
            // GIVEN — the early return: PlaySpell charges before calling ResolveSpell, so a rejected payment
            // must never reach the board at all.
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_spellCard);
            _ledger.NextPaymentSucceeds = false;
            int handChangedBaseline = _handChangedCount;

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _spellTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.InsufficientEnergy));
            Assert.That(_ledger.RefundCalls, Is.Empty, "Nothing was charged, so there is nothing to refund.");
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_ProtocolRejectedWithBoardUnavailable_RefundsTheExactChargeAndLeavesHandUnchanged()
        {
            // GIVEN
            LogAssert.Expect(LogType.Assert, BoardLogMessages.GridLayoutConfigurationMissing);
            LogAssert.Expect(LogType.Error, BoardLogMessages.GridLayoutConfigurationMissing);
            (DeployController deployController, FakeEnergyLedger ledger, int handChangedBaseline) = BuildIsolatedDeployControllerWithoutGrid(_spellCard);
            LogAssert.Expect(LogType.Error, BoardLogMessages.AbilityBoardUnavailable);
            yield return null;

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _spellTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.BoardUnavailable));
            Assert.That(ledger.PayCalls.Count, Is.EqualTo(1));
            Assert.That(ledger.RefundCalls[0], Is.EqualTo(ledger.PayCalls[0]), "The refund must return exactly what was charged — net zero.");
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_ProtocolRejectedWithCardHasNoImpacts_RefundsTheExactChargeAndLeavesHandUnchanged()
        {
            // GIVEN
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_noImpactSpellCard);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _spellTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.IllegalPlacement));
            Assert.That(_ledger.PayCalls.Count, Is.EqualTo(1));
            Assert.That(_ledger.RefundCalls[0], Is.EqualTo(_ledger.PayCalls[0]), "The refund must return exactly what was charged — net zero.");
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_ProtocolRejectedWithInvalidTargets_RefundsTheExactChargeAndLeavesHandUnchanged()
        {
            // GIVEN — the spell's authored cluster size is one; a second hex fails AbilityResolver.ValidateTargets
            // rather than DeployController's own (empty-list-only) target count check.
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_spellCard);
            int handChangedBaseline = _handChangedCount;

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _spellTarget, _secondSpellTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.IllegalPlacement));
            Assert.That(_ledger.PayCalls.Count, Is.EqualTo(1));
            Assert.That(_ledger.RefundCalls[0], Is.EqualTo(_ledger.PayCalls[0]), "The refund must return exactly what was charged — net zero.");
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TryPlayCard_ReentrantFromAbilityResolvedHandler_ReturnsResolverBusyWithoutChargingOrRotating()
        {
            // GIVEN — the latch is checked, not merely raised; DeployController.TryPlayCard says why.
            yield return ActivateBoard();

            DeployController deployController = BuildDeployController(_spellCard);
            int handChangedBaseline = _handChangedCount;
            CardPlayResult reentrantResult = CardPlayResult.Success;

            void handleAbilityResolved(int playerId, AbilityResult result) =>
                reentrantResult = deployController.TryPlayCard(ActingPlayerId, 1, new List<HexCoordinates> { _secondSpellTarget });

            MatchEvents.AbilityResolved += handleAbilityResolved;

            // WHEN
            CardPlayResult outerResult = deployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _spellTarget });

            // THEN — rejected before the deck, the ledger or AbilityController ever see it, so nothing was
            // charged for the reentrant play and nothing needs refunding.
            Assert.That((outerResult, reentrantResult), Is.EqualTo((CardPlayResult.Success, CardPlayResult.ResolverBusy)));
            Assert.That(_ledger.PayCalls.Count, Is.EqualTo(1));
            Assert.That(_ledger.RefundCalls.Count, Is.EqualTo(0));
            Assert.That(_handChangedCount - handChangedBaseline, Is.EqualTo(1), "Only the outer, successful play should have advanced its slot.");
        }

        [UnityTest]
        public IEnumerator TryPlayCard_SpellReentrantFromATroopLandingAbilityDispatch_ReturnsResolverBusyAndRefundsTheExactCharge()
        {
            // GIVEN — AbilityController._isResolvingAbilities is also raised by HandleLandingResolved, not only
            // by ResolveSpell, so a troop landing with an impact reaches the same latch a nested Protocol play
            // does. ConversionController is added here because AbilityController only reacts to
            // MatchEvents.LandingResolved, and ConversionController is what raises it once a move executes.
            _boardGO.AddComponent<ConversionController>().Construct(_gridPresenter, _unitPresenter);
            yield return ActivateBoard();

            PlaceAnchorUnit();
            CardDataSO troopWithImpact = CreateCard(
                "troop_with_impact",
                CardType.Troop,
                TroopEnergyCost,
                new[] { new ImpactEffectDefinition(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, 1, TargetFilter.All, 1) }
            );
            _spawned.Add(troopWithImpact);
            _cardPresenter.SetAuthoredCards(_troopCard, _spellCard, _noImpactSpellCard, troopWithImpact);
            _cardPresenter.BuildRegistry();

            DeployController troopDeployController = BuildDeployController(troopWithImpact);
            var reentrantLedger = new FakeEnergyLedger();
            DeployController reentrantSpellDeployController = BuildDeployController(_spellCard, reentrantLedger);

            CardPlayResult reentrantResult = CardPlayResult.Success;
            void handleAbilityResolved(int playerId, AbilityResult result) =>
                reentrantResult = reentrantSpellDeployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _spellTarget });
            MatchEvents.AbilityResolved += handleAbilityResolved;
            LogAssert.Expect(LogType.Error, BoardLogMessages.SpellResolveReentered);

            // WHEN
            CardPlayResult outerResult = troopDeployController.TryPlayCard(ActingPlayerId, 0, new List<HexCoordinates> { _deployTarget });

            // THEN
            Assert.That((outerResult, reentrantResult), Is.EqualTo((CardPlayResult.Success, CardPlayResult.ResolverBusy)));
            Assert.That(reentrantLedger.PayCalls.Count, Is.EqualTo(1));
            Assert.That(reentrantLedger.RefundCalls[0], Is.EqualTo(reentrantLedger.PayCalls[0]), "The refund must return exactly what was charged — net zero.");
        }

        [UnityTest]
        [Category("Allocation")]
        public IEnumerator TryPlayCard_RepeatedRejectionsOfTheSameCard_AllocatesNoManagedMemoryAfterTheDefinitionIsMemoized()
        {
            // GIVEN — a target hex already occupied keeps every rejection at TargetOccupied, short of the
            // spawner and the ledger, while still exercising slot read, card resolve, CardDefinition lookup and
            // validation: exactly the allocation claim DeployController.GetCardDefinition documents.
            yield return ActivateBoard();

            var occupant = new GridUnit(OccupantUnitId, ActingPlayerId, new CardId(OccupantCardIdValue), _deployTarget);
            Assert.That(_unitPresenter.RegisterUnit(occupant, null), Is.True, "Test setup expects the occupant to register.");
            DeployController deployController = BuildDeployController(_troopCard);
            var targets = new List<HexCoordinates> { _deployTarget };
            deployController.TryPlayCard(ActingPlayerId, 0, targets); // Warm-up: builds and caches the CardDefinition.
            deployController.TryPlayCard(ActingPlayerId, 0, targets); // Warm-up: excludes JIT allocation from the measurement.

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                deployController.TryPlayCard(ActingPlayerId, 0, targets);
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(allocatedAfter - allocatedBefore, Is.EqualTo(0), "TryPlayCard allocated memory on a hot path after the CardDefinition was memoized!");
        }

        private static CardDataSO CreateCard(string cardId, CardType type, int energyCost, ImpactEffectDefinition[] landingEffects)
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(cardId, cardId, "Test description.", type, energyCost, false, false, false, false, 1, landingEffects);

            return card;
        }

        private IEnumerator ActivateBoard()
        {
            _boardGO.SetActive(true);
            yield return null;

            _unitPresenter.SetUnitSpawner(new FakeUnitSpawner());
        }

        private void PlaceAnchorUnit()
        {
            var unit = new GridUnit(AnchorUnitId, ActingPlayerId, new CardId(TroopCardIdValue), _origin);
            Assert.That(_unitPresenter.RegisterUnit(unit, null), Is.True, "Test setup expects the anchor unit to register.");
        }

        private DeployController BuildDeployController(CardDataSO card)
        {
            return BuildDeployController(card, _ledger);
        }

        private DeployController BuildDeployController(CardDataSO card, IEnergyLedger ledger)
        {
            DeckPresenter deckPresenter = BuildDeckPresenter(card, HandSize);

            var go = new GameObject("DeployController_Test");
            go.SetActive(false);
            DeployController controller = go.AddComponent<DeployController>();
            controller.Construct(deckPresenter, _cardPresenter, _unitPresenter, _abilityController, ledger);
            go.SetActive(true);
            _spawned.Add(go);

            return controller;
        }

        private (DeployController Controller, FakeEnergyLedger Ledger, int HandChangedBaseline) BuildIsolatedDeployControllerWithoutGrid(CardDataSO card)
        {
            var ledger = new FakeEnergyLedger();

            var boardGO = new GameObject("DeployController_NoGrid_Board_Test");
            boardGO.SetActive(false);
            GridPresenter gridPresenter = boardGO.AddComponent<GridPresenter>();
            UnitPresenter unitPresenter = boardGO.AddComponent<UnitPresenter>();
            unitPresenter.Construct(gridPresenter, ledger);
            FuseController fuseController = boardGO.AddComponent<FuseController>();
            fuseController.Construct(unitPresenter);
            AbilityController abilityController = boardGO.AddComponent<AbilityController>();
            abilityController.Construct(gridPresenter, unitPresenter, fuseController);
            // Deliberately no SetGridLayout: this is the missing-board scenario.
            boardGO.SetActive(true);
            _spawned.Add(boardGO);

            DeckPresenter deckPresenter = BuildDeckPresenter(card, HandSize);
            int handChangedBaseline = _handChangedCount;

            var deployGO = new GameObject("DeployController_NoGrid_Controller_Test");
            deployGO.SetActive(false);
            DeployController controller = deployGO.AddComponent<DeployController>();
            controller.Construct(deckPresenter, _cardPresenter, unitPresenter, abilityController, ledger);
            deployGO.SetActive(true);
            _spawned.Add(deployGO);

            return (controller, ledger, handChangedBaseline);
        }

        private DeckPresenter BuildDeckPresenter(CardDataSO card, int handSize)
        {
            var cards = new CardDataSO[DeckState.GetMinimumKitSize(handSize)];

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = card;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(cards);
            _spawned.Add(kit);

            var go = new GameObject("DeckPresenter_Test");
            go.SetActive(false);
            DeckPresenter presenter = go.AddComponent<DeckPresenter>();
            presenter.SetKit(kit, handSize);
            go.SetActive(true);
            _spawned.Add(go);

            presenter.InitializePlayer(ActingPlayerId);

            return presenter;
        }

        private HexCell GetCell(HexCoordinates coordinates)
        {
            HexGrid grid = _gridPresenter.HexGrid;

            Assert.That(grid, Is.Not.Null, "Test setup expects the grid presenter to have initialized its hex grid.");
            Assert.That(grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test expects {coordinates} to exist on the grid.");

            return cell;
        }

        private void HandleHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard)
        {
            _handChangedCount++;
        }

        private sealed class FakeEnergyLedger : IEnergyLedger
        {
            public List<(int PlayerId, MoveType Type, int UnitEnergyCost)> PayCalls { get; } = new();

            public List<(int PlayerId, MoveType Type, int UnitEnergyCost)> RefundCalls { get; } = new();

            public bool NextPaymentSucceeds { get; set; } = true;

            public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return NextPaymentSucceeds;
            }

            public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                if (!NextPaymentSucceeds)
                {
                    return false;
                }

                PayCalls.Add((playerId, moveType, unitEnergyCost));

                return true;
            }

            public void RefundMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                RefundCalls.Add((playerId, moveType, unitEnergyCost));
            }
        }

        private sealed class FakeUnitSpawner : IUnitSpawner
        {
            private int _nextUnitId = FirstSpawnedUnitId;

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                return new GridUnit(_nextUnitId++, playerId, cardId, at);
            }
        }
    }
}
