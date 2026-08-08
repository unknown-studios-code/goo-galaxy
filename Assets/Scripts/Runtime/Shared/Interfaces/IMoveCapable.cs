namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// Contract defining movement capability characteristics for entities.
    /// Used by Board (movement validation) and Cards (card definitions).
    /// </summary>
    /// <remarks>
    /// Implement it on a reference type. Movement keeps one capability per live unit in an
    /// <c>IMoveCapable</c>-typed registry, and a value type stored behind an interface boxes on every store.
    /// </remarks>
    public interface IMoveCapable
    {
        /// <summary>Whether the entity may duplicate itself onto a nearby hex.</summary>
        public bool CanClone { get; }

        /// <summary>Whether the entity may relocate itself to a nearby hex.</summary>
        public bool CanJump { get; }

        /// <summary>
        /// Exact hex distance a Clone by this entity must cover. One for every launch card; a card authoring a
        /// wider reach clones only at that distance, never at anything shorter.
        /// </summary>
        public int CloneDistance { get; }

        /// <summary>
        /// Exact hex distance a Jump by this entity must cover. Two for every launch card; a card authoring a
        /// wider reach jumps only at that distance, never at anything shorter.
        /// </summary>
        public int JumpDistance { get; }

        /// <summary>
        /// Whether the entity may land on a hex carrying a hazard. Plasmic Leaper's Hover is the only authored
        /// case; every other entity is rejected with <c>MovementResult.TargetHazardous</c>.
        /// </summary>
        public bool IgnoresHazards { get; }
    }
}
