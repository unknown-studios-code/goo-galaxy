using System;
using System.Collections.Generic;
using System.Globalization;
using GooGalaxy.Runtime.Analytics.Interfaces;
using GooGalaxy.Runtime.Analytics.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Analytics.Controllers
{
    /// <summary>
    /// Captures gameplay facts from <see cref="MatchEvents" /> into a fixed buffer and flushes them to an
    /// <see cref="IAnalyticsSink" /> at safe boundaries. Local only; nothing is uploaded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Capture is allocation-free.</b> Every handler turns its payload into a value-type
    /// <see cref="AnalyticsRecord" /> and copies it into a buffer sized once, so the input path — where card plays and
    /// the resolution events they raise are published — pays for a struct copy and nothing else. Collection payloads
    /// are read by <c>Count</c> only, never iterated or retained, so the publisher's buffer ownership rules are never
    /// in play.
    /// </para>
    /// <para>
    /// <b>Flushing is where the cost lives</b>, and it happens only at a boundary: after a match ends, when the app is
    /// paused, when the session closes, and — so no record is ever dropped — just before a record would land in a
    /// full buffer. The buffer is sized so that last one does not happen in an ordinary match.
    /// </para>
    /// <para>
    /// A session is this component's lifetime: it opens when the container injects the sink and closes on quit or
    /// when the component is destroyed, whichever comes first. Time is real time since it opened, not match time, so a
    /// paused or time-scaled match still reports how long a person played. The clock tick and the per-frame Energy
    /// total are deliberately not captured — they would dominate the file and say nothing the spend and phase records
    /// do not.
    /// </para>
    /// <para>
    /// Gameplay assemblies never reference this one; the dependency runs only from here to <c>Runtime.Shared</c>.
    /// </para>
    /// <para>
    /// <b>It subscribes ahead of the board controllers that resolve a move's consequences.</b> A Clone or Jump's
    /// <c>MatchEvents.MoveExecuted</c> is dispatched to every subscriber in registration order, and the board's own
    /// <c>ConversionController</c> is one of them — its handler resolves conversions and raises
    /// <c>ConversionResolved</c>/<c>LandingResolved</c> synchronously, from inside that same dispatch, before
    /// <c>MoveExecuted</c> returns to its next subscriber. Left at the default execution order, this component could
    /// be enabled after <c>ConversionController</c> and would then capture <c>conversion_event</c> before
    /// <c>move_executed</c> for the very same move — the session file would read the cause after its effect. The
    /// execution order below is what keeps <c>HandleMoveExecuted</c> first in that invocation list, the same
    /// technique <c>MatchHudPresenter</c> uses to guarantee it sees <c>MatchStarted</c> before anything else does.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class AnalyticsController : MonoBehaviour
    {
        // PERF: sized for the longest match the authored durations allow — 180 s of normal play plus 60 s of
        // overtime — with both seats acting at a generous rate, so an ordinary match flushes only at its end and
        // never on the input path. The full-buffer flush in Capture stays as the fallback that keeps a record from
        // being dropped when a session runs past that.
        private const int BufferCapacity = 2048;

        private const double MillisecondsPerSecond = 1000.0;
        private const string SessionIdFormat = "yyyyMMdd-HHmmss'Z'";

        private static readonly ProfilerMarker _flushMarker = new("AnalyticsController.Flush");

        [Tooltip(
            "Whether this session is captured to the device's local analytics folder. Off leaves every match event unobserved and "
                + "writes nothing. Set it before entering Play Mode; a session already open keeps capturing until it ends."
        )]
        [SerializeField]
        private bool _isEnabled = true;

        private readonly AnalyticsBuffer _buffer = new(BufferCapacity);

        private IAnalyticsSink _sink;
        private double _sessionStartSeconds;
        private double _matchStartSeconds;
        private int _matchOrdinal;
        private int _playerOneId;
        private int _playerTwoId;
        private int _playerOneScore;
        private int _playerTwoScore;
        private bool _hasEnteredOvertime;
        private bool _isSessionOpen;
        private bool _hasSessionEnded;
        private bool _hasSinkFailed;

        public bool IsEnabled => _isEnabled;

        internal bool IsCapturing => _isSessionOpen && !_hasSessionEnded;

        /// <remarks>
        /// Opens the session, which is why it records <c>session_start</c> here rather than in <c>OnEnable</c>: the
        /// container injects while the scope is being built, before any component's <c>Start</c> can announce a match,
        /// so no gameplay fact can be published ahead of the session that should hold it.
        /// </remarks>
        [Inject]
        public void Construct(IAnalyticsSink sink)
        {
            Debug.Assert(sink != null, AnalyticsLogMessages.SinkMissing, this);

            _sink = sink;
            TryOpenSession();
        }

        protected void OnEnable()
        {
            if (!_isEnabled)
            {
                return;
            }

            MatchEvents.MatchStarted += HandleMatchStarted;
            MatchEvents.MatchPhaseChanged += HandleMatchPhaseChanged;
            MatchEvents.ScoreChanged += HandleScoreChanged;
            MatchEvents.MatchEnded += HandleMatchEnded;
            MatchEvents.EnergySpent += HandleEnergySpent;
            MatchEvents.MoveExecuted += HandleMoveExecuted;
            MatchEvents.ConversionResolved += HandleConversionResolved;
            MatchEvents.AbilityResolved += HandleAbilityResolved;
            MatchEvents.StatusApplied += HandleStatusApplied;
            MatchEvents.StatusExpired += HandleStatusExpired;
            MatchEvents.CardDiscarded += HandleCardDiscarded;
            MatchEvents.CardPlayAttempted += HandleCardPlayAttempted;
        }

        protected void OnDisable()
        {
            MatchEvents.MatchStarted -= HandleMatchStarted;
            MatchEvents.MatchPhaseChanged -= HandleMatchPhaseChanged;
            MatchEvents.ScoreChanged -= HandleScoreChanged;
            MatchEvents.MatchEnded -= HandleMatchEnded;
            MatchEvents.EnergySpent -= HandleEnergySpent;
            MatchEvents.MoveExecuted -= HandleMoveExecuted;
            MatchEvents.ConversionResolved -= HandleConversionResolved;
            MatchEvents.AbilityResolved -= HandleAbilityResolved;
            MatchEvents.StatusApplied -= HandleStatusApplied;
            MatchEvents.StatusExpired -= HandleStatusExpired;
            MatchEvents.CardDiscarded -= HandleCardDiscarded;
            MatchEvents.CardPlayAttempted -= HandleCardPlayAttempted;
        }

        protected void OnDestroy()
        {
            CloseSession();
        }

        protected void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                return;
            }

            // A paused mobile app can be killed without another callback, so what is buffered goes to disk now.
            Flush();
        }

        protected void OnApplicationQuit()
        {
            CloseSession();
        }

        /// <remarks>
        /// Synchronous: the records have reached the sink when this returns. The buffer is cleared whether or not the
        /// write succeeded, so a device that cannot write never accumulates records it will never be able to flush. A
        /// sink that throws is caught here — this can run inside a board resolution's event dispatch, and a diagnostic
        /// must never abort one — and the session writes nothing further.
        /// </remarks>
        internal void Flush()
        {
            if (!_isSessionOpen || (_buffer.Count == 0))
            {
                return;
            }

            using (_flushMarker.Auto())
            {
                WriteBufferToSink();
                _buffer.Clear();
            }
        }

        private static AnalyticsSession CreateSession()
        {
            string sessionId = DateTime.UtcNow.ToString(SessionIdFormat, CultureInfo.InvariantCulture);

            return new AnalyticsSession(sessionId, SystemInfo.deviceModel, SystemInfo.operatingSystem, Application.version);
        }

        private static long GetElapsedMilliseconds(double sinceSeconds)
        {
            return (long)((Time.realtimeSinceStartupAsDouble - sinceSeconds) * MillisecondsPerSecond);
        }

        private static int CountOf<T>(IReadOnlyList<T> items)
        {
            return items == null ? 0 : items.Count;
        }

        // The sink is null only when the container resolved a null one, which Construct asserts on; capture then
        // stays off rather than failing on the first event.
        private void TryOpenSession()
        {
            if (!_isEnabled || _isSessionOpen || _hasSessionEnded || (_sink == null))
            {
                return;
            }

            _sessionStartSeconds = Time.realtimeSinceStartupAsDouble;
            _isSessionOpen = true;

            _sink.Open(CreateSession());
            Capture(AnalyticsRecord.ForSessionStart());
        }

        // Idempotent: quit and destroy both close the session, and whichever runs second finds it already closed.
        private void CloseSession()
        {
            if (!IsCapturing)
            {
                return;
            }

            long sessionDurationMs = GetElapsedMilliseconds(_sessionStartSeconds);

            Capture(AnalyticsRecord.ForSessionEnd(sessionDurationMs, sessionDurationMs, _matchOrdinal));
            Flush();

            _hasSessionEnded = true;

            if (!_hasSinkFailed)
            {
                _sink.Close();
            }
        }

        private void WriteBufferToSink()
        {
            if (_hasSinkFailed || _sink.IsFaulted)
            {
                return;
            }

            try
            {
                _sink.Write(_buffer);
            }
            catch (Exception exception)
            {
                _hasSinkFailed = true;
                Debug.LogWarning(string.Format(AnalyticsLogMessages.SinkWriteThrewFormat, exception.GetType().Name, exception.Message), this);
            }
        }

        private void Capture(in AnalyticsRecord record)
        {
            if (_buffer.IsFull)
            {
                Flush();
            }

            _buffer.TryAdd(in record);
        }

        private long GetTimestamp()
        {
            return GetElapsedMilliseconds(_sessionStartSeconds);
        }

        private void HandleMatchStarted(MatchConfiguration config)
        {
            if (!IsCapturing)
            {
                return;
            }

            _matchOrdinal++;
            _matchStartSeconds = Time.realtimeSinceStartupAsDouble;
            _playerOneId = config.PlayerOne.Id;
            _playerTwoId = config.PlayerTwo.Id;
            _playerOneScore = 0;
            _playerTwoScore = 0;
            _hasEnteredOvertime = false;

            Capture(AnalyticsRecord.ForMatchStart(GetTimestamp(), _matchOrdinal, in config));
        }

        // The orchestrator enters Loading before it announces the match, so the Loading transition arrives while the
        // ordinal still names the previous match. It is tagged with the ordinal the upcoming MatchStarted will assign,
        // so every phase of a match carries that match's ordinal.
        private void HandleMatchPhaseChanged(MatchPhase phase)
        {
            if (!IsCapturing)
            {
                return;
            }

            if (phase == MatchPhase.Overtime)
            {
                _hasEnteredOvertime = true;
            }

            int matchOrdinal = phase == MatchPhase.Loading ? _matchOrdinal + 1 : _matchOrdinal;

            Capture(AnalyticsRecord.ForPhaseChanged(GetTimestamp(), matchOrdinal, phase));
        }

        // Not captured as its own record: the last count per seat is what match_end reports. Zeroed at match start
        // because the orchestrator resets its own score cache there and recounts before every ending, so the counts
        // that reach match_end are always the new match's.
        private void HandleScoreChanged(int playerId, int unitCount)
        {
            if (playerId == PlayerSlot.UnassignedId)
            {
                return;
            }

            if (playerId == _playerOneId)
            {
                _playerOneScore = unitCount;
            }
            else if (playerId == _playerTwoId)
            {
                _playerTwoScore = unitCount;
            }
        }

        private void HandleMatchEnded(MatchOutcome outcome)
        {
            if (!IsCapturing)
            {
                return;
            }

            long matchDurationMs = GetElapsedMilliseconds(_matchStartSeconds);

            Capture(
                AnalyticsRecord.ForMatchEnd(GetTimestamp(), _matchOrdinal, in outcome, _playerOneScore, _playerTwoScore, matchDurationMs, _hasEnteredOvertime)
            );
            Flush();
        }

        private void HandleEnergySpent(int playerId, float energyAfter, bool wasSuccessful)
        {
            if (!IsCapturing)
            {
                return;
            }

            Capture(AnalyticsRecord.ForEnergySpent(GetTimestamp(), _matchOrdinal, playerId, energyAfter, wasSuccessful));
        }

        // A Deploy is not captured here: card_deployed already reports it, with the card and cost this event does
        // not carry, and a Deploy has no source hex or acting unit of its own to name. Only the two moves a unit
        // already on the board can make — Clone and Jump — are recorded through this handler.
        private void HandleMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates)
        {
            if (!IsCapturing)
            {
                return;
            }

            if (command.Type == MoveType.Deploy)
            {
                return;
            }

            Capture(AnalyticsRecord.ForMoveExecuted(GetTimestamp(), _matchOrdinal, in command));
        }

        private void HandleConversionResolved(int actingPlayerId, ConversionResult result)
        {
            if (!IsCapturing)
            {
                return;
            }

            int convertedCount = CountOf(result.ConvertedUnitIds);
            int strippedCount = CountOf(result.ArmorStrippedUnitIds);

            Capture(AnalyticsRecord.ForConversion(GetTimestamp(), _matchOrdinal, actingPlayerId, convertedCount, strippedCount));
        }

        private void HandleAbilityResolved(int actingPlayerId, AbilityResult result)
        {
            if (!IsCapturing)
            {
                return;
            }

            int affectedHexCount = CountOf(result.AffectedHexes);
            int destroyedUnitCount = CountOf(result.DestroyedUnitIds);

            Capture(
                AnalyticsRecord.ForAbilityResolved(
                    GetTimestamp(),
                    _matchOrdinal,
                    actingPlayerId,
                    result.AffectedOwnCount,
                    result.AffectedEnemyCount,
                    affectedHexCount,
                    destroyedUnitCount
                )
            );
        }

        private void HandleStatusApplied(StatusChange change)
        {
            if (!IsCapturing)
            {
                return;
            }

            Capture(AnalyticsRecord.ForStatusApplied(GetTimestamp(), _matchOrdinal, in change));
        }

        private void HandleStatusExpired(StatusChange change)
        {
            if (!IsCapturing)
            {
                return;
            }

            Capture(AnalyticsRecord.ForStatusExpired(GetTimestamp(), _matchOrdinal, in change));
        }

        private void HandleCardDiscarded(int playerId, CardId discardedCard, int slotIndex)
        {
            if (!IsCapturing)
            {
                return;
            }

            Capture(AnalyticsRecord.ForCardDiscarded(GetTimestamp(), _matchOrdinal, playerId, discardedCard, slotIndex));
        }

        private void HandleCardPlayAttempted(CardPlayAttempt attempt)
        {
            if (!IsCapturing)
            {
                return;
            }

            AnalyticsRecord record =
                attempt.Result == CardPlayResult.Success
                    ? AnalyticsRecord.ForCardDeployed(GetTimestamp(), _matchOrdinal, attempt.PlayerId, attempt.CardId, attempt.Target, attempt.EnergyCost)
                    : AnalyticsRecord.ForCardPlayRejected(GetTimestamp(), _matchOrdinal, attempt.PlayerId, attempt.CardId, attempt.Result);

            Capture(in record);
        }
    }
}
