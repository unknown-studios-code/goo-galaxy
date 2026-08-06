using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Board
{
    [TestFixture]
    public class UnitViewTests
    {
        private const int BoardRadius = 4;
        private const int PlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int UnitId = 1;
        private const int KnownUnitId = 2;
        private const int UnknownUnitId = 99;
        private const float CellVisualSize = 1.0f;
        private const float PositionTolerance = 0.0001f;
        private const string SourceCardIdValue = "acid_crawler";

        private static readonly Color _playerOneColor = new(0f, 1f, 1f, 1f);
        private static readonly Color _playerTwoColor = new(1f, 0f, 1f, 1f);

        private static readonly HexCoordinates _firstCoords = new(0, 0);
        private static readonly HexCoordinates _secondCoords = new(1, 0);

        private readonly List<int> _convertedUnitIds = new();
        private readonly List<int> _armorStrippedUnitIds = new();

        private GameObject _boardGO;
        private GameObject _detachedGO;
        private GameObject _unitPrefabGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private UnitView _unitView;
        private FakeMoveCapability _capability;

        [SetUp]
        public void SetUp()
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);

            _unitPrefabGO = new GameObject("UnitPrefab_Test");
            _unitPrefabGO.AddComponent<SpriteRenderer>();
            _unitPrefabGO.SetActive(false);

            _boardGO = new GameObject("UnitView_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _unitView = _boardGO.AddComponent<UnitView>();

            _gridPresenter.SetGridLayout(_gridLayout);
            _unitView.SetViewConfiguration(_unitPrefabGO, null, null, null, CellVisualSize);
            _unitView.SetFactionColors(_playerOneColor, _playerTwoColor);

            _convertedUnitIds.Clear();
            _armorStrippedUnitIds.Clear();

            _capability = new FakeMoveCapability();
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();

            if (_boardGO != null)
            {
                Object.Destroy(_boardGO);
            }

            if (_detachedGO != null)
            {
                Object.Destroy(_detachedGO);
            }

            if (_unitPrefabGO != null)
            {
                Object.Destroy(_unitPrefabGO);
            }

            if (_gridLayout != null)
            {
                Object.Destroy(_gridLayout);
            }
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_UnitLandsOnTarget_PlacesAVisualAtTheTargetHex()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(UnitId, PlayerId, _firstCoords);

            // WHEN
            RaiseMoveExecuted(_firstCoords);

            // THEN
            Assert.That(_unitView.TryGetUnitVisual(UnitId, out GameObject visual), Is.True);
            Assert.That(Vector3.Distance(visual.transform.position, ExpectedWorldPosition(_firstCoords)), Is.EqualTo(0f).Within(PositionTolerance));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_UnitLandsOnTarget_TintsTheVisualWithItsOwnerColor()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(UnitId, PlayerId, _firstCoords);

            // WHEN
            RaiseMoveExecuted(_firstCoords);

            // THEN
            Assert.That(_unitView.TryGetUnitColor(UnitId, out Color color), Is.True);
            Assert.That(color, Is.EqualTo(_playerOneColor));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_SecondMoveForSameUnitId_RepositionsTheExistingInstanceInsteadOfCreatingASecondOne()
        {
            // GIVEN
            yield return ActivateBoard();

            GridUnit unit = RegisterUnitAt(UnitId, PlayerId, _firstCoords);
            RaiseMoveExecuted(_firstCoords);

            unit.Position = _secondCoords;
            Assert.That(_unitPresenter.RegisterUnit(unit, _capability), Is.True, "Test setup expects the unit to re-register at its new position.");

            // WHEN
            RaiseMoveExecuted(_secondCoords);

            // THEN
            Assert.That(_unitView.RenderedUnitCount, Is.EqualTo(1), "A Jump must reposition the existing visual, not render a second one.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleConversionResolved_ConvertedUnit_RetintsItToItsNewOwnerColor()
        {
            // GIVEN
            yield return ActivateBoard();

            GridUnit rival = RegisterUnitAt(KnownUnitId, RivalPlayerId, _secondCoords);
            RaiseMoveExecuted(_secondCoords);
            rival.PlayerId = PlayerId;

            // WHEN
            RaiseConversionResolved(PlayerId, KnownUnitId);

            // THEN
            Assert.That(_unitView.TryGetUnitColor(KnownUnitId, out Color color), Is.True);
            Assert.That(color, Is.EqualTo(_playerOneColor));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleConversionResolved_ConvertedUnitNeverRendered_CreatesItsVisual()
        {
            // GIVEN
            yield return ActivateBoard();

            GridUnit rival = RegisterUnitAt(KnownUnitId, RivalPlayerId, _secondCoords);
            rival.PlayerId = PlayerId;

            // WHEN
            RaiseConversionResolved(PlayerId, KnownUnitId);

            // THEN
            Assert.That(
                _unitView.TryGetUnitVisual(KnownUnitId, out GameObject _),
                Is.True,
                "A unit converted before it was ever rendered must still get a visual."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleConversionResolved_ArmorStrippedUnit_KeepsItsOriginalOwnerColor()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(KnownUnitId, RivalPlayerId, _secondCoords);
            RaiseMoveExecuted(_secondCoords);

            // WHEN
            RaiseArmorStripped(PlayerId, KnownUnitId);

            // THEN
            Assert.That(_unitView.TryGetUnitColor(KnownUnitId, out Color color), Is.True);
            Assert.That(color, Is.EqualTo(_playerTwoColor), "Stripping armor does not change ownership, so the tint must not change either.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ReleaseUnitVisual_UnknownUnitId_IsANoOp()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(KnownUnitId, PlayerId, _firstCoords);
            RaiseMoveExecuted(_firstCoords);

            // WHEN
            _unitView.ReleaseUnitVisual(UnknownUnitId);

            // THEN
            Assert.That(_unitView.RenderedUnitCount, Is.EqualTo(1), "Releasing an id with no visual must leave the known unit's visual untouched.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SyncUnitVisuals_RegisteredUnits_RendersOneVisualEach()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(UnitId, PlayerId, _firstCoords);
            RegisterUnitAt(KnownUnitId, RivalPlayerId, _secondCoords);

            // WHEN
            _unitView.SyncUnitVisuals();

            // THEN
            Assert.That(_unitView.RenderedUnitCount, Is.EqualTo(2));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SyncUnitVisuals_UnitRemovedFromRegistry_ReleasesItsVisual()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(UnitId, PlayerId, _firstCoords);
            RegisterUnitAt(KnownUnitId, RivalPlayerId, _secondCoords);
            _unitView.SyncUnitVisuals();
            Assert.That(_unitPresenter.UnregisterUnit(KnownUnitId), Is.True, "Test setup expects the rival unit to unregister.");

            // WHEN
            _unitView.SyncUnitVisuals();

            // THEN
            Assert.That(_unitView.TryGetUnitVisual(KnownUnitId, out GameObject _), Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SyncUnitVisuals_NoUnitPresenter_LogsUnitViewBoardUnavailable()
        {
            // GIVEN
            UnitView detachedView = CreateDetachedUnitView();
            yield return null;

            LogAssert.Expect(LogType.Error, BoardLogMessages.UnitViewBoardUnavailable);

            // WHEN
            detachedView.SyncUnitVisuals();

            // THEN
            Assert.That(detachedView.RenderedUnitCount, Is.EqualTo(0));
        }

        private static Vector3 ExpectedWorldPosition(HexCoordinates coordinates)
        {
            return HexMathUtils.ProjectToWorldSpace(coordinates, CellVisualSize);
        }

        private IEnumerator ActivateBoard()
        {
            _boardGO.SetActive(true);
            yield return null;
        }

        private UnitView CreateDetachedUnitView()
        {
            _detachedGO = new GameObject("DetachedUnitView_Test");
            _detachedGO.SetActive(false);
            UnitView view = _detachedGO.AddComponent<UnitView>();
            view.SetViewConfiguration(_unitPrefabGO, null, null, null, CellVisualSize);
            _detachedGO.SetActive(true);

            return view;
        }

        private void RaiseMoveExecuted(HexCoordinates target)
        {
            var command = new MoveCommand(MoveType.Clone, target, target, PlayerId, UnitId);
            MatchEvents.RaiseMoveExecuted(command, new List<HexCoordinates> { target });
        }

        private void RaiseConversionResolved(int actingPlayerId, int convertedUnitId)
        {
            _convertedUnitIds.Clear();
            _convertedUnitIds.Add(convertedUnitId);

            MatchEvents.RaiseConversionResolved(actingPlayerId, new ConversionResult(_convertedUnitIds, _armorStrippedUnitIds));
        }

        private void RaiseArmorStripped(int actingPlayerId, int armorStrippedUnitId)
        {
            _armorStrippedUnitIds.Clear();
            _armorStrippedUnitIds.Add(armorStrippedUnitId);

            MatchEvents.RaiseConversionResolved(actingPlayerId, new ConversionResult(_convertedUnitIds, _armorStrippedUnitIds));
        }

        private GridUnit RegisterUnitAt(int unitId, int playerId, HexCoordinates position)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(SourceCardIdValue), position);
            Assert.That(_unitPresenter.RegisterUnit(unit, _capability), Is.True, $"Test setup expects unit {unitId} to register at {position}.");

            return unit;
        }

        private sealed class FakeMoveCapability : IMoveCapable
        {
            public bool CanClone => true;

            public bool CanJump => true;
        }
    }
}
