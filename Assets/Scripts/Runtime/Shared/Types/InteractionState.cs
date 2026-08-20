namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The phase a player's card interaction is in, from picking a card to committing a deployment.
    /// </summary>
    /// <remarks>
    /// Not yet consumed by any runtime system. Treat the member semantics as provisional until a real input state
    /// machine adopts it.
    /// </remarks>
    public enum InteractionState
    {
        /// <summary>Nothing selected; input falls through to the board.</summary>
        Idle,

        /// <summary>A card is picked but no target has been chosen yet.</summary>
        CardSelected,

        /// <summary>The player is dragging the selected card toward the board.</summary>
        Dragging,

        /// <summary>A candidate target is being shown before the player commits.</summary>
        Previewing,

        /// <summary>A Protocol is picking its hex cluster.</summary>
        SpellTargeting,
    }
}
