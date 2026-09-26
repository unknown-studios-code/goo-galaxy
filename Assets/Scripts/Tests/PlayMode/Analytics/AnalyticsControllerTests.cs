using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GooGalaxy.Runtime.Analytics.Controllers;
using GooGalaxy.Runtime.Analytics.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Analytics
{
    [TestFixture]
    public class AnalyticsControllerTests
    {
        private const int LocalPlayerId = 1;
        private const int OpponentPlayerId = 2;
        private const int EventsToOverflowTheDefaultBuffer = 513;

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            MatchEvents.ResetEvents();
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
        public void Flush_FreshlyConstructed_WritesOnlySessionStart()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN
            controller.Flush();

            // THEN
            Assert.That(sink.WrittenRecords.Select(record => record.Type), Is.EqualTo(new[] { AnalyticsEventType.SessionStart }));
        }

        [Test]
        public void Construct_CalledASecondTime_DoesNotReopenTheSession()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN
            controller.Construct(sink);

            // THEN
            Assert.That(sink.OpenedSessions, Has.Count.EqualTo(1));
        }

        [Test]
        public void HandleMatchStarted_CalledTwice_IncrementsTheMatchOrdinalEachTime()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            MatchConfiguration configOne = BuildMatchConfiguration(1);
            MatchConfiguration configTwo = BuildMatchConfiguration(2);

            // WHEN
            MatchEvents.RaiseMatchStarted(configOne);
            MatchEvents.RaiseMatchStarted(configTwo);
            controller.Flush();

            // THEN
            IEnumerable<int> matchStartOrdinals = sink
                .WrittenRecords.Where(record => record.Type == AnalyticsEventType.MatchStart)
                .Select(record => record.MatchOrdinal);
            Assert.That(matchStartOrdinals, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void HandleMatchStarted_SecondMatch_ZeroesTheCachedScoresFromThePreviousMatch()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            MatchConfiguration config = BuildMatchConfiguration(1);
            MatchEvents.RaiseMatchStarted(config);
            MatchEvents.RaiseScoreChanged(LocalPlayerId, 5);
            MatchEvents.RaiseScoreChanged(OpponentPlayerId, 3);
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // WHEN — the second match starts and ends with no ScoreChanged of its own.
            MatchEvents.RaiseMatchStarted(config);
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // THEN
            AnalyticsRecord secondMatchEnd = sink.WrittenRecords.Where(record => record.Type == AnalyticsEventType.MatchEnd).Last();
            Assert.That((secondMatchEnd.SlotC, secondMatchEnd.SlotD), Is.EqualTo((0, 0)));
        }

        [Test]
        public void HandleMatchPhaseChanged_LoadingBeforeTheMatchIsAnnounced_TagsItWithTheUpcomingMatchOrdinal()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            controller.Flush();

            // THEN
            AnalyticsRecord phaseRecord = sink.WrittenRecords.Single(record => record.Type == AnalyticsEventType.PhaseChanged);
            Assert.That(phaseRecord.MatchOrdinal, Is.EqualTo(1));
        }

        [Test]
        public void MatchStarted_IsEnabledFalse_NothingReachesTheSink()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildDisabledController();

            // WHEN
            MatchEvents.RaiseMatchStarted(new MatchConfiguration(1));
            controller.Flush();

            // THEN
            Assert.That((sink.OpenedSessions.Count, sink.WrittenRecords.Count), Is.EqualTo((0, 0)));
        }

        [Test]
        public void HandleMoveExecuted_CloneRaised_CapturesRecordWithCorrectFields()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            var command = new MoveCommand(MoveType.Clone, new HexCoordinates(0, 0), new HexCoordinates(1, -1), LocalPlayerId, 5);
            var affectedCoordinates = new List<HexCoordinates> { command.Target };

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, affectedCoordinates);
            controller.Flush();

            // THEN
            AnalyticsRecord moveRecord = sink.WrittenRecords.Single(record => record.Type == AnalyticsEventType.MoveExecuted);
            Assert.That(
                (moveRecord.PlayerId, moveRecord.SlotA, moveRecord.SlotB, moveRecord.Hex, moveRecord.SourceHex),
                Is.EqualTo((LocalPlayerId, (int)MoveType.Clone, 5, command.Target, command.Source))
            );
        }

        [Test]
        public void HandleMoveExecuted_JumpRaised_CapturesRecordWithCorrectFields()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            var command = new MoveCommand(MoveType.Jump, new HexCoordinates(0, 0), new HexCoordinates(2, -1), LocalPlayerId, 7);
            var affectedCoordinates = new List<HexCoordinates> { command.Source, command.Target };

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, affectedCoordinates);
            controller.Flush();

            // THEN
            AnalyticsRecord moveRecord = sink.WrittenRecords.Single(record => record.Type == AnalyticsEventType.MoveExecuted);
            Assert.That(
                (moveRecord.PlayerId, moveRecord.SlotA, moveRecord.SlotB, moveRecord.Hex, moveRecord.SourceHex),
                Is.EqualTo((LocalPlayerId, (int)MoveType.Jump, 7, command.Target, command.Source))
            );
        }

        [Test]
        public void HandleMoveExecuted_DeployRaised_DoesNotCaptureARecord()
        {
            // GIVEN — Deploy is already reported by card_deployed; recording it again here would double-count
            // every troop placement in the session.
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            var command = MoveCommand.ForDeploy(new HexCoordinates(0, 0), LocalPlayerId);
            var affectedCoordinates = new List<HexCoordinates> { command.Target };

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, affectedCoordinates);
            controller.Flush();

            // THEN
            Assert.That(sink.WrittenRecords.Any(record => record.Type == AnalyticsEventType.MoveExecuted), Is.False);
        }

        [Test]
        public void MoveExecuted_IsEnabledFalse_NothingReachesTheSink()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildDisabledController();
            var command = new MoveCommand(MoveType.Jump, new HexCoordinates(0, 0), new HexCoordinates(1, 0), LocalPlayerId, 5);
            var affectedCoordinates = new List<HexCoordinates> { command.Target };

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, affectedCoordinates);
            controller.Flush();

            // THEN
            Assert.That((sink.OpenedSessions.Count, sink.WrittenRecords.Count), Is.EqualTo((0, 0)));
        }

        [Test]
        public void HandleAbilityResolved_Raised_CapturesTheOwnAndEnemyCounts()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            var result = new AbilityResult(new List<int> { 1, 2, 3 }, null, null, affectedOwnCount: 2, affectedEnemyCount: 1);

            // WHEN
            MatchEvents.RaiseAbilityResolved(LocalPlayerId, result);
            controller.Flush();

            // THEN
            AnalyticsRecord abilityRecord = sink.WrittenRecords.Single(record => record.Type == AnalyticsEventType.AbilityResolved);
            Assert.That((abilityRecord.SlotA, abilityRecord.SlotD), Is.EqualTo((2, 1)));
        }

        [Test]
        public void HandleStatusApplied_Raised_CapturesTheUnitOwnerStatusAndDuration()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            var change = new StatusChange(5, LocalPlayerId, OpponentPlayerId, StatusType.Frozen, 3);

            // WHEN
            MatchEvents.RaiseStatusApplied(in change);
            controller.Flush();

            // THEN
            AnalyticsRecord statusRecord = sink.WrittenRecords.Single(record => record.Type == AnalyticsEventType.StatusApplied);
            Assert.That(
                (statusRecord.PlayerId, statusRecord.SlotA, statusRecord.SlotB, statusRecord.SlotC, statusRecord.SlotD),
                Is.EqualTo((OpponentPlayerId, 5, LocalPlayerId, (int)StatusType.Frozen, 3))
            );
        }

        [Test]
        public void StatusApplied_IsEnabledFalse_NothingReachesTheSink()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildDisabledController();
            var change = new StatusChange(5, LocalPlayerId, OpponentPlayerId, StatusType.Frozen, 3);

            // WHEN
            MatchEvents.RaiseStatusApplied(in change);
            controller.Flush();

            // THEN
            Assert.That((sink.OpenedSessions.Count, sink.WrittenRecords.Count), Is.EqualTo((0, 0)));
        }

        [Test]
        public void HandleStatusExpired_Raised_CapturesTheOwnerAndStatus()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            var change = new StatusChange(5, LocalPlayerId, StatusChange.NoActingPlayer, StatusType.Rooted, 0);

            // WHEN
            MatchEvents.RaiseStatusExpired(in change);
            controller.Flush();

            // THEN
            AnalyticsRecord statusRecord = sink.WrittenRecords.Single(record => record.Type == AnalyticsEventType.StatusExpired);
            Assert.That((statusRecord.PlayerId, statusRecord.SlotA, statusRecord.SlotC), Is.EqualTo((LocalPlayerId, 5, (int)StatusType.Rooted)));
        }

        [Test]
        public void StatusExpired_IsEnabledFalse_NothingReachesTheSink()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildDisabledController();
            var change = new StatusChange(5, LocalPlayerId, StatusChange.NoActingPlayer, StatusType.Rooted, 0);

            // WHEN
            MatchEvents.RaiseStatusExpired(in change);
            controller.Flush();

            // THEN
            Assert.That((sink.OpenedSessions.Count, sink.WrittenRecords.Count), Is.EqualTo((0, 0)));
        }

        [Test]
        public void HandleMatchEnded_AfterScoreChangedForBothSeats_CapturesTheLastCountPerSeat()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            MatchEvents.RaiseMatchStarted(BuildMatchConfiguration(1));
            MatchEvents.RaiseScoreChanged(LocalPlayerId, 5);
            MatchEvents.RaiseScoreChanged(OpponentPlayerId, 3);

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // THEN
            AnalyticsRecord matchEndRecord = sink.WrittenRecords.Single(record => record.Type == AnalyticsEventType.MatchEnd);
            Assert.That((matchEndRecord.SlotC, matchEndRecord.SlotD), Is.EqualTo((5, 3)));
        }

        [Test]
        public void HandleMatchEnded_AfterEnteringOvertime_CapturesTheOvertimeFlag()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            MatchEvents.RaiseMatchStarted(BuildMatchConfiguration(1));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // THEN
            AnalyticsRecord matchEndRecord = sink.WrittenRecords.Single(record => record.Type == AnalyticsEventType.MatchEnd);
            Assert.That(matchEndRecord.Flag, Is.True);
        }

        [Test]
        public void HandleMatchEnded_Always_FlushesWithoutAnExplicitCall()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            MatchEvents.RaiseMatchStarted(BuildMatchConfiguration(1));

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // THEN
            Assert.That(sink.WrittenRecords.Any(record => record.Type == AnalyticsEventType.MatchEnd), Is.True);
        }

        [Test]
        public void Capture_BufferReachesCapacity_FlushesBeforeAddingSoNothingIsDropped()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN
            for (int i = 0; i < EventsToOverflowTheDefaultBuffer; i++)
            {
                MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            }

            controller.Flush();

            // THEN
            Assert.That(sink.WrittenRecords, Has.Count.EqualTo(1 + EventsToOverflowTheDefaultBuffer));
        }

        [Test]
        public void Flush_SinkIsFaulted_ClearsTheBufferWithoutWriting()
        {
            // GIVEN — session_start is already buffered from Construct when the fault is set, so a buffer that
            // survived the faulted flush would still contain it.
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            sink.IsFaulted = true;
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            controller.Flush();
            sink.IsFaulted = false;

            // WHEN — recovering the sink and flushing again shows what the buffer actually held.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);
            controller.Flush();

            // THEN — only the record captured after recovery reached the sink; session_start and the first
            // phase_changed were discarded when the faulted flush cleared the buffer without writing them.
            Assert.That(sink.WrittenRecords.Select(record => record.Type), Is.EqualTo(new[] { AnalyticsEventType.PhaseChanged }));
        }

        [Test]
        public void Flush_SinkThrows_LogsOneWarningAndWritesNothing()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            sink.ShouldThrowOnWrite = true;
            LogAssert.Expect(LogType.Warning, new Regex("^AnalyticsController's sink threw "));

            // WHEN
            controller.Flush();

            // THEN
            Assert.That(sink.WrittenRecords, Is.Empty);
        }

        [Test]
        public void Flush_CalledAgainAfterTheSinkThrewOnce_DoesNotWriteAgain()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            sink.ShouldThrowOnWrite = true;
            LogAssert.Expect(LogType.Warning, new Regex("^AnalyticsController's sink threw "));
            controller.Flush();
            sink.ShouldThrowOnWrite = false;

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            controller.Flush();

            // THEN
            Assert.That(sink.WrittenRecords, Is.Empty);
        }

        [Test]
        public void OnDisable_Called_UnsubscribesFromMatchEvents()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN
            controller.gameObject.SetActive(false);
            MatchEvents.RaiseMatchStarted(new MatchConfiguration(1));
            controller.Flush();

            // THEN
            Assert.That(sink.WrittenRecords.Any(record => record.Type == AnalyticsEventType.MatchStart), Is.False);
        }

        [Test]
        public void OnApplicationPause_True_FlushesTheBuffer()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN
            controller.gameObject.SendMessage("OnApplicationPause", true, SendMessageOptions.DontRequireReceiver);

            // THEN
            Assert.That(sink.WrittenRecords, Is.Not.Empty);
        }

        [Test]
        public void OnApplicationPause_False_DoesNotFlush()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN
            controller.gameObject.SendMessage("OnApplicationPause", false, SendMessageOptions.DontRequireReceiver);

            // THEN
            Assert.That(sink.WrittenRecords, Is.Empty);
        }

        [Test]
        public void OnApplicationQuit_SessionOpen_RecordsSessionEnd()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN — OnApplicationQuit is a protected Unity message with no seam; SendMessage reaches it
            // without reflection.
            controller.gameObject.SendMessage("OnApplicationQuit", SendMessageOptions.DontRequireReceiver);

            // THEN
            Assert.That(sink.WrittenRecords.Any(record => record.Type == AnalyticsEventType.SessionEnd), Is.True);
        }

        [Test]
        public void OnApplicationQuit_SessionOpen_ClosesTheSink()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN — OnApplicationQuit is a protected Unity message with no seam; SendMessage reaches it
            // without reflection.
            controller.gameObject.SendMessage("OnApplicationQuit", SendMessageOptions.DontRequireReceiver);

            // THEN
            Assert.That(sink.CloseCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OnDestroy_SessionOpen_RecordsSessionEnd()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN — destroying immediately, rather than Object.Destroy, is what makes OnDestroy run inside
            // this synchronous [Test] instead of at the end of a frame nothing here waits for.
            Object.DestroyImmediate(controller.gameObject);

            // THEN
            Assert.That(sink.WrittenRecords.Any(record => record.Type == AnalyticsEventType.SessionEnd), Is.True);
        }

        [Test]
        public void OnDestroy_SessionOpen_ClosesTheSink()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();

            // WHEN
            Object.DestroyImmediate(controller.gameObject);

            // THEN
            Assert.That(sink.CloseCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OnDestroy_AfterQuitAlreadyClosedTheSession_DoesNotCloseTheSinkAgain()
        {
            // GIVEN
            (AnalyticsController controller, FakeAnalyticsSink sink) = BuildEnabledController();
            controller.gameObject.SendMessage("OnApplicationQuit", SendMessageOptions.DontRequireReceiver);

            // WHEN
            Object.DestroyImmediate(controller.gameObject);

            // THEN
            Assert.That(sink.CloseCallCount, Is.EqualTo(1));
        }

        private static MatchConfiguration BuildMatchConfiguration(int seed)
        {
            return new MatchConfiguration(
                seed,
                new PlayerSlot(LocalPlayerId, PlayerControl.LocalHuman),
                new PlayerSlot(OpponentPlayerId, PlayerControl.Machine),
                90f,
                5f,
                30f
            );
        }

        private (AnalyticsController Controller, FakeAnalyticsSink Sink) BuildEnabledController()
        {
            var sink = new FakeAnalyticsSink();
            var go = new GameObject("AnalyticsController_Test");
            go.SetActive(false);
            AnalyticsController controller = go.AddComponent<AnalyticsController>();
            controller.Construct(sink);
            go.SetActive(true);
            _spawned.Add(go);

            return (controller, sink);
        }

        private (AnalyticsController Controller, FakeAnalyticsSink Sink) BuildDisabledController()
        {
            var sink = new FakeAnalyticsSink();
            var go = new GameObject("AnalyticsController_Disabled_Test");
            go.SetActive(false);
            AnalyticsController controller = go.AddComponent<AnalyticsController>();
            JsonUtility.FromJsonOverwrite("{\"_isEnabled\":false}", controller);
            controller.Construct(sink);
            go.SetActive(true);
            _spawned.Add(go);

            return (controller, sink);
        }
    }
}
