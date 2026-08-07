using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// Contract exposing the impacts an entity resolves when it lands.
    /// Used by Board (ability resolution) and Cards (card definitions).
    /// </summary>
    /// <remarks>
    /// Implement it on a reference type, alongside <see cref="IMoveCapable"/>: the board keeps one capability
    /// object per live unit in an <c>IMoveCapable</c>-typed registry and tests it for this interface, and a
    /// value type stored behind an interface boxes on every store.
    /// </remarks>
    public interface IAbilityCapable
    {
        /// <summary>
        /// The impacts to resolve, in authored order, once movement and standard conversion have finished.
        /// Never null; an entity with no landing ability returns an empty list.
        /// </summary>
        /// <remarks>
        /// The list is owned by the implementation and must be treated as immutable. Read it with an indexed
        /// <c>for</c> loop — <c>foreach</c> over the interface boxes the backing enumerator, one allocation per
        /// landing.
        /// </remarks>
        public IReadOnlyList<ImpactEffect> LandingEffects { get; }
    }
}
