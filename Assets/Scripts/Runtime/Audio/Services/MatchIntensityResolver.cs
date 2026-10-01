using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Audio.Services
{
    /// <summary>
    /// Maps where a match stands — its phase and its Standard clock — to the music intensity the authored bands give it.
    /// </summary>
    /// <remarks>Stateless, deterministic and allocation-free.</remarks>
    public static class MatchIntensityResolver
    {
        /// <summary>Resolves the intensity for a phase, or reports that the phase keeps whatever intensity came before it.</summary>
        /// <remarks>
        /// <para>
        /// <see cref="MatchPhase.None" />, <see cref="MatchPhase.Loading" /> and <see cref="MatchPhase.Countdown" /> resolve
        /// to <see cref="IntensityBands.Idle" />, and <see cref="MatchPhase.Overtime" /> to <see cref="IntensityBands.Overtime" />.
        /// </para>
        /// <para>
        /// In <see cref="MatchPhase.Standard" /> the final window is checked first, so <paramref name="remainingSeconds" /> at
        /// or below <see cref="IntensityBands.FinalWindowSeconds" /> wins over any elapsed band. Otherwise elapsed time picks
        /// the band, and a value exactly on a threshold belongs to the later band.
        /// </para>
        /// <para>
        /// <b>False means hold.</b> <see cref="MatchPhase.OvertimeCheck" />, <see cref="MatchPhase.Ended" />,
        /// <see cref="MatchPhase.Results" /> and any undeclared value return false with <paramref name="intensity" /> at zero,
        /// which the caller must not apply: the music keeps the intensity it had when that phase was entered.
        /// </para>
        /// </remarks>
        /// <param name="phase">The phase the match is in.</param>
        /// <param name="elapsedSeconds">Seconds of Standard play already gone. Read only in <see cref="MatchPhase.Standard" />.</param>
        /// <param name="remainingSeconds">Seconds left on the Standard clock. Read only in <see cref="MatchPhase.Standard" />.</param>
        /// <param name="bands">The authored thresholds.</param>
        /// <param name="intensity">The resolved intensity; zero when this returns false.</param>
        /// <returns>True when the phase decides an intensity; false when the caller keeps the previous one.</returns>
        public static bool TryResolve(MatchPhase phase, float elapsedSeconds, float remainingSeconds, in IntensityBands bands, out float intensity)
        {
            switch (phase)
            {
                case MatchPhase.None:
                case MatchPhase.Loading:
                case MatchPhase.Countdown:
                    intensity = bands.Idle;
                    return true;

                case MatchPhase.Standard:
                    intensity = ResolveStandard(elapsedSeconds, remainingSeconds, in bands);
                    return true;

                case MatchPhase.Overtime:
                    intensity = bands.Overtime;
                    return true;

                default:
                    intensity = 0f;
                    return false;
            }
        }

        private static float ResolveStandard(float elapsedSeconds, float remainingSeconds, in IntensityBands bands)
        {
            if (remainingSeconds <= bands.FinalWindowSeconds)
            {
                return bands.FinalWindow;
            }

            if (elapsedSeconds < bands.EarlyUntilElapsedSeconds)
            {
                return bands.Early;
            }

            if (elapsedSeconds < bands.MidUntilElapsedSeconds)
            {
                return bands.Mid;
            }

            return bands.Late;
        }
    }
}
