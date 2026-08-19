namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// The deck's entire view of the resource system for discarding a card from hand. The caller reports only
    /// who is acting; the implementation owns the price and takes the payment.
    /// </summary>
    /// <remarks>
    /// The price takes no argument because it does not vary by card or by action the way a move's does — the
    /// ledger prices a discard once, per player, from that player's own configuration, the same way
    /// <c>IEnergyLedger</c> prices a Jump. Every member sits on the input path, once per attempted discard, so
    /// implementations must stay allocation-free and must publish no event from inside these calls: a discard is
    /// not yet committed while these run, and a rejected one must be indistinguishable on the bus from one that
    /// was never attempted. A false return from <see cref="TryPayForDiscard"/> must leave the balance untouched.
    /// </remarks>
    public interface IDiscardLedger
    {
        /// <summary>
        /// Reports whether the player currently holds enough Energy to discard a card, without charging anything.
        /// </summary>
        /// <param name="playerId">The player attempting the discard.</param>
        /// <returns>
        /// True when the discard price is within the player's balance; false when it is not, or when the player
        /// is unknown to the ledger.
        /// </returns>
        public bool CanAffordDiscard(int playerId);

        /// <summary>
        /// Prices a discard from the player's own configuration and deducts it from their balance in one step.
        /// </summary>
        /// <param name="playerId">The player attempting the discard.</param>
        /// <returns>
        /// True once the price has been deducted; false when the player cannot afford it or is unknown to the
        /// ledger.
        /// </returns>
        public bool TryPayForDiscard(int playerId);

        /// <summary>
        /// Returns to the player the exact price a discard was charged.
        /// </summary>
        /// <param name="playerId">The player who was charged.</param>
        /// <remarks>
        /// Only legal after a <see cref="TryPayForDiscard"/> that returned true for the same player; the amount
        /// is re-derived from the ledger's own configuration rather than remembered.
        /// </remarks>
        public void RefundDiscard(int playerId);
    }
}
