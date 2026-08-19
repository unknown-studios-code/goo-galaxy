using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Match.Models
{
    /// <remarks>
    /// The match's own state, and the authority on which phase may follow which. Keeping the table here rather
    /// than in the orchestrator is what makes an illegal sequence testable without a scene: a transition that
    /// the table refuses leaves this object exactly as it was, so a caller that ignores the returned bool
    /// cannot corrupt the phase by trying.
    /// <para>
    /// Engine-free on purpose. Scores are cached per player so the orchestrator can tell a count that moved
    /// from one that was merely recounted, which is what keeps <c>MatchEvents.ScoreChanged</c> a statement
    /// about change rather than about work.
    /// </para>
    /// </remarks>
    internal sealed class MatchState
    {
        // A match is two players; a sizing hint rather than a rule.
        private const int PlayersPerMatch = 2;

        private readonly Dictionary<int, int> _scores = new(PlayersPerMatch);

        internal MatchPhase Phase { get; private set; } = MatchPhase.None;

        /// <remarks>
        /// Seconds of scaled match time accumulated while <see cref="MatchPhase.Standard" /> was running, and
        /// only then: the orchestrator ticks this from the same early-returning <c>Update</c> that drives the
        /// clock, so the pre-match countdown contributes nothing to it.
        /// <para>
        /// <b>Written every frame of normal play and read by nothing today.</b> Its reader is the post-match
        /// results screen — how long the match actually ran is what it reports, and the clock cannot answer that
        /// because it counts down and is reset per phase. It arrives with whatever owns
        /// <see cref="MatchPhase.Results" />, which nothing transitions into yet.
        /// </para>
        /// </remarks>
        internal float ElapsedSeconds { get; private set; }

        /// <remarks>
        /// Whether a match is under way and a second start would therefore be a mistake. True from
        /// <see cref="MatchPhase.Loading" /> through <see cref="MatchPhase.Overtime" />; false once the match
        /// has ended, so a rematch is allowed without any explicit teardown.
        /// </remarks>
        internal bool IsRunning =>
            Phase is MatchPhase.Loading or MatchPhase.Countdown or MatchPhase.Standard or MatchPhase.OvertimeCheck or MatchPhase.Overtime;

        /// <remarks>
        /// Rejects an illegal transition without mutating anything — not the phase, not the elapsed time, not
        /// the scores — so a refused call is indistinguishable from one that was never made. Re-entering the
        /// phase the match is already in is refused for the same reason: it would republish a phase nobody
        /// entered.
        /// </remarks>
        internal bool TryTransition(MatchPhase next)
        {
            if (!IsTransitionLegal(Phase, next))
            {
                return false;
            }

            Phase = next;

            return true;
        }

        /// <remarks>
        /// Returns the match to the state a fresh orchestrator holds. Deliberately not a transition: the table
        /// has no edge back to <see cref="MatchPhase.None" />, because abandoning a match and finishing one are
        /// different things, and only this method may do the first.
        /// </remarks>
        internal void Reset()
        {
            Phase = MatchPhase.None;
            ElapsedSeconds = 0f;
            _scores.Clear();
        }

        /// <remarks>Accumulates scaled match time. Zero and negative values are ignored.</remarks>
        internal void AddElapsed(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            ElapsedSeconds += deltaTime;
        }

        /// <remarks>
        /// A player who has never been counted always reports a change, including a count of zero, so the
        /// opening score of a match is published rather than swallowed as "already zero".
        /// </remarks>
        internal bool TrySetScore(int playerId, int unitCount)
        {
            if (_scores.TryGetValue(playerId, out int cached) && (cached == unitCount))
            {
                return false;
            }

            _scores[playerId] = unitCount;

            return true;
        }

        internal int GetScore(int playerId)
        {
            return _scores.TryGetValue(playerId, out int cached) ? cached : 0;
        }

        // The legal-transition table, and the whole of it. Two edges are declared here that nothing reaches
        // today, both deliberately: Standard -> Ended is the domination path GOOM-12 fills, and Ended ->
        // Results belongs to the results screen that owns that phase. Refusing them now would mean GOOM-12 and
        // the results screen each editing this table for an edge the phase enum already promises.
        private static bool IsTransitionLegal(MatchPhase current, MatchPhase next)
        {
            return current switch
            {
                MatchPhase.None => next == MatchPhase.Loading,
                MatchPhase.Loading => next == MatchPhase.Countdown,
                MatchPhase.Countdown => next == MatchPhase.Standard,
                MatchPhase.Standard => next is MatchPhase.OvertimeCheck or MatchPhase.Ended,
                MatchPhase.OvertimeCheck => next is MatchPhase.Overtime or MatchPhase.Ended,
                MatchPhase.Overtime => next == MatchPhase.Ended,
                MatchPhase.Ended => next == MatchPhase.Results,
                _ => false,
            };
        }
    }
}
