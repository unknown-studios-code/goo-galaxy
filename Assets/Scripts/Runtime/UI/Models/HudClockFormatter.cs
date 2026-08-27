namespace GooGalaxy.Runtime.UI.Models
{
    /// <summary>
    /// Turns a whole-second countdown into the <c>mm:ss</c> text the match timer draws.
    /// </summary>
    /// <remarks>
    /// <b>Allocation-free for every value a match can reach.</b> The clock publishes at most once per second and
    /// has a few hundred reachable states, so every one of them is composed once into a table at type
    /// initialization and handed back by index afterwards. Interpolating instead would cost one string per
    /// second — sixty a minute, for a value that repeats every match.
    /// <para>
    /// A value above <see cref="MaxCachedSeconds" /> falls through to composition and does allocate. Nothing in
    /// this project authors a phase that long; the branch exists so an out-of-range value renders correctly
    /// rather than throwing.
    /// </para>
    /// <para>
    /// The table is immutable and never reset. Domain reload is disabled in this project, so it survives play
    /// sessions on purpose — there is no state in it that a second match could read wrong.
    /// </para>
    /// </remarks>
    public static class HudClockFormatter
    {
        /// <summary>What the timer shows when no clock is running — before a match, and after one is abandoned.</summary>
        /// <remarks>
        /// Deliberately not <c>00:00</c>: a match that never started has not run out of time, and a zeroed clock
        /// is exactly what a player reads as the end of one.
        /// </remarks>
        public const string Blank = "--:--";

        private const int SecondsPerMinute = 60;

        // 9:59. Both authored phase durations sit far below it, and the countdown lasts seconds.
        private const int MaxCachedSeconds = 599;

        private static readonly string[] _formattedSeconds = BuildCache();

        /// <summary>Formats a whole-second countdown as <c>mm:ss</c>.</summary>
        /// <param name="totalSeconds">Seconds left. Negative values and zero both render as <c>00:00</c>.</param>
        /// <returns>The formatted text. Allocation-free up to <see cref="MaxCachedSeconds" />.</returns>
        public static string Format(int totalSeconds)
        {
            if (totalSeconds <= 0)
            {
                return _formattedSeconds[0];
            }

            if (totalSeconds <= MaxCachedSeconds)
            {
                return _formattedSeconds[totalSeconds];
            }

            return Compose(totalSeconds);
        }

        private static string[] BuildCache()
        {
            string[] cache = new string[MaxCachedSeconds + 1];

            for (int i = 0; i < cache.Length; i++)
            {
                cache[i] = Compose(i);
            }

            return cache;
        }

        private static string Compose(int totalSeconds)
        {
            int minutes = totalSeconds / SecondsPerMinute;
            int seconds = totalSeconds - (minutes * SecondsPerMinute);

            return $"{minutes:00}:{seconds:00}";
        }
    }
}
