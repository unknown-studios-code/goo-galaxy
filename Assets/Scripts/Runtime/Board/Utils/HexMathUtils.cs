using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Utils
{
    public static class HexMathUtils
    {
        private static readonly float _sqrt3 = Mathf.Sqrt(3.0f);

        /// <summary>
        /// Projects axial grid coordinates (q, r) into world space on the <b>XY plane</b>, where flat-top hexes
        /// face the camera. Z is always zero; depth between board layers is expressed with sorting order, not
        /// with position.
        /// </summary>
        /// <remarks>
        /// XY rather than XZ because this is a 2D game rendered through the URP 2D Renderer: a
        /// <c>SpriteRenderer</c> faces +Z, so a board laid out on XZ would be edge-on to the camera.
        /// </remarks>
        /// <param name="coords">The axial hex coordinates to project.</param>
        /// <param name="size">The size of the hex (distance from center to a corner vertex).</param>
        /// <returns>A Unity Vector3 representing the center of the hex in world space.</returns>
        public static Vector3 ProjectToWorldSpace(HexCoordinates coords, float size)
        {
            float x = size * (1.5f * coords.Q);
            float y = size * ((_sqrt3 * 0.5f * coords.Q) + (_sqrt3 * coords.R));

            return new Vector3(x, y, 0f);
        }
    }
}
