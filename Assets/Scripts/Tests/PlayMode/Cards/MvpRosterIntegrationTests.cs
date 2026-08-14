using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Cards
{
    [TestFixture]
    public class MvpRosterIntegrationTests
    {
        private const int BoardRadius = 10;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;
        private const int FirstSpawnedUnitId = 100;
        private const int SubjectAlphaUnitId = 1;
        private const int SubjectAlphaVictimUnitId = 2;
        private const int AcidCrawlerUnitId = 3;
        private const int BioPhalanxUnitId = 4;
        private const int BioPhalanxAttackerUnitId = 5;
        private const int VolatileMassUnitId = 6;
        private const int VolatileMassVictimUnitId = 7;
        private const int CryoVictimUnitId = 8;

        private const float SeededPlayerEnergy = 20f;
        private const float NoEnergyRegen = 0f;
        private const float VolatileMassJumpEnergyCost = 0.5f;
        private const float EnergyTolerance = 0.0001f;

        private static readonly HexCoordinates _subjectAlphaSource = new(3, 0);
        private static readonly HexCoordinates _subjectAlphaCloneTarget = new(4, 0);
        private static readonly HexCoordinates _subjectAlphaVictimCoords = new(5, 0);

        private static readonly HexCoordinates _acidCrawlerSource = new(-3, 0);
        private static readonly HexCoordinates _acidCrawlerJumpTarget = new(-5, 0);

        private static readonly HexCoordinates _bioPhalanxDefenderCoords = new(0, 4);
        private static readonly HexCoordinates _bioPhalanxAttackerSource = new(0, 2);
        private static readonly HexCoordinates _bioPhalanxAttackerCloneTarget = new(0, 3);

        private static readonly HexCoordinates _volatileMassSource = new(0, -3);
        private static readonly HexCoordinates _volatileMassJumpTarget = new(0, -5);
        private static readonly HexCoordinates _volatileMassVictimCoords = new(0, -7);

        private static readonly HexCoordinates _cryoCenter = new(5, -5);
        private static readonly HexCoordinates _cryoAdjacentOne = new(6, -5);
        private static readonly HexCoordinates _cryoAdjacentTwo = new(5, -6);

        private CardDataSO _subjectAlphaData;
        private CardDataSO _acidCrawlerData;
        private CardDataSO _bioPhalanxData;
        private CardDataSO _volatileMassData;
        private CardDataSO _cryoStasisData;

        private GameObject _cardPresenterGO;
        private CardPresenter _cardPresenter;

        private GameObject _boardGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private AbilityController _abilityController;
        private EnergyPresenter _energyPresenter;
        private FakeUnitSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _subjectAlphaData = CreateCardData("subject_alpha", "Subject Alpha", CardType.Troop, 1, true, true, false, false, 1, null);
            _acidCrawlerData = CreateCardData(
                "acid_crawler",
                "Acid Crawler",
                CardType.Troop,
                2,
                true,
                true,
                false,
                false,
                1,
                new[] { new ImpactEffectDefinition(ImpactEffectType.SpawnHazard, StatusType.None, 0, 2, TargetFilter.Self, 0) }
            );
            _bioPhalanxData = CreateCardData("bio_phalanx", "Bio-Phalanx", CardType.Troop, 3, true, true, true, false, 1, null);
            _volatileMassData = CreateCardData(
                "volatile_mass",
                "Volatile Mass",
                CardType.Troop,
                4,
                false,
                true,
                false,
                false,
                2,
                new[] { new ImpactEffectDefinition(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0) }
            );
            _cryoStasisData = CreateCardData(
                "cryo_stasis",
                "Cryo-Stasis",
                CardType.Spell,
                2,
                false,
                false,
                false,
                false,
                1,
                new[] { new ImpactEffectDefinition(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, 1, TargetFilter.All, 3) }
            );

            _cardPresenterGO = new GameObject("CardPresenter_Test");
            _cardPresenterGO.SetActive(false);
            _cardPresenter = _cardPresenterGO.AddComponent<CardPresenter>();
            _cardPresenter.SetAuthoredCards(_subjectAlphaData, _acidCrawlerData, _bioPhalanxData, _volatileMassData, _cryoStasisData);
            _cardPresenterGO.SetActive(true);

            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);

            _boardGO = new GameObject("MvpRosterIntegration_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _energyPresenter = _boardGO.AddComponent<EnergyPresenter>();
            _energyPresenter.InitializePlayer(PlayerOneId, new EnergyConfig(SeededPlayerEnergy, NoEnergyRegen, SeededPlayerEnergy));
            _energyPresenter.InitializePlayer(PlayerTwoId, new EnergyConfig(SeededPlayerEnergy, NoEnergyRegen, SeededPlayerEnergy));
            _unitPresenter.Construct(_energyPresenter);
            _boardGO.AddComponent<ConversionController>();
            _abilityController = _boardGO.AddComponent<AbilityController>();

            _gridPresenter.SetGridLayout(_gridLayout);
            _spawner = new FakeUnitSpawner();
        }

        [TearDown]
        public void TearDown()
        {
            if (_cardPresenterGO != null)
            {
                Object.Destroy(_cardPresenterGO);
            }

            if (_boardGO != null)
            {
                Object.Destroy(_boardGO);
            }

            if (_gridLayout != null)
            {
                Object.Destroy(_gridLayout);
            }

            DestroyCardData(_subjectAlphaData);
            DestroyCardData(_acidCrawlerData);
            DestroyCardData(_bioPhalanxData);
            DestroyCardData(_volatileMassData);
            DestroyCardData(_cryoStasisData);

            // Destroy is deferred to the end of the frame, so the ConversionController this fixture built is
            // still subscribed when the next test arranges its own board. Clearing the bus is what stops the
            // two boards from resolving each other's moves.
            MatchEvents.ResetEvents();
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveRosterSequence_AllFiveMvpCardsInOneMatch_ProducesTheExpectedBoardState()
        {
            // GIVEN
            yield return ActivateBoardCo();

            CardDefinition subjectAlpha = ResolveDefinition("subject_alpha");
            CardDefinition acidCrawler = ResolveDefinition("acid_crawler");
            CardDefinition bioPhalanx = ResolveDefinition("bio_phalanx");
            CardDefinition volatileMass = ResolveDefinition("volatile_mass");
            CardDefinition cryoStasis = ResolveDefinition("cryo_stasis");

            RegisterUnit(SubjectAlphaUnitId, PlayerOneId, _subjectAlphaSource, subjectAlpha);
            GridUnit subjectAlphaVictim = RegisterUnit(SubjectAlphaVictimUnitId, PlayerTwoId, _subjectAlphaVictimCoords, subjectAlpha);
            RegisterUnit(AcidCrawlerUnitId, PlayerOneId, _acidCrawlerSource, acidCrawler);
            GridUnit bioPhalanxDefender = RegisterUnit(BioPhalanxUnitId, PlayerOneId, _bioPhalanxDefenderCoords, bioPhalanx);
            RegisterUnit(BioPhalanxAttackerUnitId, PlayerTwoId, _bioPhalanxAttackerSource, subjectAlpha);
            RegisterUnit(VolatileMassUnitId, PlayerTwoId, _volatileMassSource, volatileMass);
            GridUnit volatileMassVictim = RegisterUnit(VolatileMassVictimUnitId, PlayerOneId, _volatileMassVictimCoords, subjectAlpha);
            GridUnit cryoVictim = RegisterUnit(CryoVictimUnitId, PlayerOneId, _cryoAdjacentOne, subjectAlpha);

            var cryoTargets = new List<HexCoordinates> { _cryoCenter, _cryoAdjacentOne, _cryoAdjacentTwo };

            // WHEN
            // Step 1 (Subject Alpha): Clones; its radius-1 conversion resolves against the adjacent victim.
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Clone, _subjectAlphaSource, _subjectAlphaCloneTarget, PlayerOneId, SubjectAlphaUnitId));

            // Step 2 (Acid Crawler): Jumps, leaving a hazard on the hex it vacated.
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _acidCrawlerSource, _acidCrawlerJumpTarget, PlayerOneId, AcidCrawlerUnitId));

            // Step 3 (Bio-Phalanx): a Clone lands beside it; its armor absorbs the attempt instead of flipping.
            _unitPresenter.ResolveMove(
                new MoveCommand(MoveType.Clone, _bioPhalanxAttackerSource, _bioPhalanxAttackerCloneTarget, PlayerTwoId, BioPhalanxAttackerUnitId)
            );

            // Step 4 (Volatile Mass): Jumps — radius-2 conversion resolves, then the acting unit self-destructs.
            float playerTwoEnergyBeforeVolatileMassJump = _energyPresenter.GetEnergy(PlayerTwoId);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _volatileMassSource, _volatileMassJumpTarget, PlayerTwoId, VolatileMassUnitId));

            // Between steps: the self-destruct must remove only Volatile Mass's own unit. Without this check, a
            // wrong id here would surface only as a failure in the Cryo-Stasis assertions below, misattributing
            // this step's defect to the next one.
            Assert.That(
                _unitPresenter.ActiveUnits.ContainsKey(CryoVictimUnitId),
                Is.True,
                "Volatile Mass's self-destruct must not have disturbed the unit reserved for the Cryo-Stasis step."
            );

            // Step 5 (Cryo-Stasis): freezes a 3-hex cluster that includes the reserved victim.
            _abilityController.ResolveSpell(new SpellCommand(PlayerTwoId, new CardId("cryo_stasis"), cryoTargets), cryoStasis);

            // THEN
            Assert.That(subjectAlphaVictim.PlayerId, Is.EqualTo(PlayerOneId), "Subject Alpha's radius-1 conversion should have flipped its adjacent victim.");

            HexCell acidCrawlerHazardCell = GetCell(_acidCrawlerSource);
            Assert.That(acidCrawlerHazardCell.HasHazard, Is.True, "Acid Crawler should have left a hazard on the hex it vacated.");
            Assert.That(
                acidCrawlerHazardCell.Hazard.RemainingDuration,
                Is.EqualTo(2),
                "Nothing later in this sequence deploys as the hazard's owner again, so its authored duration must be untouched."
            );

            Assert.That(
                (bioPhalanxDefender.HasArmor, bioPhalanxDefender.PlayerId),
                Is.EqualTo((false, PlayerOneId)),
                "Bio-Phalanx's armor should absorb the attempt rather than flipping ownership."
            );

            Assert.That(
                _unitPresenter.ActiveUnits.ContainsKey(VolatileMassUnitId),
                Is.False,
                "Volatile Mass's self-destruct should have removed the acting unit."
            );
            Assert.That(volatileMassVictim.PlayerId, Is.EqualTo(PlayerTwoId), "Volatile Mass's radius-2 conversion should have flipped the distant victim.");
            Assert.That(
                playerTwoEnergyBeforeVolatileMassJump - _energyPresenter.GetEnergy(PlayerTwoId),
                Is.EqualTo(VolatileMassJumpEnergyCost).Within(EnergyTolerance),
                "A Jump charges the flat Jump cost regardless of the acting unit's own Energy cost."
            );

            Assert.That(cryoVictim.HasStatus(StatusType.Frozen), Is.True, "Cryo-Stasis should have frozen the unit inside its cluster.");
            Assert.That(
                cryoVictim.ReceiveConversionAttempt(PlayerTwoId),
                Is.EqualTo(ConversionOutcome.Immune),
                "A frozen unit must resist a conversion attempt."
            );
            Assert.That(cryoVictim.PlayerId, Is.EqualTo(PlayerOneId), "Resisting the conversion attempt must leave the frozen unit's owner unchanged.");
        }

        private static CardDataSO CreateCardData(
            string cardId,
            string displayName,
            CardType type,
            int energyCost,
            bool canClone,
            bool canJump,
            bool hasArmor,
            bool ignoresHazards,
            int conversionRadius,
            ImpactEffectDefinition[] landingEffects
        )
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(
                cardId,
                displayName,
                "Test description.",
                type,
                energyCost,
                canClone,
                canJump,
                hasArmor,
                ignoresHazards,
                conversionRadius,
                landingEffects
            );

            return card;
        }

        private static void DestroyCardData(CardDataSO card)
        {
            if (card != null)
            {
                Object.Destroy(card);
            }
        }

        private IEnumerator ActivateBoardCo()
        {
            _boardGO.SetActive(true);
            yield return null;

            _unitPresenter.SetUnitSpawner(_spawner);
        }

        private CardDefinition ResolveDefinition(string cardIdValue)
        {
            bool found = _cardPresenter.TryGetCard(new CardId(cardIdValue), out ICardData cardData);
            Assert.That(found, Is.True, $"Test setup expects the CardPresenter to resolve '{cardIdValue}'.");

            return new CardDefinition(cardData);
        }

        private GridUnit RegisterUnit(int unitId, int playerId, HexCoordinates position, CardDefinition definition)
        {
            var unit = new GridUnit(unitId, playerId, definition.CardId, position, definition.HasArmor);
            Assert.That(_unitPresenter.RegisterUnit(unit, definition), Is.True, $"Test setup expects unit {unitId} to register at {position}.");

            return unit;
        }

        private HexCell GetCell(HexCoordinates coordinates)
        {
            HexGrid grid = _gridPresenter.HexGrid;

            Assert.That(grid, Is.Not.Null, "Test setup expects the grid presenter to have initialized its hex grid.");
            Assert.That(grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test expects {coordinates} to exist on the grid.");

            return cell;
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
