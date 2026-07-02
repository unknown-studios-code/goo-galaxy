using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.PlayMode.Board
{
    [TestFixture]
    public class GridPresenterTests
    {
        private GameObject _go;
        private GridPresenter _presenter;
        private GridLayoutSO _gridLayout;

        [SetUp]
        public void SetUp()
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            FieldInfo radiusField = typeof(GridLayoutSO).GetField("_gridRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            radiusField.SetValue(_gridLayout, 3);
            MethodInfo initMethod = typeof(GridLayoutSO).GetMethod("InitializeBlockedCoordinates", BindingFlags.NonPublic | BindingFlags.Instance);
            initMethod.Invoke(_gridLayout, null);

            _go = new GameObject("GridPresenter_Test");
            _go.SetActive(false);
            _go.AddComponent<UnitMovementController>();
            _presenter = _go.AddComponent<GridPresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.Destroy(_go);
            }

            if (_gridLayout != null)
            {
                Object.Destroy(_gridLayout);
            }
        }

        [UnityTest]
        public IEnumerator Awake_WithValidLayout_InitializesHexGrid()
        {
            // GIVEN
            FieldInfo gridLayoutField = typeof(GridPresenter).GetField("_gridLayout", BindingFlags.NonPublic | BindingFlags.Instance);
            gridLayoutField.SetValue(_presenter, _gridLayout);
            int expectedCells = (3 * 3 * (3 + 1)) + 1;

            // WHEN
            _go.SetActive(true);
            yield return null;

            // THEN
            Assert.IsNotNull(_presenter.HexGrid, "HexGrid should be initialized after Awake.");
            Assert.AreEqual(3, _presenter.HexGrid.GridRadius);
            Assert.AreEqual(expectedCells, _presenter.HexGrid.Cells.Count);
        }

        [UnityTest]
        public IEnumerator Awake_WithNullLayout_HexGridRemainsNull()
        {
            // GIVEN
            LogAssert.Expect(LogType.Assert, "GridLayout configuration is missing!");
            LogAssert.Expect(LogType.Error, "GridLayout configuration is missing!");

            // WHEN
            _go.SetActive(true);
            yield return null;

            // THEN
            Assert.IsNull(_presenter.HexGrid, "HexGrid should remain null when layout is not assigned.");
        }

        [UnityTest]
        public IEnumerator GetActiveUnits_WithMovementController_ReturnsEmptyRegistryByDefault()
        {
            // GIVEN
            FieldInfo gridLayoutField = typeof(GridPresenter).GetField("_gridLayout", BindingFlags.NonPublic | BindingFlags.Instance);
            gridLayoutField.SetValue(_presenter, _gridLayout);
            _go.SetActive(true);
            yield return null;

            // WHEN
            IReadOnlyDictionary<int, GridUnit> units = _presenter.GetActiveUnits();

            // THEN
            Assert.IsNotNull(units);
            Assert.AreEqual(0, units.Count);
        }

        [UnityTest]
        public IEnumerator GetActiveUnits_WithNullMovementController_ReturnsEmptyFallback()
        {
            // GIVEN
            FieldInfo gridLayoutField = typeof(GridPresenter).GetField("_gridLayout", BindingFlags.NonPublic | BindingFlags.Instance);
            gridLayoutField.SetValue(_presenter, _gridLayout);
            _go.SetActive(true);
            yield return null;

            FieldInfo controllerField = typeof(GridPresenter).GetField("_movementController", BindingFlags.NonPublic | BindingFlags.Instance);
            controllerField.SetValue(_presenter, null);

            // WHEN
            IReadOnlyDictionary<int, GridUnit> units = _presenter.GetActiveUnits();

            // THEN
            Assert.IsNotNull(units);
            Assert.AreEqual(0, units.Count);
        }
    }
}
