using System;
using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
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
    public class FuseControllerTests
    {
        private const int BoardRadius = 8;
        private const int ActingPlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int ActingUnitId = 1;
        private const int EnemyUnitId = 2;
        private const int MinFuseDurationInSeconds = 1;

        // WORKAROUND: the editor's hard ceiling for Time.timeScale — anything above it logs an error and the assignment is
        // rejected, which fails the test on the log rather than on the behaviour. It does NOT make a one-second
        // fuse expire in a single tick: Time.maximumDeltaTime (0.3333s by default) caps Time.deltaTime itself, so
        // no frame advances a fuse by more than a third of a second however high this goes. That is what
        // MaxPollFrames and AcceleratedSettleFrameCount are sized for — do not shrink them.
        private const float AcceleratedTimeScale = 100f;

        private const int MaxPollFrames = 120;
        private const int PausedFrameCount = 20;
        private const int AcceleratedSettleFrameCount = 10;

        private const float SeededPlayerEnergy = 20f;
        private const float NoEnergyRegen = 0f;
        private const float InsufficientEnergyCap = 1f;
        private const float InsufficientEnergyStarting = 0f;
        private const float EnergyTolerance = 0.0001f;
        private const string SourceCardIdValue = "volatile_mass";

        private static readonly HexCoordinates _fuseHex = new(0, 0);
        private static readonly HexCoordinates _fuseCloneSource = new(-2, 0);
        private static readonly HexCoordinates _fuseCloneTarget = new(-1, 0);
        private static readonly HexCoordinates _fuseJumpSource = new(-2, 0);
        private static readonly HexCoordinates _fuseJumpTarget = new(0, 0);
        private static readonly HexCoordinates _fuseJumpEnemyCoords = new(-1, 0);
        private static readonly HexCoordinates _fuseArmedJumpTarget = new(1, 0);

        private GameObject _boardGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private AbilityController _abilityController;
        private FuseController _fuseController;
        private EnergyPresenter _energyPresenter;
        private FakeUnitSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);

            _boardGO = new GameObject("FuseController_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _energyPresenter = _boardGO.AddComponent<EnergyPresenter>();
            _energyPresenter.InitializePlayer(ActingPlayerId, new EnergyConfig(SeededPlayerEnergy, NoEnergyRegen, SeededPlayerEnergy));
            _energyPresenter.InitializePlayer(RivalPlayerId, new EnergyConfig(SeededPlayerEnergy, NoEnergyRegen, SeededPlayerEnergy));
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
            Time.timeScale = 1f;
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
        public IEnumerator Fuses_AfterAnAbilityControllerLandingArmsAFuse_ReportsTheArmedUnit()
        {
            // GIVEN
            yield return ActivateBoardCo();

            var capability = new FakeCapability
            {
                CanClone = true,
                LandingEffects = new[]
                {
                    new ImpactEffect(ImpactEffectType.ArmFuse, StatusType.None, 0, MinFuseDurationInSeconds, TargetFilter.Self, 0, ImpactDurationUnit.Seconds),
                },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _fuseCloneSource, capability);
            var command = new MoveCommand(MoveType.Clone, _fuseCloneSource, _fuseCloneTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(_fuseController.Fuses.ArmedUnitCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator Update_FuseExpires_RemovesUnitAndFreesCell()
        {
            // GIVEN
            yield return ActivateBoardCo();
            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _fuseHex, new FakeCapability());
            _fuseController.Fuses.ArmFuse(unit, MinFuseDurationInSeconds);
            Time.timeScale = AcceleratedTimeScale;

            // WHEN
            yield return WaitUntilCo(() => !_unitPresenter.ActiveUnits.ContainsKey(unit.UnitId), $"Unit {unit.UnitId}'s fuse never expired.");

            // THEN
            Assert.That(_unitPresenter.ActiveUnits.ContainsKey(unit.UnitId), Is.False);
            Assert.That(GetCell(_fuseHex).IsOccupied, Is.False);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator Update_FuseExpires_RaisesFuseExpiredAfterUnregistration()
        {
            // GIVEN
            yield return ActivateBoardCo();
            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _fuseHex, new FakeCapability());
            _fuseController.Fuses.ArmFuse(unit, MinFuseDurationInSeconds);

            bool wasUnregisteredWhenRaised = false;
            void handleFuseExpired(int unitId, int playerId) => wasUnregisteredWhenRaised = !_unitPresenter.ActiveUnits.ContainsKey(unitId);

            MatchEvents.FuseExpired += handleFuseExpired;
            Time.timeScale = AcceleratedTimeScale;

            try
            {
                // WHEN
                yield return WaitUntilCo(() => !_unitPresenter.ActiveUnits.ContainsKey(unit.UnitId), $"Unit {unit.UnitId}'s fuse never expired.");
            }
            finally
            {
                MatchEvents.FuseExpired -= handleFuseExpired;
            }

            // THEN
            Assert.That(wasUnregisteredWhenRaised, Is.True);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator Update_FuseExpiresOnConvertedUnit_ReportsNewOwnerId()
        {
            // GIVEN — a fuse survives conversion and goes off for its new owner.
            yield return ActivateBoardCo();
            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _fuseHex, new FakeCapability());
            _fuseController.Fuses.ArmFuse(unit, MinFuseDurationInSeconds);
            unit.ReceiveConversionAttempt(RivalPlayerId);

            int reportedOwnerId = 0;
            void handleFuseExpired(int unitId, int playerId) => reportedOwnerId = playerId;

            MatchEvents.FuseExpired += handleFuseExpired;
            Time.timeScale = AcceleratedTimeScale;

            try
            {
                // WHEN
                yield return WaitUntilCo(() => !_unitPresenter.ActiveUnits.ContainsKey(unit.UnitId), $"Unit {unit.UnitId}'s fuse never expired.");
            }
            finally
            {
                MatchEvents.FuseExpired -= handleFuseExpired;
            }

            // THEN
            Assert.That(reportedOwnerId, Is.EqualTo(RivalPlayerId));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator Update_FuseExpires_DoesNotChargeOrRefundEnergy()
        {
            // GIVEN
            yield return ActivateBoardCo();
            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _fuseHex, new FakeCapability());
            _fuseController.Fuses.ArmFuse(unit, MinFuseDurationInSeconds);
            float energyBefore = _energyPresenter.GetEnergy(ActingPlayerId);
            Time.timeScale = AcceleratedTimeScale;

            // WHEN
            yield return WaitUntilCo(() => !_unitPresenter.ActiveUnits.ContainsKey(unit.UnitId), $"Unit {unit.UnitId}'s fuse never expired.");

            // THEN
            Assert.That(_energyPresenter.GetEnergy(ActingPlayerId), Is.EqualTo(energyBefore).Within(EnergyTolerance));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator Update_TimeScaleZero_FuseDoesNotAdvance()
        {
            // GIVEN — the fuse reads scaled Time.deltaTime, so a paused match must freeze it.
            yield return ActivateBoardCo();
            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _fuseHex, new FakeCapability());
            _fuseController.Fuses.ArmFuse(unit, MinFuseDurationInSeconds);
            Time.timeScale = 0f;

            // WHEN
            for (int frame = 0; frame < PausedFrameCount; frame++)
            {
                yield return null;
            }

            // THEN
            Assert.That(unit.HasFuse, Is.True);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator Update_JumpDetonationClearedFuse_NeverRaisesFuseExpired()
        {
            // GIVEN — a fuse already armed by an earlier Clone landing, exactly as Volatile Mass's own deploy
            // arms it. The owner then Jumps that same unit, which must clear the roster entry deterministically
            // rather than leave it for the clock to catch up on a unit that is already gone.
            yield return ActivateBoardCo();

            var capability = new FakeCapability
            {
                CanClone = true,
                CanJump = true,
                LandingEffects = new[]
                {
                    new ImpactEffect(ImpactEffectType.ArmFuse, StatusType.None, 0, MinFuseDurationInSeconds, TargetFilter.Self, 0, ImpactDurationUnit.Seconds),
                },
            };
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _fuseCloneSource, capability);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Clone, _fuseCloneSource, _fuseCloneTarget, ActingPlayerId, ActingUnitId));

            int clonedUnitId = GetCell(_fuseCloneTarget).OccupantUnitId;
            Assert.That(_fuseController.Fuses.ArmedUnitCount, Is.EqualTo(1), "Test setup expects the Clone landing to have armed the fuse.");

            var expiredUnitIds = new List<int>();
            void handleFuseExpired(int unitId, int playerId) => expiredUnitIds.Add(unitId);

            MatchEvents.FuseExpired += handleFuseExpired;
            Time.timeScale = AcceleratedTimeScale;

            try
            {
                // WHEN
                _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _fuseCloneTarget, _fuseArmedJumpTarget, ActingPlayerId, clonedUnitId));

                for (int frame = 0; frame < AcceleratedSettleFrameCount; frame++)
                {
                    yield return null;
                }
            }
            finally
            {
                MatchEvents.FuseExpired -= handleFuseExpired;
            }

            // THEN
            Assert.That(expiredUnitIds, Has.No.Member(clonedUnitId));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ResolveMove_JumpRejectedForInsufficientEnergy_LeavesTheFuseArmed()
        {
            // GIVEN — starving the owner of the Jump's price is the counterplay: the detonation is denied, but
            // the fuse it was racing is not.
            yield return ActivateBoardCo();
            _energyPresenter.InitializePlayer(ActingPlayerId, new EnergyConfig(InsufficientEnergyCap, NoEnergyRegen, InsufficientEnergyStarting));

            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _fuseJumpSource, new FakeCapability { CanJump = true });
            _fuseController.Fuses.ArmFuse(unit, MinFuseDurationInSeconds);
            var command = new MoveCommand(MoveType.Jump, _fuseJumpSource, _fuseJumpTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That((result, unit.HasFuse), Is.EqualTo((MovementResult.InsufficientEnergy, true)));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator Update_FuseExpiresAfterARejectedJump_ResolvesNoConversion()
        {
            // GIVEN
            yield return ActivateBoardCo();
            _energyPresenter.InitializePlayer(ActingPlayerId, new EnergyConfig(InsufficientEnergyCap, NoEnergyRegen, InsufficientEnergyStarting));

            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _fuseJumpSource, new FakeCapability { CanJump = true });
            _fuseController.Fuses.ArmFuse(unit, MinFuseDurationInSeconds);
            GridUnit enemyUnit = RegisterUnitAt(EnemyUnitId, RivalPlayerId, _fuseJumpEnemyCoords, new FakeCapability());
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _fuseJumpSource, _fuseJumpTarget, ActingPlayerId, ActingUnitId));
            Time.timeScale = AcceleratedTimeScale;

            // WHEN
            yield return WaitUntilCo(
                () => !_unitPresenter.ActiveUnits.ContainsKey(unit.UnitId),
                $"Unit {unit.UnitId}'s fuse never expired after the rejected Jump."
            );

            // THEN
            Assert.That(enemyUnit.PlayerId, Is.EqualTo(RivalPlayerId), "Fuse expiry must never resolve a conversion.");
        }

        private IEnumerator ActivateBoardCo()
        {
            _boardGO.SetActive(true);
            yield return null;

            _unitPresenter.SetUnitSpawner(_spawner);
        }

        private static IEnumerator WaitUntilCo(Func<bool> condition, string failureMessage)
        {
            for (int frame = 0; frame < MaxPollFrames; frame++)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(failureMessage);
        }

        private GridUnit RegisterUnitAt(int unitId, int playerId, HexCoordinates position, IMoveCapable capability)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(SourceCardIdValue), position);
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

        private sealed class FakeUnitSpawner : IUnitSpawner
        {
            private const int FirstSpawnedUnitId = 100;

            private int _nextUnitId = FirstSpawnedUnitId;

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                return new GridUnit(_nextUnitId++, playerId, cardId, at);
            }
        }
    }
}
