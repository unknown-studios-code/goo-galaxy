namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The settled configuration a match starts with, published on <see cref="Events.MatchEvents.MatchStarted" />.
    /// </summary>
    /// <remarks>
    /// Carries only the deterministic seed so far. Player ids and board configuration are the expected additions
    /// once match setup is real, and a system must not assume a field exists before it is declared here.
    /// <para>
    /// Default construction stays legal and yields <see cref="Seed" /> zero. That is a valid seed, not an unset
    /// marker: both peers derive the same sequence from it, so a match started from a defaulted configuration is
    /// still deterministic and still identical on both sides. Do not diagnose a zero seed as a bug.
    /// </para>
    /// </remarks>
    public readonly struct MatchConfiguration
    {
        /// <summary>Builds a configuration around the seed both peers shuffle and draw from.</summary>
        /// <param name="seed">The deterministic seed. Any value is valid, zero included.</param>
        public MatchConfiguration(int seed)
        {
            Seed = seed;
        }

        /// <summary>
        /// The deterministic seed every randomized match system derives from — deck shuffling first. Both peers
        /// receive the same value, so the same seed must always produce the same sequence.
        /// </summary>
        public int Seed { get; }
    }
}
