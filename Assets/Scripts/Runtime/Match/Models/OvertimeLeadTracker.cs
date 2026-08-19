using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Match.Models
{
    /// <remarks>
    /// How long one player has held an unbroken lead on unit count, which is the whole of the overtime win
    /// condition: a lead is not enough, it has to survive. Keeping the accumulator here rather than in the
    /// orchestrator is what makes a three-second hold testable without a scene or a frame.
    /// <para>
    /// Engine-free on purpose, and allocation-free on every path — it is ticked once per frame of overtime.
    /// </para>
    /// </remarks>
    internal sealed class OvertimeLeadTracker
    {
        private int _leaderPlayerId;
        private float _heldSeconds;

        /// <remarks>
        /// Returns the player who has now held the lead for <paramref name="holdSeconds" />, or
        /// <see cref="MatchOutcome.NoWinner" /> while nobody has. Reports the winner on every later tick as
        /// well, so a caller that cannot act on the frame the hold completes does not lose it.
        /// <para>
        /// <b>A lead starts its hold at zero.</b> The tick on which a lead first appears accumulates nothing —
        /// a lead established this instant has been held for no time — so the hold measures the frames
        /// <i>after</i> it was taken. Both a change of leader and a return to level counts reset the
        /// accumulator to zero, and nothing carries over: a player who leads, loses the lead, and takes it
        /// again starts the three seconds from the beginning.
        /// </para>
        /// <para>
        /// The threshold is inclusive, so a hold that lands exactly on <paramref name="holdSeconds" /> wins.
        /// Zero and negative deltas are ignored, the same convention <see cref="MatchClock.Tick" /> and
        /// <see cref="MatchState.AddElapsed" /> follow, so a paused frame cannot advance a hold.
        /// </para>
        /// <para>
        /// The two ids are assumed distinct, as a match's two sides always are.
        /// </para>
        /// </remarks>
        internal int Tick(int playerOneUnits, int playerTwoUnits, int playerOneId, int playerTwoId, float holdSeconds, float deltaTime)
        {
            if (playerOneUnits == playerTwoUnits)
            {
                Reset();

                return MatchOutcome.NoWinner;
            }

            int leaderPlayerId = playerOneUnits > playerTwoUnits ? playerOneId : playerTwoId;

            if (leaderPlayerId != _leaderPlayerId)
            {
                _leaderPlayerId = leaderPlayerId;
                _heldSeconds = 0f;

                return MatchOutcome.NoWinner;
            }

            if (deltaTime > 0f)
            {
                _heldSeconds += deltaTime;
            }

            return _heldSeconds >= holdSeconds ? leaderPlayerId : MatchOutcome.NoWinner;
        }

        /// <remarks>
        /// Returns the tracker to the state a fresh one holds, so the next overtime measures its first lead
        /// from zero rather than from whatever the previous match left behind.
        /// </remarks>
        internal void Reset()
        {
            _leaderPlayerId = MatchOutcome.NoWinner;
            _heldSeconds = 0f;
        }
    }
}
