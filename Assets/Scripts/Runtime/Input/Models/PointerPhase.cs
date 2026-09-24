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
    }
}
