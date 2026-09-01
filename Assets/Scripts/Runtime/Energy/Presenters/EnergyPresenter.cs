using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Services;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Energy.Presenters
{
    /// <summary>
    /// Presenter that orchestrates the real-time energy systems of active players in a match.
    /// </summary>
    /// <remarks>
    /// It is also the board's <see cref="IEnergyLedger"/> and the deck's <see cref="IDiscardLedger"/>. No caller
    /// computes a price: a move reports which action and what the acting unit is worth, a discard reports only
    /// who is acting, and both prices are resolved here against that player's own configuration, since the two
    /// players are configured independently.
    /// </remarks>
    public class EnergyPresenter : MonoBehaviour, IEnergyLedger, IDiscardLedger
    {
        // PERF: the smallest balance change worth broadcasting. Regeneration moves roughly 0.006 per frame at the
        // authored rate, so a per-frame epsilon suppresses nothing and every frame reaches every subscriber. This
        // is compared against the last published value instead, so a slow drift still crosses it.
        private const float EnergyPublishQuantum = 0.05f;

        [Header("Match Setup")]
        [Tooltip(
            "The starting energy configuration for Player 1.\n"
                + "MaxEnergy is the ceiling, RegenRate is energy per second, and StartingEnergy is the initial amount."
        )]
        [SerializeField]
        private EnergyConfig _playerOneConfig = new(10f, 1f / 2.8f, 5f);

        [Tooltip(
            "The starting energy configuration for Player 2.\n"
                + "MaxEnergy is the ceiling, RegenRate is energy per second, and StartingEnergy is the initial amount. "
                + "Keep StartingEnergy equal to Player 1 on symmetric maps; Komi applies only to deliberately asymmetric ones."
        )]
        [SerializeField]
        private EnergyConfig _playerTwoConfig = new(10f, 1f / 2.8f, 5f);

        private readonly Dictionary<int, EnergyState> _playerStates = new();

        private MatchPhase _phase = MatchPhase.None;

        // Regeneration is a property of play being open, not of a state existing. InitializeMatch seeds both
        // players during Loading, several seconds before the countdown ends, so without this gate both start the
        // match above their authored StartingEnergy by however long setup and the countdown took.
        private bool IsRegenerationOpen => _phase is MatchPhase.Standard or MatchPhase.Overtime;

        protected void OnEnable()
        {
            MatchEvents.MatchPhaseChanged += HandleMatchPhaseChanged;
        }

        protected void Update()
        {
            bool isRegenerating = IsRegenerationOpen;
            float deltaTime = Time.deltaTime;

            foreach (KeyValuePair<int, EnergyState> kvp in _playerStates)
            {
                EnergyState state = kvp.Value;

                if (isRegenerating)
                {
                    float newEnergy = EnergyRegenerator.Tick(state.CurrentEnergy, deltaTime, state.EffectiveRegenRate, state.Config.MaxEnergy);

                    if (newEnergy != state.CurrentEnergy)
                    {
                        state.SetEnergy(newEnergy);
                    }
                }

                // Flushed even while regeneration is closed: a spend refunded as a match ends still has a
                // publication owed, and stranding it would leave the HUD showing a balance the ledger does not
                // hold. Nothing regenerates on this path, so the flush only ever reports what a caller did.
                FlushPendingPublications(kvp.Key, state);
            }
        }

        protected void OnDisable()
        {
            MatchEvents.MatchPhaseChanged -= HandleMatchPhaseChanged;
        }

        /// <summary>
        /// Initializes both players' energy states from the serialized match setup configurations.
        /// Must be called explicitly by match bootstrap logic once a match starts; not invoked automatically.
        /// </summary>
        public void InitializeMatch()
        {
            InitializePlayer(1, _playerOneConfig);
            InitializePlayer(2, _playerTwoConfig);
        }

        /// <summary>
        /// Initializes the energy state for a specific player with their corresponding configuration.
        /// </summary>
        /// <param name="playerId">The unique ID of the player.</param>
        /// <param name="config">The starting energy configuration parameters.</param>
        public void InitializePlayer(int playerId, EnergyConfig config)
        {
            var state = new EnergyState(config);
            _playerStates[playerId] = state;

            // Published immediately rather than deferred to the next flush: a HUD binding to a match that is
            // starting needs its opening value on the same frame, and there is no re-entrancy hazard here
            // because no move is being resolved.
            state.MarkPublished();
            MatchEvents.RaiseEnergyChanged(playerId, state.CurrentEnergy);
        }

        /// <summary>
        /// Attempts to validate and spend the specified energy cost for a player.
        /// </summary>
        /// <param name="playerId">The ID of the player spending the energy.</param>
        /// <param name="cost">The amount of energy to spend.</param>
        /// <returns>Success if the transaction was approved and completed, or InsufficientEnergy otherwise.</returns>
        public SpendResult TrySpendEnergy(int playerId, float cost)
        {
            if (!_playerStates.TryGetValue(playerId, out EnergyState state))
            {
                MatchEvents.RaiseEnergySpent(playerId, 0f, false);
                return SpendResult.InsufficientEnergy;
            }

            float energy = state.CurrentEnergy;
            SpendResult result = EnergyValidator.TrySpend(ref energy, cost);

            if (result == SpendResult.Success)
            {
                state.SetEnergy(energy);
                MatchEvents.RaiseEnergyChanged(playerId, state.CurrentEnergy);
                MatchEvents.RaiseEnergySpent(playerId, state.CurrentEnergy, true);
            }
            else
            {
                MatchEvents.RaiseEnergySpent(playerId, state.CurrentEnergy, false);
            }

            return result;
        }

        public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
        {
            if (!_playerStates.TryGetValue(playerId, out EnergyState state))
            {
                return false;
            }

            return EnergyValidator.CanAfford(state.CurrentEnergy, MoveCostResolver.GetCost(moveType, unitEnergyCost, state.Config));
        }

        public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
        {
            if (!_playerStates.TryGetValue(playerId, out EnergyState state))
            {
                return false;
            }

            float cost = MoveCostResolver.GetCost(moveType, unitEnergyCost, state.Config);
            float energy = state.CurrentEnergy;

            // Deliberately silent, and not routed through TrySpendEnergy, which announces both its successes and
            // its rejections. The board has not raised its re-entrancy latch when this runs, so a subscriber that
            // resolved another move from inside the dispatch would be charged twice for one action; a rejection
            // announced here would also make an unaffordable move distinguishable from one never attempted.
            // Both the balance change and the spend are flushed from Update instead.
            if (EnergyValidator.TrySpend(ref energy, cost) != SpendResult.Success)
            {
                return false;
            }

            state.SetEnergy(energy);
            state.MarkSpendPending();

            return true;
        }

        public void RefundMove(int playerId, MoveType moveType, int unitEnergyCost)
        {
            if (!_playerStates.TryGetValue(playerId, out EnergyState state))
            {
                return;
            }

            // Withdrawn before the amount is even known, and on no condition: TryPayForMove records a pending
            // spend for every charge it accepts, a free one included, so the two must cancel on the same
            // predicate or a zero-priced move leaves a spend queued for a move that was rolled back. The charge
            // cannot have been flushed yet — move resolution is synchronous within one frame — so a refunded
            // move publishes nothing at all, which is what a move that never took effect should look like.
            state.CancelPendingSpend();

            float cost = MoveCostResolver.GetCost(moveType, unitEnergyCost, state.Config);

            if (cost <= 0f)
            {
                return;
            }

            state.SetEnergy(state.CurrentEnergy + cost);
        }

        public bool CanAffordDiscard(int playerId)
        {
            if (!_playerStates.TryGetValue(playerId, out EnergyState state))
            {
                return false;
            }

            return EnergyValidator.CanAfford(state.CurrentEnergy, state.Config.DiscardEnergyCost);
        }

        public bool TryPayForDiscard(int playerId)
        {
            if (!_playerStates.TryGetValue(playerId, out EnergyState state))
            {
                return false;
            }

            float cost = state.Config.DiscardEnergyCost;
            float energy = state.CurrentEnergy;

            // Deliberately silent, for the same reason TryPayForMove is: a rejection announced here would make an
            // unaffordable discard distinguishable from one that was never attempted, and a charge that is later
            // refunded must net to no change. Both the balance change and the spend are flushed from Update.
            if (EnergyValidator.TrySpend(ref energy, cost) != SpendResult.Success)
            {
                return false;
            }

            state.SetEnergy(energy);
            state.MarkSpendPending();

            return true;
        }

        public void RefundDiscard(int playerId)
        {
            if (!_playerStates.TryGetValue(playerId, out EnergyState state))
            {
                return;
            }

            // Withdrawn before the amount is even known, and on no condition — see RefundMove for why the two
            // must cancel on the same predicate rather than one gated on the cost.
            state.CancelPendingSpend();

            float cost = state.Config.DiscardEnergyCost;

            if (cost <= 0f)
            {
                return;
            }

            state.SetEnergy(state.CurrentEnergy + cost);
        }

        /// <summary>
        /// Retrieves the current energy level for a player.
        /// </summary>
        /// <param name="playerId">The unique ID of the player.</param>
        /// <returns>The current energy value, or 0 if player is not found.</returns>
        public float GetEnergy(int playerId)
        {
            if (_playerStates.TryGetValue(playerId, out EnergyState state))
            {
                return state.CurrentEnergy;
            }
            return 0f;
        }

        /// <summary>
        /// Retrieves the runtime energy state for a player.
        /// Used primarily by tests and diagnostics.
        /// </summary>
        /// <param name="playerId">The unique ID of the player.</param>
        /// <returns>The player's runtime energy state, or null if player is not found.</returns>
        public EnergyState GetState(int playerId)
        {
            _playerStates.TryGetValue(playerId, out EnergyState state);
            return state;
        }

        /// <summary>
        /// Enforces or removes overtime (2x base regeneration rate) for all active players.
        /// </summary>
        /// <param name="active">True to double regeneration rates, or false to restore standard rates.</param>
        public void SetOvertime(bool active)
        {
            foreach (EnergyState state in _playerStates.Values)
            {
                state.IsOvertime = active;
            }
        }

        /// <summary>
        /// Sets the catch-up Energy multiplier for one player — the per-player counterpart to
        /// <see cref="SetOvertime"/>, which applies to every player at once. That asymmetry is real: overtime is
        /// a property of the match, catch-up is a property of one player's position in it.
        /// </summary>
        /// <param name="playerId">The player whose multiplier changes.</param>
        /// <param name="multiplier">The multiplier to apply. 1 restores the standard regeneration rate.</param>
        /// <remarks>
        /// An unknown player id is ignored: no state is created and nothing is published. The caller owns the
        /// value's sanity — zero freezes that player's regeneration and a negative one reverses it, and nothing
        /// here bounds either.
        /// </remarks>
        public void SetCatchUpMultiplier(int playerId, float multiplier)
        {
            if (!_playerStates.TryGetValue(playerId, out EnergyState state))
            {
                return;
            }

            state.CatchUpMultiplier = multiplier;
        }

        private static void FlushPendingPublications(int playerId, EnergyState state)
        {
            int pendingSpends = state.PendingSpendCount;
            bool hasReachedCap = (state.CurrentEnergy >= state.Config.MaxEnergy) && (state.LastPublishedEnergy < state.Config.MaxEnergy);
            bool hasDriftedEnough = MathF.Abs(state.CurrentEnergy - state.LastPublishedEnergy) >= EnergyPublishQuantum;

            if ((pendingSpends == 0) && !hasDriftedEnough && !hasReachedCap)
            {
                return;
            }

            state.MarkPublished();

            MatchEvents.RaiseEnergyChanged(playerId, state.CurrentEnergy);

            for (int i = 0; i < pendingSpends; i++)
            {
                MatchEvents.RaiseEnergySpent(playerId, state.CurrentEnergy, true);
            }
        }

        private void HandleMatchPhaseChanged(MatchPhase phase)
        {
            _phase = phase;
        }
    }
}
