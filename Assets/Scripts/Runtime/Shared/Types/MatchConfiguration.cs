namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The settled configuration a match starts with, published on <see cref="Events.MatchEvents.MatchStarted" />.
    /// </summary>
    /// <remarks>
    /// The orchestrator settles every field before the match is announced: the seed every randomized system
    /// derives from, the two seats the match is played between, and how long each timed phase lasts. A system
    /// reads what it needs from here rather than reaching for the authored asset, so a mid-match edit to that
    /// asset cannot change the rules of a match already in progress.
    /// <para>
    /// Each seat carries what drives it alongside the id it is played under, which is how a system that cares —
    /// nothing here does — can tell a side played by the person holding the device from one played by a peer or
    /// by the game itself. This type reports that fact and acts on none of it.
    /// </para>
    /// <para>
    /// The overtime lead hold is the one authored value deliberately absent: nothing outside the orchestrator
    /// needs it, and the orchestrator captures it off the asset at match start on the same terms as the
    /// durations here. Add it if a HUD ever has to render the hold, or if a networked session has to agree on
    /// it — peers already agree on the overtime duration through this type, and the hold decides that phase.
    /// </para>
    /// <para>
    /// Default construction stays legal and yields <see cref="Seed" /> zero. That is a valid seed, not an unset
    /// marker: both peers derive the same sequence from it, so a match started from a defaulted configuration is
    /// still deterministic and still identical on both sides. Do not diagnose a zero seed as a bug.
    /// </para>
    /// <para>
    /// The seed-only constructor is the exception to the paragraph above, and the only one whose fields are
    /// genuinely unset: it leaves both seats unfilled — <see cref="PlayerSlot.UnassignedId" /> and
    /// <see cref="PlayerControl.Unassigned" /> — and every duration at zero, exactly as a defaulted value does.
    /// It exists for a caller that only cares about the seed — a deck re-shuffle in a test, most of them. A
    /// phase duration of zero is <b>not</b> a phase that ends instantly; it means nobody authored one, and the
    /// orchestrator never publishes a configuration built this way.
    /// </para>
    /// </remarks>
    public readonly struct MatchConfiguration
    {
        /// <summary>Builds a configuration around the seed both peers shuffle and draw from.</summary>
        /// <remarks>
        /// Leaves both seats and every duration unset. Use the full constructor to announce a match.
        /// </remarks>
        /// <param name="seed">The deterministic seed. Any value is valid, zero included.</param>
        public MatchConfiguration(int seed)
            : this(seed, default, default, 0f, 0f, 0f) { }

        /// <summary>Builds the configuration the orchestrator announces a match with.</summary>
        /// <param name="seed">The deterministic seed. Any value is valid, zero included.</param>
        /// <param name="playerOne">The first seat, with the id it is played under and what drives it.</param>
        /// <param name="playerTwo">The second seat, with the id it is played under and what drives it.</param>
        /// <param name="standardDurationSeconds">Seconds of normal play before the unit counts are compared.</param>
        /// <param name="countdownSeconds">Seconds of pre-match countdown before normal play opens.</param>
        /// <param name="overtimeDurationSeconds">Seconds of sudden death after a tied comparison.</param>
        public MatchConfiguration(
            int seed,
            PlayerSlot playerOne,
            PlayerSlot playerTwo,
            float standardDurationSeconds,
            float countdownSeconds,
            float overtimeDurationSeconds
        )
        {
            Seed = seed;
            PlayerOne = playerOne;
            PlayerTwo = playerTwo;
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
        /// The first seat taking part, unfilled when no player was named. Nothing in this type says which side
        /// of the board they start on.
        /// </summary>
        public PlayerSlot PlayerOne { get; }

        /// <summary>The second seat taking part, unfilled when no player was named.</summary>
        public PlayerSlot PlayerTwo { get; }

        /// <summary>Seconds of normal play before the unit counts decide the match. Zero when unauthored.</summary>
        public float StandardDurationSeconds { get; }

        /// <summary>Seconds of pre-match countdown before normal play opens. Zero when unauthored.</summary>
        public float CountdownSeconds { get; }

        /// <summary>Seconds of sudden death after a tied comparison. Zero when unauthored.</summary>
        public float OvertimeDurationSeconds { get; }
    }
}
