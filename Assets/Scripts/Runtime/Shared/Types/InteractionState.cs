namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The phase a player's board or card interaction is in, from picking a source to committing an action.
    /// </summary>
    /// <remarks>
    /// Two selection paths share these members, because both end in the same commit: a card pressed in hand
    /// enters <see cref="CardSelected" />, a unit tapped on the board enters <see cref="UnitSelected" />, and
    /// from either one a moving pointer enters <see cref="Dragging" />. A Protocol the player can afford enters
    /// <see cref="SpellTargeting" /> instead of <see cref="CardSelected" />, because it is aimed rather than
    /// dropped. Nothing here says which hexes are legal — that is the enumerator's answer, and this type only
    /// tracks how far the gesture has got.
    /// </remarks>
    public enum InteractionState
    {
        /// <summary>Nothing selected; input falls through to the board.</summary>
        Idle = 0,

        /// <summary>A card is picked but no target has been chosen yet.</summary>
        CardSelected = 1,

        /// <summary>A unit already on the board is picked and its Clone and Jump targets are shown.</summary>
        UnitSelected = 2,

        /// <summary>The player is dragging the selected source toward the board.</summary>
        Dragging = 3,

        /// <summary>A candidate target is being shown before the player commits.</summary>
        Previewing = 4,

        /// <summary>A Protocol is aiming its hex cluster: the cluster under the pointer is previewed and a commit casts it.</summary>
        /// <remarks>
        /// Entered from hand for a Protocol — directly when affordable, or promoted from <see cref="CardSelected" />,
        /// <see cref="Dragging" /> or <see cref="Previewing" /> once it becomes so. Left only by cancellation, or back
        /// to <see cref="CardSelected" /> if the balance falls below the cost; <c>InteractionStateMachine</c> enforces
        /// both.
        /// </remarks>
        SpellTargeting = 5,
    }
}
