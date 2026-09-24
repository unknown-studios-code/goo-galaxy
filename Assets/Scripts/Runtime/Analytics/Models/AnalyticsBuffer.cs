using System;

namespace GooGalaxy.Runtime.Analytics.Models
{
    /// <summary>
    /// A fixed-capacity, insertion-ordered store for records waiting to be flushed.
    /// </summary>
    /// <remarks>
    /// Sized once at construction and never grown, so adding a record never allocates. A full buffer refuses the
    /// record rather than dropping one it already holds; the owner is expected to test <see cref="IsFull" /> and
    /// flush first. <see cref="Clear" /> zeroes the slots that were in use, so no stale <c>CardId</c> string is kept
    /// alive; the capacity is kept and nothing is reallocated.
    /// </remarks>
    public class AnalyticsBuffer
    {
        public const int DefaultCapacity = 512;

        private readonly AnalyticsRecord[] _records;

        private int _count;

        /// <summary>Builds an empty buffer.</summary>
        /// <param name="capacity">How many records it holds before it is full. Must be at least one.</param>
        /// <exception cref="ArgumentOutOfRangeException">The capacity is below one.</exception>
        public AnalyticsBuffer(int capacity = DefaultCapacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "An analytics buffer needs room for at least one record.");
            }

            _records = new AnalyticsRecord[capacity];
        }

        public int Count => _count;

        public int Capacity => _records.Length;

        /// <summary>Whether the next <see cref="TryAdd" /> would be refused.</summary>
        public bool IsFull => _count == _records.Length;

        /// <summary>The record at a position, oldest first. Returned by reference, so reading it copies nothing.</summary>
        /// <param name="index">Zero-based position, below <see cref="Count" />.</param>
        /// <exception cref="ArgumentOutOfRangeException">The index is negative or not below <see cref="Count" />.</exception>
        public ref readonly AnalyticsRecord this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be non-negative and below Count.");
                }

                return ref _records[index];
            }
        }

        /// <summary>Appends a record, unless the buffer is full.</summary>
        /// <param name="record">The record to store. Copied in; the caller keeps no link to the stored value.</param>
        /// <returns>True when the record was stored; false when the buffer was already full.</returns>
        public bool TryAdd(in AnalyticsRecord record)
        {
            if (IsFull)
            {
                return false;
            }

            _records[_count] = record;
            _count++;

            return true;
        }

        /// <summary>Empties the buffer, zeroing the slots that were in use. The capacity is kept.</summary>
        public void Clear()
        {
            Array.Clear(_records, 0, _count);
            _count = 0;
        }
    }
}
