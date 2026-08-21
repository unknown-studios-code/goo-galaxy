using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.AI.Models
{
    /// <summary>
    /// The scratch space one enumeration pass needs, owned by the caller and reused across passes so the pass
    /// itself allocates nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>MoveOptionResolver</c> is stateless, so every buffer it works in has to come from somewhere the state
    /// is allowed to live. Building them per pass would allocate six collections per tick; building them here,
    /// once, is what makes the budget reachable.
    /// </para>
    /// <para>
    /// <b>The cluster buffers are what a Protocol option borrows.</b> One buffer per hand slot, because a slot
    /// contributes at most one Protocol option per pass, which is what guarantees no two live options ever share
    /// a buffer. They stay valid until the next <see cref="Reset" />, and that is exactly how long a
    /// <see cref="MoveOption" /> built from one is readable — see <see cref="MoveOption.TargetCluster" />.
    /// </para>
    /// <para>
    /// Every collection is sized from <see cref="BoardMetrics" /> at construction, so a full board grows none of
    /// them. The one growth that can still happen is a hand larger than any seen before, which adds a cluster
    /// buffer on that pass and never again.
    /// </para>
    /// </remarks>
    public sealed class MoveOptionBuffers
    {
        private readonly List<List<HexCoordinates>> _clusterBuffers;

        /// <summary>Builds the scratch space for a board of the default size and a hand of the given size.</summary>
        /// <param name="handSize">Hand slots to pre-build a cluster buffer for. A larger hand grows the list on use.</param>
        public MoveOptionBuffers(int handSize)
        {
            DeployFootprint = new List<HexCoordinates>(BoardMetrics.DefaultBoardCellCount);
            BoardCoordinates = new List<HexCoordinates>(BoardMetrics.DefaultBoardCellCount);
            OwnedUnits = new List<GridUnit>(BoardMetrics.DefaultBoardCellCount);
            CellScratch = new List<HexCell>(BoardMetrics.DefaultBoardCellCount);
            ClusterCandidates = new List<HexCoordinates>(BoardMetrics.MaxImpactAreaCells);
            _clusterBuffers = new List<List<HexCoordinates>>(handSize);

            for (int i = 0; i < handSize; i++)
            {
                _clusterBuffers.Add(new List<HexCoordinates>(BoardMetrics.MaxSpellClusterSize));
            }
        }

        /// <remarks>Every empty, unblocked hex adjacent to a hex the acting player occupies, filled once per pass.</remarks>
        internal List<HexCoordinates> DeployFootprint { get; }

        /// <remarks>
        /// Every hex on the board, in dictionary order, so a Protocol centre can be drawn by index. The board's
        /// coordinate set never changes, but the order this is filled in is whatever the grid enumerates — it is
        /// stable within a session and must not be treated as authored.
        /// </remarks>
        internal List<HexCoordinates> BoardCoordinates { get; }

        /// <remarks>The acting player's live units, filled from the board rather than from the unit registry.</remarks>
        internal List<GridUnit> OwnedUnits { get; }

        /// <remarks>The buffer every <c>HexGrid</c> area query writes into. Holds nothing between queries.</remarks>
        internal List<HexCell> CellScratch { get; }

        /// <remarks>The hexes a Protocol cluster may still draw from, minus the centre and minus what it drew.</remarks>
        internal List<HexCoordinates> ClusterCandidates { get; }

        /// <remarks>
        /// Clears every buffer so the next enumeration pass starts from nothing. Invalidates the cluster of every
        /// <see cref="MoveOption" /> produced by the previous pass, which is the borrowing contract those options
        /// were handed out under.
        /// </remarks>
        internal void Reset()
        {
            DeployFootprint.Clear();
            BoardCoordinates.Clear();
            OwnedUnits.Clear();
            CellScratch.Clear();
            ClusterCandidates.Clear();

            for (int i = 0; i < _clusterBuffers.Count; i++)
            {
                _clusterBuffers[i].Clear();
            }
        }

        /// <remarks>
        /// The cluster buffer belonging to one hand slot, grown on first use of a slot beyond the authored hand
        /// size. Handing a buffer out per slot is what keeps two Protocol options from writing over each other.
        /// </remarks>
        internal List<HexCoordinates> GetClusterBuffer(int slotIndex)
        {
            while (_clusterBuffers.Count <= slotIndex)
            {
                _clusterBuffers.Add(new List<HexCoordinates>(BoardMetrics.MaxSpellClusterSize));
            }

            return _clusterBuffers[slotIndex];
        }
    }
}
