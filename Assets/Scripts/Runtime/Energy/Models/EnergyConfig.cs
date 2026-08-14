using System;
using UnityEngine;

namespace GooGalaxy.Runtime.Energy.Models
{
    /// <summary>
    /// Configuration parameters for a player's energy system in a match.
    /// </summary>
    [Serializable]
    public struct EnergyConfig
    {
        private const float DefaultCloneCostMultiplier = 0.5f;

        private const float DefaultJumpEnergyCost = 0.5f;

        private const float DefaultSamplePurgeEnergyCost = 0.5f;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnergyConfig"/> struct, priced at the default action costs.
        /// </summary>
        /// <param name="maxEnergy">The maximum energy cap.</param>
        /// <param name="regenRate">The base regeneration rate per second.</param>
        /// <param name="startingEnergy">The initial starting energy.</param>
        public EnergyConfig(float maxEnergy, float regenRate, float startingEnergy)
            : this(maxEnergy, regenRate, startingEnergy, DefaultCloneCostMultiplier, DefaultJumpEnergyCost, DefaultSamplePurgeEnergyCost) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnergyConfig"/> struct.
        /// </summary>
        /// <param name="maxEnergy">The maximum energy cap.</param>
        /// <param name="regenRate">The base regeneration rate per second.</param>
        /// <param name="startingEnergy">The initial starting energy.</param>
        /// <param name="cloneCostMultiplier">The fraction of a copied unit's authored energy cost a Clone charges.</param>
        /// <param name="jumpEnergyCost">The flat energy cost of a Jump.</param>
        /// <param name="samplePurgeEnergyCost">The energy cost of discarding a card from hand.</param>
        public EnergyConfig(
            float maxEnergy,
            float regenRate,
            float startingEnergy,
            float cloneCostMultiplier,
            float jumpEnergyCost,
            float samplePurgeEnergyCost
        )
        {
            MaxEnergy = maxEnergy;
            RegenRate = regenRate;
            StartingEnergy = startingEnergy;
            CloneCostMultiplier = cloneCostMultiplier;
            JumpEnergyCost = jumpEnergyCost;
            SamplePurgeEnergyCost = samplePurgeEnergyCost;
        }

        /// <summary>
        /// The maximum energy a player can store.
        /// </summary>
        [field: SerializeField]
        public float MaxEnergy { get; private set; }

        /// <summary>
        /// The base energy regeneration rate per second.
        /// </summary>
        [field: SerializeField]
        public float RegenRate { get; private set; }

        /// <summary>
        /// The starting energy of the player.
        /// </summary>
        [field: SerializeField]
        public float StartingEnergy { get; private set; }

        /// <summary>
        /// The fraction of the copied unit's authored energy cost that a Clone charges.
        /// </summary>
        [field: Tooltip(
            "Fraction of the copied unit's authored Energy cost that a Clone charges. 0.5 keeps every launch card on a clean "
                + "half-Energy step. Above 1.0 makes cloning worse than deploying and breaks the action's purpose. Zero makes "
                + "cloning free again, which is the state this exists to remove."
        )]
        [field: SerializeField]
        public float CloneCostMultiplier { get; private set; }

        /// <summary>
        /// The flat energy cost of a Jump, regardless of which unit performs it.
        /// </summary>
        [field: Tooltip(
            "Flat Energy charged for a Jump, regardless of the unit. A Jump adds no board presence, so it is priced as tempo, " + "not as material."
        )]
        [field: SerializeField]
        public float JumpEnergyCost { get; private set; }

        /// <summary>
        /// The energy cost for the Sample Purge discard mechanic.
        /// </summary>
        [field: Tooltip("Energy charged to discard a card from the hand. Migrated from a const so every action price is authored in one place.")]
        [field: SerializeField]
        public float SamplePurgeEnergyCost { get; private set; }
    }
}
