using System;

namespace GooGalaxy.Runtime.Audio.Models
{
    [Flags]
    internal enum IntensityBandIssue
    {
        None = 0,
        IntensityOutOfRange = 1 << 0,
        ThresholdsOutOfOrder = 1 << 1,
        IntensitiesNotAscending = 1 << 2,
    }
}
