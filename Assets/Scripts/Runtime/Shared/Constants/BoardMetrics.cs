namespace GooGalaxy.Runtime.Shared.Constants
{
    /// <summary>
    /// Fixed dimensions of the match board, shared by every system that pre-sizes a collection or a pool to it.
    /// </summary>
    /// <remarks>
    /// These are sizing hints, not rules: a collection built with the wrong capacity still behaves correctly, it
    /// just resizes. They live here so the board's size is stated once — two copies of the same literal drift
    /// apart silently the first time the layout changes.
    /// </remarks>
    public static class BoardMetrics
    {
        /// <summary>The radius the MVP board is authored at, counted in rings around the center hex.</summary>
        public const int DefaultGridRadius = 4;

        /// <summary>
        /// Cells on a <see cref="DefaultGridRadius" /> board, from the hex area formula <c>3r(r + 1) + 1</c>.
        /// Also the ceiling on live units, since a cell holds at most one.
        /// </summary>
        public const int DefaultBoardCellCount = 61;

        /// <summary>
        /// Cells adjacent to any one cell on a hex board. Also the ceiling on units a single landing can
        /// convert, since conversion reaches exactly one ring.
        /// </summary>
        public const int NeighborsPerCell = 6;
    }
}
