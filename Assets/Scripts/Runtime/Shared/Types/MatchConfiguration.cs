namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The settled configuration a match starts with, published on <see cref="Events.MatchEvents.MatchStarted" />.
    /// </summary>
    /// <remarks>
    /// The orchestrator settles every field before the match is announced: the seed every randomized system
    /// derives from, the two player ids the match is played between, and how long each timed phase lasts. A
    /// system reads what it needs from here rather than reaching for the authored asset, so a mid-match edit to
    /// that asset cannot change the rules of a match already in progress.
    /// <para>
    /// Default construction stays legal and yields <see cref="Seed" /> zero. That is a valid seed, not an unset
    /// marker: both peers derive the same sequence from it, so a match started from a defaulted configuration is
    /// still deterministic and still identical on both sides. Do not diagnose a zero seed as a bug.
    /// </para>
    /// <para>
    /// The seed-only constructor is the exception to the paragraph above, and the only one whose fields are
    /// genuinely unset: it leaves both player ids at <see cref="UnassignedPlayerId" /> and every duration at
    /// zero, exactly as a defaulted value does. It exists for a caller that only cares about the seed — a deck
    /// re-shuffle in a test, most of them. A phase duration of zero is <b>not</b> a phase that ends instantly;
    /// it means nobody authored one, and the orchestrator never publishes a configuration built this way.
    /// </para>
    /// </remarks>
    public readonly struct MatchConfiguration
    {
        /// <summary>
        /// The value a player id field carries when no player was named. Real player ids start at one
        /// throughout this project, so this can never be mistaken for one.
        /// </summary>
        public const int UnassignedPlayerId = 0;

        /// <summary>Builds a configuration around the seed both peers shuffle and draw from.</summary>
        /// <remarks>
        /// Leaves the player ids and every duration unset. Use the full constructor to announce a match.
        /// </remarks>
        /// <param name="seed">The deterministic seed. Any value is valid, zero included.</param>
        public MatchConfiguration(int seed)
            : this(seed, UnassignedPlayerId, UnassignedPlayerId, 0f, 0f, 0f) { }

        /// <summary>Builds the configuration the orchestrator announces a match with.</summary>
        /// <param name="seed">The deterministic seed. Any value is valid, zero included.</param>
        /// <param name="playerOneId">The first player's id.</param>
        /// <param name="playerTwoId">The second player's id.</param>
        /// <param name="standardDurationSeconds">Seconds of normal play before the unit counts are compared.</param>
        /// <param name="countdownSeconds">Seconds of pre-match countdown before normal play opens.</param>
        /// <param name="overtimeDurationSeconds">Seconds of sudden death after a tied comparison.</param>
        public MatchConfiguration(
            int seed,
            int playerOneId,
            int playerTwoId,
            float standardDurationSeconds,
            float countdownSeconds,
            float overtimeDurationSeconds
        )
        {
            Seed = seed;
            PlayerOneId = playerOneId;
            PlayerTwoId = playerTwoId;
            StandardDurationSeconds = standardDurationSeconds;
            CountdownSeconds = countdownSeconds;
            OvertimeDurationSeconds = overtimeDurationSeconds;
        }

        /// <summary>
        /// The deterministic seed every randomized match system derives from — deck shuffling first. Both peers
        /// receive the same value, so the same seed must always produce the same sequence.
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// The first player taking part, or <see cref="UnassignedPlayerId" /> when none was named. Nothing in
        /// this type says which side of the board they start on.
        /// </summary>
        public int PlayerOneId { get; }

        /// <summary>The second player taking part, or <see cref="UnassignedPlayerId" /> when none was named.</summary>
        public int PlayerTwoId { get; }

        /// <summary>Seconds of normal play before the unit counts decide the match. Zero when unauthored.</summary>
        public float StandardDurationSeconds { get; }

        /// <summary>Seconds of pre-match countdown before normal play opens. Zero when unauthored.</summary>
        public float CountdownSeconds { get; }

        /// <summary>
        /// Seconds of sudden death after a tied comparison. Zero when unauthored, and unread until GOOM-12
        /// implements overtime.
        /// </summary>
        public float OvertimeDurationSeconds { get; }
    }
}
