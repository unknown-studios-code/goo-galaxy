using System;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// One condition starting or ending on one unit, published on <see cref="Events.MatchEvents.StatusApplied" />
    /// and <see cref="Events.MatchEvents.StatusExpired" />.
    /// </summary>
    /// <remarks>
    /// Carries only value types and describes state that outlives the callback, so a subscriber has nothing to
    /// copy. The unit may already be gone by the time a subscriber looks it up — the id is the only handle this
    /// guarantees.
    /// </remarks>
    public readonly struct StatusChange : IEquatable<StatusChange>
    {
        /// <summary>
        /// The <see cref="ActingPlayerId" /> an expiry carries: a condition runs out on its own clock, so no
        /// player acted to end it.
        /// </summary>
        public const int NoActingPlayer = PlayerSlot.UnassignedId;

        /// <summary>Describes one condition starting or ending on one unit.</summary>
        /// <param name="unitId">The unit the condition is on.</param>
        /// <param name="ownerPlayerId">The player who owns that unit at the moment of the change.</param>
        /// <param name="actingPlayerId">
        /// The player whose deployment applied the condition, or <see cref="NoActingPlayer" /> on an expiry.
        /// </param>
        /// <param name="status">The condition that started or ended.</param>
        /// <param name="remainingWindows">
        /// Defender action windows the condition lasts from now: the authored duration on an application, zero on
        /// an expiry.
        /// </param>
        public StatusChange(int unitId, int ownerPlayerId, int actingPlayerId, StatusType status, int remainingWindows)
        {
            UnitId = unitId;
            OwnerPlayerId = ownerPlayerId;
            ActingPlayerId = actingPlayerId;
            Status = status;
            RemainingWindows = remainingWindows;
        }

        public int UnitId { get; }

        /// <summary>The player who owns the unit at the moment of the change, which a conversion can have moved.</summary>
        public int OwnerPlayerId { get; }

        /// <summary>The player whose deployment applied the condition, or <see cref="NoActingPlayer" /> on an expiry.</summary>
        public int ActingPlayerId { get; }

        public StatusType Status { get; }

        /// <summary>Defender action windows the condition lasts from now; zero on an expiry.</summary>
        public int RemainingWindows { get; }

        public static bool operator ==(StatusChange left, StatusChange right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StatusChange left, StatusChange right)
        {
            return !left.Equals(right);
        }

        public bool Equals(StatusChange other)
        {
            return (UnitId == other.UnitId)
                && (OwnerPlayerId == other.OwnerPlayerId)
                && (ActingPlayerId == other.ActingPlayerId)
                && (Status == other.Status)
                && (RemainingWindows == other.RemainingWindows);
        }

        public override bool Equals(object obj)
        {
            return obj is StatusChange other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UnitId, OwnerPlayerId, ActingPlayerId, Status, RemainingWindows);
        }

        public override string ToString()
        {
            return $"Unit {UnitId} (owner {OwnerPlayerId}) {Status}: {RemainingWindows} window(s) left, acted on by player {ActingPlayerId}";
        }
    }
}
