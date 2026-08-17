using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Deck.Services
{
    /// <summary>
    /// Stateless, deterministic Kit shuffling. Runs once per player at match start; the resulting order is the
    /// whole of a match's card randomness.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why neither standard generator is used.</b> <c>UnityEngine.Random</c> is global mutable state, so the
    /// order a player receives would depend on whatever else in the frame happened to draw from it first — a
    /// particle system is enough to desynchronize two peers. <c>System.Random</c> carries no cross-platform
    /// stability guarantee: its algorithm is an implementation detail and has already changed once between
    /// runtimes, so an iOS and an Android peer of the same match are not promised the same sequence from the
    /// same seed. This shuffle must produce byte-identical output on both, so it carries its own xorshift32
    /// instead. Do not "simplify" it back to either of them.
    /// </para>
    /// <para>
    /// <b>Allocation.</b> Assumed to be setup, not a hot path: this runs once per player when a match starts, so
    /// growing the caller's result list is acceptable. Pass a list with the Kit's capacity and it allocates
    /// nothing at all.
    /// </para>
    /// </remarks>
    public static class DeckShuffler
    {
        // Any odd constant works; this is the 32-bit golden-ratio constant, chosen because multiplying a player
        // id by an odd number is injective modulo 2^32 — two players of one match can never collide on a seed.
        private const uint PlayerSeedStride = 0x9E3779B9u;

        // xorshift32 has no way out of a zero state: every shift and xor of zero is zero, so a zero-seeded generator
        // returns zero forever and the shuffle becomes the identity. Any seed reaching Shuffle is legal, including a
        // derived one that lands on zero, so zero is substituted rather than rejected.
        private const uint FallbackState = 0x6C078965u;

        /// <summary>
        /// Copies a Kit into the caller's list and shuffles it in place with a seeded Fisher-Yates pass.
        /// </summary>
        /// <remarks>
        /// The same seed and the same Kit always produce the same order, on every platform. The results list is
        /// cleared first, so it may be reused across players.
        /// </remarks>
        /// <param name="kit">The Kit's cards in authored order. Read only, never retained.</param>
        /// <param name="seed">The player's derived seed. See <see cref="DeriveSeed" />; any value is legal.</param>
        /// <param name="results">The caller's buffer, cleared and then filled with the shuffled order.</param>
        /// <exception cref="ArgumentNullException">The kit or the results buffer is null.</exception>
        public static void Shuffle(IReadOnlyList<CardId> kit, int seed, List<CardId> results)
        {
            if (kit == null)
            {
                throw new ArgumentNullException(nameof(kit), "DeckShuffler cannot shuffle a null kit.");
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "DeckShuffler needs a results buffer to write into.");
            }

            results.Clear();

            for (int i = 0; i < kit.Count; i++)
            {
                results.Add(kit[i]);
            }

            uint state = CreateState(seed);

            // Fisher-Yates, descending: index i is settled by swapping it with a uniformly chosen index in
            // [0, i]. The modulo is a small bias toward low indices for a range that does not divide 2^32
            // evenly, which is irrelevant here and — the point of this whole class — identical on both peers.
            for (int i = results.Count - 1; i > 0; i--)
            {
                state = NextState(state);

                int swapIndex = (int)(state % (uint)(i + 1));
                (results[i], results[swapIndex]) = (results[swapIndex], results[i]);
            }
        }

        /// <summary>
        /// Derives one player's shuffle seed from the match seed, so the two players of a match draw different
        /// orders from the one seed both peers agreed on.
        /// </summary>
        /// <remarks>
        /// Deterministic in both arguments and injective in the player id, so a player's order is reproducible
        /// from the match seed alone — which is what lets a peer verify the other's opening hand.
        /// </remarks>
        /// <param name="matchSeed">The match's shared seed, from <c>MatchConfiguration.Seed</c>.</param>
        /// <param name="playerId">The player the seed is for.</param>
        /// <returns>The seed to pass to <see cref="Shuffle" />.</returns>
        public static int DeriveSeed(int matchSeed, int playerId)
        {
            unchecked
            {
                uint mixed = (uint)matchSeed + ((uint)playerId * PlayerSeedStride);

                return (int)Avalanche(mixed);
            }
        }

        private static uint CreateState(int seed)
        {
            unchecked
            {
                uint state = (uint)seed;

                return state == 0u ? FallbackState : state;
            }
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
        // player ids stop producing neighbouring seeds — without it, adjacent seeds start xorshift in adjacent
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
