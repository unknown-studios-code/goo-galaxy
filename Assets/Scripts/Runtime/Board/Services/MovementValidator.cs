using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <summary>
    /// Stateless legality checks for Deploy, Clone and Jump moves.
    /// Deterministic and allocation-free: the same board state and command always produce the same
    /// result code, and no collection, string, or boxed value is created on any path.
    /// </summary>
    /// <remarks>
    /// For a Clone and a Jump the commanded unit is resolved from <c>MoveCommand.UnitId</c> against the supplied
    /// registry and cross-checked against the source cell's occupant, so the grid and the registry can never
    /// drift unnoticed. Callers must supply a non-null grid and registry.
    /// Checks run in a fixed order so the returned code is predictable when several rules are broken at once:
    /// source presence, unit identity, ownership, status, capability, range, target passability, target vacancy,
    /// target hazard. The hazard check is last because it is the only target rule that depends on the moving
    /// unit rather than on the board alone — the same reason ownership and capability sit after source presence.
    /// A cell that is both occupied and hazardous therefore reports <c>TargetOccupied</c>, the reason that holds
    /// for every unit.
    /// <para>
    /// A Deploy reads none of those source rules — see <see cref="ValidateDeploy" /> for its own order. It still
    /// carries a source: <c>MoveCommand.ForDeploy</c> sets it equal to the target, so the shared range check
    /// measures zero and passes.
    /// </para>
    /// </remarks>
    public static class MovementValidator
    {
        private const int NoAuthoredDistance = 0;

        /// <summary>
        /// Validates a Clone: a duplication that leaves the source unit in place, over the exact hex distance
        /// the capability authors.
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
        /// Validates a Jump: a relocation of the source unit itself, over the exact hex distance the capability
        /// authors.
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
        /// Validates a Deploy: a brand-new unit of the played card's type placed on an empty hex next to
        /// territory the acting player already holds.
        /// </summary>
        /// <remarks>
        /// There is no range check and <c>MoveCommand.Source</c> is never read — a Deploy has no source unit to
        /// measure from, which is also why no ownership, status, or identity rule applies. The card's own
        /// capability is required all the same, because it is what decides whether the target's hazard bars it.
        /// <para>
        /// Checks run in a fixed order, so the returned code is predictable when several rules are broken at
        /// once: capability presence, target passability, target vacancy, target hazard, then adjacency to owned
        /// territory. Adjacency is last for the same reason the hazard check is last on the other two paths — it
        /// is the only rule here that depends on the acting <i>player</i> rather than on the board alone, so a
        /// hex that is both occupied and outside the player's territory reports <c>TargetOccupied</c>, the
        /// reason that holds for every player.
        /// </para>
        /// <para>
        /// Allocation-free, and contractually so: adjacency walks the six directions over a span and reads the
        /// grid one cell at a time, rather than gathering neighbours into a buffer this stateless class has
        /// nowhere to keep.
        /// </para>
        /// </remarks>
        /// <param name="grid">The board being played on.</param>
        /// <param name="units">The registry of live units, keyed by unit id.</param>
        /// <param name="command">The requested Deploy, built with <c>MoveCommand.ForDeploy</c>.</param>
        /// <param name="capability">The played card's capability. A null one is rejected outright.</param>
        /// <returns>Success, or the first rule the command violates.</returns>
        public static MovementResult ValidateDeploy(HexGrid grid, IReadOnlyDictionary<int, GridUnit> units, in MoveCommand command, IMoveCapable capability)
        {
            if (capability == null)
            {
                return MovementResult.CapabilityMissing;
            }

            if (!grid.TryGetCell(command.Target, out HexCell targetCell) || targetCell.IsBlocked)
            {
                return MovementResult.TargetBlocked;
            }

            if (targetCell.IsOccupied)
            {
                return MovementResult.TargetOccupied;
            }

            if (targetCell.HasHazard && !capability.CanIgnoreHazards)
            {
                return MovementResult.TargetHazardous;
            }

            if (!IsAdjacentToOwnedUnit(grid, units, command.Target, command.PlayerId))
            {
                return MovementResult.NotAdjacentToOwnedTerritory;
            }

            return MovementResult.Success;
        }

        /// <remarks>
        /// Validates every rule that depends only on the board: source presence, unit identity, range, and
        /// target passability and vacancy. Ownership, capability, and the target hazard rule are excluded
        /// because they describe the commanding player and its unit rather than the board.
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
        /// <para>
        /// A Deploy skips the source rules entirely and leaves <paramref name="sourceCell" /> and
        /// <paramref name="sourceUnit" /> null, because it acts with no source unit: demanding an occupied
        /// source holding <c>command.UnitId</c> would reject every legal Deploy. The range check still runs and
        /// still passes — <c>MoveCommand.ForDeploy</c> sets the source equal to the target, which measures zero,
        /// and <see cref="GetRequiredDistance" /> answers zero for a type that authors no distance. Its adjacency
        /// to owned territory is left out for the same reason as the hazard rule above: it depends on the acting
        /// player rather than on the board, and <see cref="ValidateDeploy" /> has already enforced it.
        /// </para>
        /// </remarks>
        internal static MovementResult ValidateBoardState(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            in MoveCommand command,
            MoveType moveType,
            IMoveCapable capability,
            out HexCell sourceCell,
            out GridUnit sourceUnit,
            out HexCell targetCell
        )
        {
            targetCell = null;
            sourceCell = null;
            sourceUnit = null;

            if (moveType != MoveType.Deploy)
            {
                MovementResult sourceResult = ValidateSource(grid, units, command, out sourceCell, out sourceUnit);

                if (sourceResult != MovementResult.Success)
                {
                    return sourceResult;
                }
            }

            return ValidateTarget(grid, command, GetRequiredDistance(capability, moveType), capability: null, out targetCell);
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

            return ValidateTarget(grid, command, GetRequiredDistance(capability, moveType), capability, out _);
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

        private static MovementResult ValidateTarget(
            HexGrid grid,
            in MoveCommand command,
            int requiredDistance,
            IMoveCapable capability,
            out HexCell targetCell
        )
        {
            targetCell = null;

            if (command.Source.CalculateDistance(command.Target) != requiredDistance)
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

            if (capability != null && !capability.CanIgnoreHazards && targetCell.HasHazard)
            {
                return MovementResult.TargetHazardous;
            }

            return MovementResult.Success;
        }

        // PERF: walks the six directions over the shared span and probes the grid one coordinate at a time,
        // rather than calling HexGrid.GetNeighbors — that needs a List<HexCell> buffer, and a stateless class
        // has nowhere to keep one without allocating a fresh list on every validated Deploy.
        private static bool IsAdjacentToOwnedUnit(HexGrid grid, IReadOnlyDictionary<int, GridUnit> units, HexCoordinates target, int playerId)
        {
            ReadOnlySpan<HexCoordinates> directions = HexDirection.All;

            for (int i = 0; i < directions.Length; i++)
            {
                if (!grid.TryGetCell(target.GetNeighbor(directions[i]), out HexCell neighborCell) || !neighborCell.IsOccupied)
                {
                    continue;
                }

                if (units.TryGetValue(neighborCell.OccupantUnitId, out GridUnit neighborUnit) && neighborUnit.PlayerId == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMovePermitted(IMoveCapable capability, MoveType moveType)
        {
            if (capability == null)
            {
                return false;
            }

            return moveType switch
            {
                MoveType.Clone => capability.CanClone,
                MoveType.Jump => capability.CanJump,
                _ => false,
            };
        }

        private static int GetRequiredDistance(IMoveCapable capability, MoveType moveType)
        {
            if (capability == null)
            {
                return NoAuthoredDistance;
            }

            return moveType switch
            {
                MoveType.Clone => capability.CloneDistance,
                MoveType.Jump => capability.JumpDistance,
                _ => NoAuthoredDistance,
            };
        }
    }
}
