using System.Collections.Generic;
using GooGalaxy.Runtime.AI.Interfaces;
using GooGalaxy.Runtime.AI.Models;
using GooGalaxy.Runtime.Shared.Utils;

namespace GooGalaxy.Runtime.AI.Services
{
    /// <summary>
    /// Picks one legal action uniformly at random, reading nothing into the board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The baseline opponent, and deliberately the whole of its intelligence: no preference for conversions, no
    /// defence, no contest of the centre, no lookahead. Every option the enumerator produced is equally likely,
    /// so the opponent exercises the same rules a human does without ever being good at them.
    /// </para>
    /// <para>
    /// <b>The stream is separate from the deck's, and that is the point.</b> Both derive from the one match seed
    /// through <see cref="Xorshift32.DeriveSeed" />. Sharing a stream with the shuffle would tie the opponent's
    /// choices to the cards it was dealt: the same opening hand would be answered the same way every match, which
    /// looks like a broken opponent rather than like correlated noise.
    /// </para>
    /// <para>
    /// <b>Uniform over options makes a Protocol rare, and that is a consequence to read rather than a bug to
    /// fix.</b> The list is lopsided by construction: a Deploy contributes one option per affordable card per
    /// sector of the deploy footprint, a Clone and a Jump one per reachable ring sector, while a Protocol
    /// contributes at most one per hand slot, because the enumerator builds a single candidate cluster for it.
    /// A Protocol is therefore drawn on the order of one time in a hundred on an open board, and a PvE session
    /// will look as though it holds Protocols it never plays. Weighting the draw by action shape would even
    /// that out and is exactly what this tier must not do — choosing what kind of action to take is strategy,
    /// and this opponent reads nothing into the board. The consequence worth carrying is that
    /// <b>a PvE session is not a Protocol balance signal</b>; those reads belong to human-versus-human play.
    /// </para>
    /// <para>
    /// Allocation-free on every path, and deterministic: the same seed and the same option list always select
    /// the same index.
    /// </para>
    /// </remarks>
    public sealed class RandomMoveStrategy : IMoveStrategy
    {
        /// <summary>The stream this strategy selects on, kept apart from the deck's so the two cannot correlate.</summary>
        /// <remarks>Negative on purpose — see <see cref="Xorshift32.DeriveSeed" /> for why, and for the rule that every stream id is distinct.</remarks>
        public const int SelectionStreamId = -1;

        // Not readonly, and it must not become readonly: drawing advances the generator by mutating it in place,
        // and a readonly field would hand every call a fresh copy of the same position — the stream would return
        // the same index forever.
        private Xorshift32 _random;

        /// <summary>Starts a selection stream at a seed.</summary>
        /// <param name="seed">The stream's seed, usually from <see cref="DeriveSeed" />. Any value is legal.</param>
        public RandomMoveStrategy(int seed)
        {
            _random = new Xorshift32(seed);
        }

        /// <summary>Derives this strategy's stream seed from the seed both peers agreed on.</summary>
        /// <param name="matchSeed">The match's shared seed, or the value authored to override it.</param>
        /// <returns>The seed to construct this strategy with.</returns>
        public static int DeriveSeed(int matchSeed)
        {
            return Xorshift32.DeriveSeed(matchSeed, SelectionStreamId);
        }

        /// <inheritdoc />
        /// <remarks>
        /// An empty or null list answers false and draws nothing, so the stream stays where it was: the generator
        /// throws on an empty range, and a tick with no legal action must not be able to desynchronize the
        /// sequence from a peer replaying it.
        /// </remarks>
        public bool TrySelect(IReadOnlyList<MoveOption> options, out MoveOption selected)
        {
            if (options == null || options.Count == 0)
            {
                selected = default;

                return false;
            }

            selected = options[_random.NextIndex(options.Count)];

            return true;
        }
    }
}
