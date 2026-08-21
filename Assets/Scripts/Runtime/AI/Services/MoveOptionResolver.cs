using System.Collections.Generic;
using GooGalaxy.Runtime.AI.Models;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;

namespace GooGalaxy.Runtime.AI.Services
{
    /// <summary>
    /// Enumerates every action one player could legally take against the board as it stands right now — every
    /// Deploy, every Clone, every Jump, and one candidate cluster per Protocol in hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A client of the rules, never a second copy of them.</b> Legality is decided by
    /// <see cref="MovementValidator" /> and <see cref="AbilityTargetValidator" />, and affordability by
    /// <see cref="IEnergyLedger" />. Pricing is the one rule restated here rather than called: the
    /// <c>capability is IEnergyPriced</c> fallback is spelled out again so an unpriced unit is offered an action
    /// at exactly the price <c>UnitPresenter</c> will bill, and the two must be changed together. Everything else
    /// a rule change touches reaches this enumerator for free, and every action it offers is one a human tap
    /// could have made. The occupancy and adjacency filters it applies before calling a validator are a
    /// performance shortcut; the validator still has the last word on everything it hands back.
    /// </para>
    /// <para>
    /// <b>Where the randomness enters, and why it has to.</b> A Protocol's cluster cannot be enumerated — the
    /// authored cluster size and radius admit far too many, and <see cref="AbilityTargetValidator" /> does not
    /// read occupancy, so nearly every one of them is legal. A candidate is therefore constructed rather than
    /// searched for: a centre drawn uniformly from the board, then the rest of the cluster drawn from the cells
    /// within the authored radius of it. The generator arrives by <c>ref</c> from the caller rather than being
    /// held here, which is what keeps this class stateless and the construction reproducible — the same board,
    /// hand and generator position always produce the same clusters. Draw from
    /// <see cref="TargetStreamId" /> so this construction cannot correlate with the deck's shuffle.
    /// </para>
    /// <para>
    /// <b>Engine-free.</b> No <c>UnityEngine</c> type appears in the signature or the body, so a fixture drives
    /// the whole enumeration in EditMode against a hand-built grid.
    /// </para>
    /// <para>
    /// No LINQ, no allocation on any path after the buffers are built, and no <c>foreach</c> through an
    /// interface. The whole-board pass binds <c>HexGrid.CellValues</c>' struct enumerator directly.
    /// </para>
    /// </remarks>
    public static class MoveOptionResolver
    {
        /// <summary>
        /// The machine player's non-selection stream, kept apart from the deck's so the two cannot correlate.
        /// Candidate Protocol clusters are drawn from it here; its holder also draws the think interval and the
        /// discard slot from the same generator, so those three share one sequence by design.
        /// </summary>
        /// <remarks>Negative on purpose — see <see cref="Xorshift32.DeriveSeed" /> for why, and for the rule that every stream id is distinct.</remarks>
        public const int TargetStreamId = -2;

        /// <summary>Derives this enumerator's stream seed from the seed both peers agreed on.</summary>
        /// <param name="matchSeed">The match's shared seed, or the value authored to override it.</param>
        /// <returns>The seed to construct the generator passed to <see cref="Resolve" /> with.</returns>
        public static int DeriveSeed(int matchSeed)
        {
            return Xorshift32.DeriveSeed(matchSeed, TargetStreamId);
        }

        /// <summary>
        /// Fills <paramref name="results" /> with every action <paramref name="playerId" /> could legally take
        /// against the board right now.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The results list is cleared on entry. On any path that reaches the enumeration, every buffer is
        /// cleared too, which invalidates the cluster any Protocol option from the previous pass borrowed — see
        /// <see cref="MoveOption.TargetCluster" />. An empty board, an unaffordable hand and a player with no
        /// units each yield an empty list rather than an exception; a missing grid, registry, ledger or buffer
        /// set does the same, and returns without touching the buffers at all.
        /// </para>
        /// <para>
        /// The Deploy footprint is computed once and then filtered by affordability, because the footprint does
        /// not vary by card: scanning it per card would repeat the same whole-board pass once per hand slot.
        /// Clone and Jump targets come from the <b>ring</b> at the capability's authored distance, never the
        /// spiral, because those distances are exact rather than maximums.
        /// </para>
        /// <para>
        /// A Protocol contributes at most one option: one cluster is constructed for it and validated against
        /// <b>every</b> one of the card's impacts, so a card whose impacts disagree on cluster size or radius
        /// contributes nothing. That is an authoring fault the validator already rejects, and reconciling the two
        /// here would offer an action the board then refuses. A cluster that cannot be filled from the board
        /// contributes nothing either, never a short one.
        /// </para>
        /// </remarks>
        /// <param name="playerId">The player the actions are enumerated for. A unit owned by anyone else is skipped.</param>
        /// <param name="grid">The board being played on.</param>
        /// <param name="units">The registry of live units, keyed by unit id, as the validators read it.</param>
        /// <param name="capabilities">
        /// The movement capability registered for each live unit, keyed by unit id. A unit missing from it, or
        /// registered with none, contributes no Clone and no Jump.
        /// </param>
        /// <param name="handCards">
        /// The acting player's hand, indexed by slot, with a null entry for a slot whose card the registry does
        /// not know. May be null, in which case nothing is played from hand.
        /// </param>
        /// <param name="energyLedger">The ledger every affordability question is put to.</param>
        /// <param name="random">
        /// The generator candidate Protocol clusters are drawn from, advanced in place. Shared with the caller's
        /// other draws — see <see cref="TargetStreamId" /> for which stream it must be on.
        /// </param>
        /// <param name="buffers">Caller-owned scratch space, cleared on entry. The clusters it hands out are borrowed.</param>
        /// <param name="results">Caller-owned buffer receiving the options. Cleared on entry.</param>
        public static void Resolve(
            int playerId,
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            IReadOnlyDictionary<int, IMoveCapable> capabilities,
            IReadOnlyList<CardDefinition> handCards,
            IEnergyLedger energyLedger,
            ref Xorshift32 random,
            MoveOptionBuffers buffers,
            List<MoveOption> results
        )
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            if (grid == null || units == null || capabilities == null || buffers == null || UnityReference.IsUnavailable(energyLedger))
            {
                return;
            }

