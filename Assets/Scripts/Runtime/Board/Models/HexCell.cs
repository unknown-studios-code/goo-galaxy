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
    }
}
