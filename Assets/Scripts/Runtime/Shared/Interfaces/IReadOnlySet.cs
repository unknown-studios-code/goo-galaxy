using System.Collections.Generic;

namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// A read-only abstraction of a set.
    /// Provides a local implementation of the .NET 5+ IReadOnlySet interface for compatibility with .NET Standard 2.0 / Unity.
    /// </summary>
    /// <typeparam name="T">The type of elements in the set.</typeparam>
    public interface IReadOnlySet<T> : IReadOnlyCollection<T>
    {
        /// <summary>
        /// Determines whether the set contains a specific value.
        /// </summary>
        /// <param name="item">The item to locate in the set.</param>
        /// <returns>True if the set contains the item; otherwise, false.</returns>
        public bool Contains(T item);

        /// <summary>
        /// Determines whether the current set is a proper subset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>True if the current set is a proper subset; otherwise, false.</returns>
        public bool IsProperSubsetOf(IEnumerable<T> other);

        /// <summary>
        /// Determines whether the current set is a proper superset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>True if the current set is a proper superset; otherwise, false.</returns>
        public bool IsProperSupersetOf(IEnumerable<T> other);

        /// <summary>
        /// Determines whether the current set is a subset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>True if the current set is a subset; otherwise, false.</returns>
        public bool IsSubsetOf(IEnumerable<T> other);

        /// <summary>
        /// Determines whether the current set is a superset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>True if the current set is a superset; otherwise, false.</returns>
        public bool IsSupersetOf(IEnumerable<T> other);

        /// <summary>
        /// Determines whether the current set overlaps with the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>True if the current set overlaps; otherwise, false.</returns>
        public bool Overlaps(IEnumerable<T> other);

        /// <summary>
        /// Determines whether the current set and the specified collection contain the same elements.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>True if the set equals the collection; otherwise, false.</returns>
        public bool SetEquals(IEnumerable<T> other);
    }
}
