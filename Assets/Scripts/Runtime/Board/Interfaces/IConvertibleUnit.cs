using GooGalaxy.Runtime.Board.Models;

namespace GooGalaxy.Runtime.Board.Interfaces
{
    /// <summary>
    /// Contract for anything a landing can attempt to flip.
    /// The conversion system decides which units are adjacent to a landing; the unit itself decides what one
    /// attempt does to it, so armor, immunity, and ownership rules live in one place.
    /// </summary>
    /// <remarks>
    /// Named <c>IConvertibleUnit</c> rather than <c>IConvertible</c> to avoid colliding with
    /// <see cref="System.IConvertible" /> in files that also import <c>System</c>.
    /// </remarks>
    public interface IConvertibleUnit
    {
        /// <summary>The player who currently owns the unit.</summary>
        public int PlayerId { get; }

        /// <summary>
        /// Applies one conversion attempt from an adjacent landing and reports what it did.
        /// Mutating: a successful attempt changes ownership, and an absorbed one strips armor.
        /// </summary>
        /// <param name="newOwnerId">The player whose landing triggered the attempt.</param>
        /// <returns>The effect the attempt had, which callers use to drive feedback and scoring.</returns>
        public ConversionOutcome ReceiveConversionAttempt(int newOwnerId);
    }
}
