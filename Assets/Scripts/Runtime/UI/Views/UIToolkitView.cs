using System;
using GooGalaxy.Runtime.Shared.Constants;
using UnityEngine;
using UnityEngine.UIElements;

namespace GooGalaxy.Runtime.UI.Views
{
    /// <summary>
    /// The lifecycle every runtime UI Toolkit screen in this project inherits: acquiring the
    /// <see cref="UIDocument" />'s panel, caching element references from it exactly once, and registering and
    /// unregistering the screen's callbacks in step with the component being enabled.
    /// </summary>
    /// <remarks>
    /// <b>Template Method.</b> This class owns <c>Awake</c>, <c>OnEnable</c>, <c>Start</c> and <c>OnDisable</c>
    /// and a subclass declares none of them: Unity dispatches lifecycle callbacks by name, so a subclass that
    /// declared its own <c>OnEnable</c> would hide this one rather than extend it, and the panel would never be
    /// acquired. Fill in <see cref="CacheElements" />, <see cref="RegisterCallbacks" /> and
    /// <see cref="UnregisterCallbacks" /> instead.
    /// <para>
    /// <b>The panel is not available at a fixed moment.</b> <see cref="UIDocument" /> builds its tree in its own
    /// <c>OnEnable</c>, and Unity fixes no order between two components on one GameObject, so acquisition is
    /// attempted in <c>OnEnable</c> and again in <c>Start</c>, which every <c>OnEnable</c> in the scene precedes.
    /// A screen still without a panel at <c>Start</c> logs and renders nothing rather than throwing. Subclasses
    /// must therefore treat their cached elements as possibly absent and answer <see cref="IsPanelReady" />
    /// before writing into them.
    /// </para>
    /// <para>
    /// <b>One screen per GameObject, project-wide.</b> The <c>DisallowMultipleComponent</c> below is inherited
    /// by every subclass, so no GameObject in this project can carry two runtime screens. That is deliberate
    /// rather than incidental: this type takes its panel from the <c>UIDocument</c> beside it and a
    /// <c>UIDocument</c> hosts exactly one visual tree, so a second screen on the same object would cache its
    /// elements out of the first screen's markup and silently render nothing.
    /// </para>
    /// <para>
    /// <b><see cref="PanelInitialized" /> is how a presenter learns it may push.</b> A presenter placed ahead of
    /// its view by execution order subscribes before the panel exists, is told the moment it does, and pushes a
    /// full snapshot then. Testing <see cref="IsPanelReady" /> first covers the opposite order.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public abstract class UIToolkitView : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private bool _isPanelReady;

        /// <summary>Raised once the panel exists and <see cref="CacheElements" /> has run against it.</summary>
        /// <remarks>
        /// Raised again after every disable and re-enable, because the cached references are dropped on the way
        /// down. A subscriber that also tests <see cref="IsPanelReady" /> when it subscribes cannot miss the
        /// first one.
        /// </remarks>
        public event Action PanelInitialized;

        /// <summary>Whether the panel exists and this view's element references are cached against it.</summary>
        public bool IsPanelReady => _isPanelReady;

        /// <summary>The panel root this view was built against, or <c>null</c> before the panel exists.</summary>
        protected VisualElement Root => _root;

        protected void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_document == null)
            {
                Debug.LogError(string.Format(UiLogMessages.UiDocumentMissingFormat, name), this);
            }
        }

        protected void OnEnable()
        {
            TryInitializePanel();
        }

        protected void Start()
        {
            if (TryInitializePanel())
            {
                return;
            }

            Debug.LogError(string.Format(UiLogMessages.PanelUnavailableFormat, name), this);
        }

        protected void OnDisable()
        {
            if (!_isPanelReady)
            {
                return;
            }

            UnregisterCallbacks();

            // Dropped rather than kept: a disabled UIDocument tears its tree down, so every reference cached
            // from it is stale by the time this component is enabled again.
            _isPanelReady = false;
            _root = null;
        }

        /// <summary>Caches every element reference this screen writes into, from the freshly built panel.</summary>
        /// <param name="root">The panel root. Never null when this runs.</param>
        /// <remarks>
        /// Runs exactly once per enable, before <see cref="RegisterCallbacks" />. Query here and nowhere else —
        /// a <c>Q</c> call on an update path re-walks the tree on every frame it runs.
        /// </remarks>
        protected abstract void CacheElements(VisualElement root);

        /// <summary>Registers this screen's element callbacks, against the references just cached.</summary>
        protected abstract void RegisterCallbacks();

        /// <summary>Reverses <see cref="RegisterCallbacks" />, one unregistration per registration.</summary>
        /// <remarks>Runs before the cached references are dropped, so every one of them is still valid.</remarks>
        protected abstract void UnregisterCallbacks();

        /// <summary>
        /// Raises <see cref="PanelInitialized" />, once the panel exists and the three hooks above have run.
        /// </summary>
        /// <remarks>
        /// An override must call this base implementation. Skipping it leaves every presenter waiting on a
        /// signal that never arrives, so the opening snapshot is never pushed and the screen renders whatever
        /// the markup authored and nothing more.
        /// </remarks>
        protected virtual void OnPanelInitialized()
        {
            PanelInitialized?.Invoke();
        }

        /// <summary>Queries one element by name and reports a miss as an actionable console error.</summary>
        /// <typeparam name="T">The element type the markup declares.</typeparam>
        /// <param name="root">The panel root to query from.</param>
        /// <param name="elementName">The UXML <c>name</c>, taken from a selector constant rather than typed.</param>
        /// <returns>The element, or <c>null</c> when the markup no longer declares it under that name.</returns>
        protected T RequireElement<T>(VisualElement root, string elementName)
            where T : VisualElement
        {
            T element = root.Q<T>(elementName);

            if (element == null)
            {
                Debug.LogError(string.Format(UiLogMessages.ElementMissingFormat, name, elementName), this);
            }

            return element;
        }

        private bool TryInitializePanel()
        {
            if (_isPanelReady)
            {
                return true;
            }

            VisualElement root = _document == null ? null : _document.rootVisualElement;

            if (root == null)
            {
                return false;
            }

            // A UXML root is a full-screen flex host carrying the default PickingMode.Position, so it answers
            // every pick on screen and would swallow the board input behind the panel. Set here rather than in
            // USS, where picking-mode is not a property and is dropped after one import warning.
            root.pickingMode = PickingMode.Ignore;

            _root = root;
            CacheElements(root);
            RegisterCallbacks();
            _isPanelReady = true;

            OnPanelInitialized();

            return true;
        }
    }
}
