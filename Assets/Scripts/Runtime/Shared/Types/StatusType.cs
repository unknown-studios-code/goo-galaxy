namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// A temporary condition a board unit can carry.
    /// Cross-feature contract: spells author them and the board's movement and conversion rules read them,
    /// so it lives in Shared rather than in either assembly.
    /// </summary>
    public enum StatusType
    {
        /// <summary>
        /// No condition. The value of a default-constructed marker, never stored on a unit.
        /// </summary>
        None = 0,

        /// <summary>
        /// Cryo-Stasis. The unit can neither Clone nor Jump, and is immune to conversion for the duration.
        /// </summary>
        Frozen = 1,

        /// <summary>
        /// Plasmic Leaper's root. The unit converts normally, but the marker persists until its controller
        /// completes their next successful deployment.
        /// </summary>
        Rooted = 2,
    }
}
