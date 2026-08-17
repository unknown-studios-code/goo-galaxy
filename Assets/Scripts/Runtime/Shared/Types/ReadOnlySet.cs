using System;
using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Interfaces;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// Read-only wrapper over an existing <see cref="ISet{T}" />, exposing it as <see cref="IReadOnlySet{T}" /> so a
    /// caller cannot mutate a set it only needs to query.
    /// </summary>
    /// <remarks>
    /// Delegates every operation to the wrapped set, so a mutation made through the original reference is visible
    /// here — this wraps, it does not copy. By project convention <see cref="IReadOnlySet{T}" /> is contains/count
    /// only: enumerating one through the interface boxes the backing enumerator, so a consumer that needs to iterate
    /// asks for this concrete type instead. See <c>unity-performance-optimization.md</c> Rule 4a.
    /// </remarks>
    /// <typeparam name="T">The type of elements in the set.</typeparam>
    public class ReadOnlySet<T> : IReadOnlySet<T>
    {
        private readonly ISet<T> _set;

        /// <summary>Wraps an existing set for read-only access.</summary>
        /// <param name="set">The set to wrap. Must not be null.</param>
        /// <exception cref="ArgumentNullException">The set is null.</exception>
        public ReadOnlySet(ISet<T> set)
        {
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        /// <inheritdoc />
        public int Count => _set.Count;

        public bool Contains(T item)
        {
            return _set.Contains(item);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return _set.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return _set.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return _set.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return _set.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return _set.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return _set.SetEquals(other);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _set.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_set).GetEnumerator();
        }
    }
}
