namespace GooGalaxy.Runtime.Input.Models
{
    /// <summary>What the pointer was doing when a <see cref="PointerSample" /> was taken.</summary>
    public enum PointerPhase
    {
        /// <summary>The pointer is not down. What a default-constructed sample carries.</summary>
        None = 0,

        /// <summary>The pointer has just gone down.</summary>
        Pressed = 1,

        /// <summary>The pointer has moved while down.</summary>
        Moved = 2,

        /// <summary>The pointer has just come up.</summary>
        Released = 3,

        /// <summary>A mouse pointer has moved while its press button is up. Never reported for a finger, which cannot hover.</summary>
        Hovered = 4,

        /// <summary>
        /// The pointer came up without the player lifting it: focus lost to a call or the notification shade, a device
        /// reset, or a touch the OS cancelled. Reported where it was last seen, which is not a choice to act on.
        /// </summary>
        Canceled = 5,
    }
}
