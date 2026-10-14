namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>A global parameter of the FMOD Studio project that gameplay drives.</summary>
    public enum AudioParameter
    {
        /// <summary>How tense the match is, from 0 (calm) to 1 (overtime).</summary>
        MatchIntensity = 0,

        /// <summary>Who holds more of the board, from -1 (the opponent holds all of it) to 1 (the local player does).</summary>
        TerritoryBalance = 1,
    }
}
