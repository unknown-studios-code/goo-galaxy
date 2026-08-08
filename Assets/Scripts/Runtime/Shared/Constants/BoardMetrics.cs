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

        /// <summary>Cells adjacent to any one cell on a hex board.</summary>
        public const int NeighborsPerCell = 6;

        /// <summary>The hex distance a Clone covers on a card that authors no other value.</summary>
        public const int DefaultCloneDistance = 1;

        /// <summary>The hex distance a Jump covers on a card that authors no other value.</summary>
        public const int DefaultJumpDistance = 2;

        /// <summary>The shortest authorable move. Zero would make a move's target its own source.</summary>
        public const int MinMoveDistance = 1;

        /// <summary>
        /// The longest authorable move, being the widest separation two cells on a
        /// <see cref="DefaultGridRadius" /> board can have. A card authored past it can never find a legal target.
        /// </summary>
        public const int MaxMoveDistance = DefaultGridRadius * 2;

        /// <summary>
        /// The reach of a conversion whose card authors no wider one, counted in rings around the landing hex.
        /// </summary>
        public const int DefaultConversionRadius = 1;

        /// <summary>
        /// The widest conversion any card is authored at, counted in rings around the landing hex. Volatile
        /// Mass is the only card at this reach; raising it widens every buffer sized from
        /// <see cref="MaxConversionTargetsPerLanding" />.
        /// </summary>
        public const int MaxConversionRadius = 2;

        /// <summary>
        /// Cells within <see cref="MaxConversionRadius" /> of one landing hex, excluding that hex, and so the
        /// ceiling on units a single landing coordinate can convert. Derived from the hex area formula rather
        /// than written out, so widening <see cref="MaxConversionRadius" /> cannot leave this behind.
        /// </summary>
        public const int MaxConversionTargetsPerLanding = 3 * MaxConversionRadius * (MaxConversionRadius + 1);

        /// <summary>
        /// Cells one landing-impact area covers at its widest, including the landing hex itself. The size a
        /// spiral scratch buffer is built at so a full-radius impact never grows it.
        /// </summary>
        public const int MaxImpactAreaCells = MaxConversionTargetsPerLanding + 1;

        /// <summary>
        /// Hexes the widest authored Protocol cluster covers — Sterilization Beam's 4 — and so the ceiling on
        /// the units one cast can condition. Nothing enforces it: a card authored wider simply grows whatever
        /// was sized from it.
        /// </summary>
        public const int MaxSpellClusterSize = 4;
    }
}
