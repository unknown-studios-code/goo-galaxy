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
    /// <remarks>
    /// Applies an already-validated move to the board. Stateless and free of any engine dependency: the caller owns
    /// the affected-coordinate buffer, decides what to log, and keeps the entry point private to the Board assembly.
    /// <para>
    /// Only board rules are re-checked here, and violating one throws rather than corrupting grid or registry
    /// state. Ownership, unit status, card capability, and a Deploy's adjacency to owned territory are <b>not</b>
    /// re-checked, which is why resolution is internal: <c>UnitPresenter</c> runs the full
    /// <see cref="MovementValidator"/> first and is the only caller. Both move types that put a new unit on the
    /// board — Deploy and Clone — consult the spawner before mutating anything, so a spawner that throws leaves
    /// the board untouched and the exception is the caller's to report.
    /// </para>
    /// </remarks>
    internal static class MovementResolver
    {
        /// <remarks>
        /// Executes <paramref name="command" /> — an already-validated move — mutating <paramref name="grid" />,
        /// <paramref name="units" /> (a Deploy and a Clone add their new unit here), and the moved unit. Deploy puts
        /// a brand-new unit of <paramref name="cardId" /> on the target and touches no source; Clone leaves the
        /// source unit in place and puts a copy of it on the target; Jump relocates the existing unit, preserving
        /// its identity and runtime state. <paramref name="spawner" /> is consulted for Deploy and Clone only.
        /// <paramref name="cardId" /> is the card being deployed, and is read on the Deploy path alone — a Clone
        /// copies its source unit's own card and a Jump introduces no card, so both ignore it.
        /// <paramref name="capability" /> supplies the authored hex distance to re-check.
        /// <paramref name="affectedCoordinates" /> is a caller-owned buffer receiving the coordinates whose contents
        /// changed — the target alone for a Deploy or a Clone, source then target for a Jump — cleared on entry and
        /// left empty when the move is not applied. A Deploy vacates no hex, which downstream systems read as a fact
        /// rather than an omission. <paramref name="spawnedUnit" /> is the unit a Deploy or a Clone created, or null
        /// for a Jump and for any failure. Returns Success once the board has been mutated, or SpawnFailed when the
        /// spawner produced no usable unit. Throws <see cref="ArgumentNullException" /> when the affected-coordinate
        /// buffer is null, <see cref="InvalidOperationException" /> when the command does not match the current board
        /// state, and <see cref="ArgumentException" /> when the command carries an undefined move type.
        /// </remarks>
        internal static MovementResult Resolve(
            HexGrid grid,
            Dictionary<int, GridUnit> units,
            IUnitSpawner spawner,
            in MoveCommand command,
            CardId cardId,
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
            if (command.Type is not MoveType.Deploy and not MoveType.Clone and not MoveType.Jump)
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
                case MoveType.Deploy:
                    return ResolveDeploy(units, spawner, targetCell, command, cardId, affectedCoordinates, out spawnedUnit);
                case MoveType.Clone:
                    return ResolveClone(units, spawner, targetCell, sourceUnit, command, affectedCoordinates, out spawnedUnit);
                case MoveType.Jump:
                    ResolveJump(sourceCell, targetCell, sourceUnit, command, affectedCoordinates);
                    return MovementResult.Success;
                default:
                    throw new ArgumentException(FormatUnvalidatedMessage(command), nameof(command));
            }
        }

        private static MovementResult ResolveDeploy(
            Dictionary<int, GridUnit> units,
            IUnitSpawner spawner,
            HexCell targetCell,
            in MoveCommand command,
            CardId cardId,
            List<HexCoordinates> affectedCoordinates,
            out GridUnit spawnedUnit
        )
        {
            return SpawnOnTarget(units, spawner, targetCell, command, cardId, affectedCoordinates, out spawnedUnit);
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
            return SpawnOnTarget(units, spawner, targetCell, command, sourceUnit.CardId, affectedCoordinates, out spawnedUnit);
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

        // The spawner is consulted before anything is written, so a spawner that returns nothing — or throws —
        // leaves the grid, the registry and the affected-coordinate buffer exactly as it found them. Only the
        // target is reported: neither action vacates a hex, and the Volatile Mass fuse reads that absence as a
        // fact when it decides whether a landing armed an impact.
        private static MovementResult SpawnOnTarget(
            Dictionary<int, GridUnit> units,
            IUnitSpawner spawner,
            HexCell targetCell,
            in MoveCommand command,
            CardId cardId,
            List<HexCoordinates> affectedCoordinates,
            out GridUnit spawnedUnit
        )
        {
            spawnedUnit = null;

            if (spawner == null)
            {
                return MovementResult.SpawnFailed;
            }

            GridUnit newUnit = spawner.SpawnUnit(command.PlayerId, cardId, command.Target);

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

        private static string FormatUnvalidatedMessage(in MoveCommand command)
        {
            return string.Format(BoardLogMessages.MoveNotValidatedFormat, command.Type, command.Source, command.Target);
        }
    }
}
