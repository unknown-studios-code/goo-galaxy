using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class AbilityResolverTests
    {
        private const int BoardRadius = 4;
        private const int ActingPlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int ActingUnitId = 1;
        private const int EnemyUnitId = 10;
        private const int SecondEnemyUnitId = 11;
        private const int ThirdEnemyUnitId = 12;
        private const int FourthEnemyUnitId = 13;
        private const int FriendlyUnitId = 20;
        private const int ConvertedUnitId = 30;
        private const int ArmoredSurvivorUnitId = 31;
        private const int AlreadyOwnedUnitId = 32;
        private const int OutsideUnitId = 33;
        private const int SecondChosenUnitId = 34;
        private const int StatusDuration = 1;
        private const int HazardDuration = 3;
        private const int OverwrittenHazardDuration = 5;
        private const int JunkUnitId = 9999;
        private const string SourceCardIdValue = "acid_crawler";

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _jumpSource = new(-2, 0);
        private static readonly HexCoordinates _neighborE = new(1, 0);
        private static readonly HexCoordinates _neighborNE = new(1, -1);
        private static readonly HexCoordinates _neighborNW = new(0, -1);
        private static readonly HexCoordinates _neighborW = new(-1, 0);
        private static readonly HexCoordinates _outsideGridCoords = new(99, 99);
        private static readonly HexCoordinates _unoccupiedCoords = new(2, -1);
        private static readonly HexCoordinates _distantFromOriginCoords = new(2, 0);
        private static readonly HexCoordinates _offAxisDistantCoords = new(1, 1);

        private HexGrid _grid;
        private Dictionary<int, GridUnit> _units;
        private StatusEffectResolver _statusEffects;
        private List<ImpactEffect> _landingEffects;
        private List<HexCell> _areaBuffer;
        private List<int> _affectedUnitIds;
        private List<HexCoordinates> _affectedHexes;
        private List<int> _destroyedUnitIds;

        [SetUp]
        public void SetUp()
        {
            _grid = new HexGrid(new FakeGridLayout { GridRadius = BoardRadius });
            _units = new Dictionary<int, GridUnit>();
            _statusEffects = new StatusEffectResolver(_units.Values);
            _landingEffects = new List<ImpactEffect>(2);
            _areaBuffer = new List<HexCell>(6);
            _affectedUnitIds = new List<int>();
            _affectedHexes = new List<HexCoordinates>();
            _destroyedUnitIds = new List<int>();
        }

        [Test]
        public void Resolve_EmptyLandingEffects_LeavesAllThreeOutputBuffersEmpty()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Is.Empty);
            Assert.That(_affectedHexes, Is.Empty);
            Assert.That(_destroyedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_EmptyLandingEffects_DiagnosticsIsNone()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            AbilityDiagnostic diagnostics = Resolve(context);

            // THEN
            Assert.That(diagnostics, Is.EqualTo(AbilityDiagnostic.None));
        }

        [Test]
        public void Resolve_EmptyLandingEffects_DoesNotMutateTheLandingUnit()
        {
            // GIVEN
            GridUnit actingUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(actingUnit.IsAlive, Is.True);
            Assert.That(actingUnit.ActiveStatuses, Is.Empty);
        }

        [Test]
        public void Resolve_ApplyStatusEnemyRadiusOne_FreezesAdjacentEnemy()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _neighborE);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(enemyUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusEnemyRadiusOne_LeavesFriendlyAtSameDistanceUnfrozen()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit friendlyUnit = PlaceUnit(FriendlyUnitId, ActingPlayerId, _neighborNW);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(friendlyUnit.HasStatus(StatusType.Frozen), Is.False);
        }

        [Test]
        public void Resolve_ApplyStatusSelfRadiusZero_FreezesTheActingUnit()
        {
            // GIVEN
            GridUnit actingUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, StatusDuration, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(actingUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusTargetAll_FreezesBothTheFriendlyAndTheEnemyInRange()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _neighborE);
            GridUnit friendlyUnit = PlaceUnit(FriendlyUnitId, ActingPlayerId, _neighborNW);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(enemyUnit.HasStatus(StatusType.Frozen), Is.True);
            Assert.That(friendlyUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusClusterSizeThreeWithFourCandidates_AffectsExactlyThree()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(EnemyUnitId, RivalPlayerId, _neighborE);
            PlaceUnit(SecondEnemyUnitId, RivalPlayerId, _neighborNE);
            PlaceUnit(ThirdEnemyUnitId, RivalPlayerId, _neighborNW);
            PlaceUnit(FourthEnemyUnitId, RivalPlayerId, _neighborW);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 3));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Has.Count.EqualTo(3));
        }

        [Test]
        public void Resolve_ApplyStatusClusterSizeZero_AppliesNoCeiling()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(EnemyUnitId, RivalPlayerId, _neighborE);
            PlaceUnit(SecondEnemyUnitId, RivalPlayerId, _neighborNE);
            PlaceUnit(ThirdEnemyUnitId, RivalPlayerId, _neighborNW);
            PlaceUnit(FourthEnemyUnitId, RivalPlayerId, _neighborW);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Has.Count.EqualTo(4));
        }

        [Test]
        public void Resolve_ApplyStatusNoCandidateInRange_LeavesAffectedUnitIdsEmpty()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_TwoOverlappingApplyStatusImpacts_ReportsTheOverlappingUnitOnceInAffectedUnitIds()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(EnemyUnitId, RivalPlayerId, _neighborE);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 0));
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Rooted, 1, StatusDuration, TargetFilter.Enemy, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Has.Count.EqualTo(1));
        }

        [Test]
        public void Resolve_TwoOverlappingApplyStatusImpacts_AppliesBothStatusesToTheOverlappingUnit()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _neighborE);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 0));
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Rooted, 1, StatusDuration, TargetFilter.Enemy, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(enemyUnit.HasStatus(StatusType.Frozen), Is.True);
            Assert.That(enemyUnit.HasStatus(StatusType.Rooted), Is.True);
        }

        [Test]
        public void Resolve_ClusterCapAfterAnAlreadyReportedUnit_LeavesNoSlotForTheNextCandidate()
        {
            // GIVEN — the second impact's ClusterSize must count a unit the first impact already reported,
            // or the dedup would silently widen the cap and let an extra candidate through.
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(EnemyUnitId, RivalPlayerId, _neighborE);
            GridUnit secondEnemy = PlaceUnit(SecondEnemyUnitId, RivalPlayerId, _neighborNE);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 0));
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Rooted, 1, StatusDuration, TargetFilter.Enemy, 1));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(secondEnemy.HasStatus(StatusType.Rooted), Is.False);
        }

        [Test]
        public void Resolve_SpawnHazardOnJump_PlacesHazardOnTheVacatedSourceHex()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = JumpContext(ActingUnitId, _origin, _jumpSource);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_grid.TryGetCell(_jumpSource, out HexCell vacatedCell), Is.True);
            Assert.That(vacatedCell.HasHazard, Is.True);
        }

        [Test]
        public void Resolve_SpawnHazardOnJump_RecordsTheAuthoredDurationAndActingPlayerAsOwner()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = JumpContext(ActingUnitId, _origin, _jumpSource);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            _grid.TryGetCell(_jumpSource, out HexCell vacatedCell);
            Assert.That(vacatedCell.Hazard.RemainingDuration, Is.EqualTo(HazardDuration));
            Assert.That(vacatedCell.Hazard.OwnerPlayerId, Is.EqualTo(ActingPlayerId));
        }

        [Test]
        public void Resolve_SpawnHazardOnJump_AddsTheVacatedHexToAffectedHexes()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = JumpContext(ActingUnitId, _origin, _jumpSource);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedHexes, Does.Contain(_jumpSource));
        }

        [Test]
        public void Resolve_SpawnHazardOnClone_PlacesNoHazardAnywhere()
        {
            // GIVEN — a Clone vacates no hex, so context.HasVacatedHex is false.
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_grid.TryGetCell(_jumpSource, out HexCell sourceCell), Is.True);
            Assert.That(sourceCell.HasHazard, Is.False);
            Assert.That(_affectedHexes, Is.Empty);
        }

        [Test]
        public void Resolve_SpawnHazardOnClone_SetsHazardWithoutVacatedHexDiagnostic()
        {
            // GIVEN — regression coverage: this diagnostic did not exist before AbilityContext.
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0));

            // WHEN
            AbilityDiagnostic diagnostics = Resolve(context);

            // THEN
            Assert.That((diagnostics & AbilityDiagnostic.HazardWithoutVacatedHex) != 0, Is.True);
        }

        [Test]
        public void Resolve_SpawnHazardOntoAnAlreadyHazardousHex_ResetsTheDuration()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _grid.TryGetCell(_jumpSource, out HexCell vacatedCell);
            vacatedCell.SetHazard(RivalPlayerId, OverwrittenHazardDuration);
            AbilityContext context = JumpContext(ActingUnitId, _origin, _jumpSource);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(vacatedCell.Hazard.RemainingDuration, Is.EqualTo(HazardDuration));
        }

        [Test]
        public void Resolve_SpawnHazardOntoAnAlreadyHazardousHex_SetsHazardOverwrittenDiagnostic()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _grid.TryGetCell(_jumpSource, out HexCell vacatedCell);
            vacatedCell.SetHazard(RivalPlayerId, OverwrittenHazardDuration);
            AbilityContext context = JumpContext(ActingUnitId, _origin, _jumpSource);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0));

            // WHEN
            AbilityDiagnostic diagnostics = Resolve(context);

            // THEN
            Assert.That((diagnostics & AbilityDiagnostic.HazardOverwritten) != 0, Is.True);
        }

        [Test]
        public void Resolve_SelfDestruct_AddsTheActingUnitIdToDestroyedUnitIds()
        {
            // GIVEN
            GridUnit actingUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_destroyedUnitIds, Does.Contain(actingUnit.UnitId));
        }

        [Test]
        public void Resolve_SelfDestruct_DoesNotItselfMarkTheUnitDead()
        {
            // GIVEN
            GridUnit actingUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(actingUnit.IsAlive, Is.True, "Removal is step 6 self-cleanup; the resolver only records the id.");
        }

        [Test]
        public void Resolve_SelfDestructOnAlreadyDeadUnit_SetsSelfDestructOnDeadUnitDiagnostic()
        {
            // GIVEN
            GridUnit actingUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            actingUnit.IsAlive = false;
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0));

            // WHEN
            AbilityDiagnostic diagnostics = Resolve(context);

            // THEN
            Assert.That((diagnostics & AbilityDiagnostic.SelfDestructOnDeadUnit) != 0, Is.True);
        }

        [Test]
        public void Resolve_SelfDestructOnAlreadyDeadUnit_DoesNotThrow()
        {
            // GIVEN
            GridUnit actingUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            actingUnit.IsAlive = false;
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0));

            // WHEN
            void resolveCall() => Resolve(context);

            // THEN
            Assert.DoesNotThrow(resolveCall);
        }

        [Test]
        public void Resolve_SelfDestructWithNoActingUnit_SetsSelfDestructWithoutActingUnitDiagnostic()
        {
            // GIVEN — distinct from SelfDestructOnDeadUnit: there is no acting unit id at all to look up.
            AbilityContext context = AbilityContext.ForLanding(ActingPlayerId, AbilityContext.NoActingUnit, _origin, false, default, default);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0));

            // WHEN
            AbilityDiagnostic diagnostics = Resolve(context);

            // THEN
            Assert.That((diagnostics & AbilityDiagnostic.SelfDestructWithoutActingUnit) != 0, Is.True);
        }

        [Test]
        public void Resolve_UnknownEffectType_SetsUnknownEffectTypeDiagnostic()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect((ImpactEffectType)99, StatusType.None, 0, 0, TargetFilter.Self, 0));

            // WHEN
            AbilityDiagnostic diagnostics = Resolve(context);

            // THEN
            Assert.That((diagnostics & AbilityDiagnostic.UnknownEffectType) != 0, Is.True);
        }

        [Test]
        public void Resolve_UnknownEffectTypeFollowedByAValidEffect_StillExecutesTheValidEffect()
        {
            // GIVEN
            GridUnit actingUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect((ImpactEffectType)99, StatusType.None, 0, 0, TargetFilter.Self, 0));
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, StatusDuration, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(actingUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void Resolve_UnknownEffectTypeFollowedByAValidEffect_DoesNotThrow()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect((ImpactEffectType)99, StatusType.None, 0, 0, TargetFilter.Self, 0));
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, StatusDuration, TargetFilter.Self, 0));

            // WHEN
            void resolveCall() => Resolve(context);

            // THEN
            Assert.DoesNotThrow(resolveCall);
        }

        [Test]
        public void Resolve_ApplyStatusThenSelfDestruct_ExecutesBothInListOrder()
        {
            // GIVEN
            GridUnit actingUnit = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, StatusDuration, TargetFilter.Self, 0));
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(actingUnit.HasStatus(StatusType.Frozen), Is.True);
            Assert.That(_destroyedUnitIds, Does.Contain(actingUnit.UnitId));
        }

        [Test]
        public void Resolve_BuffersHoldingPreviousContent_ClearsThemBeforeFilling()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _affectedUnitIds.Add(JunkUnitId);
            _affectedHexes.Add(_outsideGridCoords);
            _destroyedUnitIds.Add(JunkUnitId);

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Has.No.Member(JunkUnitId));
            Assert.That(_affectedHexes, Has.No.Member(_outsideGridCoords));
            Assert.That(_destroyedUnitIds, Has.No.Member(JunkUnitId));
        }

        [Test]
        public void Resolve_NullGrid_ThrowsArgumentNullException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            void resolveCall() =>
                AbilityResolver.Resolve(
                    null,
                    _units,
                    context,
                    _landingEffects,
                    _statusEffects,
                    _areaBuffer,
                    _affectedUnitIds,
                    _affectedHexes,
                    _destroyedUnitIds,
                    out _
                );

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullUnits_ThrowsArgumentNullException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            void resolveCall() =>
                AbilityResolver.Resolve(
                    _grid,
                    null,
                    context,
                    _landingEffects,
                    _statusEffects,
                    _areaBuffer,
                    _affectedUnitIds,
                    _affectedHexes,
                    _destroyedUnitIds,
                    out _
                );

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullLandingEffects_ThrowsArgumentNullException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            void resolveCall() =>
                AbilityResolver.Resolve(_grid, _units, context, null, _statusEffects, _areaBuffer, _affectedUnitIds, _affectedHexes, _destroyedUnitIds, out _);

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullStatusEffects_ThrowsArgumentNullException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            void resolveCall() =>
                AbilityResolver.Resolve(_grid, _units, context, _landingEffects, null, _areaBuffer, _affectedUnitIds, _affectedHexes, _destroyedUnitIds, out _);

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullAreaBuffer_ThrowsArgumentNullException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            void resolveCall() =>
                AbilityResolver.Resolve(
                    _grid,
                    _units,
                    context,
                    _landingEffects,
                    _statusEffects,
                    null,
                    _affectedUnitIds,
                    _affectedHexes,
                    _destroyedUnitIds,
                    out _
                );

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullAffectedUnitIds_ThrowsArgumentNullException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            void resolveCall() =>
                AbilityResolver.Resolve(_grid, _units, context, _landingEffects, _statusEffects, _areaBuffer, null, _affectedHexes, _destroyedUnitIds, out _);

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullAffectedHexes_ThrowsArgumentNullException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            void resolveCall() =>
                AbilityResolver.Resolve(_grid, _units, context, _landingEffects, _statusEffects, _areaBuffer, _affectedUnitIds, null, _destroyedUnitIds, out _);

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void Resolve_NullDestroyedUnitIds_ThrowsArgumentNullException()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = LandingContext(ActingUnitId, _origin);

            // WHEN
            void resolveCall() =>
                AbilityResolver.Resolve(_grid, _units, context, _landingEffects, _statusEffects, _areaBuffer, _affectedUnitIds, _affectedHexes, null, out _);

            // THEN
            Assert.Throws<ArgumentNullException>(resolveCall);
        }

        [Test]
        public void ValidateTargets_ValidCryoStasisCluster_ReturnsTrue()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _neighborE, _neighborNW };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void ValidateTargets_CountBelowClusterSize_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _neighborE };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_CountAboveClusterSize_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _neighborE, _neighborNW, _neighborW };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_DuplicateHex_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _neighborE, _neighborE };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_HexOffGrid_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _neighborE, _outsideGridCoords };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_HexBeyondRadiusOfCentre_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _neighborE, _offAxisDistantCoords };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_CollinearHexesWithEndpointFirst_ReturnsFalseBecauseFarEndpointExceedsRadius()
        {
            // GIVEN — targets[0] is the rule's centre; a straight line's endpoint is not its geometric middle.
            var targets = new List<HexCoordinates> { _origin, _neighborE, _distantFromOriginCoords };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_CollinearHexesWithMidpointFirst_ReturnsTrueBecauseBothEndpointsAreWithinRadius()
        {
            // GIVEN — same three hexes, but the geometric middle is targets[0], so both endpoints are 1 away.
            var targets = new List<HexCoordinates> { _neighborE, _origin, _distantFromOriginCoords };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void ValidateTargets_ClusterSizeZero_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _neighborE, _neighborNW };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 0);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_NullTargets_ReturnsFalse()
        {
            // GIVEN
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(null, effect, _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_EmptyTargets_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates>();
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_NullGrid_ReturnsFalse()
        {
            // GIVEN
            var targets = new List<HexCoordinates> { _origin, _neighborE, _neighborNW };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, null);

            // THEN
            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateTargets_SterilizationBeamShapedClusterOfFour_ReturnsTrue()
        {
            // GIVEN — proves the rule is authored data alone: a wider cluster needs no new code, only (4, 1).
            var targets = new List<HexCoordinates> { _origin, _neighborE, _neighborNW, _neighborW };
            var effect = new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 4);

            // WHEN
            bool isValid = AbilityResolver.ValidateTargets(targets, effect, _grid);

            // THEN
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusAllyFilter_AffectsTheActingPlayersUnit()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit friendlyUnit = PlaceUnit(FriendlyUnitId, ActingPlayerId, _neighborE);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Ally, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(friendlyUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusAllyFilter_DoesNotAffectAnEnemyUnit()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = PlaceUnit(EnemyUnitId, RivalPlayerId, _neighborE);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Ally, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(enemyUnit.HasStatus(StatusType.Frozen), Is.False);
        }

        [Test]
        public void Resolve_ApplyStatusAllyFilter_IncludesANewlyConvertedUnit()
        {
            // GIVEN — Ally reads live ownership, not history: conversion (step 3) already ran, so a unit it
            // just flipped now belongs to the acting player and Ally must not exclude it.
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit convertedUnit = PlaceUnit(ConvertedUnitId, ActingPlayerId, _neighborE);
            var conversions = new ConversionResult(new List<int> { convertedUnit.UnitId }, null);
            AbilityContext context = LandingContextWithConversions(ActingUnitId, _origin, conversions);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Ally, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(convertedUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusNewlyConvertedFilter_AffectsTheUnitInConversionResult()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit convertedUnit = PlaceUnit(ConvertedUnitId, ActingPlayerId, _neighborE);
            var conversions = new ConversionResult(new List<int> { convertedUnit.UnitId }, null);
            AbilityContext context = LandingContextWithConversions(ActingUnitId, _origin, conversions);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Rooted, 1, StatusDuration, TargetFilter.NewlyConverted, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(convertedUnit.HasStatus(StatusType.Rooted), Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusNewlyConvertedFilter_DoesNotAffectAUnitAlreadyOwnedBeforeTheMove()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit convertedUnit = PlaceUnit(ConvertedUnitId, ActingPlayerId, _neighborE);
            GridUnit alreadyOwnedUnit = PlaceUnit(AlreadyOwnedUnitId, ActingPlayerId, _neighborNW);
            var conversions = new ConversionResult(new List<int> { convertedUnit.UnitId }, null);
            AbilityContext context = LandingContextWithConversions(ActingUnitId, _origin, conversions);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Rooted, 1, StatusDuration, TargetFilter.NewlyConverted, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(alreadyOwnedUnit.HasStatus(StatusType.Rooted), Is.False);
        }

        [Test]
        public void Resolve_ApplyStatusNewlyConvertedFilter_DoesNotAffectAnArmoredSurvivor_PreventsThePlasmicLeaperBug()
        {
            // GIVEN — the regression this filter exists to guard: Binding Plasma must root only what it just
            // converted, never an adjacent piece that merely lost its armor and stayed with its own owner.
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit convertedUnit = PlaceUnit(ConvertedUnitId, ActingPlayerId, _neighborE);
            GridUnit armoredSurvivor = PlaceUnit(ArmoredSurvivorUnitId, RivalPlayerId, _neighborW, hasArmor: true);
            var conversions = new ConversionResult(new List<int> { convertedUnit.UnitId }, new List<int> { armoredSurvivor.UnitId });
            AbilityContext context = LandingContextWithConversions(ActingUnitId, _origin, conversions);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Rooted, 1, StatusDuration, TargetFilter.NewlyConverted, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(armoredSurvivor.HasStatus(StatusType.Rooted), Is.False);
        }

        [Test]
        public void Resolve_ApplyStatusNewlyConvertedFilterWithEmptyConversionResult_AffectsNobody()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(FriendlyUnitId, ActingPlayerId, _neighborE);
            AbilityContext context = LandingContext(ActingUnitId, _origin);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Rooted, 1, StatusDuration, TargetFilter.NewlyConverted, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_ApplyStatusEnemyFilter_DoesNotAffectTheNewlyConvertedUnit()
        {
            // GIVEN — "Enemy" reads as "whoever survived conversion"; a unit conversion just flipped is not one.
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit convertedUnit = PlaceUnit(ConvertedUnitId, ActingPlayerId, _neighborE);
            var conversions = new ConversionResult(new List<int> { convertedUnit.UnitId }, null);
            AbilityContext context = LandingContextWithConversions(ActingUnitId, _origin, conversions);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(convertedUnit.HasStatus(StatusType.Frozen), Is.False);
        }

        [Test]
        public void Resolve_ApplyStatusEnemyFilter_AffectsTheArmoredSurvivor()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit armoredSurvivor = PlaceUnit(ArmoredSurvivorUnitId, RivalPlayerId, _neighborW, hasArmor: true);
            var conversions = new ConversionResult(null, new List<int> { armoredSurvivor.UnitId });
            AbilityContext context = LandingContextWithConversions(ActingUnitId, _origin, conversions);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.Enemy, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(armoredSurvivor.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusSelfFilterInASpellContext_AffectsNobody()
        {
            // GIVEN — Self requires an acting unit, and a Protocol has none, regardless of who stands on the hex.
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = SpellContext(new List<HexCoordinates> { _origin });
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 0, StatusDuration, TargetFilter.Self, 1));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_ApplyStatusInASpellContext_AffectsTheChosenHexes()
        {
            // GIVEN
            GridUnit chosenUnitOne = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit chosenUnitTwo = PlaceUnit(SecondChosenUnitId, RivalPlayerId, _neighborE);
            AbilityContext context = SpellContext(new List<HexCoordinates> { _origin, _neighborE });
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(chosenUnitOne.HasStatus(StatusType.Frozen), Is.True);
            Assert.That(chosenUnitTwo.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusInASpellContext_DoesNotAffectAnAdjacentHexOutsideTheList()
        {
            // GIVEN — proves the impact area is the chosen hex list, not a GetSpiralCells expansion around it.
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            GridUnit outsideUnit = PlaceUnit(OutsideUnitId, RivalPlayerId, _neighborNW);
            AbilityContext context = SpellContext(new List<HexCoordinates> { _origin });
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 3));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(outsideUnit.HasStatus(StatusType.Frozen), Is.False);
        }

        [Test]
        public void Resolve_SpawnHazardInASpellContext_PlacesNoHazard()
        {
            // GIVEN — a Protocol never vacates a hex, so its context.HasVacatedHex is always false.
            AbilityContext context = SpellContext(new List<HexCoordinates> { _origin });
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_grid.TryGetCell(_origin, out HexCell originCell), Is.True);
            Assert.That(originCell.HasHazard, Is.False);
        }

        [Test]
        public void Resolve_SpawnHazardInASpellContext_SetsHazardWithoutVacatedHexDiagnostic()
        {
            // GIVEN
            AbilityContext context = SpellContext(new List<HexCoordinates> { _origin });
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SpawnHazard, StatusType.None, 0, HazardDuration, TargetFilter.Self, 0));

            // WHEN
            AbilityDiagnostic diagnostics = Resolve(context);

            // THEN
            Assert.That((diagnostics & AbilityDiagnostic.HazardWithoutVacatedHex) != 0, Is.True);
        }

        [Test]
        public void Resolve_SelfDestructInASpellContext_DestroysNothing()
        {
            // GIVEN — a Protocol puts no unit on the board.
            AbilityContext context = SpellContext(new List<HexCoordinates> { _origin });
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_destroyedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_SelfDestructInASpellContext_SetsSelfDestructWithoutActingUnitDiagnostic()
        {
            // GIVEN
            AbilityContext context = SpellContext(new List<HexCoordinates> { _origin });
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0));

            // WHEN
            AbilityDiagnostic diagnostics = Resolve(context);

            // THEN
            Assert.That((diagnostics & AbilityDiagnostic.SelfDestructWithoutActingUnit) != 0, Is.True);
        }

        [Test]
        public void Resolve_ApplyStatusInASpellContextWithAnEmptyTargetHex_IsANoOp()
        {
            // GIVEN
            AbilityContext context = SpellContext(new List<HexCoordinates> { _unoccupiedCoords });
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 1));

            // WHEN
            void resolveCall() => Resolve(context);

            // THEN
            Assert.DoesNotThrow(resolveCall);
            Assert.That(_affectedUnitIds, Is.Empty);
        }

        [Test]
        public void Resolve_SpellContextWithNullTargets_GathersNoArea()
        {
            // GIVEN — regression: a null target list must read as "the area was handed over, and it is empty",
            // never fall back to deriving one from OriginHex — that fallback used to turn a targetless Protocol
            // into an area effect centred on (0, 0). The unit sits exactly there, so it proves which branch ran.
            GridUnit unitAtOrigin = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = SpellContext(null);
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Is.Empty);
            Assert.That(_affectedHexes, Is.Empty);
            Assert.That(_destroyedUnitIds, Is.Empty);
            Assert.That(unitAtOrigin.HasStatus(StatusType.Frozen), Is.False);
        }

        [Test]
        public void Resolve_SpellContextWithEmptyTargets_GathersNoArea()
        {
            // GIVEN — same regression as the null-list case, with an empty list instead.
            GridUnit unitAtOrigin = PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            AbilityContext context = SpellContext(new List<HexCoordinates>());
            _landingEffects.Add(new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, StatusDuration, TargetFilter.All, 0));

            // WHEN
            Resolve(context);

            // THEN
            Assert.That(_affectedUnitIds, Is.Empty);
            Assert.That(_affectedHexes, Is.Empty);
            Assert.That(_destroyedUnitIds, Is.Empty);
            Assert.That(unitAtOrigin.HasStatus(StatusType.Frozen), Is.False);
        }

        private static AbilityContext LandingContext(int actingUnitId, HexCoordinates originHex)
        {
            return AbilityContext.ForLanding(ActingPlayerId, actingUnitId, originHex, false, default, default);
        }

        private static AbilityContext LandingContextWithConversions(int actingUnitId, HexCoordinates originHex, ConversionResult conversions)
        {
            return AbilityContext.ForLanding(ActingPlayerId, actingUnitId, originHex, false, default, conversions);
        }

        private static AbilityContext JumpContext(int actingUnitId, HexCoordinates originHex, HexCoordinates vacatedHex)
        {
            return AbilityContext.ForLanding(ActingPlayerId, actingUnitId, originHex, true, vacatedHex, default);
        }

        private static AbilityContext SpellContext(IReadOnlyList<HexCoordinates> targetHexes)
        {
            return AbilityContext.ForSpell(ActingPlayerId, targetHexes);
        }

        private AbilityDiagnostic Resolve(in AbilityContext context)
        {
            AbilityResolver.Resolve(
                _grid,
                _units,
                context,
                _landingEffects,
                _statusEffects,
                _areaBuffer,
                _affectedUnitIds,
                _affectedHexes,
                _destroyedUnitIds,
                out AbilityDiagnostic diagnostics
            );

            return diagnostics;
        }

        private GridUnit PlaceUnit(int unitId, int playerId, HexCoordinates position, bool hasArmor = false)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(SourceCardIdValue), position, hasArmor);
            _units[unitId] = unit;
            GetCell(position).SetOccupant(unitId);

            return unit;
        }

        private HexCell GetCell(HexCoordinates coordinates)
        {
            Assert.That(_grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test setup expects {coordinates} to exist on the grid.");

            return cell;
        }

        private sealed class FakeGridLayout : IGridLayout
        {
            public int GridRadius { get; set; } = BoardRadius;

            public IReadOnlySet<HexCoordinates> BlockedCoordinates { get; set; } = new ReadOnlySet<HexCoordinates>(new HashSet<HexCoordinates>());
        }
    }
}
