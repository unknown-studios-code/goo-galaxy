using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.PlayMode.Board
{
    [TestFixture]
    public class GridViewTests
    {
        private const float PositionTolerance = 0.0001f;

        private GameObject _prefabGO;
        private CellView _cellPrefab;
        private GridLayoutSO _gridLayout;

        [SetUp]
        public void SetUp()
        {
            _prefabGO = new GameObject("CellPrefab_Test");
            _prefabGO.AddComponent<SpriteRenderer>();
            _cellPrefab = _prefabGO.AddComponent<CellView>();
            _prefabGO.SetActive(false);

            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(gridRadius: 4);
        }

        [TearDown]
        public void TearDown()
        {
            if (_prefabGO != null)
            {
                Object.Destroy(_prefabGO);
            }

            if (_gridLayout != null)
            {
                Object.Destroy(_gridLayout);
            }

            GridPresenter[] presenters = Object.FindObjectsByType<GridPresenter>(FindObjectsSortMode.None);
            foreach (GridPresenter presenter in presenters)
            {
                Object.Destroy(presenter.gameObject);
            }

            GridView[] views = Object.FindObjectsByType<GridView>(FindObjectsSortMode.None);
            foreach (GridView view in views)
            {
                Object.Destroy(view.gameObject);
            }
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator GridView_Initialization_Spawns61CellsAtDistinctPositions()
        {
            // GIVEN
            var presenterGO = new GameObject("GridPresenter_Test");
            presenterGO.SetActive(false);
            GridPresenter presenter = presenterGO.AddComponent<GridPresenter>();

            var viewGO = new GameObject("GridView_Test");
            viewGO.SetActive(false);
            GridView view = viewGO.AddComponent<GridView>();

            presenter.SetGridLayout(_gridLayout);
            view.SetViewConfiguration(_cellPrefab, cellVisualSize: 1.0f);

            var positions = new HashSet<Vector3>();

            // WHEN
            viewGO.SetActive(true);
            presenterGO.SetActive(true);

            yield return null;

            // THEN
            Assert.That(view.CellViews.Count, Is.EqualTo(61), "Should have spawned exactly 61 visual hex cells.");

            Assert.That(
                view.CellViews.Values,
                Is.All.Matches<CellView>(cell => Mathf.Abs(cell.transform.localPosition.z) <= PositionTolerance),
                "Cells should lie on the XY plane, facing the 2D camera."
            );

            foreach (KeyValuePair<HexCoordinates, CellView> kvp in view.CellViews)
            {
                Vector3 localPos = kvp.Value.transform.localPosition;
                positions.Add(new Vector3(Mathf.Round(localPos.x * 100f) / 100f, Mathf.Round(localPos.y * 100f) / 100f, Mathf.Round(localPos.z * 100f) / 100f));
            }

            Assert.That(positions.Count, Is.EqualTo(61), "All 61 cells must have distinct positions.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator CellViews_WhenCellPrefabIsMissing_StayEmptyAndLogError()
        {
            // GIVEN
            LogAssert.Expect(LogType.Assert, BoardLogMessages.CellViewPrefabNotAssigned);
            LogAssert.Expect(LogType.Error, BoardLogMessages.CellViewPrefabNotAssigned);

            var presenterGO = new GameObject("GridPresenter_Test");
            presenterGO.SetActive(false);
            GridPresenter presenter = presenterGO.AddComponent<GridPresenter>();
            presenter.SetGridLayout(_gridLayout);

            var viewGO = new GameObject("GridView_Test");
            viewGO.SetActive(false);
            GridView view = viewGO.AddComponent<GridView>();

            // WHEN — the view is never given a cell prefab
            viewGO.SetActive(true);
            presenterGO.SetActive(true);
            yield return null;

            // THEN
            Assert.That(view.CellViews, Is.Empty);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator CellViews_WhenGridIsInitializedTwice_AreRebuiltWithoutDuplicatingCells()
        {
            // GIVEN
            var presenterGO = new GameObject("GridPresenter_Test");
            presenterGO.SetActive(false);
            GridPresenter presenter = presenterGO.AddComponent<GridPresenter>();
            presenter.SetGridLayout(_gridLayout);

            var viewGO = new GameObject("GridView_Test");
            viewGO.SetActive(false);
            GridView view = viewGO.AddComponent<GridView>();
            view.SetViewConfiguration(_cellPrefab, cellVisualSize: 1.0f);

            viewGO.SetActive(true);
            presenterGO.SetActive(true);
            yield return null;

            // WHEN — the same grid is published again, as it is after a layout change
            MatchEvents.RaiseGridInitialized(presenter.HexGrid);
            yield return null;

            // THEN
            Assert.That(view.CellViews.Count, Is.EqualTo(61));
            Assert.That(view.transform.childCount, Is.EqualTo(61), "The previous cell instances must be destroyed, not stacked.");
        }
    }
}
