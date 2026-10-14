namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The collection tier a card belongs to, as the GDD roster assigns it.
    /// </summary>
    /// <remarks>
    /// <b>Rarity drives no match rule.</b> Per the GDD it sets a card's starting level and its DNA Strand drop rate, both of
    /// which belong to the progression system, which is not decided yet. Nothing in Match, Board, AI or Energy
    /// may read it; it is presentation and collection data only.
    /// <para>
    /// Values are explicit because the member is serialized into every card asset, so one may be added but never
    /// renumbered.
    /// </para>
    /// </remarks>
    public enum CardRarity
    {
        /// <summary>
        /// Unauthored, and what a card that sets no tier carries. This is never a fifth tier: <c>OnValidate</c> logs a
        /// warning on it, and a consumer must read it as "draw no rarity treatment".
        /// </summary>
        None = 0,

        /// <summary>
        /// The most plentiful tier.
        /// </summary>
        Common = 1,

        Rare = 2,

        Epic = 3,

        /// <summary>
        /// The scarcest tier.
        /// </summary>
        Legendary = 4,
    }
}
