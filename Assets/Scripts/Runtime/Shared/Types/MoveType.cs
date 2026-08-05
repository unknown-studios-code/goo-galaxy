namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The two fundamental board movement actions.
    /// The numeric value of each member is the exact hex distance that move requires,
    /// which movement validation relies on.
    /// </summary>
    public enum MoveType
    {
        /// <summary>
        /// Adjacent duplication: the source unit stays and a new unit appears on the target, increasing board presence.
        /// </summary>
        Clone = 1,

        /// <summary>
        /// Two-hex relocation: the existing unit leaves the source and lands on the target, leaving board presence unchanged.
        /// </summary>
        Jump = 2,
    }
}
