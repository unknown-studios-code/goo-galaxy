namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// Why a match ended, carried by <see cref="MatchOutcome" />. A results screen renders from this rather
    /// than re-deriving the reason from the final scores.
    /// </summary>
    /// <remarks>
    /// Values are explicit for the same reason <see cref="MatchPhase" />'s are: the reason is destined for the
    /// wire, so once a networked session sends it a member may be added but never renumbered. Nothing
    /// serializes or transmits it yet, which is what made inserting <see cref="None" /> ahead of the others
    /// possible at all.
    /// <para>
    /// <see cref="Domination" /> and <see cref="Surrender" /> are declared ahead of the systems that raise
    /// them — the domination check lands with GOOM-12 and a surrender needs a networked session to concede in.
    /// </para>
    /// </remarks>
    public enum MatchEndReason
    {
        /// <summary>
        /// Nothing ended the match, because no match ended. The value a defaulted <see cref="MatchOutcome" />
        /// carries, so a struct nobody constructed never reads as a real ending.
        /// </summary>
        None = 0,

        /// <summary>
        /// The standard clock ran out and one player held more units than the other.
        /// </summary>
        TimeLimit = 1,

        /// <summary>
        /// One player held every unit on the board, so the match ended the moment the last enemy unit was
        /// converted or destroyed rather than at the clock. Declared but never raised until GOOM-12.
        /// </summary>
        Domination = 2,

        /// <summary>
        /// The two players held the same number of units when the match ran out of time to break the tie.
        /// <see cref="MatchOutcome.WinnerPlayerId" /> is <see cref="MatchOutcome.NoWinner" />.
        /// </summary>
        Draw = 3,

        /// <summary>
        /// A player conceded or forfeited, so the other one wins regardless of the unit counts. Declared but
        /// never raised until a networked session exists to concede in.
        /// </summary>
        Surrender = 4,
    }
}
