using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Interfaces
{
    /// <summary>
    /// Defines the grid layout contract used by the board system.
    /// </summary>
    public interface IGridLayout
    {
        public int GridRadius { get; }

        public IReadOnlySet<HexCoordinates> BlockedCoordinates { get; }
    }
}
