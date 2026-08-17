using System.Collections;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Shared.Types;
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
            _go.AddComponent<SpriteRenderer>();
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
        [Timeout(5000)]
        public IEnumerator InitializeCell_WithCoordinates_SetsCoordinatesAndGameObjectName()
        {
            // GIVEN
            var coords = new HexCoordinates(2, -3);

            // WHEN
            _cellView.InitializeCell(coords);
            yield return null;

            // THEN
            Assert.That(_cellView.CellCoordinates, Is.EqualTo(coords));
            Assert.That(_go.name, Is.EqualTo("Cell_2_-3"));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SetHighlightState_True_SetsIsHighlightedTrue()
        {
            // GIVEN
            _cellView.InitializeCell(new HexCoordinates(0, 0));
            Assert.That(_cellView.IsHighlighted, Is.False);

            // WHEN
            _cellView.SetHighlightState(true);
            yield return null;

            // THEN
            Assert.That(_cellView.IsHighlighted, Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SetHighlightState_False_ResetsHighlight()
        {
            // GIVEN
            _cellView.InitializeCell(new HexCoordinates(0, 0));
            _cellView.SetHighlightState(true);
            Assert.That(_cellView.IsHighlighted, Is.True);

            // WHEN
            _cellView.SetHighlightState(false);
            yield return null;

            // THEN
            Assert.That(_cellView.IsHighlighted, Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SetCellColor_OnInitializedCell_DoesNotThrow()
        {
            // GIVEN
            _cellView.InitializeCell(new HexCoordinates(1, 1));

            // WHEN
            // THEN
            Assert.DoesNotThrow(() => _cellView.SetCellColor(Color.red));
            yield return null;
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SetCellColor_AfterGameObjectDestroyed_DoesNotThrow()
        {
            // GIVEN
            CellView destroyedView = CreateDestroyedCellView();
            yield return null;

            // WHEN / THEN
            Assert.DoesNotThrow(() => destroyedView.SetCellColor(Color.blue));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SetHighlightState_AfterGameObjectDestroyed_DoesNotThrow()
        {
            // GIVEN
            CellView destroyedView = CreateDestroyedCellView();
            yield return null;

            // WHEN / THEN
            Assert.DoesNotThrow(() => destroyedView.SetHighlightState(true));
        }

        // Returns a view whose GameObject — and therefore its cached renderer — is pending destruction.
        // The caller must yield one frame before using it so the destruction actually lands.
        private static CellView CreateDestroyedCellView()
        {
            var gameObject = new GameObject("CellView_Destroyed");
            gameObject.AddComponent<SpriteRenderer>();
            CellView view = gameObject.AddComponent<CellView>();
            view.InitializeCell(new HexCoordinates(0, 0));

            Object.Destroy(gameObject);

            return view;
        }
    }
}
