using System;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Shared.Commands
{
    /// <summary>
    /// Immutable, allocation-free description of a requested board move.
    /// Carries only value types so it can be validated, resolved, published as an event payload,
    /// and later replicated over the network without boxing.
    /// </summary>
    /// <remarks>
    /// No card identifier travels here. Only <see cref="MoveType.Deploy" /> would populate one — a Clone copies
    /// its source unit's card and a Jump moves a unit that already has one — so the field would be meaningless on
    /// two of the three move types, and a subscriber could not tell "this move authors no card" from "the
    /// publisher forgot to set it". A Deploy's card is passed alongside the command to the resolving call, and is
    /// recoverable afterwards from the unit registry by the landing hex's occupant id.
    /// </remarks>
    public readonly struct MoveCommand : IEquatable<MoveCommand>
    {
        /// <summary>
        /// The value <see cref="UnitId" /> carries on a <see cref="MoveType.Deploy" />, which acts with no source
        /// unit and therefore has no id to name.
        /// </summary>
        /// <remarks>
        /// Deliberately equal to <c>HexCell.NoOccupant</c>, so the "no unit here" marker the board writes into a
        /// cell and the "no unit acted" marker a command carries read as the same number wherever the two meet.
        /// Shared cannot reference the Board assembly, so the coupling is stated rather than compiled — a test
        /// pins the equality.
        /// </remarks>
        public const int NoUnit = -1;

        public MoveCommand(MoveType type, HexCoordinates source, HexCoordinates target, int playerId, int unitId)
        {
            Type = type;
            Source = source;
            Target = target;
            PlayerId = playerId;
            UnitId = unitId;
        }

        public MoveType Type { get; }

        public HexCoordinates Source { get; }

        public HexCoordinates Target { get; }

        public int PlayerId { get; }

        /// <remarks>
        /// <see cref="NoUnit" /> on a <see cref="MoveType.Deploy" />, which acts with no source unit. This names the
        /// unit that was <i>commanded</i>, never the unit that landed: a Clone leaves the commanded unit on its
        /// source and puts a new one on the target, and a Deploy has none at all. Anything reacting to a landing
        /// reads the target cell's occupant instead.
        /// </remarks>
        public int UnitId { get; }

        public static bool operator ==(MoveCommand left, MoveCommand right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MoveCommand left, MoveCommand right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Builds a <see cref="MoveType.Deploy" />: a new unit placed on <paramref name="target" />, with no
        /// source unit and no source hex.
        /// </summary>
        /// <remarks>
        /// The only supported way to construct a Deploy. A hand-built one would have to invent a value for
        /// <see cref="Source" />, and callers would drift on what that value should be — the factory settles it
        /// as the target itself, which is what makes a Deploy measure zero distance and vacate nothing.
        /// </remarks>
        /// <param name="target">The hex the new unit is placed on.</param>
        /// <param name="playerId">The player playing the card.</param>
        /// <returns>A Deploy command whose source equals its target and whose unit id is <see cref="NoUnit" />.</returns>
        public static MoveCommand ForDeploy(HexCoordinates target, int playerId)
        {
            return new MoveCommand(MoveType.Deploy, target, target, playerId, NoUnit);
        }

        public bool Equals(MoveCommand other)
        {
            return Type == other.Type && Source == other.Source && Target == other.Target && PlayerId == other.PlayerId && UnitId == other.UnitId;
        }

        public override bool Equals(object obj)
        {
            return obj is MoveCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Source, Target, PlayerId, UnitId);
        }
    }
}
