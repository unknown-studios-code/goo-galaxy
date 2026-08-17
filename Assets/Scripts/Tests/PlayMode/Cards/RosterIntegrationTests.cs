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
    // Drives each roster card end to end through the real controllers, rather than through a resolver in
    // isolation: the card asset resolves to a definition, the definition registers a unit, and the move goes
    // through UnitPresenter so conversion, abilities and Energy all run in their production order.
    // Every test arranges only the units its own card needs, on coordinates far enough apart that no two cards
    // reach each other. That separation is what lets a failure name one card instead of a sequence.
    [TestFixture]
    public class RosterIntegrationTests
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
        private const int VolatileMassAnchorUnitId = 6;
        private const int VolatileMassVictimUnitId = 7;
        private const int CryoVictimUnitId = 8;

        private const float SeededPlayerEnergy = 20f;
        private const float NoEnergyRegen = 0f;
        private const float VolatileMassJumpEnergyCost = 0.5f;
        private const int VolatileMassFuseDurationInSeconds = 3;
        private const int AcidCrawlerHazardDuration = 2;
        private const float EnergyTolerance = 0.0001f;

        private static readonly HexCoordinates _subjectAlphaSource = new(3, 0);
        private static readonly HexCoordinates _subjectAlphaCloneTarget = new(4, 0);
        private static readonly HexCoordinates _subjectAlphaVictimCoords = new(5, 0);

        private static readonly HexCoordinates _acidCrawlerSource = new(-3, 0);
        private static readonly HexCoordinates _acidCrawlerJumpTarget = new(-5, 0);

        private static readonly HexCoordinates _bioPhalanxDefenderCoords = new(0, 4);
        private static readonly HexCoordinates _bioPhalanxAttackerSource = new(0, 2);
        private static readonly HexCoordinates _bioPhalanxAttackerCloneTarget = new(0, 3);

        // Volatile Mass cannot Clone, so the hex it occupies is the one its Deploy landed on. The Jump target sits
        // exactly the default two hexes away, and the victim a further two from that target — on the outer edge
        // of the card's radius-2 reach, which is the whole point of putting it there. At distance one it would be
        // converted by a radius-1 card too and the assertion would stop testing the expanded radius.
        private static readonly HexCoordinates _volatileMassDeployHex = new(0, -3);
        private static readonly HexCoordinates _volatileMassAnchorCoords = new(1, -3);
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
        private FuseController _fuseController;
        private EnergyPresenter _energyPresenter;
        private FakeUnitSpawner _spawner;
        private int _volatileMassUnitId;

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
                new[] { new ImpactEffectDefinition(ImpactEffectType.SpawnHazard, StatusType.None, 0, AcidCrawlerHazardDuration, TargetFilter.Self, 0) }
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
                new[]
                {
                    new ImpactEffectDefinition(
                        ImpactEffectType.ArmFuse,
                        StatusType.None,
                        0,
                        VolatileMassFuseDurationInSeconds,
                        TargetFilter.Self,
                        0,
                        ImpactDurationUnit.Seconds
                    ),
                }
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

            _boardGO = new GameObject("RosterIntegration_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _energyPresenter = _boardGO.AddComponent<EnergyPresenter>();
            _energyPresenter.InitializePlayer(PlayerOneId, new EnergyConfig(SeededPlayerEnergy, NoEnergyRegen, SeededPlayerEnergy));
            _energyPresenter.InitializePlayer(PlayerTwoId, new EnergyConfig(SeededPlayerEnergy, NoEnergyRegen, SeededPlayerEnergy));
            _unitPresenter.Construct(_gridPresenter, _energyPresenter);
            _boardGO.AddComponent<ConversionController>().Construct(_gridPresenter, _unitPresenter);
            _fuseController = _boardGO.AddComponent<FuseController>();
            _fuseController.Construct(_unitPresenter);
            _abilityController = _boardGO.AddComponent<AbilityController>();
            _abilityController.Construct(_gridPresenter, _unitPresenter, _fuseController);

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
        public IEnumerator ResolveMove_SubjectAlphaClone_FlipsTheAdjacentVictim()
        {
            // GIVEN
            yield return ActivateBoardCo();
            CardDefinition subjectAlpha = ResolveDefinition("subject_alpha");
            RegisterUnit(SubjectAlphaUnitId, PlayerOneId, _subjectAlphaSource, subjectAlpha);
            GridUnit victim = RegisterUnit(SubjectAlphaVictimUnitId, PlayerTwoId, _subjectAlphaVictimCoords, subjectAlpha);

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Clone, _subjectAlphaSource, _subjectAlphaCloneTarget, PlayerOneId, SubjectAlphaUnitId));

            // THEN
            Assert.That(victim.PlayerId, Is.EqualTo(PlayerOneId), "Subject Alpha's radius-1 conversion should have flipped its adjacent victim.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_AcidCrawlerJump_LeavesAHazardOnTheVacatedHex()
        {
            // GIVEN
            yield return ArrangeAcidCrawlerJumpCo();

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _acidCrawlerSource, _acidCrawlerJumpTarget, PlayerOneId, AcidCrawlerUnitId));

            // THEN
            Assert.That(GetCell(_acidCrawlerSource).HasHazard, Is.True, "Acid Crawler should have left a hazard on the hex it vacated.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_AcidCrawlerJump_KeepsTheAuthoredHazardDuration()
        {
            // GIVEN
            yield return ArrangeAcidCrawlerJumpCo();

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _acidCrawlerSource, _acidCrawlerJumpTarget, PlayerOneId, AcidCrawlerUnitId));

            // THEN
            Assert.That(
                GetCell(_acidCrawlerSource).Hazard.RemainingDuration,
                Is.EqualTo(AcidCrawlerHazardDuration),
                "Nothing here deploys as the hazard's owner again, so its authored duration must be untouched."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_CloneBesideBioPhalanx_SpendsItsArmorInsteadOfFlippingOwnership()
        {
            // GIVEN
            yield return ActivateBoardCo();
            CardDefinition subjectAlpha = ResolveDefinition("subject_alpha");
            CardDefinition bioPhalanx = ResolveDefinition("bio_phalanx");
            GridUnit defender = RegisterUnit(BioPhalanxUnitId, PlayerOneId, _bioPhalanxDefenderCoords, bioPhalanx);
            RegisterUnit(BioPhalanxAttackerUnitId, PlayerTwoId, _bioPhalanxAttackerSource, subjectAlpha);

            // WHEN
            _unitPresenter.ResolveMove(
                new MoveCommand(MoveType.Clone, _bioPhalanxAttackerSource, _bioPhalanxAttackerCloneTarget, PlayerTwoId, BioPhalanxAttackerUnitId)
            );

            // THEN
            Assert.That(
                (defender.HasArmor, defender.PlayerId),
                Is.EqualTo((false, PlayerOneId)),
                "Bio-Phalanx's armor should absorb the attempt rather than flipping ownership."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveDeploy_VolatileMass_LeavesTheActingUnitOnTheBoard()
        {
            // GIVEN
            yield return ArrangeVolatileMassPrerequisitesCo();

            // WHEN
            DeployVolatileMass();

            // THEN
            Assert.That(
                _unitPresenter.ActiveUnits.ContainsKey(_volatileMassUnitId),
                Is.True,
                "Volatile Mass's deploy landing should have left the unit on the board rather than removing it."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveDeploy_VolatileMass_ArmsTheFuse()
        {
            // GIVEN
            yield return ArrangeVolatileMassPrerequisitesCo();

            // WHEN
            DeployVolatileMass();

            // THEN
            Assert.That(
                _unitPresenter.ActiveUnits[_volatileMassUnitId].HasFuse,
                Is.True,
                "Volatile Mass's deploy landing should have armed its fuse instead of destroying the unit immediately."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_VolatileMassJumpAfterDeploy_RemovesTheActingUnit()
        {
            // GIVEN
            yield return ArrangeVolatileMassPrerequisitesCo();
            DeployVolatileMass();

            // WHEN
            ResolveVolatileMassJump();

            // THEN
            Assert.That(
                _unitPresenter.ActiveUnits.ContainsKey(_volatileMassUnitId),
                Is.False,
                "Volatile Mass's fuse detonation should have removed the acting unit."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_VolatileMassJumpAfterDeploy_FlipsTheVictimTwoRingsOut()
        {
            // GIVEN
            yield return ArrangeVolatileMassPrerequisitesCo();
            GridUnit victim = _unitPresenter.ActiveUnits[VolatileMassVictimUnitId];
            DeployVolatileMass();

            // WHEN
            ResolveVolatileMassJump();

            // THEN
            Assert.That(victim.PlayerId, Is.EqualTo(PlayerTwoId), "Volatile Mass's radius-2 conversion should have flipped the distant victim.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_VolatileMassJumpAfterDeploy_ChargesTheFlatJumpCost()
        {
            // GIVEN
            yield return ArrangeVolatileMassPrerequisitesCo();
            DeployVolatileMass();
            float energyBeforeJump = _energyPresenter.GetEnergy(PlayerTwoId);

            // WHEN
            ResolveVolatileMassJump();

            // THEN
            Assert.That(
                energyBeforeJump - _energyPresenter.GetEnergy(PlayerTwoId),
                Is.EqualTo(VolatileMassJumpEnergyCost).Within(EnergyTolerance),
                "A Jump charges the flat Jump cost regardless of the acting unit's own Energy cost."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_CryoStasisCluster_FreezesAUnitInsideIt()
        {
            // GIVEN
            yield return ArrangeCryoVictimCo();
            GridUnit victim = _unitPresenter.ActiveUnits[CryoVictimUnitId];

            // WHEN
            ResolveCryoStasisOnCluster();

            // THEN
            Assert.That(victim.HasStatus(StatusType.Frozen), Is.True, "Cryo-Stasis should have frozen the unit inside its cluster.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_CryoStasisCluster_LeavesTheFrozenUnitImmuneToConversion()
        {
            // GIVEN
            yield return ArrangeCryoVictimCo();
            GridUnit victim = _unitPresenter.ActiveUnits[CryoVictimUnitId];
            ResolveCryoStasisOnCluster();

            // WHEN
            ConversionOutcome outcome = victim.ReceiveConversionAttempt(PlayerTwoId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.Immune), "A frozen unit must resist a conversion attempt.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_CryoStasisCluster_LeavesTheFrozenUnitsOwnerUnchanged()
        {
            // GIVEN
            yield return ArrangeCryoVictimCo();
            GridUnit victim = _unitPresenter.ActiveUnits[CryoVictimUnitId];
            ResolveCryoStasisOnCluster();

            // WHEN
            victim.ReceiveConversionAttempt(PlayerTwoId);

            // THEN
            Assert.That(victim.PlayerId, Is.EqualTo(PlayerOneId), "Resisting the conversion attempt must leave the frozen unit's owner unchanged.");
        }

        private static CardDataSO CreateCardData(
            string cardId,
            string displayName,
            CardType type,
            int energyCost,
            bool canClone,
            bool canJump,
            bool hasArmor,
            bool canIgnoreHazards,
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
                canIgnoreHazards,
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

        private IEnumerator ArrangeAcidCrawlerJumpCo()
        {
            yield return ActivateBoardCo();

            RegisterUnit(AcidCrawlerUnitId, PlayerOneId, _acidCrawlerSource, ResolveDefinition("acid_crawler"));
        }

        private IEnumerator ArrangeVolatileMassPrerequisitesCo()
        {
            yield return ActivateBoardCo();

            // The anchor is what makes the deploy below legal: ValidateDeploy requires a hex adjacent to the
            // target to already hold a unit the deploying player owns.
            RegisterUnit(VolatileMassAnchorUnitId, PlayerTwoId, _volatileMassAnchorCoords, ResolveDefinition("subject_alpha"));
            RegisterUnit(VolatileMassVictimUnitId, PlayerOneId, _volatileMassVictimCoords, ResolveDefinition("subject_alpha"));
        }

        private IEnumerator ArrangeCryoVictimCo()
        {
            yield return ActivateBoardCo();

            RegisterUnit(CryoVictimUnitId, PlayerOneId, _cryoAdjacentOne, ResolveDefinition("subject_alpha"));
        }

        private void DeployVolatileMass()
        {
            CardDefinition volatileMass = ResolveDefinition("volatile_mass");
            var command = MoveCommand.ForDeploy(_volatileMassDeployHex, PlayerTwoId);
            MovementResult result = _unitPresenter.ResolveDeploy(in command, new CardId("volatile_mass"), volatileMass);

            Assert.That(result, Is.EqualTo(MovementResult.Success), "Test setup expects Volatile Mass to deploy legally next to its anchor unit.");

            _volatileMassUnitId = GetCell(_volatileMassDeployHex).OccupantUnitId;
        }

        private void ResolveVolatileMassJump()
        {
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _volatileMassDeployHex, _volatileMassJumpTarget, PlayerTwoId, _volatileMassUnitId));
        }

        private void ResolveCryoStasisOnCluster()
        {
            var targets = new List<HexCoordinates> { _cryoCenter, _cryoAdjacentOne, _cryoAdjacentTwo };

            _abilityController.ResolveSpell(new SpellCommand(PlayerTwoId, new CardId("cryo_stasis"), targets), ResolveDefinition("cryo_stasis"));
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
