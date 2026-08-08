namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The three actions that put a unit on a sector: playing a card, and the two ways a unit already on the
    /// board can reach a new one. All three are landings, so all three convert and trigger abilities.
    /// </summary>
    /// <remarks>
    /// These values identify the action and nothing else. The hex distance a Clone or a Jump must cover is
    /// authored per card on <c>IMoveCapable</c>, so no caller may derive a distance from this enum.
    /// </remarks>
    public enum MoveType
    {
        /// <summary>
        /// Playing a card: a new unit of that card's type appears on the target, paying the card's Energy cost.
        /// The only action that introduces a card's identity to the board, and the only one with no source unit.
        /// </summary>
        Deploy = 0,

        /// <summary>
        /// Duplication: the source unit stays and a copy of it appears on the target, increasing board presence.
        /// </summary>
        Clone = 1,

        /// <summary>
        /// Relocation: the existing unit leaves the source and lands on the target, leaving board presence unchanged.
        /// </summary>
        Jump = 2,
    }
}
