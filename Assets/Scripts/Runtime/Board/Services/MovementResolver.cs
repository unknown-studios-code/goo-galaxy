using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <summary>
    /// Applies an already-validated move to the board. Stateless and free of any engine dependency: the
    /// caller owns the affected-coordinate buffer, decides what to log, and keeps the entry point private
    /// to the Board assembly.
    /// </summary>
    /// <remarks>
    /// Only board rules are re-checked here, and violating one throws rather than corrupting grid or registry
    /// state. Ownership, unit status, and card capability are <b>not</b> re-checked, which is why resolution is
    /// internal: <c>UnitPresenter</c> runs the full <see cref="MovementValidator"/> first and is the only caller.
    /// A Clone consults the spawner before mutating anything, so a spawner that throws leaves the board
    /// untouched and the exception is the caller's to report.
    /// </remarks>
    internal static class MovementResolver
    {
        /// <summary>
        /// Executes the command, mutating the grid, the unit registry, and the moved unit.
        /// Clone leaves the source untouched and asks the spawner for a new unit on the target;
        /// Jump relocates the existing unit, preserving its identity and runtime state.
        /// </summary>
        /// <param name="grid">The board to mutate.</param>
        /// <param name="units">The registry of live units, keyed by unit id. A Clone adds its new unit here.</param>
        /// <param name="spawner">The factory used to create the cloned unit. Only consulted for Clone.</param>
        /// <param name="command">The already-validated move.</param>
        /// <param name="capability">The moved unit's movement capability, supplying the authored hex distance to re-check.</param>
        /// <param name="affectedCoordinates">
        /// Caller-owned buffer that receives the coordinates whose contents changed: the target for a Clone,
        /// source then target for a Jump. Cleared on entry, and left empty when the move is not applied.
        /// </param>
        /// <param name="spawnedUnit">The unit a Clone created, or null for a Jump and for any failure.</param>
        /// <returns>Success once the board has been mutated, or SpawnFailed when the spawner produced no usable unit.</returns>
        /// <exception cref="ArgumentNullException">The affected-coordinate buffer is null.</exception>
        /// <exception cref="InvalidOperationException">The command does not match the current board state.</exception>
        /// <exception cref="ArgumentException">The command carries a move type other than Clone or Jump.</exception>
        internal static MovementResult Resolve(
            HexGrid grid,
            Dictionary<int, GridUnit> units,
            IUnitSpawner spawner,
            in MoveCommand command,
            IMoveCapable capability,
            List<HexCoordinates> affectedCoordinates,
            out GridUnit spawnedUnit
        )
        {
            if (affectedCoordinates == null)
            {
                throw new ArgumentNullException(nameof(affectedCoordinates));
            }

            spawnedUnit = null;
            affectedCoordinates.Clear();

            // Ahead of the board-state guard so an unsupported type reports what it actually is, rather than
            // the OutOfRange it would collect from authoring no distance.
            if (command.Type != MoveType.Clone && command.Type != MoveType.Jump)
            {
                throw new ArgumentException(FormatUnvalidatedMessage(command), nameof(command));
            }

            MovementResult boardState = MovementValidator.ValidateBoardState(
                grid,
                units,
                command,
                command.Type,
                capability,
                out HexCell sourceCell,
                out GridUnit sourceUnit,
                out HexCell targetCell
            );

            if (boardState != MovementResult.Success)
            {
                throw new InvalidOperationException(FormatUnvalidatedMessage(command));
            }

            switch (command.Type)
            {
                case MoveType.Clone:
                    return ResolveClone(units, spawner, targetCell, sourceUnit, command, affectedCoordinates, out spawnedUnit);
                case MoveType.Jump:
                    ResolveJump(sourceCell, targetCell, sourceUnit, command, affectedCoordinates);
                    return MovementResult.Success;
                default:
                    throw new ArgumentException(FormatUnvalidatedMessage(command), nameof(command));
            }
        }

        private static MovementResult ResolveClone(
            Dictionary<int, GridUnit> units,
            IUnitSpawner spawner,
            HexCell targetCell,
            GridUnit sourceUnit,
            in MoveCommand command,
            List<HexCoordinates> affectedCoordinates,
            out GridUnit spawnedUnit
        )
        {
            spawnedUnit = null;

            if (spawner == null)
            {
                return MovementResult.SpawnFailed;
            }

            GridUnit newUnit = spawner.SpawnUnit(command.PlayerId, sourceUnit.CardId, command.Target);

            if (newUnit == null || units.ContainsKey(newUnit.UnitId))
            {
                return MovementResult.SpawnFailed;
            }

            newUnit.Position = command.Target;
            units[newUnit.UnitId] = newUnit;
            targetCell.SetOccupant(newUnit.UnitId);
            spawnedUnit = newUnit;
            affectedCoordinates.Add(command.Target);

            return MovementResult.Success;
        }

        private static void ResolveJump(
            HexCell sourceCell,
            HexCell targetCell,
            GridUnit sourceUnit,
            in MoveCommand command,
            List<HexCoordinates> affectedCoordinates
        )
        {
            sourceCell.ClearOccupant();
            sourceUnit.Position = command.Target;
            targetCell.SetOccupant(sourceUnit.UnitId);

            affectedCoordinates.Add(command.Source);
            affectedCoordinates.Add(command.Target);
        }

        private static string FormatUnvalidatedMessage(in MoveCommand command)
        {
            return string.Format(BoardLogMessages.MoveNotValidatedFormat, command.Type, command.Source, command.Target);
        }
    }
}
