using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Models
{
    /// <summary>
    /// One hex of the board: its fixed coordinate, whether terrain makes it impassable, and which unit stands
    /// on it. Occupancy is stored as a unit id rather than a reference, so the cell never keeps a dead unit alive.
    /// </summary>
    /// <remarks>
    /// The cell is the board's view of occupancy and the unit registry is the unit's view of it; movement code
    /// keeps the two in step and validation cross-checks them, so a mismatch surfaces instead of corrupting state.
    /// A hazard is stored as a value marker plus a <see cref="HasHazard"/> flag rather than a nullable
    /// reference, so a board of 61 cells carries no hazard objects while none is active.
    /// </remarks>
    public class HexCell
    {
        /// <summary>The value <see cref="OccupantUnitId"/> carries while the cell is empty.</summary>
        public const int NoOccupant = -1;

        public HexCell(HexCoordinates coordinates, bool isBlocked = false)
        {
            Coordinates = coordinates;
            IsBlocked = isBlocked;
            OccupantUnitId = NoOccupant;
        }

        public HexCoordinates Coordinates { get; }

        /// <summary>Whether terrain makes this hex impassable. A blocked cell can never take an occupant.</summary>
        public bool IsBlocked { get; set; }

        /// <summary>
        /// The identifier of the unit standing here, or <see cref="NoOccupant"/> when the cell is empty.
        /// </summary>
        public int OccupantUnitId { get; private set; }

        public bool IsOccupied => OccupantUnitId != NoOccupant;

        /// <summary>Whether a hazard currently denies this hex to units that cannot ignore hazards.</summary>
        public bool HasHazard { get; private set; }

        /// <summary>
        /// The active hazard's owner and remaining duration. Only meaningful while <see cref="HasHazard"/> is
        /// true; it reads as a default marker otherwise.
        /// </summary>
        public HazardMarker Hazard { get; private set; }

        /// <summary>
        /// Records that the given unit now stands on this cell, replacing any previous occupant without
        /// checking for one. Callers validate vacancy first.
        /// </summary>
        /// <param name="unitId">The identifier of the arriving unit.</param>
        public void SetOccupant(int unitId)
        {
            OccupantUnitId = unitId;
        }

        /// <summary>Marks the cell empty. Safe to call on a cell that already is.</summary>
        public void ClearOccupant()
        {
            OccupantUnitId = NoOccupant;
        }

        /// <summary>
        /// Places a hazard on the cell, replacing any active one rather than stacking with it. Occupancy is
        /// untouched — a hazard denies future landings, it does not evict the unit standing here.
        /// </summary>
        /// <param name="ownerPlayerId">The player whose action windows expire the hazard.</param>
        /// <param name="duration">Owner action windows the hazard lasts. A value below one is ignored.</param>
        /// <returns>
        /// True when an active hazard was replaced and its remaining duration discarded; false when the cell
        /// was clear, or when the duration was below one and nothing was placed.
        /// </returns>
        public bool SetHazard(int ownerPlayerId, int duration)
        {
            if (duration <= 0)
            {
                return false;
            }

            bool didReplace = HasHazard;

            Hazard = new HazardMarker(ownerPlayerId, duration);
            HasHazard = true;

            return didReplace;
        }

        /// <summary>Removes the hazard. Safe to call on a cell that carries none.</summary>
        public void ClearHazard()
        {
            Hazard = default;
            HasHazard = false;
        }

        /// <summary>
        /// Closes one of the hazard owner's action windows, removing the hazard when its last one expires.
        /// Does nothing on a cell that carries no hazard.
        /// </summary>
        public void TickHazard()
        {
            if (!HasHazard)
            {
                return;
            }

            int remaining = Hazard.RemainingDuration - 1;

            if (remaining <= 0)
            {
                ClearHazard();
                return;
            }

            Hazard = new HazardMarker(Hazard.OwnerPlayerId, remaining);
        }
    }
}