            buffers.Reset();

            ScanBoard(playerId, grid, units, buffers);
            AddDeployOptions(playerId, grid, units, handCards, energyLedger, buffers, results);
            AddUnitMoveOptions(playerId, grid, units, capabilities, energyLedger, buffers, results);
            AddProtocolOptions(playerId, grid, handCards, energyLedger, ref random, buffers, results);
        }

        // One pass over the board answers three questions at once: which hexes a Protocol may centre on, which
        // units belong to the acting player, and which empty hexes sit next to one of them. Ownership is read
        // from the board's own occupancy rather than from the unit registry, so a unit the grid has already let
        // go can never contribute an option.
        private static void ScanBoard(int playerId, HexGrid grid, IReadOnlyDictionary<int, GridUnit> units, MoveOptionBuffers buffers)
        {
            foreach (HexCell cell in grid.CellValues)
            {
                // A blocked hex is still a hex the ability validator accepts as a Protocol target: it checks the
                // board's coordinate set and never reads passability.
                buffers.BoardCoordinates.Add(cell.Coordinates);

                if (cell.IsBlocked)
                {
                    continue;
                }

                if (cell.IsOccupied)
                {
                    if (units.TryGetValue(cell.OccupantUnitId, out GridUnit unit) && unit.IsAlive && unit.PlayerId == playerId)
                    {
                        buffers.OwnedUnits.Add(unit);
                    }

                    continue;
                }

                if (IsAdjacentToOwnedUnit(grid, units, cell.Coordinates, playerId, buffers.CellScratch))
                {
                    buffers.DeployFootprint.Add(cell.Coordinates);
                }
            }
        }

