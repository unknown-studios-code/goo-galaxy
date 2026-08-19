namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The stage a match is in, published on <see cref="Events.MatchEvents.MatchPhaseChanged" />. Every system
    /// that has to know whether the board accepts input reads this rather than inferring it from the clock.
    /// </summary>
    /// <remarks>
    /// Values are explicit because the phase travels to the client: adding a member is safe, renumbering or
    /// reordering one silently changes what an older peer reads.
    /// <para>
    /// The full set is declared even though the orchestrator does not reach all of it yet.
    /// <see cref="Overtime" /> and the <see cref="MatchEndReason.Domination" /> path arrive with GOOM-12, and
    /// declaring their members now is what keeps that change from renumbering the ones already on the wire.
    /// A member being declared is therefore not a promise that something transitions into it today.
    /// </para>
    /// </remarks>
    public enum MatchPhase
    {
        /// <summary>
        /// No match exists. The state a fresh orchestrator holds, and the one it returns to when a start is
        /// abandoned before the board was seeded.
        /// </summary>
        None = 0,

        /// <summary>
        /// The board is being built and seeded. The grid, the starting units, the energy states and both decks
        /// are established here; nothing accepts player input yet.
        /// </summary>
        Loading = 1,

        /// <summary>
        /// The pre-match countdown is running. The board is complete and visible, and plays are still refused.
        /// </summary>
        Countdown = 2,

        /// <summary>
        /// Normal play. The only phase in which a card can be played or discarded, and the only one the match
        /// clock counts down in.
        /// </summary>
        Standard = 3,

        /// <summary>
        /// The instant at the end of <see cref="Standard" /> where the unit counts are compared. A clear lead
        /// ends the match; a tie is what <see cref="Overtime" /> exists for.
        /// </summary>
        OvertimeCheck = 4,

        /// <summary>
        /// Sudden death after a tied <see cref="OvertimeCheck" />. Declared but never entered until GOOM-12
        /// implements it.
        /// </summary>
        Overtime = 5,

        /// <summary>
        /// The match is over and its outcome has been published. Nothing on the board resolves any more.
        /// </summary>
        Ended = 6,

        /// <summary>
        /// The post-match results screen is up. Declared for the screen that owns it; the orchestrator does not
        /// transition into it today.
        /// </summary>
        Results = 7,
    }
}
