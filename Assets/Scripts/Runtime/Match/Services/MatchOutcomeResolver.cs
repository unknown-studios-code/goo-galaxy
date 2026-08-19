using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Match.Services
{
    /// <remarks>
    /// The two rules that turn a pair of unit counts into an ending, and the only place either is written.
    /// The orchestrator asks both from more than one path — domination from the deferred recount, the count
    /// comparison from the standard expiry and again from the overtime one — so keeping them here is what
    /// stops the same comparison being spelled out twice and drifting.
    /// <para>
    /// <b>Stateless and allocation-free on every path</b>, like <see cref="MatchScoreCounter" />: it is asked
    /// on every frame a count moves, and overtime doubles how often that is.
    /// </para>
    /// <para>
    /// Neither rule reads the board. Both take the counts already settled by a single walk of the registry,
    /// which is what keeps the pair describing the same board rather than two consecutive reads of it.
    /// </para>
    /// </remarks>
    internal static class MatchOutcomeResolver
    {
        /// <remarks>
        /// True when exactly one player has been wiped off the board and the other still holds units, which is
        /// the instant the match is over regardless of the clock.
        /// <para>
        /// <b>Both sides at zero is not a domination.</b> It is a draw, and that is a design decision rather
        /// than an unhandled case: a player holding no units eliminated nothing, so there is no one to credit,
        /// and neither side can recover — a deployment has to land adjacent to territory its owner already
        /// holds, which neither player has. The counts are left to the clocks: normal play ending level opens
        /// overtime, and the overtime clock is what finally publishes the draw.
        /// </para>
        /// <para>
        /// <paramref name="winnerId" /> is <see cref="MatchOutcome.NoWinner" /> on every false return, so a
        /// caller that ignores the bool cannot read a player out of it. Counts below zero cannot occur and are
        /// treated as wiped.
        /// </para>
        /// </remarks>
        internal static bool TryResolveDomination(int playerOneUnits, int playerTwoUnits, int playerOneId, int playerTwoId, out int winnerId)
        {
            winnerId = MatchOutcome.NoWinner;

            bool isPlayerOneWiped = playerOneUnits <= 0;
            bool isPlayerTwoWiped = playerTwoUnits <= 0;

            if (isPlayerOneWiped == isPlayerTwoWiped)
            {
                return false;
            }

            winnerId = isPlayerOneWiped ? playerTwoId : playerOneId;

            return true;
        }

        /// <remarks>
        /// The comparison a clock running out settles: the higher count wins by
        /// <see cref="MatchEndReason.TimeLimit" />, and level counts are <see cref="MatchOutcome.Drawn" />.
        /// <para>
        /// Asked by both expiries. At the end of normal play a level result is not an ending at all — the
        /// orchestrator opens overtime instead of publishing this — so the draw this returns only ever reaches
        /// a subscriber from the overtime clock, which has nothing left to break the tie with.
        /// </para>
        /// </remarks>
        internal static MatchOutcome ResolveByUnitCount(int playerOneUnits, int playerTwoUnits, int playerOneId, int playerTwoId)
        {
            if (playerOneUnits == playerTwoUnits)
            {
                return MatchOutcome.Drawn;
            }

            return new MatchOutcome(playerOneUnits > playerTwoUnits ? playerOneId : playerTwoId, MatchEndReason.TimeLimit);
        }
    }
}
