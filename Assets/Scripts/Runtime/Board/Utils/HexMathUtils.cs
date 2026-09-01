using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Utils
{
    /// <summary>
    /// The single owner of the board's hex orientation and the axial-to-world projection built on it, in both
    /// directions.
    /// </summary>
    /// <remarks>
    /// The code is unambiguously flat-top — <see cref="ProjectToWorldSpace" /> writes <c>x = 1.5 * size * q</c>,
    /// which is the flat-top formula — while the GDD's prose describes a pointy-top board. That divergence is
    /// known and deliberate, not a bug waiting to be fixed: this projection has been the drawn layout since the
    /// board first rendered, and every authored layout and cell sprite is framed for it. The GDD is the side to
    /// reconcile. Nothing outside this file may re-derive either direction of the projection — see
    /// <see cref="ProjectToAxial" /> for why a second copy of the algebra is the failure mode this file exists
    /// to prevent.
    /// </remarks>
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

        /// <summary>
        /// Projects a world-space point back onto the axial grid, returning the <b>fractional</b> coordinates
        /// <c>(q, r)</c> it falls at. The exact inverse of <see cref="ProjectToWorldSpace" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The two live in one file so they cannot drift: the algebra below is <see cref="ProjectToWorldSpace" />
        /// solved for <c>q</c> and <c>r</c>, and re-deriving it against the pointy-top orientation the GDD's
        /// prose describes — rather than the flat-top one the board is actually drawn with — would move every
        /// hit test off the cells on screen.
        /// </para>
        /// <para>
        /// The Z component is ignored, matching the forward projection's flat XY board. The result is fractional
        /// and names no cell on its own — pass it through <see cref="RoundToAxial" /> for that, and put the
        /// rounded pair to <c>HexGrid.TryGetCell</c> to learn whether it is on the board at all. A
        /// non-positive <paramref name="size" /> has no inverse and yields <see cref="Vector2.zero" /> rather
        /// than a division by zero.
        /// </para>
        /// </remarks>
        /// <param name="worldPosition">The point to project, in the same world space the forward projection writes into.</param>
        /// <param name="size">The size the board was projected at — center to corner vertex. Must be greater than zero.</param>
        /// <returns>The fractional axial coordinates, <c>q</c> in <c>x</c> and <c>r</c> in <c>y</c>.</returns>
        public static Vector2 ProjectToAxial(Vector3 worldPosition, float size)
        {
            if (size <= 0f)
            {
                return Vector2.zero;
            }

            float q = worldPosition.x / (1.5f * size);
            float r = (worldPosition.y / (_sqrt3 * size)) - (q * 0.5f);

            return new Vector2(q, r);
        }

        /// <summary>Rounds fractional axial coordinates to the hex they fall inside.</summary>
        /// <remarks>
        /// Cube rounding, not two independent <c>Mathf.RoundToInt</c> calls: axial coordinates are a projection
        /// of the cube coordinates <c>(q, r, s)</c> where <c>s = -q - r</c>, and rounding the two axial
        /// components separately breaks that constraint near a cell border, which lands the result on a
        /// neighbouring hex. Rounding all three and re-deriving whichever moved furthest restores it. See
        /// <see href="https://www.redblobgames.com/grids/hexagons/#rounding">Red Blob Games, Hex rounding</see>.
        /// <para>
        /// The returned pair is a coordinate, not a cell: it can name a hex the board does not contain, which
        /// only <c>HexGrid.TryGetCell</c> can settle. Allocation-free.
        /// </para>
        /// </remarks>
        /// <param name="q">The fractional axial <c>q</c>.</param>
        /// <param name="r">The fractional axial <c>r</c>.</param>
        /// <returns>The nearest hex's axial coordinates.</returns>
        public static HexCoordinates RoundToAxial(float q, float r)
        {
            float s = -q - r;

            int roundedQ = Mathf.RoundToInt(q);
            int roundedR = Mathf.RoundToInt(r);
            int roundedS = Mathf.RoundToInt(s);

            float deltaQ = Mathf.Abs(roundedQ - q);
            float deltaR = Mathf.Abs(roundedR - r);
            float deltaS = Mathf.Abs(roundedS - s);

            if (deltaQ > deltaR && deltaQ > deltaS)
            {
                roundedQ = -roundedR - roundedS;
            }
            else if (deltaR > deltaS)
            {
                roundedR = -roundedQ - roundedS;
            }

            return new HexCoordinates(roundedQ, roundedR);
        }
    }
}
