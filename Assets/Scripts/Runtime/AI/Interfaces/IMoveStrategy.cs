using System.Collections.Generic;
using GooGalaxy.Runtime.AI.Models;

namespace GooGalaxy.Runtime.AI.Interfaces
{
    /// <summary>
    /// Picks the one action a machine player commits to out of everything that is legal for it right now.
    /// </summary>
    /// <remarks>
    /// The seam every tier of opponent difficulty replaces: the enumeration in front of it and the submission
    /// behind it are the same for a random player and for a heuristic one, so a smarter opponent is a second
    /// implementation of this interface rather than a second controller.
    /// <para>
    /// An implementation reads the options and nothing else. It must not touch the board, the ledger or the hand
    /// — every option it is handed has already been validated against all three, and re-deriving any of it here
    /// is what would let a strategy offer an action the resolver refuses.
    /// </para>
    /// </remarks>
    public interface IMoveStrategy
    {
        /// <summary>Picks one action out of the legal set.</summary>
        /// <remarks>
        /// Called once per think tick, so an implementation stays allocation-free. The options are borrowed for
        /// the duration of the call and must not be retained — a Protocol option among them borrows a cluster
        /// buffer the caller reuses on the next tick.
        /// </remarks>
        /// <param name="options">
        /// Every legal action, in enumeration order. May be empty, which is not an error: a player with no
        /// affordable card and no movable unit has nothing to pick from.
        /// </param>
        /// <param name="selected">The chosen action, or a default value when none was chosen.</param>
        /// <returns>True when an action was chosen; false when the set was empty or the strategy declined.</returns>
        public bool TrySelect(IReadOnlyList<MoveOption> options, out MoveOption selected);
    }
}
