using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Input.Presenters;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Input
{
    [TestFixture]
    public class TargetHighlightPresenterTests
    {
        private static readonly HexCoordinates _sharedHex = new(0, 0);
        private static readonly HexCoordinates _droppedHex = new(1, 0);
        private static readonly HexCoordinates _addedHex = new(-1, 0);
        private static readonly HexCoordinates _neverTargetedHex = new(2, 0);

        private readonly List<Object> _spawned = new();

        private GameObject _gridViewGO;
        private GridView _gridView;
        private TargetHighlightPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            var prefabGO = new GameObject("CellPrefab_Test");
            prefabGO.AddComponent<SpriteRenderer>();
            CellView cellPrefab = prefabGO.AddComponent<CellView>();
            _spawned.Add(prefabGO);

            _gridViewGO = new GameObject("TargetHighlightPresenter_Board_Test");
            _gridViewGO.SetActive(false);
            _gridView = _gridViewGO.AddComponent<GridView>();
            _gridView.SetViewConfiguration(cellPrefab, 1f);

            _presenter = _gridViewGO.AddComponent<TargetHighlightPresenter>();
            _presenter.Construct(_gridView);

            _gridViewGO.SetActive(true);
            _spawned.Add(_gridViewGO);
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();

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
        public void SetTargets_OverlappingSecondPass_LeavesTheSharedHexHighlightedAndFlipsOnlyTheHexesThatDiffer()
        {
            // GIVEN — the first pass highlights the shared hex and the hex the second pass will drop.
            _presenter.SetTargets(new List<HexCoordinates> { _sharedHex, _droppedHex });

            // WHEN — the second pass keeps the shared hex, drops one and adds another.
            _presenter.SetTargets(new List<HexCoordinates> { _sharedHex, _addedHex });

            // THEN
            Assert.That(
                (
                    _presenter.IsHighlighted(_sharedHex),
                    _presenter.IsHighlighted(_droppedHex),
                    _presenter.IsHighlighted(_addedHex),
                    _presenter.IsHighlighted(_neverTargetedHex)
                ),
                Is.EqualTo((true, false, true, false))
            );
        }

        [Test]
        public void SetTargets_OverlappingSecondPass_HighlightedCountReflectsOnlyTheCurrentPass()
        {
            // GIVEN
            _presenter.SetTargets(new List<HexCoordinates> { _sharedHex, _droppedHex });

            // WHEN
            _presenter.SetTargets(new List<HexCoordinates> { _sharedHex, _addedHex });

            // THEN
            Assert.That(_presenter.HighlightedCount, Is.EqualTo(2));
        }

        [Test]
        public void HandleMatchStarted_ARematchIsAnnounced_ClearsEveryHighlight()
        {
            // GIVEN
            _presenter.SetTargets(new List<HexCoordinates> { _sharedHex, _droppedHex });

            // WHEN
            MatchEvents.RaiseMatchStarted(new MatchConfiguration(0));

            // THEN
            Assert.That(_presenter.HighlightedCount, Is.EqualTo(0));
        }
    }
}
