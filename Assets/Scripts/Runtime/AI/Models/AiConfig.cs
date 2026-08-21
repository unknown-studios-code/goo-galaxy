namespace GooGalaxy.Runtime.AI.Models
{
    /// <summary>
    /// The settled tuning a machine player acts on: how long it waits between actions, the Energy balance that
    /// cuts that wait short, whether a dead hand may be cycled, and the seed its randomness derives from.
    /// </summary>
    /// <remarks>
    /// A value type with no engine dependency, so the enumerator and the strategy never see the
    /// <c>ScriptableObject</c> these values were authored on. The controller reads the asset once and passes this
    /// down, which is the same guarantee <c>MatchConfiguration</c> gives the match: an Inspector edit made
    /// mid-match changes the next match rather than the running one.
    /// </remarks>
    public readonly struct AiConfig
    {
        /// <summary>
        /// The value <see cref="Seed" /> carries when nothing was authored, meaning the streams derive from
        /// <c>MatchConfiguration.Seed</c> instead. Zero rather than a sentinel because an unauthored asset
        /// deserializes to it, and deriving from the match is the behaviour that wants no authoring.
        /// </summary>
        public const int DerivedSeed = 0;

        /// <summary>Builds the tuning a machine player acts on.</summary>
        public AiConfig(float minThinkSeconds, float maxThinkSeconds, float energyCeilingThreshold, bool isDiscardEnabled, int seed)
        {
            MinThinkSeconds = minThinkSeconds;
            MaxThinkSeconds = maxThinkSeconds;
            EnergyCeilingThreshold = energyCeilingThreshold;
            IsDiscardEnabled = isDiscardEnabled;
            Seed = seed;
        }

        /// <summary>Seconds the loop waits at least, before it enumerates and acts again.</summary>
        public float MinThinkSeconds { get; }

        /// <summary>Seconds the loop waits at most. A ceiling on waiting, not a fixed cadence.</summary>
        public float MaxThinkSeconds { get; }

        /// <summary>
        /// The Energy balance at which the remaining wait is abandoned and the loop acts at once, so a balance
        /// sitting near the cap is spent rather than regenerated into nothing.
        /// </summary>
        public float EnergyCeilingThreshold { get; }

        /// <summary>Whether a tick that enumerates no legal action may discard a card instead of doing nothing.</summary>
        public bool IsDiscardEnabled { get; }

        /// <summary>
        /// The seed both of the machine player's streams derive from, or <see cref="DerivedSeed" /> when they
        /// derive from the match's own seed instead.
        /// </summary>
        public int Seed { get; }
    }
}
