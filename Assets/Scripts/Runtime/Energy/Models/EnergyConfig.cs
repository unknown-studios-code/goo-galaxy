using System;

namespace GooGalaxy.Runtime.Energy.Models
{
    /// <summary>
    /// Immutable configuration parameters for a player's energy system in a match.
    /// </summary>
    [Serializable]
    public readonly struct EnergyConfig
    {
        /// <summary>
        /// The fixed energy cost for the Sample Purge discard mechanic.
        /// </summary>
        public const float SamplePurgeEnergyCost = 0.5f;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnergyConfig"/> struct.
        /// </summary>
        /// <param name="maxEnergy">The maximum energy cap.</param>
        /// <param name="regenRate">The base regeneration rate per second.</param>
        /// <param name="startingEnergy">The initial starting energy.</param>
        public EnergyConfig(float maxEnergy, float regenRate, float startingEnergy)
        {
            MaxEnergy = maxEnergy;
            RegenRate = regenRate;
            StartingEnergy = startingEnergy;
        }

        /// <summary>
        /// The maximum energy a player can store.
        /// </summary>
        public float MaxEnergy { get; }

        /// <summary>
        /// The base energy regeneration rate per second.
        /// </summary>
        public float RegenRate { get; }

        /// <summary>
        /// The starting energy of the player.
        /// </summary>
        public float StartingEnergy { get; }
    }
}
