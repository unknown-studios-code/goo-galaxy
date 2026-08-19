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
    /// <see cref="Surrender" /> is the one member no system raises yet: conceding needs a networked session
    /// to concede in.
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
        /// A clock ran out with one player ahead on units — the standard clock, the overtime clock, or the hold
        /// an overtime lead has to survive to win outright.
        /// </summary>
        TimeLimit = 1,

        /// <summary>
        /// One player held every live unit on the board, so the match ended the moment the last enemy unit was
        /// converted or destroyed rather than at the clock. Both players at zero is <see cref="Draw" /> instead:
        /// a player holding nothing eliminated nothing, and neither side can deploy back onto an empty board.
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
