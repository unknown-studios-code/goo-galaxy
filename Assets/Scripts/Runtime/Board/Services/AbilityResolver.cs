using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Services
{
    /// <remarks>
    /// Applies the impacts a card authored, once movement and standard conversion have finished — step 4 of the
    /// GDD's interaction resolution order. Stateless and free of any engine dependency: every buffer is
    /// caller-owned, nothing is logged, and the entry point stays internal so no assembly outside Board can
    /// call it — <c>AbilityController</c> is its only production caller, and the EditMode suite reaches it
    /// through <c>InternalsVisibleTo</c>.
    /// <para>
    /// Every card is expressible as authored data alone: the resolver dispatches on
    /// <see cref="ImpactEffectType"/> and reads the impact's own radius, duration, duration unit, filter, and
    /// cluster size, so no card needs a code path of its own. Volatile Mass's fuse is the sharpest case: both of
    /// the GDD's triggers for it fall out of one authored impact plus the context's
    /// <see cref="AbilityContext.HasVacatedHex"/>, and no code path here branches on the card's identity.
    /// </para>
    /// <para>
    /// Nothing throws for a card that is authored wrong and nothing aborts the impact loop: an impact the
    /// resolver cannot handle is skipped and the card's remaining impacts still resolve. Those conditions are
    /// reported through the <c>diagnostics</c> flags instead of a log, which keeps the resolver a pure function
    /// of board state — the presenter turns a set flag into a console message, and a test asserts on the flag
    /// without a log matcher.
    /// </para>
    /// <para>
    /// The resolver never removes a unit. A self-destruct impact only records the acting unit's id, because
    /// removal is step 6 self-cleanup and must happen after the ability event has been published — a subscriber
    /// reading the payload would otherwise be looking up units that are already gone from the registry. A fuse
    /// impact removes nothing either, and removes nothing later: it hands the unit to the fuse system, whose own
    /// ticker performs the removal seconds afterwards, outside any deployment.
    /// </para>
    /// Allocation-free on every non-throwing path once the caller's buffers are sized.
    /// </remarks>
    internal static class AbilityResolver
    {
        /// <remarks>
        /// Forwards to <see cref="AbilityTargetValidator.ValidateTargets" />, which owns the four rules and the
        /// reasoning behind them. Kept on the resolver so the controller and the EditMode suite reach the check
        /// through the type they already hold, and so the resolver and a caller choosing a cluster can never
        /// drift onto two different answers.
        /// </remarks>
        internal static bool ValidateTargets(IReadOnlyList<HexCoordinates> targets, ImpactEffect effect, HexGrid grid)
        {
            return AbilityTargetValidator.ValidateTargets(targets, effect, grid);
        }

        /// <remarks>
        /// Resolves every impact the acting card authored, in order, against the board described by
        /// <paramref name="context" /> — who is acting, where, what they vacated, what conversion just did, and, for a
        /// Protocol, the hexes the player picked; its lists are borrowed for the call and never retained.
        /// <paramref name="fuses" /> is the match's single fuse system — see <c>FuseController.Fuses</c>.
        /// <paramref name="areaBuffer" /> is caller-owned scratch overwritten per impact.
        /// <paramref name="affectedUnitIds" />, <paramref name="affectedHexes" /> and
        /// <paramref name="destroyedUnitIds" /> are caller-owned buffers cleared on entry: the first receives the
        /// units an impact conditioned, the second the coordinates whose hex state changed — the affected units'
        /// hexes plus any hex a hazard was spawned on — and the third the units a self-destruct impact marked for
        /// removal, which the caller removes after publishing. <paramref name="diagnostics" /> reports the authoring
        /// or state problems the resolution ran into, or <see cref="AbilityDiagnostic.None" />. Throws
        /// <see cref="ArgumentNullException" /> when the grid, the registry, the impact list, the status system, the
        /// fuse system, or any buffer is null.
        /// </remarks>
        internal static void Resolve(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            in AbilityContext context,
            IReadOnlyList<ImpactEffect> landingEffects,
            StatusEffectResolver statusEffects,
            FuseResolver fuses,
            List<HexCell> areaBuffer,
            List<int> affectedUnitIds,
            List<HexCoordinates> affectedHexes,
            List<int> destroyedUnitIds,
            out AbilityDiagnostic diagnostics
        )
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            if (landingEffects == null)
            {
                throw new ArgumentNullException(nameof(landingEffects));
            }

            if (statusEffects == null)
            {
                throw new ArgumentNullException(nameof(statusEffects));
            }

            if (fuses == null)
            {
                throw new ArgumentNullException(nameof(fuses));
            }

            if (areaBuffer == null)
            {
                throw new ArgumentNullException(nameof(areaBuffer));
            }

            if (affectedUnitIds == null)
            {
                throw new ArgumentNullException(nameof(affectedUnitIds));
            }

            if (affectedHexes == null)
            {
                throw new ArgumentNullException(nameof(affectedHexes));
            }

            if (destroyedUnitIds == null)
            {
                throw new ArgumentNullException(nameof(destroyedUnitIds));
            }

            diagnostics = AbilityDiagnostic.None;
            affectedUnitIds.Clear();
            affectedHexes.Clear();
            destroyedUnitIds.Clear();

            for (int i = 0; i < landingEffects.Count; i++)
            {
                ImpactEffect effect = landingEffects[i];

                switch (effect.Type)
                {
                    case ImpactEffectType.None:
                        break;
                    case ImpactEffectType.ApplyStatus:
                        if (HasExpectedDurationUnit(effect, ImpactDurationUnit.ActionWindows, ref diagnostics))
                        {
                            ApplyStatus(grid, units, context, effect, statusEffects, areaBuffer, affectedUnitIds, affectedHexes);
                        }

                        break;
                    case ImpactEffectType.SpawnHazard:
                        if (HasExpectedDurationUnit(effect, ImpactDurationUnit.ActionWindows, ref diagnostics))
                        {
                            SpawnHazard(grid, context, effect, affectedHexes, ref diagnostics);
                        }

                        break;
                    case ImpactEffectType.SelfDestruct:
                        SelfDestruct(units, context, destroyedUnitIds, ref diagnostics);
                        break;
                    case ImpactEffectType.ArmFuse:
                        if (HasExpectedDurationUnit(effect, ImpactDurationUnit.Seconds, ref diagnostics))
                        {
                            ArmFuse(units, context, effect, fuses, destroyedUnitIds, ref diagnostics);
                        }

                        break;
                    default:
                        diagnostics |= AbilityDiagnostic.UnknownEffectType;
                        break;
                }
            }
        }

        private static void ApplyStatus(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            in AbilityContext context,
            in ImpactEffect effect,
            StatusEffectResolver statusEffects,
            List<HexCell> areaBuffer,
            List<int> affectedUnitIds,
            List<HexCoordinates> affectedHexes
        )
        {
            if (effect.Status == StatusType.None || effect.Duration <= 0)
            {
                return;
            }

            GatherArea(grid, context, effect, areaBuffer);

            int appliedCount = 0;

            for (int i = 0; i < areaBuffer.Count; i++)
            {
                if (effect.ClusterSize > 0 && appliedCount >= effect.ClusterSize)
                {
                    return;
                }

                HexCell cell = areaBuffer[i];

                if (!cell.IsOccupied || !units.TryGetValue(cell.OccupantUnitId, out GridUnit unit) || unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (!IsTargeted(effect.Target, unit, context))
                {
                    continue;
                }

                statusEffects.ApplyStatus(unit, effect.Status, effect.Duration);

                // The output buffers are cleared once per deployment, not per impact, so a card with two status
                // impacts would otherwise report the same unit twice — breaking the payload's "at most once
                // each" contract and pushing both buffers past the single-impact area they are sized for. The
                // scan is linear because it is bounded by one impact area; a set would be another caller-owned
                // buffer the payload contract has to document.
                if (!affectedUnitIds.Contains(unit.UnitId))
                {
                    affectedUnitIds.Add(unit.UnitId);
                    affectedHexes.Add(cell.Coordinates);
                }

                // Counted even when the unit was already reported, so the cluster cap measures units this
                // impact acted on rather than units it happened to be the first to report.
                appliedCount++;
            }
        }

        // The one branch that separates a troop landing from a Protocol. A troop expands the impact's radius
        // around the hex it landed on; a Protocol was handed its hexes by the player and expands nothing, so
        // the radius has already done its work as a validation rule in ValidateTargets.
        private static void GatherArea(HexGrid grid, in AbilityContext context, in ImpactEffect effect, List<HexCell> areaBuffer)
        {
            if (!context.HasExplicitTargets)
            {
                grid.GetSpiralCells(context.OriginHex, effect.Radius, areaBuffer);
                return;
            }

            areaBuffer.Clear();
            IReadOnlyList<HexCoordinates> targets = context.TargetHexes;

            // A spell context with no targets resolves nothing. Falling through to the radius branch would
            // turn it into an area effect centred on the context's default origin, which is the middle of the
            // board — a Protocol must never expand a radius, whatever its target list holds.
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (grid.TryGetCell(targets[i], out HexCell cell))
                {
                    areaBuffer.Add(cell);
                }
            }
        }

        private static void SpawnHazard(
            HexGrid grid,
            in AbilityContext context,
            in ImpactEffect effect,
            List<HexCoordinates> affectedHexes,
            ref AbilityDiagnostic diagnostics
        )
        {
            // The GDD puts the corrosive trail on the hex the unit vacated, and only a Jump vacates one. A
            // Clone leaves its source occupied and a Protocol never had a hex to leave, so both are no-ops.
            // Spawning on the chosen hexes instead is deliberately NOT invented here: no card in the GDD asks
            // for it, and guessing the semantics now would lock in a rule a future card has to fight.
            if (!context.HasVacatedHex)
            {
                diagnostics |= AbilityDiagnostic.HazardWithoutVacatedHex;
                return;
            }

            if (effect.Duration <= 0 || !grid.TryGetCell(context.VacatedHex, out HexCell vacatedCell))
            {
                return;
            }

            if (vacatedCell.SetHazard(context.ActingPlayerId, effect.Duration))
            {
                diagnostics |= AbilityDiagnostic.HazardOverwritten;
            }

            affectedHexes.Add(context.VacatedHex);
        }

        private static void SelfDestruct(
            IReadOnlyDictionary<int, GridUnit> units,
            in AbilityContext context,
            List<int> destroyedUnitIds,
            ref AbilityDiagnostic diagnostics
        )
        {
            if (!context.HasActingUnit)
            {
                diagnostics |= AbilityDiagnostic.SelfDestructWithoutActingUnit;
                return;
            }

            if (!units.TryGetValue(context.ActingUnitId, out GridUnit actingUnit) || actingUnit == null || !actingUnit.IsAlive)
            {
                diagnostics |= AbilityDiagnostic.SelfDestructOnDeadUnit;
                return;
            }

            if (destroyedUnitIds.Contains(context.ActingUnitId))
            {
                return;
            }

            destroyedUnitIds.Add(context.ActingUnitId);
        }

        // The GDD gives a fuse two triggers, and the context already tells them apart without a card ever being
        // named here: HasVacatedHex is true only for a Jump, which is the deployment that detonates the bomb on
        // purpose. That path is a self-destruct in every respect — the Jump's own landing has already converted
        // at the card's authored radius by the time this runs, so all that is left is the removal, and it is
        // recorded rather than performed for the same reason every other removal is. Any other deployment leaves
        // the unit standing with the clock running, and the clock is the fuse system's business, not this one's.
        private static void ArmFuse(
            IReadOnlyDictionary<int, GridUnit> units,
            in AbilityContext context,
            in ImpactEffect effect,
            FuseResolver fuses,
            List<int> destroyedUnitIds,
            ref AbilityDiagnostic diagnostics
        )
        {
            if (!context.HasActingUnit)
            {
                diagnostics |= AbilityDiagnostic.FuseWithoutActingUnit;
                return;
            }

            if (context.HasVacatedHex)
            {
                SelfDestruct(units, context, destroyedUnitIds, ref diagnostics);
                return;
            }

            // A miss is not reported: the acting unit is read off the landing hex, so its absence is already the
            // "nothing landed" case the caller handles by passing no impacts at all. The fuse system rejects a
            // null or dead unit on its own, so this only has to avoid arming one that was never there.
            if (units.TryGetValue(context.ActingUnitId, out GridUnit actingUnit))
            {
                fuses.ArmFuse(actingUnit, effect.Duration);
            }
        }

        // Checked on every branch that reads a duration, not only on the new one. Action windows and seconds are
        // the same number wearing two incompatible clocks, so a status authored in seconds would silently become
        // that many deployments — plausible, wrong, and invisible. Skipping the one impact and reporting it is
        // the only honest answer, because nothing here can tell which of the two the designer meant.
        // SelfDestruct is absent on purpose: it carries no duration for a unit to disagree with.
        private static bool HasExpectedDurationUnit(in ImpactEffect effect, ImpactDurationUnit expected, ref AbilityDiagnostic diagnostics)
        {
            if (effect.DurationUnit == expected)
            {
                return true;
            }

            diagnostics |= AbilityDiagnostic.DurationUnitMismatch;

            return false;
        }

        private static bool IsTargeted(TargetFilter filter, GridUnit unit, in AbilityContext context)
        {
            return filter switch
            {
                TargetFilter.Self => context.HasActingUnit && unit.UnitId == context.ActingUnitId,
                TargetFilter.Enemy => unit.PlayerId != context.ActingPlayerId,
                TargetFilter.All => true,
                TargetFilter.Ally => unit.PlayerId == context.ActingPlayerId,
                TargetFilter.NewlyConverted => ContainsUnitId(context.Conversions.ConvertedUnitIds, unit.UnitId),
                _ => false,
            };
        }

        private static bool ContainsUnitId(IReadOnlyList<int> unitIds, int unitId)
        {
            if (unitIds == null)
            {
                return false;
            }

            for (int i = 0; i < unitIds.Count; i++)
            {
                if (unitIds[i] == unitId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
