namespace GooGalaxy.Runtime.Match.Models
{
    /// <summary>
    /// The outcome of discarding a card from hand. Every rejection reason is a distinct code so callers (HUD
    /// feedback, AI, network reconciliation) can react without re-running validation.
    /// </summary>
    /// <remarks>
    /// Values are explicit for the same reason <see cref="CardPlayResult" />'s are.
    /// <para>
    /// Every non-<see cref="Success" /> code leaves the hand, the cycle and the player's balance exactly as
    /// they were, and publishes nothing on <c>MatchEvents</c> — a rejected discard is indistinguishable on the
    /// bus from one that was never attempted.
    /// </para>
    /// </remarks>
    public enum CardDiscardResult
    {
        /// <summary>
        /// The card was discarded, the cycle rotated, and the hand now holds its replacement.
        /// </summary>
        Success = 0,

        /// <summary>
        /// No deck has been initialized for the acting player, so there is no hand to discard from.
        /// </summary>
        UnknownPlayer = 1,

        /// <summary>
        /// The slot index names no card in the player's hand.
        /// </summary>
        SlotOutOfRange = 2,

        /// <summary>
        /// The acting player could not pay the discard's Energy cost. Their balance is untouched.
        /// </summary>
        InsufficientEnergy = 3,

        /// <summary>
        /// A deployment or another discard is already being resolved. Re-entrant discards from an event
        /// subscriber are rejected; queue the follow-up discard instead.
        /// </summary>
        DeckBusy = 4,

        /// <summary>
        /// A dependency this controller needs was never injected — the deck, the ledger, or the deploy
        /// controller — or the deck refused to carry the discard out. Nothing was applied, and any charge
        /// already taken has been refunded.
        /// </summary>
        DeckUnavailable = 5,

        /// <summary>
        /// The match is not in a phase that accepts plays — it has not started, is still counting down, or has
        /// already ended. Nothing was read from the hand and nothing was charged. Distinct from
        /// <see cref="DeckUnavailable" />, which reports a deck that is missing rather than a match that is
        /// closed.
        /// </summary>
        MatchNotInPlay = 6,
    }
}
