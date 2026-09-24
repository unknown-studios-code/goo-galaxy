using System.Collections;
using GooGalaxy.Runtime.Input.Constants;
using GooGalaxy.Runtime.Input.Models;
using GooGalaxy.Runtime.Input.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private PointerSample? _lastPressedSample;
        private PointerSample? _lastMovedSample;
        private PointerSample? _lastReleasedSample;

        public override void Setup()
        {
            base.Setup();

            // Unity's PlayMode test runner reuses one fixture instance across every test in the class, so every
            // field a test can write is reset here rather than relying on a fresh instance per test.
            _lastPressedSample = null;
            _lastMovedSample = null;
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

        private void HandlePointerReleased(PointerSample sample)
        {
            _lastReleasedSample = sample;
        }
    }
}
