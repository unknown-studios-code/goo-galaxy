using System;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// Axial hex coordinate pair (q, r) used as the shared board vocabulary across assemblies.
    /// Lives in Shared so cross-assembly contracts (events, commands) can address board positions
    /// without any assembly having to depend on the Board feature assembly.
    /// </summary>
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

        /// <summary>
        /// Returns the coordinate reached by applying the supplied axial offset to this one.
        /// </summary>
        /// <param name="offset">The axial offset to apply (typically a <c>HexDirection</c> constant).</param>
        /// <returns>The neighboring coordinate.</returns>
        public HexCoordinates GetNeighbor(HexCoordinates offset)
        {
            return new(Q + offset.Q, R + offset.R);
        }

        /// <summary>
        /// Calculates the hex distance (number of steps) between this coordinate and another.
        /// </summary>
        /// <param name="other">The coordinate to measure against.</param>
        /// <returns>The number of hex steps separating the two coordinates.</returns>
        public int CalculateDistance(HexCoordinates other)
        {
            return (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(Q + R - (other.Q + other.R))) / 2;
        }

        public bool Equals(HexCoordinates other)
        {
            return Q == other.Q && R == other.R;
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
    }
}
