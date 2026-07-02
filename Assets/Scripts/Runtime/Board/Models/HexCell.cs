namespace GooGalaxy.Runtime.Board.Models
{
    public class HexCell
    {
        public HexCell(HexCoordinates coordinates, bool isBlocked = false)
        {
            Coordinates = coordinates;
            IsBlocked = isBlocked;
        }

        public HexCoordinates Coordinates { get; }

        public bool IsBlocked { get; set; }
    }
}
