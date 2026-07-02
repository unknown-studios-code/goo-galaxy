using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Interfaces;

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
