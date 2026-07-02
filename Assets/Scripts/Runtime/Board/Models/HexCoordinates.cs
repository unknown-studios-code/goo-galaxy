using System;

namespace GooGalaxy.Runtime.Board.Models
{
    public readonly struct HexCoordinates : IEquatable<HexCoordinates>
    {
        public HexCoordinates(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int Q { get; }

        public int R { get; }

        public static bool operator ==(HexCoordinates left, HexCoordinates right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexCoordinates left, HexCoordinates right)
        {
            return !left.Equals(right);
        }

        public HexCoordinates GetNeighbor(HexCoordinates offset)
        {
            return new(Q + offset.Q, R + offset.R);
        }

        public int CalculateDistance(HexCoordinates other)
        {
            return (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(Q + R - (other.Q + other.R))) / 2;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoordinates other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Q, R);
        }

        public override string ToString()
        {
            return $"({Q}, {R})";
        }

        public bool Equals(HexCoordinates other)
        {
            return Q == other.Q && R == other.R;
        }
    }
}
