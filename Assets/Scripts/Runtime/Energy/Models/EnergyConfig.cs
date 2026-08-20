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

        public EnergyConfig(float maxEnergy, float regenRate, float startingEnergy)
            : this(maxEnergy, regenRate, startingEnergy, DefaultCloneCostMultiplier, DefaultJumpEnergyCost, DefaultDiscardEnergyCost) { }

        public EnergyConfig(float maxEnergy, float regenRate, float startingEnergy, float cloneCostMultiplier, float jumpEnergyCost, float discardEnergyCost)
        {
            MaxEnergy = maxEnergy;
            RegenRate = regenRate;
            StartingEnergy = startingEnergy;
            CloneCostMultiplier = cloneCostMultiplier;
            JumpEnergyCost = jumpEnergyCost;
            DiscardEnergyCost = discardEnergyCost;
        }

        [field: Tooltip(
            "Ceiling on stored Energy. The GDD authors 10. Regeneration at the cap is discarded, so a player parked here is losing tempo — "
                + "raising it removes that pressure and lets a player bank a whole opening, lowering it makes the cheapest cards the only playable ones."
        )]
        [field: SerializeField]
        public float MaxEnergy { get; private set; }

        [field: Tooltip(
            "Energy per second, before the Overtime and catch-up multipliers. The GDD authors 1/2.8 — one whole Energy every 2.8 seconds. "
                + "This is the clock every action price is balanced against, so it is the single most disruptive value on this struct to move."
        )]
        [field: SerializeField]
        public float RegenRate { get; private set; }

        [field: Tooltip(
            "Energy both players open the match holding. The GDD authors 5, half the cap, which funds one opening deployment without funding two. "
                + "Keep the two players equal on symmetric maps; a deliberate difference is Komi and belongs only to an asymmetric one."
        )]
        [field: SerializeField]
        public float StartingEnergy { get; private set; }

        [field: Tooltip(
            "Fraction of the copied unit's authored Energy cost that a Clone charges. 0.5 keeps every launch card on a clean "
                + "half-Energy step. Above 1.0 makes cloning worse than deploying and breaks the action's purpose. Zero makes "
                + "cloning free again, which is the state this exists to remove."
        )]
        [field: SerializeField]
        public float CloneCostMultiplier { get; private set; }

        [field: Tooltip(
            "Flat Energy charged for a Jump, regardless of the unit. A Jump adds no board presence, so it is priced as tempo, not as material. "
                + "0.5 keeps it the cheapest action on the board; at or above the cheapest card's Clone price a Jump stops being worth the tempo."
        )]
        [field: SerializeField]
        public float JumpEnergyCost { get; private set; }

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
