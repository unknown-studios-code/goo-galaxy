namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// Which units inside an impact's area the effect actually applies to, relative to the acting player.
    /// </summary>
    /// <remarks>
    /// Values are explicit because they are authored into card assets: adding a member is safe, renumbering
    /// one silently repoints every asset already saved with the old number.
    /// <para>
    /// Every filter is evaluated <b>after</b> standard conversion has run, because the GDD resolves a landing
    /// in a fixed order and the impact ability is step 4 while conversion is step 3. Ownership therefore reads
    /// as it stands at the moment the impact resolves, not as it stood when the unit landed.
    /// </para>
    /// </remarks>
    public enum TargetFilter
    {
        /// <summary>
        /// Only the unit that just landed. A Protocol has no unit acting on the board, so on a spell this
        /// selects nobody at all rather than falling back to some other unit.
        /// </summary>
        Self = 0,

        /// <summary>
        /// Units not owned by the acting player when the impact resolves — what conversion could not take. For
        /// the units this very landing took, use <see cref="NewlyConverted"/> instead.
        /// </summary>
        Enemy = 1,

        /// <summary>Every living unit in the area, friendly and hostile alike — Cryo-Stasis freezes both.</summary>
        All = 2,

        /// <summary>
        /// Units that belong to the acting player at the moment the impact resolves. Because conversion runs
        /// first, this <b>includes</b> the units this landing just converted; it is "everything that is mine
        /// now", where <see cref="NewlyConverted"/> is "what became mine because of this landing".
        /// </summary>
        Ally = 3,

        /// <summary>
        /// Exactly the units standard conversion flipped on this landing, and nothing else.
        /// </summary>
        /// <remarks>
        /// The distinction from <see cref="Ally"/> is the one the GDD's Plasmic Leaper depends on: its Binding
        /// Plasma roots "all newly converted enemy pieces" and explicitly "does NOT root pieces that were
        /// already owned by the player". An adjacent armored unit that only lost its shell was never converted,
        /// so it is not selected either.
        /// A Protocol converts nothing, so on a spell this selects nobody.
        /// </remarks>
        NewlyConverted = 4,
    }
}
