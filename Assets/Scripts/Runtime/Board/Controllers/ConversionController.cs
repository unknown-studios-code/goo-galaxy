using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Board.Controllers
{
    /// <summary>
    /// Turns every executed move into the conversion attempts it triggers, and publishes what they did through
    /// <c>MatchEvents.ConversionResolved</c>. The rules live in <see cref="ConversionResolver" />; this
    /// component wires them to the scene's grid and unit registry and owns the buffers it publishes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A landing that converts nothing publishes nothing, so downstream systems can treat the event as a fact
    /// rather than a poll. <c>MatchEvents.LandingResolved</c> is raised unconditionally right after, because
    /// step 4 — the card's landing impact — has to run whether or not step 3 converted anything. The published
    /// lists are this component's own reusable buffers and are only valid for the duration of the dispatch.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class ConversionController : MonoBehaviour
    {
        // A Jump publishes two coordinates, and each reaches the widest area an authored conversion radius
        // covers. The two areas overlap heavily in practice, so this is a ceiling, not an expected count.
        private const int MaxAffectedCoordinates = 2;
        private const int MaxAttemptsPerLanding = MaxAffectedCoordinates * BoardMetrics.MaxConversionTargetsPerLanding;

        private static readonly ProfilerMarker _resolveConversionsMarker = new("ConversionController.ResolveConversions");

        private readonly List<HexCell> _areaBuffer = new(BoardMetrics.MaxImpactAreaCells);
        private readonly HashSet<int> _attemptedUnitIds = new(MaxAttemptsPerLanding);
        private readonly List<int> _convertedUnitIds = new(MaxAttemptsPerLanding);
        private readonly List<int> _armorStrippedUnitIds = new(MaxAttemptsPerLanding);

        private ReadOnlyCollection<int> _convertedUnitIdsView;
        private ReadOnlyCollection<int> _armorStrippedUnitIdsView;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private bool _isResolvingConversions;
        private bool _hasLoggedBoardUnavailable;

        [Inject]
        public void Construct(GridPresenter gridPresenter, UnitPresenter unitPresenter)
        {
            Debug.Assert(gridPresenter != null, BoardLogMessages.GridPresenterMissing, this);
            Debug.Assert(unitPresenter != null, BoardLogMessages.UnitPresenterMissing, this);

            _gridPresenter = gridPresenter;
            _unitPresenter = unitPresenter;
        }

        protected void Awake()
        {
            _convertedUnitIdsView = new ReadOnlyCollection<int>(_convertedUnitIds);
            _armorStrippedUnitIdsView = new ReadOnlyCollection<int>(_armorStrippedUnitIds);
        }

        protected void OnEnable()
        {
            MatchEvents.MoveExecuted += HandleMoveExecuted;
        }

        protected void OnDisable()
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

            // The acting unit is the landing hex's occupant, never `command.UnitId` — see MoveCommand.UnitId.
            // Reading the radius off the command would miss the registry on every Deploy and silently fall back
            // to the default: a deployed Volatile Mass, authored at radius 2, would under-convert with no error
            // and a green suite.
            int conversionRadius =
                grid.TryGetCell(command.Target, out HexCell landingCell) && landingCell.IsOccupied
                    ? GetConversionRadius(landingCell.OccupantUnitId)
                    : BoardMetrics.DefaultConversionRadius;

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
                        conversionRadius,
                        _areaBuffer,
                        _attemptedUnitIds,
                        _convertedUnitIds,
                        _armorStrippedUnitIds
                    );
                }

                // Built once and handed to both publishes: ConversionResolved suppresses an empty result, but
                // LandingResolved carries it regardless so step 4 can tell "nobody was converted" apart from
                // "I was never told".
                var conversions = new ConversionResult(_convertedUnitIdsView, _armorStrippedUnitIdsView);

                PublishConversionResolved(command.PlayerId, conversions);
                PublishLandingResolved(command, conversions);
            }
            finally
            {
                _isResolvingConversions = false;
            }
        }

        private void PublishConversionResolved(int actingPlayerId, ConversionResult result)
        {
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

        private void PublishLandingResolved(in MoveCommand command, ConversionResult conversions)
        {
            try
            {
                MatchEvents.RaiseLandingResolved(command, conversions);
            }
            catch (Exception exception)
            {
                // Same dispatch-boundary reasoning as the conversion publish above: the move and its conversions
                // are already committed to the models, so a subscriber's throw must not unwind into the move
                // pipeline and report a failure over a board that did change.
                Debug.LogError(BoardLogMessages.LandingResolvedSubscriberFailed, this);
                Debug.LogException(exception, this);
            }
        }

        private int GetConversionRadius(int unitId)
        {
            if (!_unitPresenter.TryGetCapability(unitId, out IMoveCapable capability))
            {
                return BoardMetrics.DefaultConversionRadius;
            }

            return capability is IConversionCapable conversionCapable ? conversionCapable.ConversionRadius : BoardMetrics.DefaultConversionRadius;
        }

        private bool TryGetHexGrid(out HexGrid grid)
        {
            grid = _gridPresenter != null ? _gridPresenter.HexGrid : null;

            return grid != null;
        }
    }
}
