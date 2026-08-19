namespace GooGalaxy.Runtime.Match.Models
{
    /// <summary>
    /// The outcome of starting a match. Every rejection reason is a distinct code so callers (a lobby screen, a
    /// test, a networked session driver) can react without re-running setup.
    /// </summary>
    /// <remarks>
    /// Values are explicit for the same reason <c>CardPlayResult</c>'s are: the code is read outside this
    /// assembly, so adding a member is safe and renumbering one is not.
    /// <para>
    /// <b>Setup is all-or-nothing.</b> Every non-<see cref="Success" /> code leaves the match unstarted and the
    /// board untouched: no unit is seeded, no deck is dealt, no energy state is created, and nothing is
    /// published on <c>MatchEvents</c>. A failed start is indistinguishable on the bus from one that was never
    /// attempted, which is what makes retrying after a fix safe.
    /// </para>
    /// </remarks>
    public enum MatchStartResult
    {
        /// <summary>
        /// The board is seeded, both players hold a hand and an energy balance, and the countdown is running.
        /// </summary>
        Success = 0,

        /// <summary>
        /// A match is already under way. Nothing was re-seeded and nothing was published; end the running match
        /// before starting another.
        /// </summary>
        AlreadyRunning = 1,

        /// <summary>
        /// No Match Config asset is assigned, so nothing authors the phase durations or the starting position.
        /// </summary>
        ConfigMissing = 2,

        /// <summary>
        /// A system the match needs was never injected or has been destroyed — the grid, the unit registry, the
        /// card roster, the deck, or the energy ledger — or the grid presenter built no board.
        /// </summary>
        DomainUnavailable = 3,

        /// <summary>
        /// A starting placement could not be honoured: it names a card the roster does not carry, reuses a unit
        /// id, or targets a hex that is off the board, blocked, or already occupied. The whole opening position
        /// is refused rather than partially seeded, because a board missing one unit hands the other player a
        /// geometry advantage. The log names the offending placement's authored index.
        /// </summary>
        InvalidPlacement = 4,
    }
}
