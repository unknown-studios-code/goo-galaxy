namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// One hazard occupying a hex — Acid Crawler's corrosive trail — together with how long it still lasts and
    /// whose action windows expire it.
    /// </summary>
    /// <remarks>
    /// A value type, mirroring <see cref="StatusMarker"/>: a hazard is to a hex what a status is to a unit, and
    /// consistency between the two is what makes the board's "temporary condition" vocabulary readable. The cell
    /// therefore carries a separate <c>HasHazard</c> flag, so "no hazard" is a false boolean rather than a null
    /// reference the whole board has to test for — one allocation-free flag instead of one heap object per hex.
    /// </remarks>
    public readonly struct HazardMarker
    {
        public HazardMarker(int ownerPlayerId, int remainingDuration)
        {
            OwnerPlayerId = ownerPlayerId;
            RemainingDuration = remainingDuration;
        }

        /// <summary>
        /// The player whose action windows expire this hazard. Per the GDD a corrosive trail lasts a number of
        /// <b>owner</b> action windows, so it ticks when this player deploys, not when the defender does.
        /// </summary>
        public int OwnerPlayerId { get; }

        /// <summary>
        /// Action windows the hazard still lasts. Always one or greater while the hex reports a hazard; the
        /// cell clears the marker instead of storing zero.
        /// </summary>
        public int RemainingDuration { get; }
    }
}
