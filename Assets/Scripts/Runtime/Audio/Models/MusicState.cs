namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>Which music track an <see cref="Interfaces.IAudioService" /> is playing, if any.</summary>
    public enum MusicState
    {
        /// <summary>No music is playing.</summary>
        None = 0,

        /// <summary>The adaptive in-match track, shaped by the match-intensity and territory-balance parameters.</summary>
        Match = 1,
    }
}
