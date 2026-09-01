using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;
using UnityEngine.UIElements;

namespace GooGalaxy.Runtime.Input.Services
{
    /// <summary>
    /// Turns a screen point into the board hex under it, and answers whether that point is over the HUD rather
    /// than over the board at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The board's own bounds decide what is off it.</b> The projection is inverted with
    /// <see cref="HexMathUtils.ProjectToAxial" /> and the result put to <c>HexGrid.TryGetCell</c>, never
    /// measured against a radius — the grid already owns which coordinates exist, and a second copy of that
    /// answer would disagree the first time the layout blocks or drops one.
    /// </para>
    /// <para>
    /// <b>The camera arrives once and is held.</b> <c>Camera.main</c> walks the scene by tag, so it is never
    /// read on a pointer path; the owner resolves it at wake and hands it over. A resolver holding a camera
    /// Unity has destroyed answers false rather than throwing, which is what a scene unload mid-gesture looks
    /// like. Its <see cref="Transform" /> is cached alongside it at construction for the same reason —
    /// <c>Camera.transform</c> is itself an extern property fetch, and this is a 60–240 Hz path — so the one
    /// null test on <see cref="_camera" /> covers a destroyed camera for both.
    /// </para>
    /// <para>
    /// <b>Panel space is not screen space, and the difference is a vertical mirror.</b> UI Toolkit measures from
    /// the top-left and the screen from the bottom-left, so a raw screen point picked against a panel hits the
    /// element vertically opposite the one under the finger — which reads as a HUD that blocks taps at the top
    /// of the board and lets them through over the hand strip. Verified empirically against a live panel:
    /// <c>ScreenToPanel(0, 0)</c> returns panel <c>(0, 0)</c>, so a raw screen point is never flipped by
    /// <see cref="RuntimePanelUtils.ScreenToPanel" /> itself — the caller must do it, exactly as Unity's own
    /// uGUI bridge (<c>PanelEventHandler.cs</c>) does. <see cref="ToPanelPoint" /> is the one place that flip
    /// happens; nothing else in this assembly may re-derive it.
    /// </para>
    /// <para>Allocation-free on every path once constructed.</para>
    /// </remarks>
    public sealed class BoardPointerResolver
    {
        // The Z the board is drawn on. The forward projection writes zero, so a point on the board plane is a
        // point at zero depth in world space, and an orthographic camera needs a positive distance to reach it.
        private const float BoardPlaneZ = 0f;

        private readonly Camera _camera;
        private readonly Transform _cameraTransform;
        private readonly float _cellVisualSize;

        /// <summary>Builds the resolver against the camera the board is framed by.</summary>
        /// <param name="camera">The camera the board is drawn through. Held for the resolver's life and never re-read.</param>
        /// <param name="cellVisualSize">
        /// The size the board was projected at — center to corner vertex. Must be the value the view actually
        /// drew with, or every hit test lands on the wrong hex.
        /// </param>
        public BoardPointerResolver(Camera camera, float cellVisualSize)
        {
            _camera = camera;
            _cameraTransform = camera != null ? camera.transform : null;
            _cellVisualSize = cellVisualSize;
        }

        /// <summary>Converts a screen point into the coordinate space of a runtime UI Toolkit panel.</summary>
        /// <remarks>
        /// <b>The Y flip is mandatory, not a simplification to trim.</b> Screen space is bottom-left origin and
        /// panel space is top-left origin, and <see cref="RuntimePanelUtils.ScreenToPanel" /> does not perform
        /// that flip itself — measured directly against a live panel, where <c>ScreenToPanel(0, 0)</c> answers
        /// panel <c>(0, 0)</c> rather than the bottom-left corner's panel-space equivalent. Skipping the flip
        /// here would hit the element vertically opposite the one under the finger, which reads as a HUD that
        /// blocks taps at the top of the board and lets them through over the hand strip. Unity's own uGUI
        /// bridge (<c>PanelEventHandler.cs</c>) flips for the identical reason.
        /// <para>
        /// <b>One caller cannot reach this and duplicates it instead.</b>
        /// <c>MatchHudView.IsScreenPointInDiscardZone</c> performs the same flip inline, because this assembly
        /// already references <c>Runtime.UI</c> and calling back the other way would close a cycle. That copy and
        /// this one must change together.
        /// </para>
        /// </remarks>
        /// <param name="panel">The panel to convert into. Must not be null.</param>
        /// <param name="screenPosition">The point to convert, in screen pixels with the origin bottom-left.</param>
        /// <returns>The equivalent point in the panel's coordinate space, origin top-left.</returns>
        public static Vector2 ToPanelPoint(IPanel panel, Vector2 screenPosition)
        {
            var flippedPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            return RuntimePanelUtils.ScreenToPanel(panel, flippedPosition);
        }

        /// <summary>Reports whether a screen point falls on an element of a runtime UI Toolkit panel.</summary>
        /// <remarks>
        /// A point the panel picks nothing at is a point the board can have. That makes the answer only as good
        /// as the panel's picking modes: an element that covers the board window without
        /// <c>picking-mode: ignore</c> swallows every tap on the board behind it.
        /// </remarks>
        /// <param name="panel">The panel to test against. A null one reports false, so no panel means no occlusion.</param>
        /// <param name="screenPosition">The point to test, in screen pixels with the origin bottom-left.</param>
        /// <returns>True when the panel picks an element at that point.</returns>
        public static bool IsScreenPointOverPanel(IPanel panel, Vector2 screenPosition)
        {
            if (panel == null)
            {
                return false;
            }

            Vector2 panelPosition = ToPanelPoint(panel, screenPosition);

            return panel.Pick(panelPosition) != null;
        }

        /// <summary>Resolves the board hex under a screen point.</summary>
        /// <param name="screenPosition">The point to resolve, in screen pixels with the origin bottom-left.</param>
        /// <param name="grid">The board to resolve against, which is what decides whether the hex exists.</param>
        /// <param name="coordinates">The hex under the point, or a default value when there is none.</param>
        /// <returns>True when the point falls on a hex the board contains.</returns>
        public bool TryResolveHex(Vector2 screenPosition, HexGrid grid, out HexCoordinates coordinates)
        {
            coordinates = default;

            if (_camera == null || grid == null)
            {
                return false;
            }

            // An orthographic camera needs a depth to unproject through, and the board sits at zero. Reading it
            // off the camera rather than assuming a distance keeps the resolver correct if the rig is moved back.
            // PERF: the Transform is cached alongside the camera at construction — Camera.transform and
            // Transform.position are both externs, and this runs on a 60–240 Hz pointer-move path. The null
            // check above already covers a destroyed camera for _cameraTransform too, so no second one is added.
            float distanceToBoard = BoardPlaneZ - _cameraTransform.position.z;
            Vector3 worldPosition = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distanceToBoard));

            Vector2 axial = HexMathUtils.ProjectToAxial(worldPosition, _cellVisualSize);
            HexCoordinates candidate = HexMathUtils.RoundToAxial(axial.x, axial.y);

            if (!grid.TryGetCell(candidate, out _))
            {
                return false;
            }

            coordinates = candidate;

            return true;
        }
    }
}
