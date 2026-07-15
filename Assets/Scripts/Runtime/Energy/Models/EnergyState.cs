using System;

namespace GooGalaxy.Runtime.Energy.Models
{
    /// <summary>
    /// Represents the mutable, live runtime energy state of a player.
    /// </summary>
    public class EnergyState
    {
        private float _currentEnergy;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnergyState"/> class.
        /// </summary>
        /// <param name="config">The starting energy configuration.</param>
        public EnergyState(EnergyConfig config)
        {
            Config = config;
            SetEnergy(config.StartingEnergy);
        }

        /// <summary>
        /// Gets the immutable configuration parameters for this state.
        /// </summary>
        public EnergyConfig Config { get; }

        /// <summary>
        /// Gets the player's current energy, always clamped between 0 and Config.MaxEnergy.
        /// </summary>
        public float CurrentEnergy => _currentEnergy;

        /// <summary>
        /// Sets the player's current energy, clamping the value between 0 and Config.MaxEnergy.
        /// </summary>
        /// <param name="value">The unclamped energy value to apply.</param>
        public void SetEnergy(float value)
        {
            _currentEnergy = MathF.Max(0f, MathF.Min(value, Config.MaxEnergy));
        }

        /// <summary>
        /// Gets or sets whether overtime is active for this player.
        /// </summary>
        public bool IsOvertime { get; set; }

        /// <summary>
        /// Gets or sets the catch-up multiplier (default is 1.0).
        /// </summary>
        public float CatchUpMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Gets the effective energy regeneration rate per second, taking overtime and catch-up multipliers into account.
        /// </summary>
        public float EffectiveRegenRate => Config.RegenRate * (IsOvertime ? 2.0f : 1.0f) * CatchUpMultiplier;
    }
}
