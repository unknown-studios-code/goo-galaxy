using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Interfaces
{
    /// <summary>
    /// Defines the grid layout contract used by the board system.
    /// </summary>
    public interface IGridLayout
    {
        /// <summary>The number of hex rings the grid extends outward from the centre cell.</summary>
        public int GridRadius { get; }

        /// <summary>The axial coordinates of every hex the layout marks impassable.</summary>
        /// <remarks>Contains/count only by convention — see <c>unity-performance-optimization.md</c> Rule 4a.</remarks>
        public IReadOnlySet<HexCoordinates> BlockedCoordinates { get; }
    }
}
