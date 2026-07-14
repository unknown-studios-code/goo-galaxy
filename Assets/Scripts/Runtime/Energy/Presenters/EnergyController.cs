using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Services;
using GooGalaxy.Runtime.Shared.Events;
using UnityEngine;

namespace GooGalaxy.Runtime.Energy.Presenters
{
    /// <summary>
    /// Presenter that orchestrates the real-time energy systems of active players in a match.
    /// </summary>
    public class EnergyController : MonoBehaviour
    {
        [Header("Match Setup")]
        [Tooltip(
            "The starting energy configuration for Player 1.\n"
                + "MaxEnergy is the ceiling, RegenRate is energy per second, and StartingEnergy is the initial amount."
        )]
        [SerializeField]
        private EnergyConfig _playerOneConfig = new(10f, 1f / 2.8f, 5f);

        [Tooltip(
            "The starting energy configuration for Player 2.\n"
                + "MaxEnergy is the ceiling, RegenRate is energy per second, and StartingEnergy is the initial amount (includes Komi bonus)."
        )]
        [SerializeField]
        private EnergyConfig _playerTwoConfig = new(10f, 1f / 2.8f, 5.5f);

        private readonly Dictionary<int, EnergyState> _playerStates = new();

        private void Update()
        {
            foreach (KeyValuePair<int, EnergyState> kvp in _playerStates)
            {
                int playerId = kvp.Key;
                EnergyState state = kvp.Value;

                float oldEnergy = state.CurrentEnergy;
                float newEnergy = EnergyRegenerator.Tick(oldEnergy, Time.deltaTime, state.EffectiveRegenRate, state.Config.MaxEnergy);

                if (MathF.Abs(newEnergy - oldEnergy) > 0.0001f)
                {
                    state.SetEnergy(newEnergy);
                    StaticGameEvents.OnEnergyChanged(playerId, state.CurrentEnergy);
                }
            }
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
            StaticGameEvents.OnEnergyChanged(playerId, state.CurrentEnergy);
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
                StaticGameEvents.OnEnergySpent(playerId, 0f, false);
                return SpendResult.InsufficientEnergy;
            }

            float energy = state.CurrentEnergy;
            SpendResult result = EnergyValidator.TrySpend(ref energy, cost);

            if (result == SpendResult.Success)
            {
                state.SetEnergy(energy);
                StaticGameEvents.OnEnergyChanged(playerId, state.CurrentEnergy);
                StaticGameEvents.OnEnergySpent(playerId, state.CurrentEnergy, true);
            }
            else
            {
                StaticGameEvents.OnEnergySpent(playerId, state.CurrentEnergy, false);
            }

            return result;
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
    }
}
