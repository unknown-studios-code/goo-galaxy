using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class FuseResolverTests
    {
        private const int PlayerOneId = 1;
        private const int UnitOneId = 1;
        private const int JunkUnitId = 9999;
        private const int FuseDurationInSeconds = 3;
        private const string SourceCardIdValue = "volatile_mass";

        private static readonly HexCoordinates _spawnCoords = new(0, 0);

        private Dictionary<int, GridUnit> _units;
        private FuseResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _units = new Dictionary<int, GridUnit>();
            _resolver = new FuseResolver(_units);
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();
        }

        [Test]
        public void Constructor_NullUnits_ThrowsArgumentNullException()
        {
            // GIVEN

            // WHEN
            static void constructCall() => new FuseResolver(null);

            // THEN
            Assert.Throws<ArgumentNullException>(constructCall);
        }

        [Test]
        public void ArmFuse_NullUnit_DoesNotArm()
        {
            // GIVEN

            // WHEN
            _resolver.ArmFuse(null, FuseDurationInSeconds);

            // THEN
            Assert.That(_resolver.ArmedUnitCount, Is.EqualTo(0));
        }

        [Test]
        public void ArmFuse_DeadUnit_DoesNotArm()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            unit.IsAlive = false;

            // WHEN
            _resolver.ArmFuse(unit, FuseDurationInSeconds);

            // THEN
            Assert.That(_resolver.ArmedUnitCount, Is.EqualTo(0));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ArmFuse_NonPositiveDuration_DoesNotArm(int duration)
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);

            // WHEN
            _resolver.ArmFuse(unit, duration);

            // THEN
            Assert.That(_resolver.ArmedUnitCount, Is.EqualTo(0));
        }

        [Test]
        public void ArmFuse_AlreadyArmedUnit_DoesNotAddASecondRosterEntry()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);

            // WHEN
            _resolver.ArmFuse(unit, FuseDurationInSeconds);

            // THEN
            Assert.That(_resolver.ArmedUnitCount, Is.EqualTo(1));
        }

        [Test]
        public void TickFuses_UnitArmedTwice_ReportsItOnce()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);
            var expiredUnitIds = new List<int>();

            // WHEN
            _resolver.TickFuses(FuseDurationInSeconds, expiredUnitIds);

            // THEN
            Assert.That(expiredUnitIds, Is.EqualTo(new List<int> { unit.UnitId }), "A duplicated id would report the same unit twice.");
        }

        [Test]
        public void ArmFuse_ArmsUnit_RaisesFuseArmedOnce()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            int raisedCount = 0;
            void handleFuseArmed(int unitId, int playerId, float remainingSeconds) => raisedCount++;
            MatchEvents.FuseArmed += handleFuseArmed;

            try
            {
                // WHEN
                _resolver.ArmFuse(unit, FuseDurationInSeconds);
            }
            finally
            {
                MatchEvents.FuseArmed -= handleFuseArmed;
            }

            // THEN
            Assert.That(raisedCount, Is.EqualTo(1));
        }

        [Test]
        public void TickFuses_NothingArmed_ReturnsWithoutTouchingBuffer()
        {
            // GIVEN
            var expiredUnitIds = new List<int> { JunkUnitId };

            // WHEN
            _resolver.TickFuses(FuseDurationInSeconds, expiredUnitIds);

            // THEN
            Assert.That(expiredUnitIds, Is.EqualTo(new List<int> { JunkUnitId }));
        }

        [Test]
        public void TickFuses_NullBuffer_ThrowsArgumentNullException()
        {
            // GIVEN

            // WHEN
            void tickCall() => _resolver.TickFuses(FuseDurationInSeconds, null);

            // THEN
            Assert.Throws<ArgumentNullException>(tickCall);
        }

        [Test]
        public void TickFuses_DeltaBelowDuration_ReportsNothing()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);
            var expiredUnitIds = new List<int>();

            // WHEN
            _resolver.TickFuses(FuseDurationInSeconds - 1f, expiredUnitIds);

            // THEN
            Assert.That(expiredUnitIds, Is.Empty);
        }

        [Test]
        public void TickFuses_DeltaReachesZero_ReportsExpiredId()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);
            var expiredUnitIds = new List<int>();

            // WHEN
            _resolver.TickFuses(FuseDurationInSeconds, expiredUnitIds);

            // THEN
            Assert.That(expiredUnitIds, Is.EqualTo(new List<int> { unit.UnitId }));
        }

        [Test]
        public void TickFuses_AccumulatedAcrossThreeCalls_ExpiresOnTheThird()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);
            var expiredUnitIds = new List<int>();
            float perCallDelta = FuseDurationInSeconds / 3f;
            _resolver.TickFuses(perCallDelta, expiredUnitIds);
            _resolver.TickFuses(perCallDelta, expiredUnitIds);

            // WHEN
            _resolver.TickFuses(perCallDelta, expiredUnitIds);

            // THEN
            Assert.That(expiredUnitIds, Is.EqualTo(new List<int> { unit.UnitId }));
        }

        [Test]
        public void TickFuses_UnitRemovedFromRegistry_DropsIdSilently()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);
            _units.Remove(UnitOneId);
            var expiredUnitIds = new List<int>();

            // WHEN
            _resolver.TickFuses(FuseDurationInSeconds, expiredUnitIds);

            // THEN
            Assert.That(expiredUnitIds, Is.Empty);
        }

        [Test]
        public void TickFuses_UnitNotAlive_DropsIdSilently()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);
            unit.IsAlive = false;
            var expiredUnitIds = new List<int>();

            // WHEN
            _resolver.TickFuses(FuseDurationInSeconds, expiredUnitIds);

            // THEN
            Assert.That(expiredUnitIds, Is.Empty);
        }

        [Test]
        public void TickFuses_AppendsToExistingBuffer_DoesNotClearIt()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);
            var expiredUnitIds = new List<int> { JunkUnitId };

            // WHEN
            _resolver.TickFuses(FuseDurationInSeconds, expiredUnitIds);

            // THEN
            Assert.That(expiredUnitIds, Is.EqualTo(new List<int> { JunkUnitId, unit.UnitId }));
        }

        [Test]
        public void ClearFuse_ArmedUnit_NeverExpires()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ArmFuse(unit, FuseDurationInSeconds);
            _resolver.ClearFuse(unit.UnitId);
            var expiredUnitIds = new List<int>();

            // WHEN
            _resolver.TickFuses(FuseDurationInSeconds, expiredUnitIds);

            // THEN
            Assert.That(expiredUnitIds, Is.Empty);
        }

        [Test]
        public void ClearFuse_UnknownId_DoesNothing()
        {
            // GIVEN

            // WHEN
            void clearCall() => _resolver.ClearFuse(JunkUnitId);

            // THEN
            Assert.DoesNotThrow(clearCall);
        }

        private GridUnit RegisterUnit(int unitId, int playerId)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(SourceCardIdValue), _spawnCoords);
            _units[unitId] = unit;

            return unit;
        }
    }
}
