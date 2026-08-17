namespace GooGalaxy.Runtime.Deck.Models
{
    /// <summary>
    /// The outcome of playing a card from hand. Every rejection reason is a distinct code so callers
    /// (HUD feedback, AI, network reconciliation) can react without re-running validation.
    /// </summary>
    /// <remarks>
    /// Values are explicit because the code travels to the client as a rejection reason: adding a member is
    /// safe, renumbering or reordering one silently changes what an older peer reads.
    /// <para>
    /// This enum exists so <c>Runtime.Deck</c> does not re-export <c>MovementResult</c> and <c>SpellResult</c> to
    /// a HUD. A card play resolves down one of two very different paths, and a screen that had to know which of
    /// the two board enums it was holding would be switching on card type — exactly the decision
    /// <c>DeployController</c> exists to absorb. The board's codes are mapped into these in one place, so the
    /// grouping is auditable and the HUD depends on the Deck assembly alone.
    /// </para>
    /// <para>
    /// Every non-<see cref="Success" /> code leaves the hand exactly as it was. A rejected play costs the player
    /// no card and no Energy, so an illegal target is a free mistake.
    /// </para>
    /// </remarks>
    public enum CardPlayResult
    {
        /// <summary>
        /// The card was played, the board applied it, and the hand has rotated.
        /// </summary>
        Success = 0,

        /// <summary>
        /// No deck has been initialized for the acting player, so there is no hand to play from.
        /// </summary>
        UnknownPlayer = 1,

        /// <summary>
        /// The slot index names no card in the player's hand.
        /// </summary>
        SlotOutOfRange = 2,

        /// <summary>
        /// The slot names a card the registry does not know, so its authored data could not be resolved.
        /// </summary>
        CardNotFound = 3,

        /// <summary>
        /// The play carried the wrong number of target hexes: a troop takes exactly one, and a Protocol takes at
        /// least one. A Protocol's exact cluster size is authored on the card and is checked by the board, which
        /// reports it as <see cref="IllegalPlacement" />.
        /// </summary>
        InvalidTargetCount = 4,

        /// <summary>
        /// The acting player could not pay the card's Energy cost. Their balance is untouched.
        /// </summary>
        InsufficientEnergy = 5,

        /// <summary>
        /// The board refused the placement: the hex is occupied, blocked, hazardous, outside the player's
        /// territory, does not form the cluster the card authored, or the card cannot act there at all.
        /// </summary>
        IllegalPlacement = 6,

        /// <summary>
        /// No initialized board was available, or it could not carry the play out. Nothing was applied and
        /// nothing was charged.
        /// </summary>
        BoardUnavailable = 7,

        /// <summary>
        /// A deployment is already being resolved. Re-entrant plays from an event subscriber are rejected;
        /// queue the follow-up play instead.
        /// </summary>
        ResolverBusy = 8,
    }
}
