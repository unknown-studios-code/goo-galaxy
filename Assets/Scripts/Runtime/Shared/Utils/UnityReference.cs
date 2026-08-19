using UnityEngine;

namespace GooGalaxy.Runtime.Shared.Utils
{
    /// <summary>
    /// Null checks for a dependency held behind an interface whose implementation is a <see cref="Object" />.
    /// </summary>
    /// <remarks>
    /// A field typed as an interface binds C#'s reference <c>==</c> rather than <see cref="Object" />'s overload,
    /// so a destroyed MonoBehaviour reaching a consumer through an interface seam reads as alive and the consumer
    /// proceeds against an object Unity has already torn down — silently, because the managed state behind it is
    /// usually still intact enough not to throw. Every such seam in this project routes its guard through here
    /// instead of comparing the field directly. Allocation-free on every path: the constraint keeps the argument a
    /// reference type, so the type test is a cast rather than a boxing conversion.
    /// </remarks>
    public static class UnityReference
    {
        /// <summary>
        /// Reports whether a dependency held behind an interface is missing or has already been destroyed.
        /// </summary>
        /// <typeparam name="T">The interface the dependency is held as.</typeparam>
        /// <param name="reference">The dependency to test. May be null.</param>
        /// <returns>
        /// True when the reference is null, or when it is a <see cref="Object" /> Unity has destroyed; false when
        /// the dependency is safe to call.
        /// </returns>
        public static bool IsUnavailable<T>(T reference)
            where T : class
        {
            if (reference == null)
            {
                return true;
            }

            return reference is Object unityObject && unityObject == null;
        }
    }
}
