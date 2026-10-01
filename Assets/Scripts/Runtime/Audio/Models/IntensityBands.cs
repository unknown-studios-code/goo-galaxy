namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>
    /// The authored thresholds that map a match's phase and clock to a music intensity, as the engine-free values
    /// <see cref="Services.MatchIntensityResolver" /> reads.
    /// </summary>
    /// <remarks>
    /// Intensities are in the 0..1 range of the FMOD <c>MatchIntensity</c> parameter, and every threshold is in seconds
    /// of the Standard phase. Constructed as given and never validated here — <c>AudioConfigSO</c> owns the authoring
    /// rules, so this type can carry deliberately broken values into a test.
    /// </remarks>
    public readonly struct IntensityBands
    {
        public IntensityBands(
            float idle,
            float early,
            float earlyUntilElapsedSeconds,
            float mid,
            float midUntilElapsedSeconds,
            float late,
            float finalWindow,
            float finalWindowSeconds,
            float overtime
        )
        {
            Idle = idle;
            Early = early;
            EarlyUntilElapsedSeconds = earlyUntilElapsedSeconds;
            Mid = mid;
            MidUntilElapsedSeconds = midUntilElapsedSeconds;
            Late = late;
            FinalWindow = finalWindow;
            FinalWindowSeconds = finalWindowSeconds;
            Overtime = overtime;
        }

        /// <summary>Intensity before play opens: loading and the pre-match countdown.</summary>
        public float Idle { get; }

        /// <summary>Intensity from the start of Standard play until <see cref="EarlyUntilElapsedSeconds" />.</summary>
        public float Early { get; }

        /// <summary>Elapsed Standard seconds at which <see cref="Early" /> gives way to <see cref="Mid" />.</summary>
        public float EarlyUntilElapsedSeconds { get; }

        /// <summary>Intensity from <see cref="EarlyUntilElapsedSeconds" /> until <see cref="MidUntilElapsedSeconds" />.</summary>
        public float Mid { get; }

        /// <summary>Elapsed Standard seconds at which <see cref="Mid" /> gives way to <see cref="Late" />.</summary>
        public float MidUntilElapsedSeconds { get; }

        /// <summary>Intensity for the rest of Standard play, until the final window opens.</summary>
        public float Late { get; }

        /// <summary>Intensity once <see cref="FinalWindowSeconds" /> or fewer remain on the Standard clock.</summary>
        public float FinalWindow { get; }

        /// <summary>Remaining Standard seconds at or below which <see cref="FinalWindow" /> applies, whatever has elapsed.</summary>
        public float FinalWindowSeconds { get; }

        /// <summary>Intensity for the whole of overtime.</summary>
        public float Overtime { get; }
    }
}
