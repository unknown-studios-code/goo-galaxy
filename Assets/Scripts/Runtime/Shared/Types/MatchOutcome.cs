using System;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// How a match finished: who won it, and what ended it. Published on
    /// <see cref="Events.MatchEvents.MatchEnded" />.
    /// </summary>
    /// <remarks>
    /// A draw is modelled explicitly rather than as a magic number: <see cref="WinnerPlayerId" /> carries
    /// <see cref="NoWinner" /> and <see cref="IsDraw" /> is the predicate a caller reads. Zero was chosen for
    /// it because real player ids start at one throughout this project, so the sentinel can never collide with
    /// a player — the same convention <c>FuseController</c> uses for an owner it could not resolve. A negative
    /// id would have read as "unknown" instead of "nobody", and a nullable would have boxed at every event
    /// dispatch on this bus.
    /// <para>
    /// <see cref="Reason" /> stays authoritative on its own: a caller that wants to know whether the match was
    /// drawn tests <see cref="IsDraw" />, and one that wants to know <i>why</i> reads the reason. The two agree
    /// by construction only for values this project builds — nothing stops a caller pairing a winner with
    /// <see cref="MatchEndReason.Draw" />, and no constructor validation is imposed to prevent it, because a
    /// value type must stay default-constructible.
    /// </para>
    /// <para>
    /// <b>A defaulted value is not an outcome.</b> It carries <see cref="MatchEndReason.None" /> and
    /// <see cref="NoWinner" />, which is why the reason's zero is <c>None</c> rather than a real ending — a
    /// struct nobody constructed must not assert that the clock ran out. <see cref="IsDraw" /> still answers
    /// true for it, since nobody won a match that was never played, so code that has to tell a real draw from
    /// an unset value tests <see cref="Reason" /> rather than <see cref="IsDraw" /> alone.
    /// </para>
    /// </remarks>
    public readonly struct MatchOutcome : IEquatable<MatchOutcome>
    {
        /// <summary>The value <see cref="WinnerPlayerId" /> carries when nobody won.</summary>
        public const int NoWinner = 0;

        /// <summary>The outcome of a match that ended level, with no winner.</summary>
        public static readonly MatchOutcome Drawn = new(NoWinner, MatchEndReason.Draw);

        /// <summary>Pairs the winning player with the reason the match ended.</summary>
        /// <param name="winnerPlayerId">The winning player, or <see cref="NoWinner" /> for a draw.</param>
        /// <param name="reason">What ended the match.</param>
        public MatchOutcome(int winnerPlayerId, MatchEndReason reason)
        {
            WinnerPlayerId = winnerPlayerId;
            Reason = reason;
        }

        /// <summary>The player who won, or <see cref="NoWinner" /> when the match was drawn.</summary>
        public int WinnerPlayerId { get; }

        /// <summary>What ended the match.</summary>
        public MatchEndReason Reason { get; }

        /// <summary>Whether the match ended with no winner.</summary>
        public bool IsDraw => WinnerPlayerId == NoWinner;

        public static bool operator ==(MatchOutcome left, MatchOutcome right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MatchOutcome left, MatchOutcome right)
        {
            return !left.Equals(right);
        }

        public bool Equals(MatchOutcome other)
        {
            return (WinnerPlayerId == other.WinnerPlayerId) && (Reason == other.Reason);
        }

        public override bool Equals(object obj)
        {
            return obj is MatchOutcome other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(WinnerPlayerId, Reason);
        }

        public override string ToString()
        {
            return IsDraw ? $"Draw ({Reason})" : $"Player {WinnerPlayerId} ({Reason})";
        }
    }
}
