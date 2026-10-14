namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// A moment in the local player's own gesture worth acknowledging, raised by
    /// <see cref="Interfaces.IInteractionFeedbackSource" /> at most once per step of a gesture.
    /// </summary>
    /// <remarks>
    /// Local by construction: only the person holding this device produces these, so a consumer never filters them by
    /// seat. What a gesture finally does to the board is not reported here — that arrives on
    /// <see cref="Events.MatchEvents" /> like any other player's action.
    /// </remarks>
    public enum InteractionFeedback
    {
        /// <summary>Nothing happened. The value of a default-constructed feedback, never raised.</summary>
        None = 0,

        /// <summary>A card in hand or a unit on the board became the live selection.</summary>
        Selected = 1,

        /// <summary>The live selection started being dragged.</summary>
        DragStarted = 2,

        /// <summary>A tap, commit or release landed where the live selection has no legal action, and the selection was dropped.</summary>
        DropRejected = 3,

        /// <summary>The player deliberately let go of the live selection without acting.</summary>
        Cleared = 4,
    }
}
