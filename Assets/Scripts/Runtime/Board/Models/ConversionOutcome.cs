namespace GooGalaxy.Runtime.Board.Models
{
    /// <summary>
    /// What a single conversion attempt did to the unit it targeted.
    /// Per the GDD conversion rules, one attempt either strips armor or flips ownership, never both, so an
    /// armored unit needs two separate attempts to change hands.
    /// </summary>
    public enum ConversionOutcome
    {
        /// <summary>
        /// The attempt had no effect: the unit is already owned by the attacking player, or is no longer alive.
        /// </summary>
        None = 0,

        /// <summary>
        /// Armored Membrane absorbed the attempt. The armor is gone and does not regenerate; ownership is unchanged.
        /// </summary>
        ArmorStripped = 1,

        /// <summary>
        /// The unit is Frozen and cannot be converted for the duration. Nothing changed.
        /// </summary>
        Immune = 2,

        /// <summary>
        /// Ownership flipped to the attacking player. Card identity and active statuses are kept.
        /// </summary>
        Converted = 3,
    }
}
