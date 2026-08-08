using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
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
    public class ConversionControllerTests
    {
        private const int BoardRadius = 4;
        private const int ActingPlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int ActingUnitId = 1;
        private const int SecondActingUnitId = 2;
        private const int EnemyUnitId = 10;
        private const int ArmoredEnemyUnitId = 11;
        private const int SecondEnemyUnitId = 12;
        private const string SourceCardIdValue = "acid_crawler";
        private const string ArmoredCardIdValue = "bio_phalanx";

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _adjacentCoords = new(1, 0);
        private static readonly HexCoordinates _distantCoords = new(3, 0);
        private static readonly HexCoordinates _secondAdjacentCoords = new(4, 0);

        private readonly List<int> _capturedConvertedIds = new();
        private readonly List<int> _capturedArmorStrippedIds = new();

        private GameObject _boardGO;
        private GameObject _detachedGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private FakeMoveCapability _capability;
        private int _capturedActingPlayerId;
        private int _conversionResolvedCallCount;

        [SetUp]
        public void SetUp()
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);

            _boardGO = new GameObject("ConversionController_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _boardGO.AddComponent<ConversionController>();

            _gridPresenter.SetGridLayout(_gridLayout);

            _capability = new FakeMoveCapability();

            _capturedConvertedIds.Clear();
            _capturedArmorStrippedIds.Clear();
            _capturedActingPlayerId = 0;
            _conversionResolvedCallCount = 0;

            MatchEvents.ConversionResolved += HandleConversionResolved;
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ConversionResolved -= HandleConversionResolved;
            MatchEvents.ResetEvents();

            if (_boardGO != null)
            {
                Object.Destroy(_boardGO);
            }

            if (_detachedGO != null)
            {
                Object.Destroy(_detachedGO);
            }

            if (_gridLayout != null)
            {
                Object.Destroy(_gridLayout);
            }
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_LandingAdjacentToEnemy_RaisesConversionResolvedWithActingPlayerIdAndConvertedUnitId()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = RegisterUnitAt(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            var command = new MoveCommand(MoveType.Clone, _distantCoords, _origin, ActingPlayerId, ActingUnitId);

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, new List<HexCoordinates> { _origin });

            // THEN
            Assert.That(_capturedActingPlayerId, Is.EqualTo(ActingPlayerId));
            Assert.That(_capturedConvertedIds, Does.Contain(enemyUnit.UnitId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_LandingConvertsNothing_RaisesNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _distantCoords, _origin, ActingPlayerId, ActingUnitId);

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, new List<HexCoordinates> { _origin });

            // THEN
            Assert.That(_conversionResolvedCallCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_ArmoredEnemyAdjacent_RaisesConversionResolvedWithArmorStrippedOnly()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            GridUnit armoredEnemy = RegisterArmoredUnitAt(ArmoredEnemyUnitId, RivalPlayerId, _adjacentCoords);
            var command = new MoveCommand(MoveType.Clone, _distantCoords, _origin, ActingPlayerId, ActingUnitId);

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, new List<HexCoordinates> { _origin });

            // THEN
            Assert.That(_capturedArmorStrippedIds, Does.Contain(armoredEnemy.UnitId));
            Assert.That(_capturedConvertedIds, Is.Empty);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_MissingBoardReferences_LogsErrorAndRaisesNothing()
        {
            // GIVEN
            CreateDetachedConversionController();
            yield return null;

            var command = new MoveCommand(MoveType.Clone, _distantCoords, _origin, ActingPlayerId, ActingUnitId);
            LogAssert.Expect(LogType.Error, BoardLogMessages.ConversionBoardUnavailable);

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, Array.Empty<HexCoordinates>());

            // THEN
            Assert.That(_conversionResolvedCallCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_ConversionResolvedSubscriberThrows_LogsFailureAndLeavesTheEnemyConverted()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = RegisterUnitAt(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            var command = new MoveCommand(MoveType.Clone, _distantCoords, _origin, ActingPlayerId, ActingUnitId);

            static void handleThrowing(int actingPlayerId, ConversionResult result) => throw new InvalidOperationException("Faulty subscriber.");

            MatchEvents.ConversionResolved += handleThrowing;
            LogAssert.Expect(LogType.Error, BoardLogMessages.ConversionResolvedSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex("Faulty subscriber"));

            // WHEN
            try
            {
                MatchEvents.RaiseMoveExecuted(command, new List<HexCoordinates> { _origin });
            }
            finally
            {
                MatchEvents.ConversionResolved -= handleThrowing;
            }

            // THEN
            Assert.That(enemyUnit.PlayerId, Is.EqualTo(ActingPlayerId), "The conversion is applied to the model before the subscribers are dispatched.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_SubscriberRaisesMoveExecutedDuringDispatch_LogsReenteredAndOuterSubscriberKeepsOriginalIds()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            GridUnit enemyUnit = RegisterUnitAt(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            var outerCommand = new MoveCommand(MoveType.Clone, _distantCoords, _origin, ActingPlayerId, ActingUnitId);
            var reentrantCommand = new MoveCommand(MoveType.Clone, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            void handleReentrant(int actingPlayerId, ConversionResult result) =>
                MatchEvents.RaiseMoveExecuted(reentrantCommand, new List<HexCoordinates> { _distantCoords });

            MatchEvents.ConversionResolved += handleReentrant;
            LogAssert.Expect(LogType.Error, BoardLogMessages.ConversionResolveReentered);

            // WHEN
            try
            {
                MatchEvents.RaiseMoveExecuted(outerCommand, new List<HexCoordinates> { _origin });
            }
            finally
            {
                MatchEvents.ConversionResolved -= handleReentrant;
            }

            // THEN
            Assert.That(_capturedConvertedIds, Is.EqualTo(new[] { enemyUnit.UnitId }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator HandleMoveExecuted_ConversionResultRetainedPastDispatch_ReflectsTheNextLandingInstead()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            RegisterUnitAt(EnemyUnitId, RivalPlayerId, _adjacentCoords);
            RegisterUnitAt(SecondActingUnitId, ActingPlayerId, _distantCoords);
            GridUnit secondEnemy = RegisterUnitAt(SecondEnemyUnitId, RivalPlayerId, _secondAdjacentCoords);

            var firstCommand = new MoveCommand(MoveType.Clone, _adjacentCoords, _origin, ActingPlayerId, ActingUnitId);
            var secondCommand = new MoveCommand(MoveType.Clone, _secondAdjacentCoords, _distantCoords, ActingPlayerId, SecondActingUnitId);

            ConversionResult retainedResult = default;
            void handleRetaining(int actingPlayerId, ConversionResult result) => retainedResult = result;

            MatchEvents.ConversionResolved += handleRetaining;

            // WHEN
            try
            {
                MatchEvents.RaiseMoveExecuted(firstCommand, new List<HexCoordinates> { _origin });
                MatchEvents.RaiseMoveExecuted(secondCommand, new List<HexCoordinates> { _distantCoords });
            }
            finally
            {
                MatchEvents.ConversionResolved -= handleRetaining;
            }

            // THEN
            Assert.That(
                retainedResult.ConvertedUnitIds,
                Is.EqualTo(new[] { secondEnemy.UnitId }),
                "ConversionResult wraps the presenter's live buffers by design: retaining it past its own dispatch aliases whatever landing resolves next."
            );
        }

        private IEnumerator ActivateBoard()
        {
            _boardGO.SetActive(true);
            yield return null;
        }

        private void CreateDetachedConversionController()
        {
            _detachedGO = new GameObject("DetachedConversionController_Test");
            _detachedGO.AddComponent<ConversionController>();
        }

        private void HandleConversionResolved(int actingPlayerId, ConversionResult result)
        {
            _conversionResolvedCallCount++;
            _capturedActingPlayerId = actingPlayerId;

            _capturedConvertedIds.Clear();
            for (int i = 0; i < result.ConvertedUnitIds.Count; i++)
            {
                _capturedConvertedIds.Add(result.ConvertedUnitIds[i]);
            }

            _capturedArmorStrippedIds.Clear();
            for (int i = 0; i < result.ArmorStrippedUnitIds.Count; i++)
            {
                _capturedArmorStrippedIds.Add(result.ArmorStrippedUnitIds[i]);
            }
        }

        private GridUnit RegisterUnitAt(int unitId, int playerId, HexCoordinates position)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(SourceCardIdValue), position);
            Assert.That(_unitPresenter.RegisterUnit(unit, _capability), Is.True, $"Test setup expects unit {unitId} to register at {position}.");

            return unit;
        }

        private GridUnit RegisterArmoredUnitAt(int unitId, int playerId, HexCoordinates position)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(ArmoredCardIdValue), position, hasArmor: true);
            Assert.That(_unitPresenter.RegisterUnit(unit, _capability), Is.True, $"Test setup expects unit {unitId} to register at {position}.");

            return unit;
        }

        private sealed class FakeMoveCapability : IMoveCapable
        {
            public bool CanClone => true;

            public bool CanJump => true;

            public bool IgnoresHazards => false;

            public int CloneDistance => BoardMetrics.DefaultCloneDistance;

            public int JumpDistance => BoardMetrics.DefaultJumpDistance;
        }
    }
}