        private static bool IsAdjacentToOwnedUnit(
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            HexCoordinates coordinates,
            int playerId,
            List<HexCell> scratch
        )
        {
            grid.GetNeighbors(coordinates, scratch);

            for (int i = 0; i < scratch.Count; i++)
            {
                HexCell neighbor = scratch[i];

                if (!neighbor.IsOccupied)
                {
                    continue;
                }

                if (units.TryGetValue(neighbor.OccupantUnitId, out GridUnit unit) && unit.PlayerId == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddDeployOptions(
            int playerId,
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            IReadOnlyList<CardDefinition> handCards,
            IEnergyLedger energyLedger,
            MoveOptionBuffers buffers,
            List<MoveOption> results
        )
        {
            if (handCards == null || buffers.DeployFootprint.Count == 0)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < handCards.Count; slotIndex++)
            {
                CardDefinition card = handCards[slotIndex];

                if (card == null || card.Type == CardType.Spell)
                {
                    continue;
                }

                if (!energyLedger.CanAffordMove(playerId, MoveType.Deploy, card.EnergyCost))
                {
                    continue;
                }

                for (int i = 0; i < buffers.DeployFootprint.Count; i++)
                {
                    var command = MoveCommand.ForDeploy(buffers.DeployFootprint[i], playerId);

                    if (MovementValidator.ValidateDeploy(grid, units, in command, card) == MovementResult.Success)
                    {
                        results.Add(MoveOption.ForDeploy(slotIndex, command.Target));
                    }
                }
            }
        }

        private static void AddUnitMoveOptions(
            int playerId,
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            IReadOnlyDictionary<int, IMoveCapable> capabilities,
            IEnergyLedger energyLedger,
            MoveOptionBuffers buffers,
            List<MoveOption> results
        )
        {
            for (int i = 0; i < buffers.OwnedUnits.Count; i++)
            {
                GridUnit unit = buffers.OwnedUnits[i];

                if (!capabilities.TryGetValue(unit.UnitId, out IMoveCapable capability) || capability == null)
                {
                    continue;
                }

                // The same fallback the board applies when it charges the move, so an unpriced unit is offered
                // an action at exactly the price it will be billed.
                int unitEnergyCost = capability is IEnergyPriced priced ? priced.EnergyCost : BoardMetrics.DefaultUnitEnergyCost;

                if (capability.CanClone && energyLedger.CanAffordMove(playerId, MoveType.Clone, unitEnergyCost))
                {
                    AddRingMoveOptions(playerId, grid, units, unit, capability, MoveType.Clone, capability.CloneDistance, buffers.CellScratch, results);
                }

                if (capability.CanJump && energyLedger.CanAffordMove(playerId, MoveType.Jump, unitEnergyCost))
                {
                    AddRingMoveOptions(playerId, grid, units, unit, capability, MoveType.Jump, capability.JumpDistance, buffers.CellScratch, results);
                }
            }
        }

        private static void AddRingMoveOptions(
            int playerId,
            HexGrid grid,
            IReadOnlyDictionary<int, GridUnit> units,
            GridUnit unit,
            IMoveCapable capability,
            MoveType moveType,
            int distance,
            List<HexCell> scratch,
            List<MoveOption> results
        )
        {
            grid.GetRingCells(unit.Position, distance, scratch);

            for (int i = 0; i < scratch.Count; i++)
            {
                HexCoordinates target = scratch[i].Coordinates;
                var command = new MoveCommand(moveType, unit.Position, target, playerId, unit.UnitId);

                MovementResult validation =
                    moveType == MoveType.Clone
                        ? MovementValidator.ValidateClone(grid, units, in command, capability)
                        : MovementValidator.ValidateJump(grid, units, in command, capability);

                if (validation != MovementResult.Success)
                {
                    continue;
                }

                results.Add(
                    moveType == MoveType.Clone
                        ? MoveOption.ForClone(unit.UnitId, unit.Position, target)
                        : MoveOption.ForJump(unit.UnitId, unit.Position, target)
                );
            }
        }

        private static void AddProtocolOptions(
            int playerId,
            HexGrid grid,
            IReadOnlyList<CardDefinition> handCards,
            IEnergyLedger energyLedger,
            ref Xorshift32 random,
            MoveOptionBuffers buffers,
            List<MoveOption> results
        )
        {
            if (handCards == null || buffers.BoardCoordinates.Count == 0)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < handCards.Count; slotIndex++)
            {
                CardDefinition card = handCards[slotIndex];

                if (card == null || card.Type != CardType.Spell || card.LandingEffects.Count == 0)
                {
                    continue;
                }

                // Priced as a Deploy, which is what DeployController charges a Protocol at.
                if (!energyLedger.CanAffordMove(playerId, MoveType.Deploy, card.EnergyCost))
                {
                    continue;
                }

                List<HexCoordinates> cluster = buffers.GetClusterBuffer(slotIndex);

                if (!TryBuildCluster(grid, card.LandingEffects[0], ref random, buffers, cluster))
                {
                    continue;
                }

                if (!AreTargetsValidForEveryImpact(cluster, card.LandingEffects, grid))
                {
                    cluster.Clear();
                    continue;
                }

                results.Add(MoveOption.ForProtocol(slotIndex, card.CardId, cluster));
            }
        }

        // Built from the first impact and then checked against all of them, matching what AbilityController does
        // before it resolves: every impact must accept the cluster, so building from one and validating against
        // the rest is the only construction that cannot offer an action the board refuses.
        private static bool TryBuildCluster(HexGrid grid, ImpactEffect effect, ref Xorshift32 random, MoveOptionBuffers buffers, List<HexCoordinates> cluster)
        {
            cluster.Clear();

            if (effect.ClusterSize <= 0)
            {
                return false;
            }

            HexCoordinates centre = buffers.BoardCoordinates[random.NextIndex(buffers.BoardCoordinates.Count)];

            cluster.Add(centre);

            int remaining = effect.ClusterSize - 1;

            if (remaining == 0)
            {
                return true;
            }

            grid.GetSpiralCells(centre, effect.Radius, buffers.CellScratch);
            buffers.ClusterCandidates.Clear();

            for (int i = 0; i < buffers.CellScratch.Count; i++)
            {
                HexCoordinates candidate = buffers.CellScratch[i].Coordinates;

                if (candidate != centre)
                {
                    buffers.ClusterCandidates.Add(candidate);
                }
            }

            if (buffers.ClusterCandidates.Count < remaining)
            {
                cluster.Clear();

                return false;
            }

            // Draw without replacement by swapping the drawn entry with the last one and shortening the list,
            // which is a Fisher-Yates step: it cannot repeat a hex, and the ability validator rejects a cluster
            // that does.
            for (int i = 0; i < remaining; i++)
            {
                int candidateCount = buffers.ClusterCandidates.Count;
                int drawnIndex = random.NextIndex(candidateCount);

                cluster.Add(buffers.ClusterCandidates[drawnIndex]);

                buffers.ClusterCandidates[drawnIndex] = buffers.ClusterCandidates[candidateCount - 1];
                buffers.ClusterCandidates.RemoveAt(candidateCount - 1);
            }

            return true;
        }

        private static bool AreTargetsValidForEveryImpact(IReadOnlyList<HexCoordinates> cluster, IReadOnlyList<ImpactEffect> effects, HexGrid grid)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (!AbilityTargetValidator.ValidateTargets(cluster, effects[i], grid))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
