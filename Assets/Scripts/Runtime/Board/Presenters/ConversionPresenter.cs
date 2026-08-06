using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using Unity.Profiling;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Presenters
{
    /// <summary>
    /// Turns every executed move into the conversion attempts it triggers, and publishes what they did through
    /// <c>MatchEvents.ConversionResolved</c>. The rules live in <see cref="ConversionResolver" />; this
    /// component wires it to the scene's grid and unit registry and owns the buffers it publishes.
    /// </summary>
    /// <remarks>
    /// A landing that converts nothing publishes nothing, so downstream systems can treat the event as a fact
    /// rather than a poll. The published lists are the presenter's own reusable buffers and are only valid for
    /// the duration of the dispatch.
    /// </remarks>
    [DisallowMultipleComponent]
    public class ConversionPresenter : MonoBehaviour
    {
        // A Jump publishes two coordinates, so the widest neighbourhood one landing can reach is both rings.
        private const int MaxAffectedCoordinates = 2;
        private const int MaxAttemptsPerLanding = MaxAffectedCoordinates * BoardMetrics.NeighborsPerCell;

        private static readonly ProfilerMarker _resolveConversionsMarker = new("ConversionPresenter.ResolveConversions");

        [SerializeField]
        private GridPresenter _gridPresenter;

        [SerializeField]
        private UnitPresenter _unitPresenter;

        private readonly List<HexCell> _neighborBuffer = new(BoardMetrics.NeighborsPerCell);
        private readonly HashSet<int> _attemptedUnitIds = new(MaxAttemptsPerLanding);
        private readonly List<int> _convertedUnitIds = new(MaxAttemptsPerLanding);
        private readonly List<int> _armorStrippedUnitIds = new(MaxAttemptsPerLanding);

        private ReadOnlyCollection<int> _convertedUnitIdsView;
        private ReadOnlyCollection<int> _armorStrippedUnitIdsView;
        private bool _isResolvingConversions;
        private bool _hasLoggedBoardUnavailable;

        private void Awake()
        {
            _convertedUnitIdsView = new ReadOnlyCollection<int>(_convertedUnitIds);
            _armorStrippedUnitIdsView = new ReadOnlyCollection<int>(_armorStrippedUnitIds);

            if (_gridPresenter == null)
            {
                TryGetComponent(out _gridPresenter);
            }

            if (_unitPresenter == null)
            {
                TryGetComponent(out _unitPresenter);
            }
        }

        private void OnEnable()
        {
            MatchEvents.MoveExecuted += HandleMoveExecuted;
        }

        private void OnDisable()
        {
            MatchEvents.MoveExecuted -= HandleMoveExecuted;
        }

        private void HandleMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates)
        {
            // UnitPresenter already rejects a move resolved from inside MoveExecuted, so this can only be
            // reached by something raising the bus event directly. It still has to be rejected: re-entering
            // would clear the id buffers the outer ConversionResolved subscribers are still iterating.
            if (_isResolvingConversions)
            {
                Debug.LogError(BoardLogMessages.ConversionResolveReentered, this);
                return;
            }

            if (_unitPresenter == null || !TryGetHexGrid(out HexGrid grid))
            {
                // Latched: a misconfigured board fails on every move for the rest of the match, and one console
                // line naming the cause is more useful than one per move burying everything after it.
                if (!_hasLoggedBoardUnavailable)
                {
                    _hasLoggedBoardUnavailable = true;
                    Debug.LogError(BoardLogMessages.ConversionBoardUnavailable, this);
                }

                return;
            }

            _hasLoggedBoardUnavailable = false;
            _isResolvingConversions = true;

            try
            {
                // Scoped to the rules alone: the publish below runs every subscriber's work, and folding that
                // into this marker would charge UnitView's effect spawning to the resolver.
                using (_resolveConversionsMarker.Auto())
                {
                    ConversionResolver.Resolve(
                        grid,
                        _unitPresenter.ActiveUnits,
                        affectedCoordinates,
                        command.PlayerId,
                        _neighborBuffer,
                        _attemptedUnitIds,
                        _convertedUnitIds,
                        _armorStrippedUnitIds
                    );
                }

                PublishConversionResolved(command.PlayerId);
            }
            finally
            {
                _isResolvingConversions = false;
            }
        }

        private void PublishConversionResolved(int actingPlayerId)
        {
            var result = new ConversionResult(_convertedUnitIdsView, _armorStrippedUnitIdsView);

            if (result.IsEmpty)
            {
                return;
            }

            try
            {
                MatchEvents.RaiseConversionResolved(actingPlayerId, result);
            }
            catch (Exception exception)
            {
                // Deliberately broad, and the one place the style rule's "no try/catch as flow control" does not
                // apply: this is a dispatch boundary into arbitrary subscriber code, so no narrower type exists to
                // name. The conversions are already committed to the models by now, and letting a subscriber's
                // throw unwind into the move pipeline would report a failed move over a board that did change.
                // Nothing is swallowed — the exception is logged with its stack.
                Debug.LogError(BoardLogMessages.ConversionResolvedSubscriberFailed, this);
                Debug.LogException(exception, this);
            }
        }

        private bool TryGetHexGrid(out HexGrid grid)
        {
            grid = _gridPresenter != null ? _gridPresenter.HexGrid : null;

            return grid != null;
        }
    }
}
