namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>How large a landing's conversion was, as the sound effects grade it.</summary>
    public enum ConversionTier
    {
        /// <summary>Nothing was converted.</summary>
        None = 0,

        /// <summary>Exactly one unit was converted.</summary>
        Single = 1,

        /// <summary>A small group was converted; see <see cref="MatchSfxModel" /> for the bounds.</summary>
        Chain = 2,

        /// <summary>A large group was converted; see <see cref="MatchSfxModel" /> for the bound.</summary>
        Mass = 3,
    }
}
