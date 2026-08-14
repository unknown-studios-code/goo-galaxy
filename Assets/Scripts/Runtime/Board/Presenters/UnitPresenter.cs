using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Board.Presenters
{
    /// <summary>
    /// Presenter owning the live unit registry and the single entry point for executing board moves.
    /// Validation and mutation are delegated to the movement services; this component wires them to the
    /// scene's grid, owns the affected-coordinate buffer it publishes, and keeps the registry in sync with
    /// cell occupancy.
    /// </summary>
    /// <remarks>
    /// The registry, not the unit model, is the authority on which cell a unit holds: <c>GridUnit.Position</c>
    /// is mutable and would otherwise let a relocated unit leave its old cell marked occupied forever.
    /// </remarks>
    [DisallowMultipleComponent]
    public class UnitPresenter : MonoBehaviour
    {
        private const int AffectedCoordinatesCapacity = 2;

        private const int LiveUnitCapacity = BoardMetrics.DefaultBoardCellCount;

        private static readonly ProfilerMarker _resolveMoveMarker = new("UnitPresenter.ResolveMove");

        private readonly Dictionary<int, GridUnit> _activeUnits = new(LiveUnitCapacity);
        private readonly Dictionary<int, IMoveCapable> _unitCapabilities = new(LiveUnitCapacity);
        private readonly Dictionary<int, HexCoordinates> _registeredPositions = new(LiveUnitCapacity);
        private readonly List<HexCoordinates> _affectedCoordinates = new(AffectedCoordinatesCapacity);

        private ReadOnlyCollection<HexCoordinates> _affectedCoordinatesView;
        private GridPresenter _gridPresenter;
        private IUnitSpawner _unitSpawner;
        private IEnergyLedger _energyLedger;
        private bool _isResolvingMove;
        private bool _hasLoggedSpawnFailure;

        /// <summary>
        /// The units this presenter tracks, keyed by unit id. Reflects every later move.
        /// </summary>
        /// <remarks>
        /// Iterate with the concrete key set or an id you already hold: <c>foreach</c> over the interface boxes
        /// the backing <c>Dictionary</c> enumerator, one allocation per pass.
        /// </remarks>
        public IReadOnlyDictionary<int, GridUnit> ActiveUnits => _activeUnits;

        /// <summary>The tracked units as values only, iterable without boxing the backing enumerator.</summary>
        /// <remarks>
        /// The concrete <c>ValueCollection</c> is returned on purpose: it is the type <c>foreach</c> needs to bind
        /// the struct enumerator directly, and it exposes no mutator, so nothing leaks that
        /// <see cref="ActiveUnits" /> does not already expose. Prefer it for any whole-registry pass.
        /// </remarks>
        public Dictionary<int, GridUnit>.ValueCollection ActiveUnitValues => _activeUnits.Values;

        /// <summary>
        /// Supplies the board this presenter moves units on, and the ledger every move is priced and paid through.
        /// </summary>
        /// <remarks>
        /// The ledger is held as an interface from <c>Runtime.Shared</c>, so the board never learns what a move
        /// costs and no dependency on the Energy assembly is created by charging for one. Both arrive before
        /// <c>Awake</c>, because the container force-resolves a registered component while the scope wakes.
        /// </remarks>
        /// <param name="gridPresenter">The board the moves are resolved against.</param>
        /// <param name="energyLedger">The resource system's ledger, resolved from the container.</param>
        [Inject]
        public void Construct(GridPresenter gridPresenter, IEnergyLedger energyLedger)
        {
            _gridPresenter = gridPresenter;
            _energyLedger = energyLedger;
        }

        protected void Awake()
        {
            _affectedCoordinatesView = new ReadOnlyCollection<HexCoordinates>(_affectedCoordinates);

            Debug.Assert(_gridPresenter != null, BoardLogMessages.GridPresenterMissing, this);
            Debug.Assert(_energyLedger != null, BoardLogMessages.EnergyLedgerMissing, this);
        }

        /// <summary>
        /// Assigns the factory used to create units for Clone moves.
        /// Must be set by match bootstrap before any Clone is resolved.
        /// </summary>
        /// <remarks>
        /// This is also the only point at which the spawn-failure log re-arms: a broken spawner is reported
        /// once, not once per Clone the player attempts.
        /// </remarks>
        /// <param name="spawner">The spawner implementation, or null to clear it.</param>
        public void SetUnitSpawner(IUnitSpawner spawner)
        {
            _unitSpawner = spawner;
            _hasLoggedSpawnFailure = false;
        }

        /// <summary>
        /// Adds a unit to the registry and marks its current cell as occupied.
        /// Re-registering an existing identifier first releases the cell that unit was recorded on.
        /// </summary>
        /// <param name="unit">The unit to track.</param>
        /// <param name="capability">The unit's movement capability, typically its card definition.</param>
        /// <returns>
        /// True once the unit is tracked and owns its cell; false if the board was unavailable, the position is
        /// off-grid, or another unit already stands there. The registry is left untouched on failure.
        /// </returns>
        public bool RegisterUnit(GridUnit unit, IMoveCapable capability)
        {
            if (unit == null)
            {
                return false;
            }

            if (!TryGetHexGrid(out HexGrid grid) || !grid.TryGetCell(unit.Position, out HexCell cell))
            {
                Debug.LogError(string.Format(BoardLogMessages.UnitRegistrationFailedFormat, unit.UnitId, unit.Position), this);
                return false;
            }

            if (cell.IsOccupied && cell.OccupantUnitId != unit.UnitId)
            {
                Debug.LogError(string.Format(BoardLogMessages.UnitRegistrationCellOccupiedFormat, unit.UnitId, unit.Position, cell.OccupantUnitId), this);
                return false;
            }

            ReleaseRegisteredCell(grid, unit.UnitId);

            _activeUnits[unit.UnitId] = unit;
            _unitCapabilities[unit.UnitId] = capability;
            _registeredPositions[unit.UnitId] = unit.Position;
            cell.SetOccupant(unit.UnitId);

            return true;
        }

        /// <summary>
        /// Removes a unit from the registry and frees the cell it occupied.
        /// </summary>
        /// <param name="unitId">The identifier of the unit to drop.</param>
        /// <returns>
        /// True if the unit was registered and has been removed; false if it was unknown, or if the board was
        /// unavailable and its cell could therefore not be released. The registry is left untouched on failure.
        /// </returns>
        public bool UnregisterUnit(int unitId)
        {
            if (!_activeUnits.ContainsKey(unitId))
            {
                return false;
            }

            if (!TryGetHexGrid(out HexGrid grid))
            {
                Debug.LogError(string.Format(BoardLogMessages.UnitUnregistrationFailedFormat, unitId), this);
                return false;
            }

            ReleaseRegisteredCell(grid, unitId);

            _activeUnits.Remove(unitId);
            _unitCapabilities.Remove(unitId);
            _registeredPositions.Remove(unitId);

            return true;
        }

        /// <summary>
        /// Looks up the capability object registered with a unit — in practice its card definition.
        /// </summary>
        /// <remarks>
        /// The registry is typed to <see cref="IMoveCapable"/> because movement is the capability every unit
        /// has, but the object behind it carries the rest of the card's contracts too. Callers that need one of
        /// those test the returned reference for it (<c>capability is IConversionCapable conversionCapable</c>)
        /// and fall back to a default when it does not implement it, rather than assuming a concrete type.
        /// </remarks>
        /// <param name="unitId">The identifier of the unit to look up.</param>
        /// <param name="capability">
        /// The registered capability. Null is a legitimate value — <see cref="RegisterUnit" /> accepts a unit
        /// with no capability — so callers must null-check even when this returns true.
        /// </param>
        /// <returns>True when the unit is registered; false when it is unknown.</returns>
        public bool TryGetCapability(int unitId, out IMoveCapable capability)
        {
            return _unitCapabilities.TryGetValue(unitId, out capability);
        }

        /// <summary>
        /// Validates and, when legal, executes a move, publishing <c>MatchEvents.MoveExecuted</c> on success.
        /// Nothing is published and the board is left untouched for any non-Success result.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A subscriber that throws does not change the returned result, because the board has already been
        /// mutated by then. Dispatch to the remaining subscribers is still lost, so a throwing handler is a
        /// defect in that handler. A subscriber that resolves another move is rejected with
        /// <see cref="MovementResult.ResolverBusy" />, because that would clear the affected-coordinate buffer
        /// the current subscribers are still reading.
        /// </para>
        /// <para>
        /// The Energy charge sits between validation and mutation: an illegal move is rejected before it can
        /// cost anything, and an application that fails after the charge is refunded, so a rejected move of any
        /// kind leaves the balance exactly where it was. The board reports the action and what the acting unit
        /// is worth and never learns the price — <see cref="IEnergyLedger" /> owns that.
        /// </para>
        /// </remarks>
        /// <param name="command">The requested move.</param>
        /// <returns>Success once the board has been mutated, or the specific reason the command was rejected.</returns>
        public MovementResult ResolveMove(in MoveCommand command)
        {
            using (_resolveMoveMarker.Auto())
            {
                if (_isResolvingMove)
                {
                    Debug.LogError(BoardLogMessages.MoveResolveReentered, this);
                    return MovementResult.ResolverBusy;
                }

                if (!TryGetHexGrid(out HexGrid grid))
                {
                    Debug.LogError(BoardLogMessages.GridPresenterMissing, this);
                    return MovementResult.BoardUnavailable;
                }

                if (!_unitCapabilities.TryGetValue(command.UnitId, out IMoveCapable capability))
                {
                    return MovementResult.UnitNotFound;
                }

                MovementResult validation = ValidateMove(grid, command, capability);

                if (validation != MovementResult.Success)
                {
                    return validation;
                }

                if (_energyLedger == null)
                {
                    return MovementResult.BoardUnavailable;
                }

                int unitEnergyCost = capability is IEnergyPriced priced ? priced.EnergyCost : BoardMetrics.DefaultUnitEnergyCost;

                if (!_energyLedger.TryPayForMove(command.PlayerId, command.Type, unitEnergyCost))
                {
                    return MovementResult.InsufficientEnergy;
                }

                _isResolvingMove = true;

                // Cleared only once the move is committed, so the refund below covers every way out of the block
                // — each early return and any exception that escapes it — rather than resting on the callee
                // catching its own. The ledger re-derives the price from the same arguments, so the board never
                // remembers an amount it is not allowed to compute; net change over a failed move is zero.
                bool isChargeOutstanding = true;

                try
                {
                    MovementResult resolution = ApplyValidatedMove(grid, command, capability);

                    if (resolution != MovementResult.Success)
                    {
                        return resolution;
                    }

                    isChargeOutstanding = false;

                    PublishMoveExecuted(command);
                }
                finally
                {
                    if (isChargeOutstanding)
                    {
                        _energyLedger.RefundMove(command.PlayerId, command.Type, unitEnergyCost);
                    }

                    _isResolvingMove = false;
                }

                return MovementResult.Success;
            }
        }

        private MovementResult ValidateMove(HexGrid grid, in MoveCommand command, IMoveCapable capability)
        {
            return command.Type switch
            {
                MoveType.Clone => MovementValidator.ValidateClone(grid, _activeUnits, command, capability),
                MoveType.Jump => MovementValidator.ValidateJump(grid, _activeUnits, command, capability),
                _ => MovementResult.InvalidCommand,
            };
        }

        private MovementResult ApplyValidatedMove(HexGrid grid, in MoveCommand command, IMoveCapable capability)
        {
            MovementResult resolution;
            GridUnit spawnedUnit;

            try
            {
                resolution = MovementResolver.Resolve(grid, _activeUnits, _unitSpawner, command, capability, _affectedCoordinates, out spawnedUnit);
            }
            catch (Exception exception)
            {
                // The command passed full validation a few lines above, so the resolver's own contract exceptions
                // are unreachable here and anything thrown came out of the spawner — before any mutation.
                LogSpawnFailure(command, exception);

                return MovementResult.SpawnFailed;
            }

            if (resolution != MovementResult.Success)
            {
                LogSpawnFailure(command, null);
                return resolution;
            }

            if (command.Type == MoveType.Jump)
            {
                _registeredPositions[command.UnitId] = command.Target;
            }

            if (spawnedUnit != null)
            {
                _unitCapabilities[spawnedUnit.UnitId] = capability;
                _registeredPositions[spawnedUnit.UnitId] = spawnedUnit.Position;
            }

            return MovementResult.Success;
        }

        private void PublishMoveExecuted(in MoveCommand command)
        {
            try
            {
                MatchEvents.RaiseMoveExecuted(command, _affectedCoordinatesView);
            }
            catch (Exception exception)
            {
                Debug.LogError(BoardLogMessages.MoveExecutedSubscriberFailed, this);
                Debug.LogException(exception, this);
            }
        }

        private void ReleaseRegisteredCell(HexGrid grid, int unitId)
        {
            if (!_registeredPositions.TryGetValue(unitId, out HexCoordinates position))
            {
                return;
            }

            if (grid.TryGetCell(position, out HexCell cell) && cell.OccupantUnitId == unitId)
            {
                cell.ClearOccupant();
            }
        }

        // PERF: latched until the spawner is replaced, so a broken one cannot allocate a formatted message and a
        // stack trace on every Clone the player attempts.
        private void LogSpawnFailure(in MoveCommand command, Exception exception)
        {
            if (_hasLoggedSpawnFailure)
            {
                return;
            }

            _hasLoggedSpawnFailure = true;
            Debug.LogError(string.Format(BoardLogMessages.UnitSpawnFailedFormat, command.PlayerId, command.Target), this);

            if (exception != null)
            {
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
