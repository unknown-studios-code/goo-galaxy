using System.Collections;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Board
{
    [TestFixture]
    public class BoardCameraControllerTests
    {
        private const int BoardRadius = 4;
        private const int SmallGridRadius = 2;
        private const float CellVisualSize = 1f;
        private const float MarginFraction = 0.1f;
        private const float PortraitAspect = 0.45f;
        private const float WideAspect = 2f;
        private const float SquareAspect = 1f;
        private const float FloatTolerance = 0.01f;

        // At BoardRadius = 4, CellVisualSize = 1, MarginFraction = 0.1: halfWidth = 7, halfHeight = sqrt(3) * 4.5.
        // Width binds only below an aspect of ~0.898, so PortraitAspect sizes to width while SquareAspect and
        // WideAspect both size to height — and to the same value. A refit test must cross that bound to move.
        private const float PortraitBoundSize = 17.1111f;
        private const float WideBoundSize = 8.5737f;

        // At SmallGridRadius = 2, CellVisualSize = 1, MarginFraction = 0.1, aspect = 1: halfWidth = 4, halfHeight = sqrt(3) * 2.5.
        private const float SquareBoundSize = 4.7631f;

        private GameObject _cameraGO;
        private Camera _camera;
        private BoardCameraController _controller;
        private GridLayoutSO _gridLayout;

        [SetUp]
        public void SetUp()
        {
            _cameraGO = new GameObject(nameof(BoardCameraController));
            _camera = _cameraGO.AddComponent<Camera>();
            _camera.orthographic = true;
            _controller = _cameraGO.AddComponent<BoardCameraController>();
            _controller.SetFitConfiguration(CellVisualSize, MarginFraction);
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();

            if (_cameraGO != null)
            {
                Object.Destroy(_cameraGO);
            }

            if (_gridLayout != null)
            {
                Object.Destroy(_gridLayout);
            }
        }

        [Test]
        public void FitToBoard_PortraitAspect_SizesToTheWidthBoundExtent()
        {
            // GIVEN
            _camera.aspect = PortraitAspect;

            // WHEN
            _controller.FitToBoard(BoardRadius);

            // THEN
            Assert.That(_camera.orthographicSize, Is.EqualTo(PortraitBoundSize).Within(FloatTolerance));
        }

        [Test]
        public void FitToBoard_WideAspect_SizesToTheHeightBoundExtent()
        {
            // GIVEN
            _camera.aspect = WideAspect;

            // WHEN
            _controller.FitToBoard(BoardRadius);

            // THEN
            Assert.That(_camera.orthographicSize, Is.EqualTo(WideBoundSize).Within(FloatTolerance));
        }

        [Test]
        public void FitToBoard_NegativeRadius_LeavesTheOrthographicSizeUnchanged()
        {
            // GIVEN
            _camera.aspect = SquareAspect;
            _controller.FitToBoard(BoardRadius);
            float sizeBeforeCall = _camera.orthographicSize;

            // WHEN
            _controller.FitToBoard(-1);

            // THEN
            Assert.That(_camera.orthographicSize, Is.EqualTo(sizeBeforeCall).Within(FloatTolerance));
        }

        [Test]
        public void HandleGridInitialized_GridRaisedWithoutExplicitFitCall_SizesTheCameraToTheGridRadius()
        {
            // GIVEN
            _camera.aspect = SquareAspect;
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(SmallGridRadius);
            IHexGrid grid = new HexGrid(_gridLayout);

            // WHEN
            MatchEvents.RaiseGridInitialized(grid);

            // THEN
            Assert.That(_camera.orthographicSize, Is.EqualTo(SquareBoundSize).Within(FloatTolerance));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator LateUpdate_CameraAspectChanges_RefitsOnTheNextFrame()
        {
            // GIVEN
            _camera.aspect = PortraitAspect;
            _controller.FitToBoard(BoardRadius);

            // WHEN
            _camera.aspect = WideAspect;
            yield return null;

            // THEN
            Assert.That(_camera.orthographicSize, Is.EqualTo(WideBoundSize).Within(FloatTolerance));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator LateUpdate_CameraAspectUnchangedAfterARefit_DoesNotRefitAgain()
        {
            // GIVEN
            _camera.aspect = PortraitAspect;
            _controller.FitToBoard(BoardRadius);
            _camera.aspect = WideAspect;
            yield return null;

            // WHEN
            yield return null;

            // THEN
            Assert.That(_camera.orthographicSize, Is.EqualTo(WideBoundSize).Within(FloatTolerance));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator FitToBoard_PerspectiveCamera_LogsProjectionErrorOnlyOnce()
        {
            // GIVEN
            _camera.orthographic = false;
            _camera.aspect = SquareAspect;
            LogAssert.Expect(LogType.Error, BoardLogMessages.CameraFitRequiresOrthographic);

            // WHEN
            _controller.FitToBoard(BoardRadius);
            yield return null;
            yield return null;
            yield return null;

            // THEN
            LogAssert.NoUnexpectedReceived();
        }
    }
}
