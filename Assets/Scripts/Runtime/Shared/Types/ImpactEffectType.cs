namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The kind of work one authored landing impact performs. Lives in Shared because cards author the value
    /// and the board's ability rules dispatch on it, so neither assembly has to depend on the other.
    /// </summary>
    /// <remarks>
    /// Values are explicit because they are authored into card assets and will travel to the client with the
    /// card definition: adding a member is safe, renumbering or reordering one silently repoints every asset
    /// already saved with the old number.
    /// </remarks>
    public enum ImpactEffectType
    {
        /// <summary>
        /// No impact. The value of a default-constructed effect, and a no-op when it reaches the resolver.
        /// </summary>
        None = 0,

        /// <summary>
        /// Applies a <see cref="StatusType"/> to the units the effect's radius and target filter select.
        /// </summary>
        ApplyStatus = 1,

        /// <summary>
        /// Leaves a hazard on the hex the acting unit vacated. Only a Jump vacates a hex, so this is a no-op
        /// on a Clone.
        /// </summary>
        SpawnHazard = 2,

        /// <summary>
        /// Removes the acting unit once the landing has fully resolved, per the GDD's step 6 self-cleanup.
        /// </summary>
        SelfDestruct = 3,
    }
}
