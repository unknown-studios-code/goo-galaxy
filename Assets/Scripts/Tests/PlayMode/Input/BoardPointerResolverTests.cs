using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Input.Services;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Input
{
    [TestFixture]
    public class BoardPointerResolverTests
    {
        private const int BoardRadius = 4;
        private const float CellVisualSize = 1f;

        private static readonly HexCoordinates _knownHex = new(2, -1);
        private static readonly Vector2 _offGridScreenPosition = new(1_000_000f, 1_000_000f);

        private const int LayoutSettleFrameBudget = 10;

        private readonly List<Object> _spawned = new();

        private GameObject _cameraGO;
        private Camera _camera;
        private HexGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _cameraGO = new GameObject("BoardPointerResolver_Camera_Test");
            _camera = _cameraGO.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 0.5f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _spawned.Add(_cameraGO);

            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(gridLayout);

            _grid = new HexGrid(gridLayout);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _spawned)
            {
                if (created != null)
                {
                    Object.Destroy(created);
                }
            }

            _spawned.Clear();
        }

        [Test]
        public void TryResolveHex_ScreenPointOverAKnownCell_ResolvesThatCell()
        {
            // GIVEN
            Vector3 worldPosition = HexMathUtils.ProjectToWorldSpace(_knownHex, CellVisualSize);
            var screenPosition = (Vector2)_camera.WorldToScreenPoint(worldPosition);
            var resolver = new BoardPointerResolver(_camera, CellVisualSize);

            // WHEN
            bool wasResolved = resolver.TryResolveHex(screenPosition, _grid, out HexCoordinates resolved);

            // THEN
            Assert.That((wasResolved, resolved), Is.EqualTo((true, _knownHex)));
        }

        [Test]
        public void TryResolveHex_PointOffTheBoard_ReturnsFalse()
        {
            // GIVEN
            var resolver = new BoardPointerResolver(_camera, CellVisualSize);

            // WHEN
            bool wasResolved = resolver.TryResolveHex(_offGridScreenPosition, _grid, out _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolveHex_CameraDestroyed_ReturnsFalse()
        {
            // GIVEN — the resolver is built against a live camera, exactly as production does at Start, and the
            // camera is then torn down under it, the way a scene unload mid-gesture would.
            Vector3 worldPosition = HexMathUtils.ProjectToWorldSpace(_knownHex, CellVisualSize);
            var screenPosition = (Vector2)_camera.WorldToScreenPoint(worldPosition);
            var resolver = new BoardPointerResolver(_camera, CellVisualSize);
            Object.DestroyImmediate(_cameraGO);

            // WHEN
            bool wasResolved = resolver.TryResolveHex(screenPosition, _grid, out _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void IsScreenPointOverPanel_NullPanel_ReturnsFalse()
        {
            // GIVEN

            // WHEN
            bool isOverPanel = BoardPointerResolver.IsScreenPointOverPanel(null, Vector2.zero);

            // THEN
            Assert.That(isOverPanel, Is.False);
        }

        [UnityTest]
        public IEnumerator ToPanelPoint_ScreenPointAtTheBottomOfTheScreen_ResolvesBelowThePointAtTheTop()
        {
            // GIVEN — proved as an ordering rather than against a literal panel height, since the panel's pixel
            // size relative to Screen.height depends on the runner's own DPI scale, which this fixture does not
            // control. The ordering is what the flip actually guarantees regardless of that scale: a screen
            // point with a smaller Y (nearer the bottom, screen space is bottom-left origin) must land at a
            // larger panel Y (nearer the bottom, panel space is top-left origin) than a point with a larger
            // screen Y. Without the flip the two would come out the other way around.
            var documentGO = new GameObject(nameof(BoardPointerResolverTests));
            _spawned.Add(documentGO);

            PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _spawned.Add(panelSettings);

            UIDocument document = documentGO.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            int frameBudget = LayoutSettleFrameBudget;

            while ((document.rootVisualElement == null) && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(document.rootVisualElement, Is.Not.Null, "Test setup expects the UIDocument to have created its root within the wait budget.");
            IPanel panel = document.rootVisualElement.panel;

            // WHEN
            Vector2 bottomOfScreen = BoardPointerResolver.ToPanelPoint(panel, new Vector2(0f, 0f));
            Vector2 topOfScreen = BoardPointerResolver.ToPanelPoint(panel, new Vector2(0f, Screen.height));

            // THEN
            Assert.That(bottomOfScreen.y, Is.GreaterThan(topOfScreen.y));
        }
    }
}
