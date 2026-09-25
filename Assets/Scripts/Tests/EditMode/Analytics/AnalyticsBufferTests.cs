using System;
using GooGalaxy.Runtime.Analytics.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Analytics
{
    [TestFixture]
    public class AnalyticsBufferTests
    {
        [Test]
        public void Constructor_NoCapacityGiven_DefaultsTo512()
        {
            // GIVEN

            // WHEN
            var buffer = new AnalyticsBuffer();

            // THEN
            Assert.That(buffer.Capacity, Is.EqualTo(512));
        }

        [Test]
        public void Constructor_ExplicitCapacity_SetsTheCapacity()
        {
            // GIVEN

            // WHEN
            var buffer = new AnalyticsBuffer(10);

            // THEN
            Assert.That(buffer.Capacity, Is.EqualTo(10));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_CapacityBelowOne_ThrowsArgumentOutOfRangeException(int capacity)
        {
            // GIVEN

            // WHEN / THEN
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnalyticsBuffer(capacity));
        }

        [Test]
        public void TryAdd_BufferBelowCapacity_ReturnsTrueAndIncrementsCount()
        {
            // GIVEN
            var buffer = new AnalyticsBuffer(2);
            var record = AnalyticsRecord.ForPhaseChanged(0L, 0, MatchPhase.Standard);

            // WHEN
            bool wasAdded = buffer.TryAdd(in record);

            // THEN
            Assert.That((wasAdded, buffer.Count), Is.EqualTo((true, 1)));
        }

        [Test]
        public void TryAdd_BufferAtCapacity_ReturnsFalseAndLeavesCountUnchanged()
        {
            // GIVEN
            var buffer = new AnalyticsBuffer(1);
            var record = AnalyticsRecord.ForPhaseChanged(0L, 0, MatchPhase.Standard);
            buffer.TryAdd(in record);

            // WHEN
            bool wasAdded = buffer.TryAdd(in record);

            // THEN
            Assert.That((wasAdded, buffer.Count), Is.EqualTo((false, 1)));
        }

        [Test]
        public void IsFull_BelowCapacity_ReturnsFalse()
        {
            // GIVEN
            var buffer = new AnalyticsBuffer(2);
            var record = AnalyticsRecord.ForPhaseChanged(0L, 0, MatchPhase.Standard);
            buffer.TryAdd(in record);

            // WHEN / THEN
            Assert.That(buffer.IsFull, Is.False);
        }

        [Test]
        public void IsFull_AtCapacity_ReturnsTrue()
        {
            // GIVEN
            var buffer = new AnalyticsBuffer(1);
            var record = AnalyticsRecord.ForPhaseChanged(0L, 0, MatchPhase.Standard);
            buffer.TryAdd(in record);

            // WHEN / THEN
            Assert.That(buffer.IsFull, Is.True);
        }

        [Test]
        public void Clear_BufferWithRecords_ResetsCountToZero()
        {
            // GIVEN
            var buffer = new AnalyticsBuffer(4);
            var record = AnalyticsRecord.ForPhaseChanged(0L, 0, MatchPhase.Standard);
            buffer.TryAdd(in record);
            buffer.TryAdd(in record);

            // WHEN
            buffer.Clear();

            // THEN
            Assert.That(buffer.Count, Is.EqualTo(0));
        }

        [Test]
        public void Indexer_MultipleRecordsAdded_ReturnsThemInInsertionOrder()
        {
            // GIVEN
            var buffer = new AnalyticsBuffer(3);
            var first = AnalyticsRecord.ForPhaseChanged(0L, 0, MatchPhase.Standard);
            var second = AnalyticsRecord.ForPhaseChanged(0L, 1, MatchPhase.Standard);
            var third = AnalyticsRecord.ForPhaseChanged(0L, 2, MatchPhase.Standard);
            buffer.TryAdd(in first);
            buffer.TryAdd(in second);
            buffer.TryAdd(in third);

            // WHEN / THEN
            Assert.That((buffer[0].MatchOrdinal, buffer[1].MatchOrdinal, buffer[2].MatchOrdinal), Is.EqualTo((0, 1, 2)));
        }

        [Test]
        public void Indexer_IndexBelowZero_ThrowsArgumentOutOfRangeException()
        {
            // GIVEN
            var buffer = new AnalyticsBuffer(1);

            // WHEN / THEN
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[-1]);
        }

        [Test]
        public void Indexer_IndexAtOrAboveCount_ThrowsArgumentOutOfRangeException()
        {
            // GIVEN
            var buffer = new AnalyticsBuffer(2);
            var record = AnalyticsRecord.ForPhaseChanged(0L, 0, MatchPhase.Standard);
            buffer.TryAdd(in record);

            // WHEN / THEN
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[1]);
        }
    }
}
