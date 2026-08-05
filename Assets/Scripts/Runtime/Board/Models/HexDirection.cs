using System;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Models
{
    public static class HexDirection
    {
        public static readonly HexCoordinates E = new(1, 0);
        public static readonly HexCoordinates NE = new(1, -1);
        public static readonly HexCoordinates NW = new(0, -1);
        public static readonly HexCoordinates W = new(-1, 0);
        public static readonly HexCoordinates SW = new(-1, 1);
        public static readonly HexCoordinates SE = new(0, 1);
        private static readonly HexCoordinates[] _all = new HexCoordinates[] { E, NE, NW, W, SW, SE };
        public static ReadOnlySpan<HexCoordinates> All => _all;
    }
}
