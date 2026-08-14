using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// The board's entire view of the resource system. Movement reports who acted, which action, and what the
    /// acting entity is worth; the implementation decides the price and takes the payment.
    /// Used by Board (move resolution) and Energy (the ledger itself).
    /// </summary>
    /// <remarks>
    /// The board never computes a price, so the pricing rule stays on the implementation side and can be
    /// re-tuned without touching movement. Every member sits on the input path, once per attempted move, so
    /// implementations must stay allocation-free.
    /// </remarks>
    public interface IEnergyLedger
    {
        /// <summary>
        /// Prices a move and reports whether the player currently holds enough Energy for it, without charging
        /// anything or publishing anything.
        /// </summary>
        /// <param name="playerId">The player attempting the move.</param>
        /// <param name="moveType">The action being priced, which is what selects the pricing rule.</param>
        /// <param name="unitEnergyCost">The acting entity's authored Energy cost, which some prices scale from.</param>
        /// <returns>
        /// True when the resulting price is within the player's balance; false when it is not, or when the
        /// player is unknown to the ledger.
        /// </returns>
        public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost);

        /// <summary>
        /// Prices a move and deducts it from the player's balance in one step.
        /// </summary>
        /// <param name="playerId">The player attempting the move.</param>
        /// <param name="moveType">The action being priced, which is what selects the pricing rule.</param>
        /// <param name="unitEnergyCost">The acting entity's authored Energy cost, which some prices scale from.</param>
        /// <returns>
        /// True once the price has been deducted; false when the player cannot afford it or is unknown to the
        /// ledger.
        /// </returns>
        /// <remarks>
        /// An implementation must publish no event from inside this call. It runs before the board sets its
        /// re-entrancy latch, so a synchronously dispatching implementation whose subscriber resolved another
        /// move would charge twice for one action. A false return must leave the balance untouched — a rejected
        /// move is indistinguishable from a move that was never attempted.
        /// </remarks>
        public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost);

        /// <summary>
        /// Returns to the player the exact price a move was charged, re-derived from the same three arguments.
        /// </summary>
        /// <param name="playerId">The player who was charged.</param>
        /// <param name="moveType">The action that was charged, which is what selects the pricing rule.</param>
        /// <param name="unitEnergyCost">The acting entity's authored Energy cost, which some prices scale from.</param>
        /// <remarks>
        /// Only legal after a <see cref="TryPayForMove"/> that returned true for the same three arguments; the
        /// amount is re-derived rather than remembered, so any other argument refunds Energy that was never
        /// paid. Publishes nothing, for the same reason <see cref="TryPayForMove"/> does not: a charge and the
        /// refund that reverses it net to no change, and a move that never took effect must be indistinguishable
        /// on the bus from one that was never attempted.
        /// </remarks>
        public void RefundMove(int playerId, MoveType moveType, int unitEnergyCost);
    }
}
