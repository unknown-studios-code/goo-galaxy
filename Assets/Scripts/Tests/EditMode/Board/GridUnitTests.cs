using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Board
{
    [TestFixture]
    public class GridUnitTests
    {
        private const int UnitId = 7;
        private const int PlayerId = 1;
        private const int EnemyPlayerId = 2;
        private const int ThirdPlayerId = 3;
        private const int FreezeDuration = 1;

        private static readonly HexCoordinates _spawnCoords = new(2, -1);

        [Test]
        public void Constructor_WithCardId_InitializesIdentityAndPosition()
        {
            // GIVEN
            var cardId = new CardId("acid_crawler");

            // WHEN
            var unit = new GridUnit(UnitId, PlayerId, cardId, _spawnCoords);

            // THEN
            Assert.That(unit.UnitId, Is.EqualTo(UnitId));
            Assert.That(unit.PlayerId, Is.EqualTo(PlayerId));
            Assert.That(unit.CardId, Is.EqualTo(cardId));
            Assert.That(unit.Position, Is.EqualTo(_spawnCoords));
        }

        [Test]
        public void Constructor_NewUnit_IsAlive()
        {
            // GIVEN
            var cardId = new CardId("acid_crawler");

            // WHEN
            var unit = new GridUnit(UnitId, PlayerId, cardId, _spawnCoords);

            // THEN
            Assert.That(unit.IsAlive, Is.True);
        }

        [Test]
        public void Constructor_WithoutArmorArgument_LeavesUnitUnarmored()
        {
            // GIVEN
            var cardId = new CardId("acid_crawler");

            // WHEN
            var unit = new GridUnit(UnitId, PlayerId, cardId, _spawnCoords);

            // THEN
            Assert.That(unit.HasArmor, Is.False);
        }

        [Test]
        public void Constructor_WithArmoredCard_StartsWithArmorIntact()
        {
            // GIVEN
            var cardId = new CardId("bio_phalanx");

            // WHEN
            var unit = new GridUnit(UnitId, PlayerId, cardId, _spawnCoords, hasArmor: true);

            // THEN
            Assert.That(unit.HasArmor, Is.True);
        }

        [Test]
        public void Constructor_NewUnit_StartsWithNoActiveStatuses()
        {
            // GIVEN
            var cardId = new CardId("acid_crawler");

            // WHEN
            var unit = new GridUnit(UnitId, PlayerId, cardId, _spawnCoords);

            // THEN
            Assert.That(unit.ActiveStatuses, Is.Empty);
        }

        [Test]
        public void Constructor_NewUnit_IsNotFrozen()
        {
            // GIVEN
            var cardId = new CardId("acid_crawler");

            // WHEN
            var unit = new GridUnit(UnitId, PlayerId, cardId, _spawnCoords);

            // THEN
            Assert.That(unit.IsFrozen, Is.False);
        }

        [Test]
        public void ActiveStatuses_BeforeAnyStatusIsApplied_IsTheSharedEmptyInstance()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            GridUnit otherUnit = CreateUnit();

            // WHEN
            IReadOnlyList<StatusMarker> statuses = unit.ActiveStatuses;

            // THEN
            Assert.That(statuses, Is.SameAs(otherUnit.ActiveStatuses), "An unstatused unit must not allocate a list of its own.");
        }

        [Test]
        public void ActiveStatuses_ReadRepeatedlyBeforeAnyStatus_AllocatesNoManagedMemory()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            _ = unit.ActiveStatuses; // Warm-up to exclude JIT allocation from the measurement.

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                _ = unit.ActiveStatuses;
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(allocatedAfter - allocatedBefore, Is.EqualTo(0), "The status list must stay unallocated until a status is applied.");
        }

        [Test]
        public void ActiveStatuses_AfterTheLastStatusIsRemoved_IsEmpty()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            unit.AddStatus(StatusType.Frozen, FreezeDuration);

            // WHEN
            unit.RemoveStatus(StatusType.Frozen);

            // THEN
            Assert.That(unit.ActiveStatuses, Is.Empty);
        }

        [Test]
        public void AddStatus_InactiveStatus_MakesItActive()
        {
            // GIVEN
            GridUnit unit = CreateUnit();

            // WHEN
            unit.AddStatus(StatusType.Frozen, FreezeDuration);

            // THEN
            Assert.That(unit.HasStatus(StatusType.Frozen), Is.True);
        }

        [Test]
        public void AddStatus_InactiveStatus_RecordsTheRequestedDuration()
        {
            // GIVEN
            GridUnit unit = CreateUnit();

            // WHEN
            unit.AddStatus(StatusType.Rooted, 3);

            // THEN
            Assert.That(unit.ActiveStatuses[0].RemainingDuration, Is.EqualTo(3));
        }

        [Test]
        public void AddStatus_StatusAlreadyActive_RefreshesInsteadOfStacking()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            unit.AddStatus(StatusType.Frozen, 1);

            // WHEN
            unit.AddStatus(StatusType.Frozen, 4);

            // THEN
            Assert.That(unit.ActiveStatuses, Has.Count.EqualTo(1));
            Assert.That(unit.ActiveStatuses[0].RemainingDuration, Is.EqualTo(4));
        }

        [Test]
        public void AddStatus_TwoDifferentStatuses_KeepsBoth()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            unit.AddStatus(StatusType.Frozen, FreezeDuration);

            // WHEN
            unit.AddStatus(StatusType.Rooted, 2);

            // THEN
            Assert.That(unit.ActiveStatuses, Has.Count.EqualTo(2));
        }

        [Test]
        public void AddStatus_NoneType_IsIgnored()
        {
            // GIVEN
            GridUnit unit = CreateUnit();

            // WHEN
            unit.AddStatus(StatusType.None, FreezeDuration);

            // THEN
            Assert.That(unit.ActiveStatuses, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void AddStatus_NonPositiveDuration_IsIgnored(int duration)
        {
            // GIVEN
            GridUnit unit = CreateUnit();

            // WHEN
            unit.AddStatus(StatusType.Frozen, duration);

            // THEN
            Assert.That(unit.ActiveStatuses, Is.Empty);
        }

        [Test]
        public void RemoveStatus_ActiveStatus_ClearsIt()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            unit.AddStatus(StatusType.Frozen, FreezeDuration);

            // WHEN
            unit.RemoveStatus(StatusType.Frozen);

            // THEN
            Assert.That(unit.HasStatus(StatusType.Frozen), Is.False);
        }

        [Test]
        public void RemoveStatus_ActiveStatus_LeavesTheOtherStatusesIntact()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            unit.AddStatus(StatusType.Frozen, FreezeDuration);
            unit.AddStatus(StatusType.Rooted, 2);

            // WHEN
            unit.RemoveStatus(StatusType.Frozen);

            // THEN
            Assert.That(unit.HasStatus(StatusType.Rooted), Is.True);
        }

        [Test]
        public void RemoveStatus_StatusThatWasNeverApplied_LeavesTheUnitUnchanged()
        {
            // GIVEN
            GridUnit unit = CreateUnit();

            // WHEN
            unit.RemoveStatus(StatusType.Rooted);

            // THEN
            Assert.That(unit.ActiveStatuses, Is.Empty);
        }

        [Test]
        public void IsFrozen_WhileFrozenStatusIsActive_IsTrue()
        {
            // GIVEN
            GridUnit unit = CreateUnit();

            // WHEN
            unit.AddStatus(StatusType.Frozen, FreezeDuration);

            // THEN
            Assert.That(unit.IsFrozen, Is.True);
        }

        [Test]
        public void IsFrozen_AfterTheFrozenStatusIsRemoved_IsFalse()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            unit.AddStatus(StatusType.Frozen, FreezeDuration);

            // WHEN
            unit.RemoveStatus(StatusType.Frozen);

            // THEN
            Assert.That(unit.IsFrozen, Is.False);
        }

        [Test]
        public void ReceiveConversionAttempt_FromEnemyOnUnarmoredUnit_ReportsConverted()
        {
            // GIVEN
            GridUnit unit = CreateUnit();

            // WHEN
            ConversionOutcome outcome = unit.ReceiveConversionAttempt(EnemyPlayerId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.Converted));
        }

        [Test]
        public void ReceiveConversionAttempt_FromEnemyOnUnarmoredUnit_FlipsOwnership()
        {
            // GIVEN
            GridUnit unit = CreateUnit();

            // WHEN
            unit.ReceiveConversionAttempt(EnemyPlayerId);

            // THEN
            Assert.That(unit.PlayerId, Is.EqualTo(EnemyPlayerId));
        }

        [Test]
        public void ReceiveConversionAttempt_Converted_KeepsCardIdentityAndStatuses()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            unit.AddStatus(StatusType.Rooted, 2);

            // WHEN
            unit.ReceiveConversionAttempt(EnemyPlayerId);

            // THEN
            Assert.That(unit.CardId, Is.EqualTo(new CardId("acid_crawler")));
            Assert.That(unit.HasStatus(StatusType.Rooted), Is.True);
        }

        [Test]
        public void ReceiveConversionAttempt_FirstAttemptOnArmoredUnit_ReportsArmorStripped()
        {
            // GIVEN
            var unit = new GridUnit(UnitId, PlayerId, new CardId("bio_phalanx"), _spawnCoords, hasArmor: true);

            // WHEN
            ConversionOutcome outcome = unit.ReceiveConversionAttempt(EnemyPlayerId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.ArmorStripped));
        }

        [Test]
        public void ReceiveConversionAttempt_FirstAttemptOnArmoredUnit_StripsArmorWithoutFlippingOwnership()
        {
            // GIVEN
            var unit = new GridUnit(UnitId, PlayerId, new CardId("bio_phalanx"), _spawnCoords, hasArmor: true);

            // WHEN
            unit.ReceiveConversionAttempt(EnemyPlayerId);

            // THEN
            Assert.That(unit.HasArmor, Is.False);
            Assert.That(unit.PlayerId, Is.EqualTo(PlayerId));
        }

        [Test]
        public void ReceiveConversionAttempt_SecondAttemptOnArmoredUnit_ReportsConverted()
        {
            // GIVEN
            var unit = new GridUnit(UnitId, PlayerId, new CardId("bio_phalanx"), _spawnCoords, hasArmor: true);
            unit.ReceiveConversionAttempt(EnemyPlayerId);

            // WHEN
            ConversionOutcome outcome = unit.ReceiveConversionAttempt(EnemyPlayerId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.Converted));
        }

        [Test]
        public void ReceiveConversionAttempt_OnAnAlreadyConvertedUnit_DoesNotRestoreArmor()
        {
            // GIVEN
            var unit = new GridUnit(UnitId, PlayerId, new CardId("bio_phalanx"), _spawnCoords, hasArmor: true);
            unit.ReceiveConversionAttempt(EnemyPlayerId);
            unit.ReceiveConversionAttempt(EnemyPlayerId);

            // WHEN
            ConversionOutcome outcome = unit.ReceiveConversionAttempt(ThirdPlayerId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.Converted), "Armor is stripped for good, so a later attacker faces an unarmored unit.");
        }

        [Test]
        public void ReceiveConversionAttempt_OnFrozenUnit_ReportsImmune()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            unit.AddStatus(StatusType.Frozen, FreezeDuration);

            // WHEN
            ConversionOutcome outcome = unit.ReceiveConversionAttempt(EnemyPlayerId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.Immune));
        }

        [Test]
        public void ReceiveConversionAttempt_OnFrozenArmoredUnit_LeavesTheArmorIntact()
        {
            // GIVEN
            var unit = new GridUnit(UnitId, PlayerId, new CardId("bio_phalanx"), _spawnCoords, hasArmor: true);
            unit.AddStatus(StatusType.Frozen, FreezeDuration);

            // WHEN
            unit.ReceiveConversionAttempt(EnemyPlayerId);

            // THEN
            Assert.That(unit.HasArmor, Is.True);
        }

        [Test]
        public void ReceiveConversionAttempt_FromTheCurrentOwner_ReportsNone()
        {
            // GIVEN
            GridUnit unit = CreateUnit();

            // WHEN
            ConversionOutcome outcome = unit.ReceiveConversionAttempt(PlayerId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.None));
        }

        [Test]
        public void ReceiveConversionAttempt_OnDeadUnit_ReportsNone()
        {
            // GIVEN
            GridUnit unit = CreateUnit();
            unit.IsAlive = false;

            // WHEN
            ConversionOutcome outcome = unit.ReceiveConversionAttempt(EnemyPlayerId);

            // THEN
            Assert.That(outcome, Is.EqualTo(ConversionOutcome.None));
        }

        private static GridUnit CreateUnit()
        {
            return new GridUnit(UnitId, PlayerId, new CardId("acid_crawler"), _spawnCoords);
        }
    }
}
