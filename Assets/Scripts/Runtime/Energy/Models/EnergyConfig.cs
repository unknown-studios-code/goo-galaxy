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

        private const float DefaultDiscardEnergyCost = 0.5f;

        // The authorable band for a discard, enforced in the Inspector rather than only described in the tooltip.
        // It bounds what a designer can dial in; it does not bind the constructors, which tests drive past both
        // ends on purpose to exercise the free-discard and unaffordable-discard paths.
        private const float MinDiscardEnergyCost = 0.25f;

        private const float MaxDiscardEnergyCost = 1f;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnergyConfig"/> struct, priced at the default action costs.
        /// </summary>
        /// <param name="maxEnergy">The maximum energy cap.</param>
        /// <param name="regenRate">The base regeneration rate per second.</param>
        /// <param name="startingEnergy">The initial starting energy.</param>
        public EnergyConfig(float maxEnergy, float regenRate, float startingEnergy)
            : this(maxEnergy, regenRate, startingEnergy, DefaultCloneCostMultiplier, DefaultJumpEnergyCost, DefaultDiscardEnergyCost) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnergyConfig"/> struct.
        /// </summary>
        /// <param name="maxEnergy">The maximum energy cap.</param>
        /// <param name="regenRate">The base regeneration rate per second.</param>
        /// <param name="startingEnergy">The initial starting energy.</param>
        /// <param name="cloneCostMultiplier">The fraction of a copied unit's authored energy cost a Clone charges.</param>
        /// <param name="jumpEnergyCost">The flat energy cost of a Jump.</param>
        /// <param name="discardEnergyCost">The energy cost of discarding a card from hand.</param>
        public EnergyConfig(float maxEnergy, float regenRate, float startingEnergy, float cloneCostMultiplier, float jumpEnergyCost, float discardEnergyCost)
        {
            MaxEnergy = maxEnergy;
            RegenRate = regenRate;
            StartingEnergy = startingEnergy;
            CloneCostMultiplier = cloneCostMultiplier;
            JumpEnergyCost = jumpEnergyCost;
            DiscardEnergyCost = discardEnergyCost;
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
            "Flat Energy charged for a Jump, regardless of the unit. A Jump adds no board presence, so it is priced as tempo, not as material. "
                + "0.5 keeps it the cheapest action on the board; at or above the cheapest card's Clone price a Jump stops being worth the tempo."
        )]
        [field: SerializeField]
        public float JumpEnergyCost { get; private set; }

        /// <summary>
        /// The energy charged per card discarded, independent of that card's own authored cost.
        /// </summary>
        [field: Tooltip(
            "Energy charged to discard one card from the hand, whatever that card costs. 0.5 keeps a discard cheaper than any "
                + "Clone; below about 0.25 a player cycles the whole Kit for near-nothing and the hand stops constraining anything, "
                + "and above roughly 1 a discard costs more than playing the card and the hand stops cycling at all."
        )]
        [field: Range(MinDiscardEnergyCost, MaxDiscardEnergyCost)]
        [field: SerializeField]
        public float DiscardEnergyCost { get; private set; }
    }
}
