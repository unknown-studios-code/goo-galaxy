using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class StatusEffectResolverTests
    {
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;
        private const int UnitOneId = 1;
        private const int UnitTwoId = 2;
        private const string SourceCardIdValue = "acid_crawler";

        private static readonly HexCoordinates _spawnCoords = new(0, 0);

        private Dictionary<int, GridUnit> _units;
        private StatusEffectResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _units = new Dictionary<int, GridUnit>();
            _resolver = new StatusEffectResolver(_units.Values);
        }

        [Test]
        public void ApplyStatus_LiveUnit_SetsHasStatusTrue()
        {
            // GIVEN
            GridUnit unit = CreateUnit(UnitOneId, PlayerOneId);

            // WHEN
            _resolver.ApplyStatus(unit, StatusType.Frozen, 1);

            // THEN
            Assert.That(unit.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void ApplyStatus_LiveUnit_MakesReceiveConversionAttemptReportImmune()
        {
            // GIVEN
            GridUnit unit = CreateUnit(UnitOneId, PlayerOneId);
            _resolver.ApplyStatus(unit, StatusType.Frozen, 1);

            // WHEN
            ConversionOutcome outcome = unit.ReceiveConversionAttempt(PlayerTwoId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.Immune));
        }

        [Test]
        public void ApplyStatus_NullUnit_DoesNotThrow()
        {
            // GIVEN
            // no unit instance

            // WHEN
            void applyCall() => _resolver.ApplyStatus(null, StatusType.Frozen, 1);

            // THEN
            Assert.DoesNotThrow(applyCall);
        }

        [Test]
        public void ApplyStatus_DeadUnit_LeavesItWithNoStatus()
        {
            // GIVEN
            GridUnit unit = CreateUnit(UnitOneId, PlayerOneId);
            unit.IsAlive = false;

            // WHEN
            _resolver.ApplyStatus(unit, StatusType.Frozen, 1);

            // THEN
            Assert.That(unit.HasStatus(StatusType.Frozen), Is.False);
        }

        [Test]
        public void ApplyStatus_NoneType_LeavesTheUnitWithNoStatus()
        {
            // GIVEN
            GridUnit unit = CreateUnit(UnitOneId, PlayerOneId);

            // WHEN
            _resolver.ApplyStatus(unit, StatusType.None, 1);

            // THEN
            Assert.That(unit.ActiveStatuses, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ApplyStatus_NonPositiveDuration_LeavesTheUnitWithNoStatus(int duration)
        {
            // GIVEN
            GridUnit unit = CreateUnit(UnitOneId, PlayerOneId);

            // WHEN
            _resolver.ApplyStatus(unit, StatusType.Frozen, duration);

            // THEN
            Assert.That(unit.ActiveStatuses, Is.Empty);
        }

        [Test]
        public void ApplyStatus_SameStatusAppliedTwice_RefreshesInsteadOfStacking()
        {
            // GIVEN
            GridUnit unit = CreateUnit(UnitOneId, PlayerOneId);
            _resolver.ApplyStatus(unit, StatusType.Frozen, 1);

            // WHEN
            _resolver.ApplyStatus(unit, StatusType.Frozen, 4);

            // THEN
            Assert.That(unit.ActiveStatuses, Has.Count.EqualTo(1));
            Assert.That(unit.ActiveStatuses[0].RemainingDuration, Is.EqualTo(4));
        }

        [Test]
        public void TickDurations_ForOwningPlayer_DecrementsTheirUnitsMarker()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ApplyStatus(unit, StatusType.Rooted, 2);

            // WHEN
            _resolver.TickDurations(PlayerOneId);

            // THEN
            Assert.That(unit.ActiveStatuses[0].RemainingDuration, Is.EqualTo(1));
        }

        [Test]
        public void TickDurations_ForOwningPlayer_LeavesTheOtherPlayersMarkerUntouched()
        {
            // GIVEN
            RegisterUnit(UnitOneId, PlayerOneId);
            GridUnit rivalUnit = RegisterUnit(UnitTwoId, PlayerTwoId);
            _resolver.ApplyStatus(rivalUnit, StatusType.Rooted, 2);

            // WHEN
            _resolver.TickDurations(PlayerOneId);

            // THEN
            Assert.That(rivalUnit.ActiveStatuses[0].RemainingDuration, Is.EqualTo(2));
        }

        [Test]
        public void TickDurations_MarkerReachingZero_IsRemovedAndHasStatusGoesFalse()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ApplyStatus(unit, StatusType.Frozen, 1);

            // WHEN
            _resolver.TickDurations(PlayerOneId);

            // THEN
            Assert.That(unit.ActiveStatuses, Is.Empty);
            Assert.That(unit.HasStatus(StatusType.Frozen), Is.False);
        }

        [Test]
        public void TickDurations_MarkerWithDurationTwo_SurvivesTheFirstTick()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ApplyStatus(unit, StatusType.Rooted, 2);

            // WHEN
            _resolver.TickDurations(PlayerOneId);

            // THEN
            Assert.That(unit.HasStatus(StatusType.Rooted), Is.True);
        }

        [Test]
        public void TickDurations_MarkerWithDurationTwo_ExpiresOnTheSecondTick()
        {
            // GIVEN
            GridUnit unit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ApplyStatus(unit, StatusType.Rooted, 2);
            _resolver.TickDurations(PlayerOneId);

            // WHEN
            _resolver.TickDurations(PlayerOneId);

            // THEN
            Assert.That(unit.HasStatus(StatusType.Rooted), Is.False);
        }

        [Test]
        public void TickDurations_WithExemptUnit_DoesNotDecrementTheExemptUnitsMarker()
        {
            // GIVEN
            GridUnit exemptUnit = RegisterUnit(UnitOneId, PlayerOneId);
            _resolver.ApplyStatus(exemptUnit, StatusType.Frozen, 1);
            var exemptUnitIds = new List<int> { exemptUnit.UnitId };

            // WHEN
            _resolver.TickDurations(PlayerOneId, exemptUnitIds);

            // THEN
            Assert.That(exemptUnit.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void TickDurations_WithExemptUnit_StillDecrementsANonExemptUnitsMarker()
        {
            // GIVEN
            GridUnit exemptUnit = RegisterUnit(UnitOneId, PlayerOneId);
            GridUnit otherUnit = RegisterUnit(UnitTwoId, PlayerOneId);
            _resolver.ApplyStatus(exemptUnit, StatusType.Frozen, 1);
            _resolver.ApplyStatus(otherUnit, StatusType.Frozen, 1);
            var exemptUnitIds = new List<int> { exemptUnit.UnitId };

            // WHEN
            _resolver.TickDurations(PlayerOneId, exemptUnitIds);

            // THEN
            Assert.That(otherUnit.HasStatus(StatusType.Frozen), Is.False);
        }

        [Test]
        public void TickDurations_PlayerWithNoStatuses_DoesNotThrow()
        {
            // GIVEN
            RegisterUnit(UnitOneId, PlayerOneId);

            // WHEN
            void tickCall() => _resolver.TickDurations(PlayerOneId);

            // THEN
            Assert.DoesNotThrow(tickCall);
        }

        [Test]
        public void Constructor_NullValueCollection_ThrowsArgumentNullException()
        {
            // GIVEN
            // no value collection to bind

            // WHEN
            static void constructCall() => new StatusEffectResolver(null);

            // THEN
            Assert.Throws<ArgumentNullException>(constructCall);
        }

        private GridUnit CreateUnit(int unitId, int playerId)
        {
            return new GridUnit(unitId, playerId, new CardId(SourceCardIdValue), _spawnCoords);
        }

        private GridUnit RegisterUnit(int unitId, int playerId)
        {
            GridUnit unit = CreateUnit(unitId, playerId);
            _units[unitId] = unit;

            return unit;
        }
    }
}
