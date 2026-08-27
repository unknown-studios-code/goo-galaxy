using UnityEngine;
using UnityEngine.UIElements;

namespace GooGalaxy.Runtime.UI.Views.Elements
{
    /// <summary>
    /// A container that keeps its children clear of the notch, the rounded corners and the gesture bar by
    /// converting <see cref="Screen.safeArea" /> into panel space and applying it as its own padding.
    /// </summary>
    /// <remarks>
    /// <b>Recomputed on every layout, not once at startup.</b> Android's gesture bar changes height when the
    /// navigation mode changes, and both platforms report a different safe area after a split into
    /// multi-window, so a value captured in <c>Start</c> is wrong for the rest of the session. Rotation would
    /// be a third case and is not a live one: the project is portrait-locked. This already covers it if that
    /// lock is ever lifted.
    /// <para>
    /// <b>The conversion factor is the panel's own width over the screen's.</b> Panel Settings scales this
    /// project's runtime panels with screen size, matched on width, so one factor converts both axes exactly and
    /// nothing here has to read the reference resolution back out of the asset.
    /// </para>
    /// <para>
    /// <b>Writing padding from a geometry callback re-triggers layout.</b> The applied safe area and scale are
    /// cached and compared first, so the second pass finds nothing to change and the loop terminates after one
    /// extra layout instead of running every frame.
    /// </para>
    /// </remarks>
    [UxmlElement]
    public partial class SafeAreaElement : VisualElement
    {
        private static readonly Rect _unapplied = new(float.NaN, float.NaN, float.NaN, float.NaN);

        private Rect _appliedSafeArea = _unapplied;
        private float _appliedScale = float.NaN;

        public SafeAreaElement()
        {
            RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
        }

        /// <summary>Whether the safe area is honored. Turn it off to lay a screen out edge to edge.</summary>
        /// <remarks>Switching it off clears the padding this element applied, rather than freezing the last value.</remarks>
        [UxmlAttribute]
        public bool IsSafeAreaApplied { get; set; } = true;

        /// <summary>Recomputes the padding from the current screen safe area, if it has moved since the last pass.</summary>
        /// <remarks>
        /// Called on every geometry change. Safe to call directly when something outside layout changed the safe
        /// area — a navigation-mode switch that produced no resize, for instance.
        /// </remarks>
        public void RefreshSafeArea()
        {
            if (!IsSafeAreaApplied)
            {
                ClearSafeArea();
                return;
            }

            IPanel hostPanel = panel;

            if (hostPanel == null)
            {
                return;
            }

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float panelWidth = hostPanel.visualTree.layout.width;

            if ((screenWidth <= 0f) || (screenHeight <= 0f) || (panelWidth <= 0f) || float.IsNaN(panelWidth))
            {
                return;
            }

            float scale = panelWidth / screenWidth;
            Rect safeArea = Screen.safeArea;

            if ((safeArea == _appliedSafeArea) && Mathf.Approximately(scale, _appliedScale))
            {
                return;
            }

            _appliedSafeArea = safeArea;
            _appliedScale = scale;

            // Screen space is bottom-left origin and panel padding is top-left, so the top inset is measured
            // down from the screen height while the bottom inset is the safe area's own origin.
            style.paddingLeft = Mathf.Max(0f, safeArea.xMin * scale);
            style.paddingRight = Mathf.Max(0f, (screenWidth - safeArea.xMax) * scale);
            style.paddingTop = Mathf.Max(0f, (screenHeight - safeArea.yMax) * scale);
            style.paddingBottom = Mathf.Max(0f, safeArea.yMin * scale);
        }

        private void ClearSafeArea()
        {
            _appliedSafeArea = _unapplied;
            _appliedScale = float.NaN;

            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            RefreshSafeArea();
        }
    }
}
