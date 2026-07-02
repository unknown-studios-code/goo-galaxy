using System.Collections;
using System.Reflection;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.PlayMode.Board
{
    [TestFixture]
    public class CellViewTests
    {
        private GameObject _go;
        private CellView _cellView;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CellView_Test");
            _go.AddComponent<MeshRenderer>();
            _cellView = _go.AddComponent<CellView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.Destroy(_go);
            }
        }

        [UnityTest]
        public IEnumerator InitializeCell_SetsCoordinatesAndName()
        {
            // GIVEN
            var coords = new HexCoordinates(2, -3);

            // WHEN
            _cellView.InitializeCell(coords);
            yield return null;

            // THEN
            Assert.AreEqual(coords, _cellView.CellCoordinates);
            Assert.AreEqual("Cell_2_-3", _go.name);
        }

        [UnityTest]
        public IEnumerator SetHighlightState_True_SetsIsHighlightedTrue()
        {
            // GIVEN
            _cellView.InitializeCell(new HexCoordinates(0, 0));
            Assert.IsFalse(_cellView.IsHighlighted);

            // WHEN
            _cellView.SetHighlightState(true);
            yield return null;

            // THEN
            Assert.IsTrue(_cellView.IsHighlighted);
        }

        [UnityTest]
        public IEnumerator SetHighlightState_False_ResetsHighlight()
        {
            // GIVEN
            _cellView.InitializeCell(new HexCoordinates(0, 0));
            _cellView.SetHighlightState(true);
            Assert.IsTrue(_cellView.IsHighlighted);

            // WHEN
            _cellView.SetHighlightState(false);
            yield return null;

            // THEN
            Assert.IsFalse(_cellView.IsHighlighted);
        }

        [UnityTest]
        public IEnumerator SetCellColor_DoesNotThrow()
        {
            // GIVEN
            _cellView.InitializeCell(new HexCoordinates(1, 1));

            // WHEN
            // THEN
            Assert.DoesNotThrow(() => _cellView.SetCellColor(Color.red));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NullMeshRenderer_ApplyColor_DoesNotThrow()
        {
            // GIVEN
            var bareGO = new GameObject("CellView_NoRenderer");
            CellView bareView = bareGO.AddComponent<CellView>();
            bareView.InitializeCell(new HexCoordinates(0, 0));

            FieldInfo rendererField = typeof(CellView).GetField("_meshRenderer", BindingFlags.NonPublic | BindingFlags.Instance);
            rendererField.SetValue(bareView, null);

            // WHEN
            // THEN
            Assert.DoesNotThrow(() => bareView.SetCellColor(Color.blue));
            Assert.DoesNotThrow(() => bareView.SetHighlightState(true));

            yield return null;
            Object.Destroy(bareGO);
        }
    }
}
