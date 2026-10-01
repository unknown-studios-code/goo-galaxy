namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>The short musical flourish played once a match ends, seen from the local player's side.</summary>
    /// <remarks>The Audio &amp; Sound Design chapter of the GDD names these DiscoveryComplete, ExpeditionRecalled and Stalemate.</remarks>
    public enum MatchStinger
    {
        /// <summary>No stinger to play.</summary>
        None = 0,

        /// <summary>The local player won.</summary>
        Victory = 1,

        /// <summary>The opponent won.</summary>
        Defeat = 2,

        /// <summary>Nobody won.</summary>
        Draw = 3,
    }
}
