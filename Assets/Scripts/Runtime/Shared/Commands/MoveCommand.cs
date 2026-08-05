using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Shared.Commands
{
    /// <summary>
    /// Immutable, allocation-free description of a requested board move.
    /// Carries only value types so it can be validated, resolved, published as an event payload,
    /// and later replicated over the network without boxing.
    /// </summary>
    public readonly struct MoveCommand
    {
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

        public int UnitId { get; }
    }
}
