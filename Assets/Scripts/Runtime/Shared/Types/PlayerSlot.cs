namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// One seat at a match: the id the seat is played under, and what drives it.
    /// </summary>
    /// <remarks>
    /// A seat, not a person. Nothing here identifies an account, a device or a peer — it names one side of one
    /// match and says who moves for it, which is what lets a local player, a remote one and the AI share a
    /// single type without any of them knowing the others exist.
    /// <para>
    /// A defaulted value is the unfilled seat: <see cref="Id" /> is <see cref="UnassignedId" /> and
    /// <see cref="Control" /> is <see cref="PlayerControl.Unassigned" />. That is what
    /// <see cref="MatchConfiguration" />'s seed-only constructor leaves behind, so a zero id must never be read
    /// as a player numbered zero.
    /// </para>
    /// </remarks>
    public readonly struct PlayerSlot
    {
        /// <summary>
        /// The value <see cref="Id" /> carries when no player was named. Real player ids start at one
        /// throughout this project, so this can never be mistaken for one.
        /// </summary>
        public const int UnassignedId = 0;

        /// <summary>Fills a seat.</summary>
        /// <param name="id">The id the seat is played under. <see cref="UnassignedId" /> leaves it unfilled.</param>
        /// <param name="control">What drives the seat.</param>
        public PlayerSlot(int id, PlayerControl control)
        {
            Id = id;
            Control = control;
        }

        /// <summary>The id this seat is played under, or <see cref="UnassignedId" /> when no player was named.</summary>
        public int Id { get; }

        /// <summary>What drives this seat, or <see cref="PlayerControl.Unassigned" /> when no player was named.</summary>
        public PlayerControl Control { get; }
    }
}
