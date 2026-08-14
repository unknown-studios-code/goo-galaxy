using System;
using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Board
{
    [TestFixture]
    public class AbilityControllerTests
    {
        private const int BoardRadius = 8;
        private const int ActingPlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int ActingUnitId = 1;
        private const int EnemyUnitId = 2;
        private const int FriendlyUnitId = 3;
        private const int ThirdUnitId = 4;
        private const int ProbeUnitId = 5;
        private const int FourthUnitId = 6;
        private const int FreezeDuration = 1;
        private const int HazardDuration = 2;
        private const int RivalHazardDuration = 5;
        private const int FirstSpawnedUnitId = 100;
        private const string SourceCardIdValue = "acid_crawler";

        private static readonly HexCoordinates _volatileSource = new(-2, 0);
        private static readonly HexCoordinates _volatileTarget = new(0, 0);
        private static readonly HexCoordinates _volatileEnemyCoords = new(2, 0);

        private static readonly HexCoordinates _orderSource = new(-2, 0);
        private static readonly HexCoordinates _orderTarget = new(0, 0);
        private static readonly HexCoordinates _orderEnemyCoords = new(1, 0);

        private static readonly HexCoordinates _emptySource = new(-2, 0);
        private static readonly HexCoordinates _emptyTarget = new(0, 0);

        private static readonly HexCoordinates _cryoSource = new(-2, 0);
        private static readonly HexCoordinates _cryoTarget = new(0, 0);
        private static readonly HexCoordinates _cryoFriendlyCoords = new(1, 0);
        private static readonly HexCoordinates _cryoThawStart = new(3, 0);
        private static readonly HexCoordinates _cryoThawAlt = new(5, 0);

        private static readonly HexCoordinates _acidSource = new(-2, 0);
        private static readonly HexCoordinates _acidTarget = new(0, 0);
        private static readonly HexCoordinates _acidProbeStart = new(-3, 0);
        private static readonly HexCoordinates _acidTickStart = new(2, 0);
        private static readonly HexCoordinates _acidTickAlt = new(4, 0);

        private static readonly HexCoordinates _selfFreezeSource = new(-2, 0);
        private static readonly HexCoordinates _selfFreezeTarget = new(0, 0);

        private static readonly HexCoordinates _thawAttackerSource = new(-2, 0);
        private static readonly HexCoordinates _thawAttackerTarget = new(0, 0);
        private static readonly HexCoordinates _thawEnemyCoords = new(1, 0);
        private static readonly HexCoordinates _thawDefenderStart = new(5, 0);
        private static readonly HexCoordinates _thawDefenderAlt = new(7, 0);

        private static readonly HexCoordinates _uncapableSource = new(-2, 0);
        private static readonly HexCoordinates _uncapableTarget = new(0, 0);

        private static readonly HexCoordinates _hazardOverwriteHex = new(0, 0);
        private static readonly HexCoordinates _hazardOverwriteFirstLanding = new(2, 0);
        private static readonly HexCoordinates _hazardOverwriteTickStart = new(4, 0);
        private static readonly HexCoordinates _hazardOverwriteTickAlt = new(6, 0);
        private static readonly HexCoordinates _hazardOverwriteSecondLanding = new(0, 2);

        private static readonly HexCoordinates _untouchedHazardHex = new(0, 0);
        private static readonly HexCoordinates _untouchedHazardFirstLanding = new(2, 0);
        private static readonly HexCoordinates _untouchedHazardTickStart = new(4, 0);
        private static readonly HexCoordinates _untouchedHazardTickAlt = new(6, 0);

        private static readonly HexCoordinates _gridResetHazardHex = new(0, 0);
        private static readonly HexCoordinates _gridResetFirstLanding = new(2, 0);
        private static readonly HexCoordinates _gridResetTickStart = new(4, 0);
        private static readonly HexCoordinates _gridResetTickAlt = new(6, 0);

        private static readonly HexCoordinates _unknownEffectStart = new(0, 0);
        private static readonly HexCoordinates _unknownEffectTarget = new(2, 0);

        private static readonly HexCoordinates _neverLandedTarget = new(7, 0);

        private static readonly HexCoordinates _spellCenter = new(0, 0);
        private static readonly HexCoordinates _spellAdjacentOne = new(1, 0);
        private static readonly HexCoordinates _spellAdjacentTwo = new(0, -1);
        private static readonly HexCoordinates _spellFarSource = new(-2, 0);
        private static readonly HexCoordinates _spellFarTarget = new(6, 0);

        private static readonly HexCoordinates _cloneHazardSource = new(-2, 0);
        private static readonly HexCoordinates _cloneHazardTarget = new(-1, 0);

        private readonly List<string> _eventOrder = new();

        private GameObject _boardGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private AbilityController _abilityController;
        private FakeUnitSpawner _spawner;

        private GridUnit _lastActingUnit;
        private GridUnit _lastEnemyUnit;
        private GridUnit _lastFriendlyUnit;

        private int _conversionResolvedCallCount;
        private int _landingResolvedCallCount;
        private int _abilityResolvedCallCount;

        [SetUp]
        public void SetUp()
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);

            _boardGO = new GameObject("AbilityController_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _unitPresenter.Construct(_gridPresenter, new FakeEnergyLedger());
            _boardGO.AddComponent<ConversionController>().Construct(_gridPresenter, _unitPresenter);
            _abilityController = _boardGO.AddComponent<AbilityController>();
            _abilityController.Construct(_gridPresenter, _unitPresenter);

            _gridPresenter.SetGridLayout(_gridLayout);
            _spawner = new FakeUnitSpawner();

            _eventOrder.Clear();
            _conversionResolvedCallCount = 0;
            _landingResolvedCallCount = 0;
            _abilityResolvedCallCount = 0;

            MatchEvents.ConversionResolved += HandleConversionResolved;
            MatchEvents.LandingResolved += HandleLandingResolved;
            MatchEvents.AbilityResolved += HandleAbilityResolved;
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ConversionResolved -= HandleConversionResolved;
            MatchEvents.LandingResolved -= HandleLandingResolved;
            MatchEvents.AbilityResolved -= HandleAbilityResolved;
            MatchEvents.ResetEvents();

            if (_boardGO != null)
            {
                Object.Destroy(_boardGO);
            }

            if (_gridLayout != null)
            {
                Object.Destroy(_gridLayout);
            }
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_VolatileMassJump_ConvertsTheEnemyAtRadiusTwo()
        {
            // GIVEN / WHEN
            yield return ArrangeAndExecuteVolatileMassJumpAsync();

            // THEN
            Assert.That(_lastEnemyUnit.PlayerId, Is.EqualTo(ActingPlayerId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_VolatileMassJump_LeavesTheActingUnitDead()
        {
            // GIVEN / WHEN
            yield return ArrangeAndExecuteVolatileMassJumpAsync();

            // THEN
            Assert.That(_lastActingUnit.IsAlive, Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_VolatileMassJump_LeavesTheLandingCellUnoccupied()
        {
            // GIVEN / WHEN
            yield return ArrangeAndExecuteVolatileMassJumpAsync();

            // THEN
            HexCell landingCell = GetCell(_volatileTarget);
            Assert.That(landingCell.IsOccupied, Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_VolatileMassJump_RemovesTheActingUnitFromActiveUnits()
        {
            // GIVEN / WHEN
            yield return ArrangeAndExecuteVolatileMassJumpAsync();

            // THEN
            Assert.That(_unitPresenter.ActiveUnits.ContainsKey(_lastActingUnit.UnitId), Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_VolatileMassJump_PublishesAbilityResolved()
        {
            // GIVEN / WHEN
            yield return ArrangeAndExecuteVolatileMassJumpAsync();

            // THEN
            Assert.That(_abilityResolvedCallCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_LandingConvertsAndResolvesAnAbility_RaisesEventsInTheDocumentedOrder()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            var capability = new FakeCapability
            {
                CanJump = true,
                ConversionRadius = 1,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, FreezeDuration, TargetFilter.Self, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _orderSource, capability);
            RegisterUnitAt(EnemyUnitId, RivalPlayerId, _orderEnemyCoords, new FakeCapability());
            var command = new MoveCommand(MoveType.Jump, _orderSource, _orderTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(_eventOrder, Is.EqualTo(new[] { "ConversionResolved", "LandingResolved", "AbilityResolved" }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_JumpIntoEmptySpace_StillRaisesLandingResolvedAndAbilityResolved()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            var capability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, FreezeDuration, TargetFilter.Self, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _emptySource, capability);
            var command = new MoveCommand(MoveType.Jump, _emptySource, _emptyTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(_landingResolvedCallCount, Is.EqualTo(1));
            Assert.That(_abilityResolvedCallCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_JumpIntoEmptySpace_NeverRaisesConversionResolved()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            var capability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, FreezeDuration, TargetFilter.Self, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _emptySource, capability);
            var command = new MoveCommand(MoveType.Jump, _emptySource, _emptyTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(_conversionResolvedCallCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator LandingResolved_CryoStasisEffect_FreezesTheFriendlyUnitInRange()
        {
            // GIVEN / WHEN
            yield return ArrangeCryoStasisLandingAsync();

            // THEN
            Assert.That(_lastFriendlyUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_FrozenUnitAttemptsToMove_ReturnsSourceFrozen()
        {
            // GIVEN
            yield return ArrangeCryoStasisLandingAsync();
            var command = new MoveCommand(MoveType.Jump, _cryoFriendlyCoords, _cryoThawStart, ActingPlayerId, FriendlyUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceFrozen));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ReceiveConversionAttempt_FrozenUnit_ReturnsImmune()
        {
            // GIVEN
            yield return ArrangeCryoStasisLandingAsync();

            // WHEN
            ConversionOutcome outcome = _lastFriendlyUnit.ReceiveConversionAttempt(RivalPlayerId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.Immune));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_AfterTheAffectedPlayersNextDeployment_TheFrozenUnitCanMoveAndBeConvertedAgain()
        {
            // GIVEN
            yield return ArrangeCryoStasisLandingAsync();
            RegisterUnitAt(ThirdUnitId, ActingPlayerId, _cryoThawStart, new FakeCapability { CanJump = true });
            var thawCommand = new MoveCommand(MoveType.Jump, _cryoThawStart, _cryoThawAlt, ActingPlayerId, ThirdUnitId);

            // WHEN
            _unitPresenter.ResolveMove(thawCommand);

            // THEN
            Assert.That(_lastFriendlyUnit.HasStatus(StatusType.Frozen), Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator LandingResolved_SelfAppliedFreezeEffect_RemainsActiveImmediatelyAfterItsOwnLanding()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            var capability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, FreezeDuration, TargetFilter.Self, 0) },
            };
            GridUnit actingUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _selfFreezeSource, capability);
            var command = new MoveCommand(MoveType.Jump, _selfFreezeSource, _selfFreezeTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(
                actingUnit.HasStatus(StatusType.Frozen),
                Is.True,
                "The exemption list must protect a self-applied status from being ticked by its own landing."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_AcidCrawlerJump_RejectsAMoveOntoTheVacatedHexWithTargetHazardous()
        {
            // GIVEN
            yield return ArrangeAcidCrawlerLandingAsync();
            RegisterUnitAt(ProbeUnitId, RivalPlayerId, _acidProbeStart, new FakeCapability { CanClone = true });
            var probeCommand = new MoveCommand(MoveType.Clone, _acidProbeStart, _acidSource, RivalPlayerId, ProbeUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(probeCommand);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.TargetHazardous));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_AcidCrawlerHazard_SurvivesOneOwnerDeployment()
        {
            // GIVEN
            yield return ArrangeAcidCrawlerLandingAsync();
            RegisterUnitAt(ThirdUnitId, ActingPlayerId, _acidTickStart, new FakeCapability { CanJump = true });

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _acidTickStart, _acidTickAlt, ActingPlayerId, ThirdUnitId));

            // THEN
            HexCell vacatedCell = GetCell(_acidSource);
            Assert.That(vacatedCell.HasHazard, Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_AcidCrawlerHazard_IsGoneAfterTheSecondOwnerDeployment()
        {
            // GIVEN
            yield return ArrangeAcidCrawlerLandingAsync();
            RegisterUnitAt(ThirdUnitId, ActingPlayerId, _acidTickStart, new FakeCapability { CanJump = true });
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _acidTickStart, _acidTickAlt, ActingPlayerId, ThirdUnitId));

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _acidTickAlt, _acidTickStart, ActingPlayerId, ThirdUnitId));

            // THEN
            HexCell vacatedCell = GetCell(_acidSource);
            Assert.That(vacatedCell.HasHazard, Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_PlainDeploymentTicksAFrozenUnitOwnedByTheDeployingPlayer_ThawsItWithoutPublishingAbilityResolved()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            var attackerCapability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.Enemy, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _thawAttackerSource, attackerCapability);
            // Armored so the radius-1 standard conversion (step 3) only strips its armor rather than flipping its
            // ownership — otherwise it would no longer be an "enemy" by the time the freeze (step 4) checks for one.
            GridUnit frozenUnit = RegisterUnitAt(EnemyUnitId, RivalPlayerId, _thawEnemyCoords, new FakeCapability(), hasArmor: true);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _thawAttackerSource, _thawAttackerTarget, ActingPlayerId, ActingUnitId));
            Assert.That(frozenUnit.HasStatus(StatusType.Frozen), Is.True, "Test setup expects the Cryo-Stasis landing to have frozen the defender's unit.");

            RegisterUnitAt(ThirdUnitId, RivalPlayerId, _thawDefenderStart, new FakeCapability { CanJump = true });
            int abilityResolvedCallCountBeforePlainDeployment = _abilityResolvedCallCount;
            var plainCommand = new MoveCommand(MoveType.Jump, _thawDefenderStart, _thawDefenderAlt, RivalPlayerId, ThirdUnitId);

            // WHEN
            _unitPresenter.ResolveMove(plainCommand);

            // THEN
            Assert.That(
                _abilityResolvedCallCount,
                Is.EqualTo(abilityResolvedCallCountBeforePlainDeployment),
                "A card with no landing effects must not publish AbilityResolved."
            );
            Assert.That(frozenUnit.HasStatus(StatusType.Frozen), Is.False, "The frozen unit's own controller deploying must still tick and thaw it.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_UnitWithoutIAbilityCapable_DoesNotThrowAndPublishesNoAbilityResolved()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _uncapableSource, new FakeMoveOnlyCapability());
            var command = new MoveCommand(MoveType.Jump, _uncapableSource, _uncapableTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            void resolveCall() => _unitPresenter.ResolveMove(command);

            // THEN
            Assert.DoesNotThrow(resolveCall);
            Assert.That(_abilityResolvedCallCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_SecondHazardOnAnAlreadyHazardousHex_KeepsTheFullAuthoredDuration()
        {
            // GIVEN — regression test: SetHazard replaces the marker but leaves HasHazard true, so a hex
            // re-hazarded by the very landing that already tracks it must not be ticked by that same landing.
            yield return ActivateBoardAsync();

            var hazardCapability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0) },
            };

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _hazardOverwriteHex, hazardCapability);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _hazardOverwriteHex, _hazardOverwriteFirstLanding, ActingPlayerId, ActingUnitId));

            RegisterUnitAt(ThirdUnitId, ActingPlayerId, _hazardOverwriteTickStart, new FakeCapability { CanJump = true });
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _hazardOverwriteTickStart, _hazardOverwriteTickAlt, ActingPlayerId, ThirdUnitId));

            RegisterUnitAt(FourthUnitId, ActingPlayerId, _hazardOverwriteHex, hazardCapability);
            LogAssert.Expect(LogType.Warning, BoardLogMessages.HazardOverwritten);

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _hazardOverwriteHex, _hazardOverwriteSecondLanding, ActingPlayerId, FourthUnitId));

            // THEN
            HexCell cell = GetCell(_hazardOverwriteHex);
            Assert.That(cell.Hazard.RemainingDuration, Is.EqualTo(HazardDuration));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_HazardOverwritesAnOpponentOwnedHazard_KeepsTheFullDurationAndFlipsTheOwnerToTheActingPlayer()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            var hazardCapability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, RivalHazardDuration, TargetFilter.Self, 0) },
            };

            RegisterUnitAt(EnemyUnitId, RivalPlayerId, _hazardOverwriteHex, hazardCapability);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _hazardOverwriteHex, _hazardOverwriteFirstLanding, RivalPlayerId, EnemyUnitId));

            RegisterUnitAt(ThirdUnitId, RivalPlayerId, _hazardOverwriteTickStart, new FakeCapability { CanJump = true });
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _hazardOverwriteTickStart, _hazardOverwriteTickAlt, RivalPlayerId, ThirdUnitId));

            var actingHazardCapability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _hazardOverwriteHex, actingHazardCapability);
            LogAssert.Expect(LogType.Warning, BoardLogMessages.HazardOverwritten);

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _hazardOverwriteHex, _hazardOverwriteSecondLanding, ActingPlayerId, ActingUnitId));

            // THEN
            HexCell cell = GetCell(_hazardOverwriteHex);
            Assert.That(cell.Hazard.OwnerPlayerId, Is.EqualTo(ActingPlayerId));
            Assert.That(cell.Hazard.RemainingDuration, Is.EqualTo(HazardDuration));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_HazardOnAHexTheLandingDidNotTouch_StillTicksAndIsRemovedAtZero()
        {
            // GIVEN — the exemption is scoped to this landing's own affected hexes, not every tracked hazard.
            yield return ActivateBoardAsync();

            var hazardCapability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, 1, TargetFilter.Self, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _untouchedHazardHex, hazardCapability);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _untouchedHazardHex, _untouchedHazardFirstLanding, ActingPlayerId, ActingUnitId));

            RegisterUnitAt(ThirdUnitId, ActingPlayerId, _untouchedHazardTickStart, new FakeCapability { CanJump = true });

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _untouchedHazardTickStart, _untouchedHazardTickAlt, ActingPlayerId, ThirdUnitId));

            // THEN
            HexCell cell = GetCell(_untouchedHazardHex);
            Assert.That(cell.HasHazard, Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleGridInitialized_ClearsTrackedHazards_SoAPreviouslyTrackedHazardNoLongerTicks()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            var hazardCapability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _gridResetHazardHex, hazardCapability);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _gridResetHazardHex, _gridResetFirstLanding, ActingPlayerId, ActingUnitId));

            MatchEvents.RaiseGridInitialized(_gridPresenter.HexGrid);

            RegisterUnitAt(ThirdUnitId, ActingPlayerId, _gridResetTickStart, new FakeCapability { CanJump = true });

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _gridResetTickStart, _gridResetTickAlt, ActingPlayerId, ThirdUnitId));

            // THEN
            HexCell cell = GetCell(_gridResetHazardHex);
            Assert.That(
                cell.Hazard.RemainingDuration,
                Is.EqualTo(HazardDuration),
                "Clearing the tracked hazard list on GridInitialized must stop the previous grid's hazard from ticking."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_SpawnHazardAuthoredOnACloneOnlyTroop_LogsHazardWithoutVacatedHex()
        {
            // GIVEN — Clone never vacates a hex, so a SpawnHazard impact authored on a Clone-only troop can
            // never find one to mark; this is an authoring mistake the controller must diagnose, not drop silently.
            yield return ActivateBoardAsync();

            var capability = new FakeCapability
            {
                CanClone = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _cloneHazardSource, capability);
            LogAssert.Expect(LogType.Warning, BoardLogMessages.HazardWithoutVacatedHex);

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Clone, _cloneHazardSource, _cloneHazardTarget, ActingPlayerId, ActingUnitId));

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_TwoLandingsBothTriggerUnknownEffectType_LogsTheErrorOnce()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            var capability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect((ImpactEffectType)99, StatusType.None, 0, 0, TargetFilter.Self, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _unknownEffectStart, capability);
            LogAssert.Expect(LogType.Error, BoardLogMessages.UnknownImpactEffectType);

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _unknownEffectStart, _unknownEffectTarget, ActingPlayerId, ActingUnitId));
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _unknownEffectTarget, _unknownEffectStart, ActingPlayerId, ActingUnitId));

            // THEN
            Assert.That(_abilityResolvedCallCount, Is.EqualTo(2), "Both landings must still resolve and publish; only the diagnostic logging is latched.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleLandingResolved_TargetNeverLanded_StillTicksAnExistingHazard()
        {
            // GIVEN
            yield return ArrangeAcidCrawlerLandingAsync();
            var neverLandedCommand = new MoveCommand(MoveType.Jump, _acidTarget, _neverLandedTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            MatchEvents.RaiseMoveExecuted(neverLandedCommand, Array.Empty<HexCoordinates>());

            // THEN
            HexCell hazardCell = GetCell(_acidSource);
            Assert.That(
                hazardCell.Hazard.RemainingDuration,
                Is.EqualTo(HazardDuration - 1),
                "Step 6 self-cleanup must still run and tick this player's hazards."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleLandingResolved_TargetNeverLanded_PublishesNoAbilityResolved()
        {
            // GIVEN
            yield return ArrangeAcidCrawlerLandingAsync();
            int abilityResolvedCallCountBeforeTheNonLanding = _abilityResolvedCallCount;
            var neverLandedCommand = new MoveCommand(MoveType.Jump, _acidTarget, _neverLandedTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            MatchEvents.RaiseMoveExecuted(neverLandedCommand, Array.Empty<HexCoordinates>());

            // THEN
            Assert.That(_abilityResolvedCallCount, Is.EqualTo(abilityResolvedCallCountBeforeTheNonLanding));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleLandingResolved_JumpWhoseTargetHexIsEmpty_SpawnsNoHazardOnTheVacatedHex()
        {
            // GIVEN — the two replacements for the removed early-return guards cover "step 6 runs" and "no
            // second AbilityResolved", but neither proves no impact resolves at all: a Jump whose target hex
            // ended up empty must not spawn a hazard on the hex it vacated, even though the card authors one.
            yield return ArrangeAcidCrawlerLandingAsync();
            var neverLandedCommand = new MoveCommand(MoveType.Jump, _acidTarget, _neverLandedTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            MatchEvents.RaiseMoveExecuted(neverLandedCommand, Array.Empty<HexCoordinates>());

            // THEN
            HexCell vacatedCell = GetCell(_acidTarget);
            Assert.That(vacatedCell.HasHazard, Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_CryoStasisCluster_FreezesTheAlliedUnitInRange()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            GridUnit alliedUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _spellAdjacentOne, new FakeCapability());
            RegisterUnitAt(EnemyUnitId, RivalPlayerId, _spellAdjacentTwo, new FakeCapability());
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 3) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), CryoStasisTargets());

            // WHEN
            _abilityController.ResolveSpell(command, capability);

            // THEN
            Assert.That(alliedUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_CryoStasisCluster_FreezesTheEnemyUnitInRange()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _spellAdjacentOne, new FakeCapability());
            GridUnit enemyUnit = RegisterUnitAt(EnemyUnitId, RivalPlayerId, _spellAdjacentTwo, new FakeCapability());
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 3) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), CryoStasisTargets());

            // WHEN
            _abilityController.ResolveSpell(command, capability);

            // THEN
            Assert.That(enemyUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_CryoStasisCluster_PublishesAbilityResolved()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _spellAdjacentOne, new FakeCapability());
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 3) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), CryoStasisTargets());

            // WHEN
            _abilityController.ResolveSpell(command, capability);

            // THEN
            Assert.That(_abilityResolvedCallCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_CryoStasisCluster_ReturnsSuccess()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _spellAdjacentOne, new FakeCapability());
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 3) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), CryoStasisTargets());

            // WHEN
            SpellResult result = _abilityController.ResolveSpell(command, capability);

            // THEN
            Assert.That(result, Is.EqualTo(SpellResult.Success));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_NullCapability_ReturnsCardHasNoImpacts()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), CryoStasisTargets());

            // WHEN
            SpellResult result = _abilityController.ResolveSpell(command, null);

            // THEN
            Assert.That(result, Is.EqualTo(SpellResult.CardHasNoImpacts));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_NullCapabilityWithAlsoInvalidTargets_ReturnsCardHasNoImpacts()
        {
            // GIVEN — two rejections at once: no capability at all, and a target list that could not validate
            // against any real effect either. CardHasNoImpacts wins because the card is inspected before its
            // targets.
            yield return ActivateBoardAsync();
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates>());

            // WHEN
            SpellResult result = _abilityController.ResolveSpell(command, null);

            // THEN
            Assert.That(result, Is.EqualTo(SpellResult.CardHasNoImpacts));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_BoardUnavailableWithAlsoNullCapability_ReturnsBoardUnavailable()
        {
            // GIVEN — board availability is checked before the card, so it wins even when the capability is
            // also null.
            yield return ActivateBoardAsync();
            Object.DestroyImmediate(_gridPresenter);
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates> { _spellCenter });
            LogAssert.Expect(LogType.Error, BoardLogMessages.AbilityBoardUnavailable);

            // WHEN
            SpellResult result = _abilityController.ResolveSpell(command, null);

            // THEN
            Assert.That(result, Is.EqualTo(SpellResult.BoardUnavailable));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_BoardUnavailable_ReturnsBoardUnavailable()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            Object.DestroyImmediate(_gridPresenter);
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates> { _spellCenter });
            LogAssert.Expect(LogType.Error, BoardLogMessages.AbilityBoardUnavailable);

            // WHEN
            SpellResult result = _abilityController.ResolveSpell(command, capability);

            // THEN
            Assert.That(result, Is.EqualTo(SpellResult.BoardUnavailable));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_BoardUnavailable_LeavesTheBoardUnmutated()
        {
            // GIVEN — atomicity: a rejected spell must not touch a single unit, even one that is a perfectly
            // valid target, once the board itself has become unavailable.
            yield return ActivateBoardAsync();
            GridUnit targetUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _spellCenter, new FakeCapability());
            Object.DestroyImmediate(_gridPresenter);
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates> { _spellCenter });
            LogAssert.Expect(LogType.Error, BoardLogMessages.AbilityBoardUnavailable);

            // WHEN
            _abilityController.ResolveSpell(command, capability);

            // THEN
            Assert.That(targetUnit.HasStatus(StatusType.Frozen), Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_TargetHexCountMismatch_ReturnsInvalidTargets()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 3) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates> { _spellCenter, _spellAdjacentOne });

            // WHEN
            SpellResult result = _abilityController.ResolveSpell(command, capability);

            // THEN
            Assert.That(result, Is.EqualTo(SpellResult.InvalidTargets));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_InvalidTargets_LeavesTheTargetUnitUnaffected()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            GridUnit targetUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _spellCenter, new FakeCapability());
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 3) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates> { _spellCenter, _spellAdjacentOne });

            // WHEN
            _abilityController.ResolveSpell(command, capability);

            // THEN
            Assert.That(targetUnit.HasStatus(StatusType.Frozen), Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_ReentrantCall_ReturnsResolverBusy()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, FreezeDuration, TargetFilter.All, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates> { _spellCenter });
            SpellResult reentrantResult = SpellResult.Success;

            void handleReentrant(int actingPlayerId, AbilityResult result) => reentrantResult = _abilityController.ResolveSpell(command, capability);

            MatchEvents.AbilityResolved += handleReentrant;
            LogAssert.Expect(LogType.Error, BoardLogMessages.SpellResolveReentered);

            // WHEN
            try
            {
                _abilityController.ResolveSpell(command, capability);
            }
            finally
            {
                MatchEvents.AbilityResolved -= handleReentrant;
            }

            // THEN
            Assert.That(reentrantResult, Is.EqualTo(SpellResult.ResolverBusy));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_ReentrantCall_DoesNotPublishASecondAbilityResolved()
        {
            // GIVEN
            yield return ActivateBoardAsync();
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, FreezeDuration, TargetFilter.All, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates> { _spellCenter });

            void handleReentrant(int actingPlayerId, AbilityResult result) => _abilityController.ResolveSpell(command, capability);

            MatchEvents.AbilityResolved += handleReentrant;
            LogAssert.Expect(LogType.Error, BoardLogMessages.SpellResolveReentered);

            // WHEN
            try
            {
                _abilityController.ResolveSpell(command, capability);
            }
            finally
            {
                MatchEvents.AbilityResolved -= handleReentrant;
            }

            // THEN
            Assert.That(_abilityResolvedCallCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_ReentrantCallWithTheBoardAlsoDestroyed_ReturnsResolverBusy()
        {
            // GIVEN — reentrancy is checked before board availability, so it wins even when the board becomes
            // unavailable in between the outer and the nested call: both conditions are genuinely true for the
            // nested one.
            yield return ActivateBoardAsync();
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, FreezeDuration, TargetFilter.All, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates> { _spellCenter });
            SpellResult reentrantResult = SpellResult.Success;

            void handleReentrant(int actingPlayerId, AbilityResult result)
            {
                Object.DestroyImmediate(_gridPresenter);
                reentrantResult = _abilityController.ResolveSpell(command, capability);
            }

            MatchEvents.AbilityResolved += handleReentrant;
            LogAssert.Expect(LogType.Error, BoardLogMessages.SpellResolveReentered);

            // WHEN
            try
            {
                _abilityController.ResolveSpell(command, capability);
            }
            finally
            {
                MatchEvents.AbilityResolved -= handleReentrant;
            }

            // THEN
            Assert.That(reentrantResult, Is.EqualTo(SpellResult.ResolverBusy));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_SpawnHazardAuthoredOnAProtocol_LogsHazardWithoutVacatedHex()
        {
            // GIVEN — a Protocol has no acting unit and vacates no hex, so a SpawnHazard impact authored on
            // one is always this same authoring mistake.
            yield return ActivateBoardAsync();
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.All, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("miscast_protocol"), new List<HexCoordinates> { _spellCenter });
            LogAssert.Expect(LogType.Warning, BoardLogMessages.HazardWithoutVacatedHex);

            // WHEN
            _abilityController.ResolveSpell(command, capability);

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_SelfDestructAuthoredOnAProtocol_LogsSelfDestructWithoutActingUnit()
        {
            // GIVEN — a Protocol puts no unit on the board, so a SelfDestruct impact authored on one can never
            // find an acting unit to remove.
            yield return ActivateBoardAsync();
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("miscast_protocol"), new List<HexCoordinates> { _spellCenter });
            LogAssert.Expect(LogType.Warning, BoardLogMessages.SelfDestructWithoutActingUnit);

            // WHEN
            _abilityController.ResolveSpell(command, capability);

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_SpawnHazardAuthoredOnAProtocolDeployedTwice_LogsTheWarningOnlyOnce()
        {
            // GIVEN — latching: a second deployment of the same misauthored card must not add a second entry.
            yield return ActivateBoardAsync();
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.All, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("miscast_protocol"), new List<HexCoordinates> { _spellCenter });
            LogAssert.Expect(LogType.Warning, BoardLogMessages.HazardWithoutVacatedHex);

            // WHEN
            _abilityController.ResolveSpell(command, capability);
            _abilityController.ResolveSpell(command, capability);

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_SpawnHazardAuthoredOnAProtocol_AfterGridInitialized_LogsTheWarningAgain()
        {
            // GIVEN — proves HandleGridInitialized resets the diagnostic latch, not just the hazard tracking list.
            yield return ActivateBoardAsync();
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.All, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("miscast_protocol"), new List<HexCoordinates> { _spellCenter });
            LogAssert.Expect(LogType.Warning, BoardLogMessages.HazardWithoutVacatedHex);
            _abilityController.ResolveSpell(command, capability);

            MatchEvents.RaiseGridInitialized(_gridPresenter.HexGrid);
            LogAssert.Expect(LogType.Warning, BoardLogMessages.HazardWithoutVacatedHex);

            // WHEN
            _abilityController.ResolveSpell(command, capability);

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_ExistingHazardOwnedByTheCastingPlayer_TicksAndDisappears()
        {
            // GIVEN — proves step 6 self-cleanup runs on the spell path too, or a Protocol-only player never
            // expires anything.
            yield return ActivateBoardAsync();

            var hazardCapability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, 1, TargetFilter.Self, 0) },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _acidSource, hazardCapability);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _acidSource, _acidTarget, ActingPlayerId, ActingUnitId));

            var spellCapability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, FreezeDuration, TargetFilter.All, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new List<HexCoordinates> { _spellCenter });

            // WHEN
            _abilityController.ResolveSpell(command, spellCapability);

            // THEN
            HexCell hazardCell = GetCell(_acidSource);
            Assert.That(hazardCell.HasHazard, Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_SelfAppliedFreezeEffect_RemainsActiveImmediatelyAfterItsOwnDeployment()
        {
            // GIVEN — the canonical defensive Cryo-Stasis play: freezing your own units must not be undone by
            // the very deployment that cast it.
            yield return ActivateBoardAsync();
            GridUnit ownUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _spellAdjacentOne, new FakeCapability());
            var capability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 3) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), CryoStasisTargets());

            // WHEN
            _abilityController.ResolveSpell(command, capability);

            // THEN
            Assert.That(ownUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_ClosesTheActingPlayersActionWindow_ExpiringAStatusFromAnEarlierDeployment()
        {
            // GIVEN
            yield return ActivateBoardAsync();

            var enemyCapability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.Enemy, 0) },
            };
            RegisterUnitAt(EnemyUnitId, RivalPlayerId, _spellFarSource, enemyCapability);
            // Armored so the radius-1 standard conversion (step 3) only strips its armor rather than flipping its
            // ownership. Unarmored, the rival's landing would convert it before the freeze (step 4) checks Enemy,
            // and it would no longer read as one; converted, it would also belong to the rival by the time this
            // test's own spell runs step 6, so the later tick below would not touch it either. Staying armored and
            // on ActingPlayerId is what keeps both the freeze and the tick this test is about observable at all.
            GridUnit ownUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _spellAdjacentOne, new FakeCapability(), hasArmor: true);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _spellFarSource, _spellCenter, RivalPlayerId, EnemyUnitId));
            Assert.That(ownUnit.HasStatus(StatusType.Frozen), Is.True, "Test setup expects the earlier landing to have frozen the unit.");

            var spellCapability = new FakeCapability
            {
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Rooted, 0, FreezeDuration, TargetFilter.Self, 1) },
            };
            var command = new SpellCommand(ActingPlayerId, new CardId("subject_alpha"), new List<HexCoordinates> { _spellFarTarget });

            // WHEN
            _abilityController.ResolveSpell(command, spellCapability);

            // THEN
            Assert.That(ownUnit.HasStatus(StatusType.Frozen), Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_RejectedAsCardHasNoImpacts_LeavesAnEarlierFrozenStatusActive()
        {
            // GIVEN — CardHasNoImpacts is a rejection, not a deployment: step 6 must not run, so an earlier
            // freeze must survive a spell that never got past this check.
            yield return ArrangeCryoStasisLandingAsync();
            Assert.That(_lastFriendlyUnit.HasStatus(StatusType.Frozen), Is.True, "Test setup expects the earlier landing to have frozen the unit.");
            var command = new SpellCommand(ActingPlayerId, new CardId("subject_alpha"), new List<HexCoordinates> { _spellCenter });

            // WHEN
            _abilityController.ResolveSpell(command, null);

            // THEN
            Assert.That(_lastFriendlyUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveSpell_RejectedAsCardHasNoImpacts_LeavesAnExistingHazardsDurationUnchanged()
        {
            // GIVEN
            yield return ArrangeAcidCrawlerLandingAsync();
            var command = new SpellCommand(ActingPlayerId, new CardId("subject_alpha"), new List<HexCoordinates> { _spellCenter });

            // WHEN
            _abilityController.ResolveSpell(command, null);

            // THEN
            HexCell hazardCell = GetCell(_acidSource);
            Assert.That(hazardCell.Hazard.RemainingDuration, Is.EqualTo(HazardDuration));
        }

        private static List<HexCoordinates> CryoStasisTargets()
        {
            return new List<HexCoordinates> { _spellCenter, _spellAdjacentOne, _spellAdjacentTwo };
        }

        private IEnumerator ActivateBoardAsync()
        {
            _boardGO.SetActive(true);
            yield return null;

            _unitPresenter.SetUnitSpawner(_spawner);
        }

        private IEnumerator ArrangeAndExecuteVolatileMassJumpAsync()
        {
            yield return ActivateBoardAsync();

            var capability = new FakeCapability
            {
                CanJump = true,
                ConversionRadius = 2,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0) },
            };
            _lastActingUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _volatileSource, capability);
            _lastEnemyUnit = RegisterUnitAt(EnemyUnitId, RivalPlayerId, _volatileEnemyCoords, new FakeCapability());
            var command = new MoveCommand(MoveType.Jump, _volatileSource, _volatileTarget, ActingPlayerId, ActingUnitId);

            _unitPresenter.ResolveMove(command);
        }

        private IEnumerator ArrangeCryoStasisLandingAsync()
        {
            yield return ActivateBoardAsync();

            var capability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, FreezeDuration, TargetFilter.All, 3) },
            };
            _lastActingUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _cryoSource, capability);
            _lastFriendlyUnit = RegisterUnitAt(FriendlyUnitId, ActingPlayerId, _cryoFriendlyCoords, new FakeCapability { CanJump = true });
            var command = new MoveCommand(MoveType.Jump, _cryoSource, _cryoTarget, ActingPlayerId, ActingUnitId);

            _unitPresenter.ResolveMove(command);
        }

        private IEnumerator ArrangeAcidCrawlerLandingAsync()
        {
            yield return ActivateBoardAsync();

            var capability = new FakeCapability
            {
                CanJump = true,
                LandingEffects = new[] { new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0) },
            };
            _lastActingUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _acidSource, capability);
            var command = new MoveCommand(MoveType.Jump, _acidSource, _acidTarget, ActingPlayerId, ActingUnitId);

            _unitPresenter.ResolveMove(command);
        }

        private void HandleConversionResolved(int actingPlayerId, ConversionResult result)
        {
            _conversionResolvedCallCount++;
            _eventOrder.Add("ConversionResolved");
        }

        private void HandleLandingResolved(MoveCommand command, ConversionResult conversions)
        {
            _landingResolvedCallCount++;
            _eventOrder.Add("LandingResolved");
        }

        private void HandleAbilityResolved(int actingPlayerId, AbilityResult result)
        {
            _abilityResolvedCallCount++;
            _eventOrder.Add("AbilityResolved");
        }

        private GridUnit RegisterUnitAt(int unitId, int playerId, HexCoordinates position, IMoveCapable capability, bool hasArmor = false)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(SourceCardIdValue), position, hasArmor);
            Assert.That(_unitPresenter.RegisterUnit(unit, capability), Is.True, $"Test setup expects unit {unitId} to register at {position}.");

            return unit;
        }

        private HexCell GetCell(HexCoordinates coordinates)
        {
            HexGrid grid = _gridPresenter.HexGrid;

            Assert.That(grid, Is.Not.Null, "Test setup expects the grid presenter to have initialized its hex grid.");
            Assert.That(grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test expects {coordinates} to exist on the grid.");

            return cell;
        }

        private sealed class FakeCapability : IMoveCapable, IConversionCapable, IAbilityCapable
        {
            private static readonly ImpactEffect[] _noLandingEffects = Array.Empty<ImpactEffect>();

            public bool CanClone { get; set; }

            public bool CanJump { get; set; }

            public bool IgnoresHazards { get; set; }

            public int CloneDistance { get; set; } = BoardMetrics.DefaultCloneDistance;

            public int JumpDistance { get; set; } = BoardMetrics.DefaultJumpDistance;

            public int ConversionRadius { get; set; } = 1;

            public IReadOnlyList<ImpactEffect> LandingEffects { get; set; } = _noLandingEffects;
        }

        private sealed class FakeMoveOnlyCapability : IMoveCapable
        {
            public bool CanClone => false;

            public bool CanJump => true;

            public bool IgnoresHazards => false;

            public int CloneDistance => BoardMetrics.DefaultCloneDistance;

            public int JumpDistance => BoardMetrics.DefaultJumpDistance;
        }

        private sealed class FakeUnitSpawner : IUnitSpawner
        {
            private int _nextUnitId = FirstSpawnedUnitId;

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                return new GridUnit(_nextUnitId++, playerId, cardId, at);
            }
        }

        // Permissive on purpose: this fixture exercises conversion, landing and ability resolution, never Energy
        // pricing, so every move is affordable and no test has to seed a balance.
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
