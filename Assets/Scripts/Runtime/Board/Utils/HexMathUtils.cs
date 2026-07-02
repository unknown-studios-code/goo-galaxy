using GooGalaxy.Runtime.Board.Models;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Utils
{
    public static class HexMathUtils
    {
        private static readonly float _sqrt3 = Mathf.Sqrt(3.0f);

        /// <summary>
        /// Projects axial grid coordinates (q, r) into Unity 3D world space (X, Z plane).
        /// Flat-top hexes lie flat on the XZ plane.
        /// </summary>
        /// <param name="coords">The axial hex coordinates to project.</param>
        /// <param name="size">The size of the hex (distance from center to a corner vertex).</param>
        /// <returns>A Unity Vector3 representing the center of the hex in world space.</returns>
        public static Vector3 ProjectToWorldSpace(HexCoordinates coords, float size)
        {
            float x = size * (1.5f * coords.Q);
            float z = size * ((_sqrt3 * 0.5f * coords.Q) + (_sqrt3 * coords.R));

            return new Vector3(x, 0f, z);
        }
    }
}
