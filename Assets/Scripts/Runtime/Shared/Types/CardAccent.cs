namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The presentation family a card belongs to, which the HUD draws as the accent bar on its hand slot.
    /// Named for what the card does, never for the colour it happens to take.
    /// </summary>
    /// <remarks>
    /// <b>The role is authored; the colour is not.</b> A card asset carries this value and nothing else about its
    /// accent, so the colour resolves from a USS token at draw time and a colourblind stylesheet can swap the whole
    /// family. A <c>UnityEngine.Color</c> baked into the asset would sit permanently outside that swap, which is
    /// what this enum replaced: in Colorblind Palette Mode the second seat becomes orange, and an authored orange
    /// accent would have put the opponent's faction colour on a card in the local player's own hand.
    /// <para>
    /// <b>An accent groups cards; it does not identify one.</b> A second card that restricts the opponent's options
    /// is <see cref="Control" /> too and shares the bar. With an eight-card Kit and a roster past six specimens,
    /// more cards than roles is the expected state rather than a collision — the card's name is what tells two
    /// members of one family apart.
    /// </para>
    /// <para>
    /// Values are explicit because the member is serialized into every card asset, so one may be added but never
    /// renumbered.
    /// </para>
    /// </remarks>
    public enum CardAccent
    {
        /// <summary>
        /// No accent, and what a card that authors none carries. The slot draws no bar at all rather than a bar in
        /// some substituted default, which is why this is the zero value.
        /// </summary>
        None = 0,

        /// <summary>
        /// A card with no special property — the plain specimen the others are measured against.
        /// </summary>
        Baseline = 1,

        /// <summary>
        /// A card whose effect restricts what the opponent can do, rather than converting territory itself.
        /// </summary>
        Control = 2,

        /// <summary>
        /// A card whose landing converts across a wider area than a standard one.
        /// </summary>
        Explosive = 3,

        /// <summary>
        /// A card whose units survive what would flip a standard one.
        /// </summary>
        Defensive = 4,

        /// <summary>
        /// A card that leaves the board itself hostile after it lands.
        /// </summary>
        Corrosive = 5,
    }
}
