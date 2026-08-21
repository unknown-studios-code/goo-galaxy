using System;

namespace GooGalaxy.Runtime.Shared.Utils
{
    /// <summary>
    /// A seeded xorshift32 generator that produces byte-identical output on every platform, together with the
    /// derivation that splits one match seed into an independent stream per consumer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why neither standard generator is used.</b> <c>UnityEngine.Random</c> is global mutable state, so a
    /// sequence drawn from it would depend on whatever else in the frame happened to draw from it first — a
    /// particle system is enough to desynchronize two peers. <c>System.Random</c> carries no cross-platform
    /// stability guarantee: its algorithm is an implementation detail and has already changed once between
    /// runtimes, so an iOS and an Android peer of the same match are not promised the same sequence from the
    /// same seed. Anything a match replays or a peer verifies must come out identical on both, which is why the
    /// arithmetic below is spelled out as exact integer operations rather than delegated to a library. Do not
    /// "simplify" it back to either of them.
    /// </para>
    /// <para>
    /// <b>Why this is a mutable struct.</b> Drawing advances the generator, so its position has to live where
    /// the caller keeps it: a <c>readonly struct</c> would force every draw to hand back a replacement value,
    /// and a class would allocate one object per stream. Hold it in a local or a field and draw from that same
    /// storage — copying the value copies the position, which forks the stream rather than sharing it.
    /// </para>
    /// <para>
    /// Allocation-free on every non-throwing path, so drawing is legal in a hot path.
    /// </para>
    /// </remarks>
    public struct Xorshift32
    {
        // Any odd constant works; this is the 32-bit golden-ratio constant, chosen because multiplying a stream
        // id by an odd number is injective modulo 2^32 — two streams of one match can never collide on a seed.
        private const uint StreamSeedStride = 0x9E3779B9u;

        // xorshift32 has no way out of a zero state: every shift and xor of zero is zero, so a zero-seeded
        // generator returns zero forever and every draw from it comes back as index zero. Any seed reaching the
        // constructor is legal, including a derived one that lands on zero, so zero is substituted rather than
        // rejected.
        private const uint FallbackState = 0x6C078965u;

        private uint _state;

        /// <summary>Starts a stream at a seed.</summary>
        /// <param name="seed">The stream's seed, usually from <see cref="DeriveSeed" />. Any value is legal, zero included.</param>
        public Xorshift32(int seed)
        {
            unchecked
            {
                uint state = (uint)seed;

                _state = state == 0u ? FallbackState : state;
            }
        }

        /// <summary>
        /// Derives one stream's seed from the match seed, so several consumers of the single seed both peers
        /// agreed on draw unrelated sequences from it.
        /// </summary>
        /// <remarks>
        /// Deterministic in both arguments and injective in the stream id, so a stream is reproducible from the
        /// match seed alone — which is what lets a peer verify what the other drew.
        /// </remarks>
        /// <param name="matchSeed">The match's shared seed, from <c>MatchConfiguration.Seed</c>.</param>
        /// <param name="streamId">
        /// Identifies the consumer the stream belongs to. The non-negative half of the space is the player ids
        /// <c>DeckShuffler</c> splits by, and real ids start at one — <c>PlayerSlot.UnassignedId</c> is the zero
        /// that says "no player" — so the negative half is reserved for consumers that are not a player, however many
        /// players a match grows to. Every consumer must hold a distinct id; two sharing one draw the same sequence.
        /// </param>
        /// <returns>The seed to construct a generator with.</returns>
        public static int DeriveSeed(int matchSeed, int streamId)
        {
            unchecked
            {
                uint mixed = (uint)matchSeed + ((uint)streamId * StreamSeedStride);

                return (int)Avalanche(mixed);
            }
        }

        /// <summary>Advances the stream by one step and reduces the new state to an index below a bound.</summary>
        /// <remarks>
        /// The reduction is a modulo, which biases a range that does not divide 2^32 evenly slightly toward low
        /// indices. That is irrelevant at the ranges this project draws over and — the point of this whole type
        /// — identical on both peers. Advances exactly once per call, and does not advance when it throws.
        /// </remarks>
        /// <param name="exclusiveUpperBound">One past the highest index that may be returned. Must be positive.</param>
        /// <returns>An index in the range <c>[0, exclusiveUpperBound)</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The bound is zero or negative.</exception>
        public int NextIndex(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound), "Xorshift32 cannot pick an index out of an empty range.");
            }

            _state = NextState(_state);

            return (int)(_state % (uint)exclusiveUpperBound);
        }

        // xorshift32, Marsaglia's (13, 17, 5) triple: a full-period generator over the 2^32-1 non-zero states,
        // specified as exact integer operations rather than as a library behaviour, which is what makes it
        // reproducible across runtimes and architectures.
        private static uint NextState(uint state)
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;

                return state;
            }
        }

        // The murmur3 finalizer: a bijection over 32 bits, so distinct inputs stay distinct while neighbouring
        // stream ids stop producing neighbouring seeds — without it, adjacent seeds start xorshift in adjacent
        // states and the first few draws correlate visibly.
        private static uint Avalanche(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x85EBCA6Bu;
                value ^= value >> 13;
                value *= 0xC2B2AE35u;
                value ^= value >> 16;

                return value;
            }
        }
    }
}
