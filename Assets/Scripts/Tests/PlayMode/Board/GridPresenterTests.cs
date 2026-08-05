using System.Collections;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Shared.Constants;
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
            _gridLayout.SetAuthoredData(gridRadius: 3);

            _go = new GameObject("GridPresenter_Test");
            _go.SetActive(false);
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
            _presenter.SetGridLayout(_gridLayout);

            // WHEN
            _go.SetActive(true);
            yield return null;

            // THEN
            Assert.That(_presenter.HexGrid, Is.Not.Null, "HexGrid should be initialized after Awake.");
            Assert.That(_presenter.HexGrid.GridRadius, Is.EqualTo(3));
            Assert.That(_presenter.HexGrid.Cells.Count, Is.EqualTo(37));
        }

        [UnityTest]
        public IEnumerator Awake_WithNullLayout_HexGridRemainsNull()
        {
            // GIVEN
            LogAssert.Expect(LogType.Assert, BoardLogMessages.GridLayoutConfigurationMissing);
            LogAssert.Expect(LogType.Error, BoardLogMessages.GridLayoutConfigurationMissing);

            // WHEN
            _go.SetActive(true);
            yield return null;

            // THEN
            Assert.That(_presenter.HexGrid, Is.Null, "HexGrid should remain null when layout is not assigned.");
        }
    }
}
