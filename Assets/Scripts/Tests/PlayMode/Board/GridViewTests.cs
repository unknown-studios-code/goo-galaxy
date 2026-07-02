using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.PlayMode.Board
{
    [TestFixture]
    public class GridViewTests
    {
        private GameObject _prefabGO;
        private CellView _cellPrefab;
        private GridLayoutSO _gridLayout;

        [SetUp]
        public void SetUp()
        {
            _prefabGO = new GameObject("CellPrefab_Test");
            _prefabGO.AddComponent<MeshRenderer>();
            _cellPrefab = _prefabGO.AddComponent<CellView>();
            _prefabGO.SetActive(false);

            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            FieldInfo radiusField = typeof(GridLayoutSO).GetField("_gridRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            radiusField.SetValue(_gridLayout, 4);
            MethodInfo initMethod = typeof(GridLayoutSO).GetMethod("InitializeBlockedCoordinates", BindingFlags.NonPublic | BindingFlags.Instance);
            initMethod.Invoke(_gridLayout, null);
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
        public IEnumerator GridView_Initialization_Spawns61CellsAtDistinctPositions()
        {
            // GIVEN
            var presenterGO = new GameObject("GridPresenter_Test");
            presenterGO.SetActive(false);
            GridPresenter presenter = presenterGO.AddComponent<GridPresenter>();

            var viewGO = new GameObject("GridView_Test");
            viewGO.SetActive(false);
            GridView view = viewGO.AddComponent<GridView>();

            FieldInfo gridLayoutField = typeof(GridPresenter).GetField("_gridLayout", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(gridLayoutField, "GridPresenter should have _gridLayout field.");
            gridLayoutField.SetValue(presenter, _gridLayout);

            FieldInfo cellPrefabField = typeof(GridView).GetField("_cellPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(cellPrefabField, "GridView should have _cellPrefab field.");
            cellPrefabField.SetValue(view, _cellPrefab);

            FieldInfo cellVisualSizeField = typeof(GridView).GetField("_cellVisualSize", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(cellVisualSizeField, "GridView should have _cellVisualSize field.");
            cellVisualSizeField.SetValue(view, 1.0f);

            var positions = new HashSet<Vector3>();

            // WHEN
            viewGO.SetActive(true);
            presenterGO.SetActive(true);

            yield return null;

            // THEN
            Assert.AreEqual(61, view.CellViews.Count, "Should have spawned exactly 61 visual hex cells.");

            foreach (KeyValuePair<HexCoordinates, CellView> kvp in view.CellViews)
            {
                CellView cellView = kvp.Value;
                Vector3 localPos = cellView.transform.localPosition;

                Assert.AreEqual(0f, localPos.y, "Cells should lie flat on the XZ plane.");

                var rounded = new Vector3(Mathf.Round(localPos.x * 100f) / 100f, Mathf.Round(localPos.y * 100f) / 100f, Mathf.Round(localPos.z * 100f) / 100f);
                positions.Add(rounded);
            }

            Assert.AreEqual(61, positions.Count, "All 61 cells must have distinct positions.");
        }
    }
}
