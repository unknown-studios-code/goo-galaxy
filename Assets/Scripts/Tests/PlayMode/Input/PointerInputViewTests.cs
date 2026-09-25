using System.Collections;
using GooGalaxy.Runtime.Input.Constants;
using GooGalaxy.Runtime.Input.Models;
using GooGalaxy.Runtime.Input.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Input
{
    [TestFixture]
    public class PointerInputViewTests : InputTestFixture
    {
        private const string MatchInputAssetPath = "Assets/Settings/Input/MatchInput.inputactions";
        private const string InputActionsFieldName = "_inputActions";

        private static readonly Vector2 _pressPoint = new(100f, 200f);
        private static readonly Vector2 _movePoint = new(150f, 220f);

        private GameObject _viewGO;
        private PointerInputView _view;
        private InputActionAsset _inputActions;
        private Mouse _mouse;
        private Touchscreen _touchscreen;
        private PointerSample? _lastPressedSample;
        private PointerSample? _lastMovedSample;
        private PointerSample? _lastHoveredSample;
        private PointerSample? _lastReleasedSample;

        public override void Setup()
        {
            base.Setup();

            // Unity's PlayMode test runner reuses one fixture instance across every test in the class, so every
            // field a test can write is reset here rather than relying on a fresh instance per test.
            _lastPressedSample = null;
            _lastMovedSample = null;
            _lastHoveredSample = null;
            _lastReleasedSample = null;

            _mouse = InputSystem.AddDevice<Mouse>();

            InputActionAsset sourceActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(MatchInputAssetPath);
            Assert.That(sourceActions, Is.Not.Null, $"Test setup expects '{MatchInputAssetPath}' to exist and import as an InputActionAsset.");
            _inputActions = Object.Instantiate(sourceActions);

            _viewGO = new GameObject(nameof(PointerInputView));
            _viewGO.SetActive(false);
            _view = _viewGO.AddComponent<PointerInputView>();

            var serializedView = new SerializedObject(_view);
            serializedView.FindProperty(InputActionsFieldName).objectReferenceValue = _inputActions;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void TearDown()
        {
            if (_viewGO != null)
            {
                Object.Destroy(_viewGO);
            }

            if (_inputActions != null)
            {
                Object.Destroy(_inputActions);
            }

            if (_mouse != null)
            {
                InputSystem.RemoveDevice(_mouse);
            }

            if (_touchscreen != null)
            {
                InputSystem.RemoveDevice(_touchscreen);
            }

            base.TearDown();
        }

        [Test]
        public void OnEnable_Always_EnablesTheMatchActionMap()
        {
            // GIVEN
            InputActionMap matchMap = _inputActions.FindActionMap(InputActionNames.MatchMap);

            // WHEN
            ActivateView();

            // THEN
            Assert.That(matchMap.enabled, Is.True);
        }

        [Test]
        public void OnDisable_Always_DisablesTheMatchActionMap()
        {
            // GIVEN
            ActivateView();
            InputActionMap matchMap = _inputActions.FindActionMap(InputActionNames.MatchMap);

            // WHEN
            _view.enabled = false;

            // THEN
            Assert.That(matchMap.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator OnDisable_DuringAPress_ClearsIsPointerDown()
        {
            // GIVEN
            ActivateView();
            yield return SendPointerStateAsync(_pressPoint, isPressed: true);

            // WHEN
            _view.enabled = false;

            // THEN
            Assert.That(_view.IsPointerDown, Is.False);
        }

        [UnityTest]
        public IEnumerator OnDisable_DuringAPress_DoesNotRaisePointerReleased()
        {
            // GIVEN
            ActivateView();
            _view.PointerReleased += HandlePointerReleased;
            yield return SendPointerStateAsync(_pressPoint, isPressed: true);

            // WHEN
            _view.enabled = false;

            // THEN
            Assert.That(_lastReleasedSample, Is.Null);
        }

        [UnityTest]
        public IEnumerator PointerPressed_OnAPress_ReportsThePressedPositionAndPhase()
        {
            // GIVEN
            ActivateView();
            _view.PointerPressed += HandlePointerPressed;

            // WHEN
            yield return SendPointerStateAsync(_pressPoint, isPressed: true);

            // THEN
            Assert.That((_lastPressedSample.Value.ScreenPosition, _lastPressedSample.Value.Phase), Is.EqualTo((_pressPoint, PointerPhase.Pressed)));
        }

        [UnityTest]
        public IEnumerator PointerMoved_WhileDown_ReportsTheMovedPositionAndPhase()
        {
            // GIVEN
            ActivateView();
            _view.PointerMoved += HandlePointerMoved;
            yield return SendPointerStateAsync(_pressPoint, isPressed: true);

            // WHEN
            yield return SendPointerStateAsync(_movePoint, isPressed: true);

            // THEN
            Assert.That((_lastMovedSample.Value.ScreenPosition, _lastMovedSample.Value.Phase), Is.EqualTo((_movePoint, PointerPhase.Moved)));
        }

        [UnityTest]
        public IEnumerator PointerMoved_WhileNotDown_DoesNotRaise()
        {
            // GIVEN
            ActivateView();
            _view.PointerMoved += HandlePointerMoved;

            // WHEN
            yield return SendPointerStateAsync(_movePoint, isPressed: false);

            // THEN
            Assert.That(_lastMovedSample, Is.Null);
        }

        [UnityTest]
        public IEnumerator PointerHovered_MouseMovesWithNoButtonHeld_ReportsTheMovedPositionAndHoveredPhase()
        {
            // GIVEN
            ActivateView();
            _view.PointerHovered += HandlePointerHovered;

            // WHEN
            yield return SendHoverAsync(_movePoint);

            // THEN
            Assert.That((_lastHoveredSample.Value.ScreenPosition, _lastHoveredSample.Value.Phase), Is.EqualTo((_movePoint, PointerPhase.Hovered)));
        }

        [UnityTest]
        public IEnumerator PointerHovered_MouseMovesWhilePressed_DoesNotRaise()
        {
            // GIVEN — the arrangement press can itself report a position change a moment before the button
            // change lands in the same Input System update, which is a legitimate hover the instant before the
            // press and not what this test is about, so the sample it may have set is cleared before the act.
            ActivateView();
            _view.PointerHovered += HandlePointerHovered;
            yield return SendPointerStateAsync(_pressPoint, isPressed: true);
            _lastHoveredSample = null;

            // WHEN
            yield return SendPointerStateAsync(_movePoint, isPressed: true);

            // THEN
            Assert.That(_lastHoveredSample, Is.Null);
        }

        [UnityTest]
        public IEnumerator PointerHovered_TouchscreenMoves_NeverRaises()
        {
            // GIVEN — Touchscreen.position mirrors the primary touch and only updates for an active touch
            // contact, so the move must be queued as a real touch state rather than through Set: Set merely
            // writes the aggregate position control's own backing memory, which Touchscreen's state processing
            // then overwrites back to zero the moment the frame runs with no active touch behind it — measured
            // here directly against the device, independent of this view's own bindings.
            ActivateView();
            _view.PointerHovered += HandlePointerHovered;
            _touchscreen = InputSystem.AddDevice<Touchscreen>();

            // WHEN
            InputSystem.QueueStateEvent(
                _touchscreen,
                new TouchState
                {
                    touchId = 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Began,
                    position = _movePoint,
                }
            );
            InputSystem.Update();
            yield return null;

            // THEN — proves the touch reading genuinely reached the position action before checking it raised no
            // hover, or a binding fault that stopped the reading from arriving at all would pass this test for
            // the wrong reason: no hover, because nothing happened.
            Assert.That(_view.CurrentScreenPosition, Is.EqualTo(_movePoint), "Test setup expects the touch reading to have reached the position action.");
            Assert.That(_lastHoveredSample, Is.Null);
        }

        // PointerHovered_MouseDisabled_DoesNotRaise was removed rather than fixed: a disabled device is dropped by
        // the Input System itself before HandlePositionPerformed's own IsHoverCapable check ever runs, per
        // InputSystem.DisableDevice's documented behaviour, so no event this fixture can send reaches that check
        // with the device already disabled — the test asserted silence for a reason unrelated to the branch it
        // named. Reaching the branch genuinely would need a device that reports Mouse.enabled false while still
        // delivering state changes, which is not a real device and not a seam this frozen runtime can be given.

        [UnityTest]
        public IEnumerator CurrentScreenPosition_MovedWhileNotDown_StillUpdatesFromTheDevice()
        {
            // GIVEN
            ActivateView();

            // WHEN
            yield return SendPointerStateAsync(_movePoint, isPressed: false);

            // THEN
            Assert.That(_view.CurrentScreenPosition, Is.EqualTo(_movePoint));
        }

        [UnityTest]
        public IEnumerator PointerReleased_OnARelease_ReportsThePhase()
        {
            // GIVEN
            ActivateView();
            _view.PointerReleased += HandlePointerReleased;
            yield return SendPointerStateAsync(_pressPoint, isPressed: true);

            // WHEN
            yield return SendPointerStateAsync(_pressPoint, isPressed: false);

            // THEN
            Assert.That(_lastReleasedSample.Value.Phase, Is.EqualTo(PointerPhase.Released));
        }

        [UnityTest]
        public IEnumerator IsPointerDown_AfterAPress_ReturnsTrue()
        {
            // GIVEN
            ActivateView();

            // WHEN
            yield return SendPointerStateAsync(_pressPoint, isPressed: true);

            // THEN
            Assert.That(_view.IsPointerDown, Is.True);
        }

        [UnityTest]
        public IEnumerator IsPointerDown_AfterARelease_ReturnsFalse()
        {
            // GIVEN
            ActivateView();
            yield return SendPointerStateAsync(_pressPoint, isPressed: true);

            // WHEN
            yield return SendPointerStateAsync(_pressPoint, isPressed: false);

            // THEN
            Assert.That(_view.IsPointerDown, Is.False);
        }

        // Drives the fixture's own isolated Mouse through InputTestFixture's Set/Press/Release helpers instead of
        // a hand-rolled MouseState event: both the position and the button move within one InputSystem.Update()
        // call, matching the single combined state the production device would report for one physical move-
        // or-click. [UnityTest] forces every InputTestFixture.Set call to queue rather than update immediately
        // (see InputTestFixture.Set's IsUnityTest branch), so the Update() below is what actually applies them.
        private IEnumerator SendPointerStateAsync(Vector2 position, bool isPressed)
        {
            Set(_mouse.position, position);

            if (isPressed)
            {
                Press(_mouse.leftButton);
            }
            else
            {
                Release(_mouse.leftButton);
            }

            InputSystem.Update();
            yield return null;
        }

        // Moves the mouse position without touching its button, matching a real hover: no MouseState button bit
        // set, unlike SendPointerStateAsync which always drives both together for a press or a release.
        private IEnumerator SendHoverAsync(Vector2 position)
        {
            Set(_mouse.position, position);
            InputSystem.Update();
            yield return null;
        }

        private void ActivateView()
        {
            _viewGO.SetActive(true);
        }

        private void HandlePointerPressed(PointerSample sample)
        {
            _lastPressedSample = sample;
        }

        private void HandlePointerMoved(PointerSample sample)
        {
            _lastMovedSample = sample;
        }

        private void HandlePointerHovered(PointerSample sample)
        {
            _lastHoveredSample = sample;
        }

        private void HandlePointerReleased(PointerSample sample)
        {
            _lastReleasedSample = sample;
        }
    }
}
