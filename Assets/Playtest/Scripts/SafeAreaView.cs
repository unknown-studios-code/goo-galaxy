using UnityEngine;
using UnityEngine.UIElements;

namespace GooGalaxy.Playtest
{
    /// <summary>
    /// Keeps a UI Toolkit panel inside the device's safe area by padding its root, so nothing renders under a
    /// notch or behind the home indicator. Runs in edit mode as well, so the Game view preview matches play.
    /// </summary>
    /// <remarks>
    /// A component of its own rather than a block inside the HUD view: <c>[ExecuteAlways]</c> on a view that also
    /// subscribes to the static match bus would accumulate handlers across every assembly reload in the editor.
    /// This one only ever reads <c>Screen.safeArea</c> and writes padding, so it is inert outside of layout.
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class SafeAreaView : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // Awake does not run on a domain reload in edit mode, so the reference is re-resolved here.
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            RegisterOnRoot();
            ApplySafeArea();
        }

        private void OnDisable()
        {
            UnregisterFromRoot();
        }

#if UNITY_EDITOR
        // Re-applies after the Inspector swaps the source asset or the panel settings, which rebuilds the root.
        private void OnValidate()
        {
            if (_document == null)
            {
                return;
            }

            RegisterOnRoot();
            ApplySafeArea();
        }
#endif

        /// <summary>
        /// Pads the panel root by the current safe-area insets. Called automatically on enable and whenever the
        /// panel is laid out; call it directly only after changing the panel from code.
        /// </summary>
        public void ApplySafeArea()
        {
            VisualElement root = _document == null ? null : _document.rootVisualElement;

            if (root == null)
            {
                return;
            }

            float panelWidth = root.resolvedStyle.width;
            float panelHeight = root.resolvedStyle.height;

            // Before the first layout pass the resolved size is NaN, and NaN fails every ordered comparison —
            // so it has to be rejected explicitly or the padding below is computed as NaN and the panel breaks.
            if (!IsUsableLength(panelWidth) || !IsUsableLength(panelHeight) || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            // Screen.safeArea is in screen pixels with a bottom-left origin, while the panel has its own scaled
            // coordinate space. Each inset is taken as a fraction of the screen and re-expressed in panel units,
            // which keeps this correct under any PanelSettings scale mode.
            Rect safeArea = Screen.safeArea;

            root.style.paddingLeft = safeArea.xMin / Screen.width * panelWidth;
            root.style.paddingRight = (Screen.width - safeArea.xMax) / Screen.width * panelWidth;
            root.style.paddingTop = (Screen.height - safeArea.yMax) / Screen.height * panelHeight;
            root.style.paddingBottom = safeArea.yMin / Screen.height * panelHeight;
        }

        private static bool IsUsableLength(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private void RegisterOnRoot()
        {
            VisualElement root = _document == null ? null : _document.rootVisualElement;

            if (root == _root)
            {
                return;
            }

            UnregisterFromRoot();
            _root = root;

            if (_root != null)
            {
                _root.RegisterCallback<GeometryChangedEvent>(HandleRootGeometryChanged);
            }
        }

        private void UnregisterFromRoot()
        {
            if (_root == null)
            {
                return;
            }

            _root.UnregisterCallback<GeometryChangedEvent>(HandleRootGeometryChanged);
            _root = null;
        }

        // Padding does not change the root's own border box, so re-applying here cannot re-trigger this event.
        private void HandleRootGeometryChanged(GeometryChangedEvent evt)
        {
            ApplySafeArea();
        }
    }
}
