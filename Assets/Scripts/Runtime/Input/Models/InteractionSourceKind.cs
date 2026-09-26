namespace GooGalaxy.Runtime.Input.Models
{
    /// <summary>Which selection path an <see cref="InteractionSource" /> started.</summary>
    public enum InteractionSourceKind
    {
        /// <summary>Nothing is selected. What <see cref="InteractionSource.None" /> carries.</summary>
        None = 0,

        /// <summary>A card pressed in hand: a troop highlights every hex it could be deployed onto, a Protocol previews the cluster under the pointer.</summary>
        HandSlot = 1,

        /// <summary>A unit tapped on the board, which highlights its Clone and Jump targets.</summary>
        BoardUnit = 2,
    }
}
