using System;
using GooGalaxy.Runtime.Input.Constants;
using GooGalaxy.Runtime.Input.Interfaces;
using GooGalaxy.Runtime.Input.Models;
using GooGalaxy.Runtime.Shared.Constants;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GooGalaxy.Runtime.Input.Views
{
    /// <summary>
    /// Reads the device's pointer through the Input System and republishes it as the single stream the
    /// interaction layer is written against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It decides nothing.</b> No board is read here, no selection is held, and no gesture is classified —
    /// this component turns two Input System actions into <see cref="PointerSample" /> values and stops. That is
    /// what keeps <see cref="IPointerSource" /> replaceable by a fake with no device behind it, and what keeps
    /// the Input System named in exactly one file of this assembly.
    /// </para>
    /// <para>
    /// <b>One pointer, and the first one wins.</b> A press arriving while another is already down is dropped,
    /// and a release is only reported for the control that started the live press. Without both guards a second
    /// finger would start a second selection on a board that has room for one, and lifting either finger would
    /// end whichever gesture happened to be live. The board camera frames all 61 sectors on every aspect, so
    /// there is no pan and no pinch-zoom for a second finger to be doing instead.
    /// </para>
    /// <para>
    /// <b>The map is enabled with the component and disabled with it.</b> An action map left enabled across a
    /// scene unload keeps dispatching into callbacks whose objects Unity has already destroyed, which surfaces
    /// as a <c>MissingReferenceException</c> from inside the Input System rather than from anything in this
    /// assembly. The subscriptions are torn down in the same callback for the same reason.
    /// </para>
    /// <para>
    /// The asset arrives through the Inspector rather than through <c>Resources.Load</c> or a path, so a
    /// missing one is a wiring fault visible in the scene instead of a string that resolves to null at runtime.
    /// </para>
    /// <para>
    /// <b><c>OnDisable</c> clears the latch but raises no <see cref="PointerReleased" />.</b> A subscriber that
    /// outlives this view's own disable — one on a different enable/disable scope — would be left holding a live
    /// selection with no pointer behind it. That gap cannot open in practice today because
    /// <c>MatchInputController</c> and this view are registered on the same scope and disabled together; it
    /// would if the two were ever split across scopes.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class PointerInputView : MonoBehaviour, IPointerSource
    {
        [Tooltip("The asset declaring the Match action map. Without it no finger reaches the board and the match cannot be played.")]
        [SerializeField]
        private InputActionAsset _inputActions;

        private InputActionMap _matchMap;
        private InputAction _pointerPositionAction;
        private InputAction _pointerPressAction;
        private InputControl _activePressControl;
        private Vector2 _currentScreenPosition;
        private bool _isPointerDown;

        public event Action<PointerSample> PointerPressed;

        public event Action<PointerSample> PointerMoved;

        public event Action<PointerSample> PointerReleased;

        public Vector2 CurrentScreenPosition => _currentScreenPosition;

        public bool IsPointerDown => _isPointerDown;

        protected void Awake()
        {
            ResolveActions();
        }

        protected void OnEnable()
        {
            if (_pointerPressAction != null)
            {
                _pointerPressAction.started += HandlePressStarted;
                _pointerPressAction.canceled += HandlePressCanceled;
            }

            if (_pointerPositionAction != null)
            {
                _pointerPositionAction.performed += HandlePositionPerformed;
            }

            _matchMap?.Enable();
        }

        protected void OnDisable()
        {
            _matchMap?.Disable();

            if (_pointerPressAction != null)
            {
                _pointerPressAction.started -= HandlePressStarted;
                _pointerPressAction.canceled -= HandlePressCanceled;
            }

            if (_pointerPositionAction != null)
            {
                _pointerPositionAction.performed -= HandlePositionPerformed;
            }

            // A finger still down when the component goes away would otherwise leave the latch set, and the
            // first press of the next enable would be dropped as a second finger.
            ReleaseActivePointer();
        }

        /// <remarks>An override that skips the base call drops the event and no subscriber sees the pointer.</remarks>
        protected virtual void OnPointerPressed()
        {
            PointerPressed?.Invoke(BuildSample(PointerPhase.Pressed));
        }

        /// <remarks>An override that skips the base call drops the event and no subscriber sees the pointer.</remarks>
        protected virtual void OnPointerMoved()
        {
            PointerMoved?.Invoke(BuildSample(PointerPhase.Moved));
        }

        /// <remarks>An override that skips the base call drops the event and no subscriber sees the pointer.</remarks>
        protected virtual void OnPointerReleased()
        {
            PointerReleased?.Invoke(BuildSample(PointerPhase.Released));
        }

        private void ResolveActions()
        {
            if (_inputActions == null)
            {
                Debug.LogError(InputLogMessages.PointerActionAssetMissing, this);

                return;
            }

            _matchMap = _inputActions.FindActionMap(InputActionNames.MatchMap, throwIfNotFound: false);

            if (_matchMap == null)
            {
                Debug.LogError(string.Format(InputLogMessages.PointerActionMapMissingFormat, InputActionNames.MatchMap), this);

                return;
            }

            _pointerPositionAction = FindAction(InputActionNames.PointerPosition);
            _pointerPressAction = FindAction(InputActionNames.PointerPress);
        }

        private InputAction FindAction(string actionName)
        {
            InputAction action = _matchMap.FindAction(actionName, throwIfNotFound: false);

            if (action == null)
            {
                Debug.LogError(string.Format(InputLogMessages.PointerActionMissingFormat, actionName, InputActionNames.MatchMap), this);
            }

            return action;
        }

        private void ReleaseActivePointer()
        {
            _activePressControl = null;
            _isPointerDown = false;
        }

        private PointerSample BuildSample(PointerPhase phase)
        {
            // Unscaled, so a paused match freezes the board without freezing the gesture the player is mid-way
            // through — the tap-versus-drag line is a property of the finger, not of match time.
            return new PointerSample(_currentScreenPosition, phase, Time.unscaledTime);
        }

        private void HandlePressStarted(InputAction.CallbackContext context)
        {
            if (_isPointerDown)
            {
                return;
            }

            _activePressControl = context.control;
            _isPointerDown = true;

            // Read rather than remembered: the press action carries a button value, and the position action may
            // not have reported yet on the frame a finger first touches down.
            if (_pointerPositionAction != null)
            {
                _currentScreenPosition = _pointerPositionAction.ReadValue<Vector2>();
            }

            OnPointerPressed();
        }

        private void HandlePressCanceled(InputAction.CallbackContext context)
        {
            if (!_isPointerDown || !ReferenceEquals(context.control, _activePressControl))
            {
                return;
            }

            ReleaseActivePointer();
            OnPointerReleased();
        }

        private void HandlePositionPerformed(InputAction.CallbackContext context)
        {
            _currentScreenPosition = context.ReadValue<Vector2>();

            if (!_isPointerDown)
            {
                return;
            }

            OnPointerMoved();
        }
    }
}
