namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// What drives one seat at a match: nobody yet, the person holding this device, a peer across the wire, or
    /// the game itself.
    /// </summary>
    /// <remarks>
    /// <see cref="Unassigned" /> holds zero, and no member may ever be renumbered onto it. Every consumer meets
    /// this enum through a <see cref="PlayerSlot" /> sooner or later, and a defaulted slot is what
    /// <see cref="MatchConfiguration" />'s seed-only constructor leaves behind — the test suite builds one with
    /// <c>new MatchConfiguration()</c> — so a zero-valued <see cref="Machine" /> would silently declare both of
    /// that match's seats machine-driven.
    /// </remarks>
    public enum PlayerControl
    {
        /// <summary>Nobody was named for the seat. What a defaulted <see cref="PlayerSlot" /> carries.</summary>
        Unassigned = 0,

        /// <summary>Driven by the person holding this device.</summary>
        LocalHuman = 1,

        /// <summary>Driven by a person on another device, reaching this one through the session.</summary>
        RemoteHuman = 2,

        /// <summary>Driven by the game itself — the AI opponent of a single-player match.</summary>
        Machine = 3,
    }
}
