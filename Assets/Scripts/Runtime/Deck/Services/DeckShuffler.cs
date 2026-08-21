using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;

namespace GooGalaxy.Runtime.Deck.Services
{
    /// <summary>
    /// Stateless, deterministic Kit shuffling. Runs once per player at match start; the resulting order is the
    /// whole of a match's card randomness.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where the randomness comes from.</b> Every draw is taken from <see cref="Xorshift32" />, which carries
    /// the reasoning: neither <c>UnityEngine.Random</c> nor <c>System.Random</c> promises two peers the same
    /// sequence from the same seed, and this shuffle must produce byte-identical output on both. Do not
    /// "simplify" it back to either of them.
    /// </para>
    /// <para>
    /// <b>Allocation.</b> Assumed to be setup, not a hot path: this runs once per player when a match starts, so
    /// growing the caller's result list is acceptable. Pass a list with the Kit's capacity and it allocates
    /// nothing at all.
    /// </para>
    /// </remarks>
    public static class DeckShuffler
    {
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

            var random = new Xorshift32(seed);

            // Fisher-Yates, descending: index i is settled by swapping it with a uniformly chosen index in
            // [0, i]. The bound is i + 1 because the index may land on i itself, which is the case that leaves
            // a card where it already was.
            for (int i = results.Count - 1; i > 0; i--)
            {
                int swapIndex = random.NextIndex(i + 1);

                (results[i], results[swapIndex]) = (results[swapIndex], results[i]);
            }
        }

        /// <summary>
        /// Derives one player's shuffle seed from the match seed, so the two players of a match draw different
        /// orders from the one seed both peers agreed on.
        /// </summary>
        /// <remarks>
        /// Deterministic in both arguments and injective in the player id, so a player's order is reproducible
        /// from the match seed alone — which is what lets a peer verify the other's opening hand. The player is
        /// the stream <see cref="Xorshift32.DeriveSeed" /> splits the match seed by.
        /// </remarks>
        /// <param name="matchSeed">The match's shared seed, from <c>MatchConfiguration.Seed</c>.</param>
        /// <param name="playerId">The player the seed is for.</param>
        /// <returns>The seed to pass to <see cref="Shuffle" />.</returns>
        public static int DeriveSeed(int matchSeed, int playerId)
        {
            return Xorshift32.DeriveSeed(matchSeed, playerId);
        }
    }
}
