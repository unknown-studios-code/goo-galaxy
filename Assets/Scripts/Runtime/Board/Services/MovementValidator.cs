using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <summary>
    /// Stateless legality checks for Clone and Jump moves.
    /// Deterministic and allocation-free: the same board state and command always produce the same
    /// result code, and no collection, string, or boxed value is created on any path.
    /// </summary>
    /// <remarks>
    /// The commanded unit is resolved from <c>MoveCommand.UnitId</c> against the supplied registry and
    /// cross-checked against the source cell's occupant, so the grid and the registry can never drift
    /// unnoticed. Callers must supply a non-null grid and registry.
    /// Checks run in a fixed order so the returned code is predictable when several rules are broken at once:
    /// source presence, unit identity, ownership, status, capability, range, target passability, target vacancy,
    /// target hazard. The hazard check is last because it is the only target rule that depends on the moving
    /// unit rather than on the board alone — the same reason ownership and capability sit after source presence.
    /// A cell that is both occupied and hazardous therefore reports <c>TargetOccupied</c>, the reason that holds
    /// for every unit.
    /// </remarks>
    public static class MovementValidator
    {
        /// <summary>
        /// Validates a Clone: an adjacent (distance 1) duplication that leaves the source unit in place.
        /// </summary>
        /// <param name="grid">The board being played on.</param>
        /// <param name="units">The registry of live units, keyed by unit id.</param>
        /// <param name="command">The requested move.</param>
        /// <param name="capability">The commanded unit's movement capability, typically its card definition.</param>
        /// <returns>Success, or the first rule the command violates.</returns>
        public static MovementResult ValidateClone(HexGrid grid, IReadOnlyDictionary<int, GridUnit> units, in MoveCommand command, IMoveCapable capability)
        {
            return Validate(grid, units, command, capability, MoveType.Clone);
        }

        /// <summary>
        /// Validates a Jump: a distance 2 relocation of the source unit itself.
        /// </summary>
        /// <param name="grid">The board being played on.</param>
        /// <param name="units">The registry of live units, keyed by unit id.</param>
        /// <param name="command">The requested move.</param>
        /// <param name="capability">The commanded unit's movement capability, typically its card definition.</param>
        /// <returns>Success, or the first rule the command violates.</returns>
        public static MovementResult ValidateJump(HexGrid grid, IReadOnlyDictionary<int, GridUnit> units, in MoveCommand command, IMoveCapable capability)
        {
            return Validate(grid, units, command, capability, MoveType.Jump);
        }

        /// <summary>
        /// Validates every rule that depends only on the board: source presence, unit identity, range, and
        /// target passability and vacancy. Ownership, capability, and the target hazard rule are excluded
        /// because they describe the commanding player and its unit rather than the board.
        /// </summary>
        /// <remarks>
        /// Shared with <see cref="MovementResolver"/> so its pre-mutation guards cannot drift away from the
        /// rules enforced here.
        /// <para>
        /// The hazard rule is deliberately <b>not</b> re-checked. It is capability-relative, and re-checking it
        /// without the capability would reject the one unit type the rule exists to exempt: a Hover unit that
        /// full validation legally cleared onto a hazardous hex would then fail the resolver's pre-mutation
        /// guard and throw over a perfectly legal move. A non-Hover unit can never reach the resolver with a
        /// hazardous target, because <see cref="ValidateClone"/> and <see cref="ValidateJump"/> already
        /// rejected it, so nothing is lost by leaving the check out.
        /// </para>
        /// </remarks>
        /// <param name="grid">The board being played on.</param>
        /// <param name="units">The registry of live units, keyed by unit id.</param>
        /// <param name="command">The requested move.</param>
        /// <param name="moveType">The move type whose hex distance the command must match.</param>
        /// <param name="sourceCell">The source cell, set when the result is Success.</param>
        /// <param name="sourceUnit">The commanded unit, set when the result is Success.</param>
        /// <param name="targetCell">The target cell, set when the result is Success.</param>
        /// <returns>Success, or the first board rule the command violates.</returns>
        internal static MovementResult ValidateBoardState(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            in MoveCommand command,
            MoveType moveType,
            out HexCell sourceCell,
            out GridUnit sourceUnit,
            out HexCell targetCell
        )
        {
            targetCell = null;
            MovementResult sourceResult = ValidateSource(grid, units, command, out sourceCell, out sourceUnit);

            if (sourceResult != MovementResult.Success)
            {
                return sourceResult;
            }

            return ValidateTarget(grid, command, moveType, null, out targetCell);
        }

        private static MovementResult Validate(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            in MoveCommand command,
            IMoveCapable capability,
            MoveType moveType
        )
        {
            MovementResult sourceResult = ValidateSource(grid, units, command, out _, out GridUnit sourceUnit);

            if (sourceResult != MovementResult.Success)
            {
                return sourceResult;
            }

            if (sourceUnit.PlayerId != command.PlayerId)
            {
                return MovementResult.SourceNotOwned;
            }

            if (sourceUnit.IsFrozen)
            {
                return MovementResult.SourceFrozen;
            }

            if (!IsMovePermitted(capability, moveType))
            {
                return MovementResult.CapabilityMissing;
            }

            return ValidateTarget(grid, command, moveType, capability, out _);
        }

        private static MovementResult ValidateSource(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            in MoveCommand command,
            out HexCell sourceCell,
            out GridUnit sourceUnit
        )
        {
            sourceUnit = null;

            if (!grid.TryGetCell(command.Source, out sourceCell) || !sourceCell.IsOccupied)
            {
                return MovementResult.SourceEmpty;
            }

            if (sourceCell.OccupantUnitId != command.UnitId || !units.TryGetValue(command.UnitId, out sourceUnit))
            {
                return MovementResult.UnitNotFound;
            }

            return MovementResult.Success;
        }

        // A null capability means the caller is checking board rules only, so the hazard rule — the one
        // target rule that depends on the moving unit — is skipped along with ownership and capability.
        private static MovementResult ValidateTarget(HexGrid grid, in MoveCommand command, MoveType moveType, IMoveCapable capability, out HexCell targetCell)
        {
            targetCell = null;

            if (command.Source.CalculateDistance(command.Target) != (int)moveType)
            {
                return MovementResult.OutOfRange;
            }

            if (!grid.TryGetCell(command.Target, out targetCell) || targetCell.IsBlocked)
            {
                return MovementResult.TargetBlocked;
            }

            if (targetCell.IsOccupied)
            {
                return MovementResult.TargetOccupied;
            }

            if (capability != null && !capability.IgnoresHazards && targetCell.HasHazard)
            {
                return MovementResult.TargetHazardous;
            }

            return MovementResult.Success;
        }

        private static bool IsMovePermitted(IMoveCapable capability, MoveType moveType)
        {
            if (capability == null)
            {
                return false;
            }

            return moveType == MoveType.Clone ? capability.CanClone : capability.CanJump;
        }
    }
}
