using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Interfaces;
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
    public class UnitPresenterTests
    {
        private const int BoardRadius = 4;
        private const int ActingPlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int ActingUnitId = 1;
        private const int RivalUnitId = 2;
        private const int UnknownUnitId = 99;
        private const int FirstSpawnedUnitId = 100;
        private const string SourceCardIdValue = "acid_crawler";

        private const MoveType UndefinedMoveType = (MoveType)3;

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _adjacentCoords = new(1, 0);
        private static readonly HexCoordinates _secondAdjacentCoords = new(1, -1);
        private static readonly HexCoordinates _distantCoords = new(2, 0);
        private static readonly HexCoordinates _cloneJumpCoords = new(3, 0);
        private static readonly HexCoordinates _outsideGridCoords = new(9, 0);

        private readonly List<HexCoordinates> _publishedCoordinates = new();

        private GameObject _boardGO;
        private GameObject _detachedPresenterGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private FakeUnitSpawner _spawner;
        private FakeMoveCapability _capability;
        private MoveCommand _publishedCommand;
        private IReadOnlyList<HexCoordinates> _publishedPayload;
        private int _publishedEventCount;

        [SetUp]
        public void SetUp()
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);

            _boardGO = new GameObject("UnitPresenter_Test");
            _boardGO.SetActive(false);
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();

            _gridPresenter.SetGridLayout(_gridLayout);

            _spawner = new FakeUnitSpawner();
            _capability = new FakeMoveCapability(canClone: true, canJump: true);

            _publishedEventCount = 0;
            _publishedPayload = null;
            _publishedCoordinates.Clear();

            MatchEvents.MoveExecuted += HandleMoveExecuted;
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.MoveExecuted -= HandleMoveExecuted;
            MatchEvents.ResetEvents();

            if (_boardGO != null)
            {
                Object.Destroy(_boardGO);
            }

            if (_detachedPresenterGO != null)
            {
                Object.Destroy(_detachedPresenterGO);
            }

            if (_gridLayout != null)
            {
                Object.Destroy(_gridLayout);
            }
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_LegalClone_ReturnsSuccess()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_LegalClone_PublishesTheCommandAndTargetCoordinate()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(_publishedEventCount, Is.EqualTo(1));
            Assert.That(_publishedCommand.Type, Is.EqualTo(MoveType.Clone));
            Assert.That(_publishedCommand.Source, Is.EqualTo(_origin));
            Assert.That(_publishedCommand.Target, Is.EqualTo(_adjacentCoords));
            Assert.That(_publishedCoordinates, Has.Count.EqualTo(1));
            Assert.That(_publishedCoordinates[0], Is.EqualTo(_adjacentCoords));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_LegalJump_ReturnsSuccessAndPublishesSourceThenTarget()
        {
            // GIVEN
            yield return ActivateBoard();

            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success));
            Assert.That(_publishedCoordinates, Has.Count.EqualTo(2));
            Assert.That(_publishedCoordinates[0], Is.EqualTo(_origin));
            Assert.That(_publishedCoordinates[1], Is.EqualTo(_distantCoords));
            Assert.That(unit.Position, Is.EqualTo(_distantCoords));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_PublishedPayload_CannotBeDowncastToAMutableList()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(_publishedPayload as List<HexCoordinates>, Is.Null, "A subscriber must not be able to mutate the presenter's live buffer.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_CloneOntoOccupiedTarget_ReturnsTargetOccupiedAndPublishesNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            RegisterUnitAt(RivalUnitId, RivalPlayerId, _adjacentCoords);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.TargetOccupied));
            Assert.That(_publishedEventCount, Is.EqualTo(0));
            Assert.That(GetCell(_adjacentCoords).OccupantUnitId, Is.EqualTo(RivalUnitId));
            Assert.That(_spawner.SpawnCallCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_JumpIssuedByWrongPlayer_ReturnsSourceNotOwnedAndPublishesNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, RivalPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceNotOwned));
            Assert.That(_publishedEventCount, Is.EqualTo(0));
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(ActingUnitId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_FrozenUnit_ReturnsSourceFrozenAndPublishesNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            unit.AddStatus(StatusType.Frozen, 1);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SourceFrozen));
            Assert.That(_publishedEventCount, Is.EqualTo(0));
            Assert.That(GetCell(_adjacentCoords).OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_UnregisteredUnit_ReturnsUnitNotFoundAndPublishesNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, UnknownUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.UnitNotFound));
            Assert.That(_publishedEventCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_CloneWithFailingSpawner_ReturnsSpawnFailedAndPublishesNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            _spawner.ReturnsNull = true;
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitSpawnFailedFormat, ActingPlayerId, _adjacentCoords));

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SpawnFailed));
            Assert.That(_publishedEventCount, Is.EqualTo(0));
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_TwoConsecutiveSpawnFailures_LogsTheErrorOnce()
        {
            // GIVEN
            yield return ActivateBoard();

            _spawner.ReturnsNull = true;
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var firstCommand = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);
            var secondCommand = new MoveCommand(MoveType.Clone, _origin, _secondAdjacentCoords, ActingPlayerId, ActingUnitId);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitSpawnFailedFormat, ActingPlayerId, _adjacentCoords));

            // WHEN
            _unitPresenter.ResolveMove(firstCommand);
            _unitPresenter.ResolveMove(secondCommand);

            // THEN
            Assert.That(_spawner.SpawnCallCount, Is.EqualTo(2), "Both Clones must reach the spawner; only the logging is latched.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_SpawnFailuresSeparatedByASuccessfulJump_StillLogsTheErrorOnce()
        {
            // GIVEN
            yield return ActivateBoard();

            _spawner.ReturnsNull = true;
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitSpawnFailedFormat, ActingPlayerId, _adjacentCoords));

            // WHEN
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId));
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId));
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Clone, _distantCoords, _cloneJumpCoords, ActingPlayerId, ActingUnitId));

            // THEN
            Assert.That(_spawner.SpawnCallCount, Is.EqualTo(2), "A successful Jump between two failed Clones must not re-arm the latch.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SetUnitSpawner_AfterASpawnFailure_ReArmsTheErrorLog()
        {
            // GIVEN
            yield return ActivateBoard();

            _spawner.ReturnsNull = true;
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitSpawnFailedFormat, ActingPlayerId, _adjacentCoords));
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitSpawnFailedFormat, ActingPlayerId, _adjacentCoords));
            _unitPresenter.ResolveMove(command);

            // WHEN
            _unitPresenter.SetUnitSpawner(_spawner);
            _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(_spawner.SpawnCallCount, Is.EqualTo(2), "Replacing the spawner must re-arm the failure log.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_CloneWithThrowingSpawner_ReturnsSpawnFailedAndLeavesBoardUnchanged()
        {
            // GIVEN
            yield return ActivateBoard();

            _spawner.ThrowsOnSpawn = true;
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitSpawnFailedFormat, ActingPlayerId, _adjacentCoords));
            LogAssert.Expect(LogType.Exception, new Regex("Fake spawner failure"));

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SpawnFailed));
            Assert.That(_publishedEventCount, Is.EqualTo(0));
            Assert.That(GetCell(_adjacentCoords).OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_CloneWithoutSpawner_ReturnsSpawnFailedAndLeavesBoardUnchanged()
        {
            // GIVEN
            yield return ActivateBoard();

            _unitPresenter.SetUnitSpawner(null);
            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitSpawnFailedFormat, ActingPlayerId, _adjacentCoords));

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.SpawnFailed));
            Assert.That(_publishedEventCount, Is.EqualTo(0));
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(ActingUnitId));
            Assert.That(GetCell(_adjacentCoords).OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_UndefinedMoveType_ReturnsInvalidCommandAndPublishesNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(UndefinedMoveType, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.InvalidCommand));
            Assert.That(_publishedEventCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_ReentrantCallFromSubscriber_ReturnsResolverBusyAndStillNotifiesLaterSubscribers()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var outerCommand = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            var reentrantCommand = new MoveCommand(MoveType.Clone, _distantCoords, _cloneJumpCoords, ActingPlayerId, ActingUnitId);
            MovementResult reentrantResult = MovementResult.Success;
            int lateSubscriberCallCount = 0;

            void handleReentrantMove(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates) =>
                reentrantResult = _unitPresenter.ResolveMove(reentrantCommand);

            void handleLateMove(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates) => lateSubscriberCallCount++;

            MatchEvents.MoveExecuted += handleReentrantMove;
            MatchEvents.MoveExecuted += handleLateMove;
            LogAssert.Expect(LogType.Error, BoardLogMessages.MoveResolveReentered);
            MovementResult outerResult;

            // WHEN
            try
            {
                outerResult = _unitPresenter.ResolveMove(outerCommand);
            }
            finally
            {
                MatchEvents.MoveExecuted -= handleReentrantMove;
                MatchEvents.MoveExecuted -= handleLateMove;
            }

            // THEN
            Assert.That(outerResult, Is.EqualTo(MovementResult.Success));
            Assert.That(reentrantResult, Is.EqualTo(MovementResult.ResolverBusy));
            Assert.That(lateSubscriberCallCount, Is.EqualTo(1), "A rejected re-entrant call must not break event dispatch for later subscribers.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_SubscriberThrows_StillReportsSuccess()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            static void handleThrowingMove(MoveCommand executed, IReadOnlyList<HexCoordinates> affectedCoordinates) =>
                throw new InvalidOperationException("Faulty subscriber.");

            MatchEvents.MoveExecuted += handleThrowingMove;
            LogAssert.Expect(LogType.Error, BoardLogMessages.MoveExecutedSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex("Faulty subscriber"));
            MovementResult result;

            // WHEN
            try
            {
                result = _unitPresenter.ResolveMove(command);
            }
            finally
            {
                MatchEvents.MoveExecuted -= handleThrowingMove;
            }

            // THEN
            Assert.That(
                result,
                Is.EqualTo(MovementResult.Success),
                "The board was already mutated, so the caller must not read a subscriber fault as a rejection."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_SubscriberThrows_LeavesTheBoardMutated()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var command = new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId);

            static void handleThrowingMove(MoveCommand executed, IReadOnlyList<HexCoordinates> affectedCoordinates) =>
                throw new InvalidOperationException("Faulty subscriber.");

            MatchEvents.MoveExecuted += handleThrowingMove;
            LogAssert.Expect(LogType.Error, BoardLogMessages.MoveExecutedSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex("Faulty subscriber"));

            // WHEN
            try
            {
                _unitPresenter.ResolveMove(command);
            }
            finally
            {
                MatchEvents.MoveExecuted -= handleThrowingMove;
            }

            // THEN
            Assert.That(GetCell(_distantCoords).OccupantUnitId, Is.EqualTo(ActingUnitId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_AfterASubscriberFault_StillAcceptsTheNextMove()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);

            static void handleThrowingMove(MoveCommand executed, IReadOnlyList<HexCoordinates> affectedCoordinates) =>
                throw new InvalidOperationException("Faulty subscriber.");

            MatchEvents.MoveExecuted += handleThrowingMove;
            LogAssert.Expect(LogType.Error, BoardLogMessages.MoveExecutedSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex("Faulty subscriber"));

            try
            {
                _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId));
            }
            finally
            {
                MatchEvents.MoveExecuted -= handleThrowingMove;
            }

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _distantCoords, _origin, ActingPlayerId, ActingUnitId));

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success), "The re-entrancy guard must be released even when dispatch fails.");
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_WithoutGridPresenter_ReturnsBoardUnavailableAndLogsMissingGrid()
        {
            // GIVEN
            UnitPresenter detachedPresenter = CreateDetachedPresenter();
            yield return null;

            var command = new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId);
            LogAssert.Expect(LogType.Error, BoardLogMessages.GridPresenterMissing);

            // WHEN
            MovementResult result = detachedPresenter.ResolveMove(command);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.BoardUnavailable));
            Assert.That(_publishedEventCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator RegisterUnit_WithInitializedGrid_ReturnsTrueAndMarksUnitCellOccupied()
        {
            // GIVEN
            yield return ActivateBoard();

            var unit = new GridUnit(ActingUnitId, ActingPlayerId, new CardId(SourceCardIdValue), _origin);

            // WHEN
            bool wasRegistered = _unitPresenter.RegisterUnit(unit, _capability);

            // THEN
            Assert.That(wasRegistered, Is.True);
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(1));
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(ActingUnitId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator RegisterUnit_WithoutGridPresenter_ReturnsFalseAndLeavesRegistryEmpty()
        {
            // GIVEN
            UnitPresenter detachedPresenter = CreateDetachedPresenter();
            yield return null;

            var unit = new GridUnit(ActingUnitId, ActingPlayerId, new CardId(SourceCardIdValue), _origin);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitRegistrationFailedFormat, ActingUnitId, _origin));

            // WHEN
            bool wasRegistered = detachedPresenter.RegisterUnit(unit, _capability);

            // THEN
            Assert.That(wasRegistered, Is.False);
            Assert.That(detachedPresenter.ActiveUnits.Count, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator RegisterUnit_PositionOutsideGrid_ReturnsFalseAndLeavesRegistryEmpty()
        {
            // GIVEN
            yield return ActivateBoard();

            var unit = new GridUnit(ActingUnitId, ActingPlayerId, new CardId(SourceCardIdValue), _outsideGridCoords);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitRegistrationFailedFormat, ActingUnitId, _outsideGridCoords));

            // WHEN
            bool wasRegistered = _unitPresenter.RegisterUnit(unit, _capability);

            // THEN
            Assert.That(wasRegistered, Is.False);
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator RegisterUnit_CellHeldByAnotherUnit_ReturnsFalseAndLeavesTheIncumbentInPlace()
        {
            // GIVEN
            yield return ActivateBoard();

            GridUnit incumbentUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var intruderUnit = new GridUnit(RivalUnitId, RivalPlayerId, new CardId(SourceCardIdValue), _origin);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitRegistrationCellOccupiedFormat, RivalUnitId, _origin, ActingUnitId));

            // WHEN
            bool wasRegistered = _unitPresenter.RegisterUnit(intruderUnit, _capability);

            // THEN
            Assert.That(wasRegistered, Is.False);
            Assert.That(_unitPresenter.ActiveUnits[ActingUnitId], Is.SameAs(incumbentUnit));
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(ActingUnitId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator RegisterUnit_SameIdAtNewPosition_FreesThePreviouslyHeldCell()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            var relocatedUnit = new GridUnit(ActingUnitId, ActingPlayerId, new CardId(SourceCardIdValue), _adjacentCoords);

            // WHEN
            bool wasRegistered = _unitPresenter.RegisterUnit(relocatedUnit, _capability);

            // THEN
            Assert.That(wasRegistered, Is.True);
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
            Assert.That(GetCell(_adjacentCoords).OccupantUnitId, Is.EqualTo(ActingUnitId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator RegisterUnit_SameInstanceAfterItsPositionWasMutated_FreesThePreviouslyRegisteredCell()
        {
            // GIVEN
            yield return ActivateBoard();

            GridUnit unit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);

            unit.Position = _adjacentCoords;

            // WHEN
            bool wasRegistered = _unitPresenter.RegisterUnit(unit, _capability);

            // THEN
            Assert.That(wasRegistered, Is.True);
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(HexCell.NoOccupant), "The cell the unit was registered on must be released.");
            Assert.That(GetCell(_adjacentCoords).OccupantUnitId, Is.EqualTo(ActingUnitId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator UnregisterUnit_UnitPositionDriftedOntoAnotherCell_FreesOnlyTheRegisteredCell()
        {
            // GIVEN
            yield return ActivateBoard();

            GridUnit strayUnit = RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            RegisterUnitAt(RivalUnitId, RivalPlayerId, _adjacentCoords);
            strayUnit.Position = _adjacentCoords;

            // WHEN
            bool wasRemoved = _unitPresenter.UnregisterUnit(ActingUnitId);

            // THEN
            Assert.That(wasRemoved, Is.True);
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
            Assert.That(GetCell(_adjacentCoords).OccupantUnitId, Is.EqualTo(RivalUnitId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator UnregisterUnit_RegisteredUnit_FreesCellAndReturnsTrue()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);

            // WHEN
            bool wasRemoved = _unitPresenter.UnregisterUnit(ActingUnitId);

            // THEN
            Assert.That(wasRemoved, Is.True);
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(0));
            Assert.That(GetCell(_origin).OccupantUnitId, Is.EqualTo(HexCell.NoOccupant));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator UnregisterUnit_AfterAJump_FreesTheCellTheUnitActuallyLandedOn()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Jump, _origin, _distantCoords, ActingPlayerId, ActingUnitId));

            // WHEN
            bool wasRemoved = _unitPresenter.UnregisterUnit(ActingUnitId);

            // THEN
            Assert.That(wasRemoved, Is.True);
            Assert.That(
                GetCell(_distantCoords).OccupantUnitId,
                Is.EqualTo(HexCell.NoOccupant),
                "A Jump must update the position the registry releases on removal."
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator UnregisterUnit_WithoutGridPresenter_ReturnsFalseAndKeepsTheUnitRegistered()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);

            // Immediate destruction is the scenario, not cleanup: the board has to be gone before the act step.
            Object.DestroyImmediate(_gridPresenter);
            LogAssert.Expect(LogType.Error, string.Format(BoardLogMessages.UnitUnregistrationFailedFormat, ActingUnitId));

            // WHEN
            bool wasRemoved = _unitPresenter.UnregisterUnit(ActingUnitId);

            // THEN
            Assert.That(wasRemoved, Is.False, "Dropping the unit while its cell cannot be released would strand that cell as occupied.");
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator UnregisterUnit_UnknownUnitId_ReturnsFalse()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);

            // WHEN
            bool wasRemoved = _unitPresenter.UnregisterUnit(UnknownUnitId);

            // THEN
            Assert.That(wasRemoved, Is.False);
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ResolveMove_ClonedUnit_IsRegisteredAndCanMoveItself()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId));
            var cloneCommand = new MoveCommand(MoveType.Jump, _adjacentCoords, _cloneJumpCoords, ActingPlayerId, FirstSpawnedUnitId);

            // WHEN
            MovementResult result = _unitPresenter.ResolveMove(cloneCommand);

            // THEN
            Assert.That(result, Is.EqualTo(MovementResult.Success), "The clone must inherit the source unit's movement capability.");
            Assert.That(GetCell(_cloneJumpCoords).OccupantUnitId, Is.EqualTo(FirstSpawnedUnitId));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator UnregisterUnit_ClonedUnit_FreesTheCellTheCloneOccupies()
        {
            // GIVEN
            yield return ActivateBoard();

            RegisterUnitAt(ActingUnitId, ActingPlayerId, _origin);
            _unitPresenter.ResolveMove(new MoveCommand(MoveType.Clone, _origin, _adjacentCoords, ActingPlayerId, ActingUnitId));

            // WHEN
            bool wasRemoved = _unitPresenter.UnregisterUnit(FirstSpawnedUnitId);

            // THEN
            Assert.That(wasRemoved, Is.True);
            Assert.That(
                GetCell(_adjacentCoords).OccupantUnitId,
                Is.EqualTo(HexCell.NoOccupant),
                "A Clone must record its new unit's position in the registry."
            );
        }

        [Test]
        public void RegisterUnit_WithNullUnit_ReturnsFalse()
        {
            // GIVEN
            // no unit instance to register

            // WHEN
            bool registered = _unitPresenter.RegisterUnit(null, _capability);

            // THEN
            Assert.That(registered, Is.False);
        }

        private IEnumerator ActivateBoard()
        {
            _boardGO.SetActive(true);
            yield return null;

            _unitPresenter.SetUnitSpawner(_spawner);
        }

        private UnitPresenter CreateDetachedPresenter()
        {
            _detachedPresenterGO = new GameObject("DetachedUnitPresenter_Test");

            return _detachedPresenterGO.AddComponent<UnitPresenter>();
        }

        private void HandleMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates)
        {
            _publishedEventCount++;
            _publishedCommand = command;
            _publishedPayload = affectedCoordinates;

            _publishedCoordinates.Clear();

            for (int i = 0; i < affectedCoordinates.Count; i++)
            {
                _publishedCoordinates.Add(affectedCoordinates[i]);
            }
        }

        private GridUnit RegisterUnitAt(int unitId, int playerId, HexCoordinates position)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(SourceCardIdValue), position);
            Assert.That(_unitPresenter.RegisterUnit(unit, _capability), Is.True, $"Test setup expects unit {unitId} to register at {position}.");

            return unit;
        }

        private HexCell GetCell(HexCoordinates coordinates)
        {
            HexGrid grid = _gridPresenter.HexGrid;

            Assert.That(grid, Is.Not.Null, "Test setup expects the grid presenter to have initialized its hex grid.");
            Assert.That(grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test expects {coordinates} to exist on the grid.");

            return cell;
        }

        private sealed class FakeMoveCapability : IMoveCapable
        {
            public FakeMoveCapability(bool canClone, bool canJump)
            {
                CanClone = canClone;
                CanJump = canJump;
            }

            public bool CanClone { get; }

            public bool CanJump { get; }
        }

        private sealed class FakeUnitSpawner : IUnitSpawner
        {
            private int _nextUnitId = FirstSpawnedUnitId;

            public int SpawnCallCount { get; private set; }

            public bool ReturnsNull { get; set; }

            public bool ThrowsOnSpawn { get; set; }

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                SpawnCallCount++;

                if (ThrowsOnSpawn)
                {
                    throw new InvalidOperationException("Fake spawner failure.");
                }

                return ReturnsNull ? null : new GridUnit(_nextUnitId++, playerId, cardId, at);
            }
        }
    }
}
