using System;

namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>
    /// Which outputs of an <see cref="AdaptiveMusicModel" /> one update moved, so a caller writes only those to the
    /// audio engine.
    /// </summary>
    [Flags]
    public enum MusicChange
    {
        /// <summary>Nothing moved; there is nothing to write.</summary>
        None = 0,

        /// <summary><see cref="AdaptiveMusicModel.Intensity" /> moved.</summary>
        Intensity = 1 << 0,

        /// <summary><see cref="AdaptiveMusicModel.Balance" /> moved.</summary>
        Balance = 1 << 1,

        /// <summary><see cref="AdaptiveMusicModel.MusicState" /> moved: start or stop the track it now names.</summary>
        Music = 1 << 2,

        /// <summary>The match ended with a stinger to play.</summary>
        Stinger = 1 << 3,
    }
}
