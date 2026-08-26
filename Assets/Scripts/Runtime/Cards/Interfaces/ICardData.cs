using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Cards.Interfaces
{
    /// <summary>
    /// Read-only contract describing the authored data of a single card (troop or spell).
    /// Implemented by <c>CardDataSO</c> (Unity authoring) and <c>CardDefinition</c> (runtime copy).
    /// Domain assemblies must program against this interface, never against <c>CardDataSO</c> directly.
    /// </summary>
    public interface ICardData
    {
        /// <summary>Unique, stable identifier used as the registry lookup key. Must not be empty.</summary>
        public CardId CardId { get; }

        /// <summary>Player-facing card name shown in the HUD and card inspector tools.</summary>
        public string DisplayName { get; }

        /// <summary>
        /// Player-facing flavour and rules text shown beside the card. Plain text: no markup and no
        /// localization key, and empty on a card whose text has not been authored yet.
        /// </summary>
        public string Description { get; }

        /// <summary>Whether this card deploys a troop unit or resolves a one-time Protocol effect.</summary>
        public CardType Type { get; }

        /// <summary>
        /// The presentation family this card belongs to, which the HUD draws as the accent bar on its hand slot.
        /// </summary>
        /// <remarks>
        /// <see cref="CardAccent.None" /> on a card that authors no accent, and a consumer must read that as "draw
        /// no accent" rather than substituting a default — an unauthored card shows no bar at all.
        /// <para>
        /// <b>The role is authored here and the colour is not.</b> The accent resolves from a USS token at draw
        /// time, which is what lets the colourblind stylesheet swap card accents along with the faction pair. A
        /// <c>UnityEngine.Color</c> on this contract would sit outside that swap, so nothing here may carry one.
        /// </para>
        /// <para>
        /// This is the first presentation member on the contract; the artwork and rarity tracked in GOOM-30 join it
        /// here rather than on a parallel one. That task introduces a rarity enum whose tiers each take a palette
        /// treatment — a second colour system arriving on this same slot, and one that has to resolve through
        /// tokens the way this member does, or the bar and the border will disagree about what the card is.
        /// </para>
        /// </remarks>
        public CardAccent Accent { get; }

        /// <summary>
        /// The card's authored Energy cost. See <see cref="Shared.Interfaces.IEnergyPriced.EnergyCost" /> for how it
        /// prices an action.
        /// </summary>
        public int EnergyCost { get; }

        /// <summary>
        /// Whether this card's units may Clone at all. See <see cref="Shared.Interfaces.IMoveCapable.CanClone" /> for
        /// the capability contract.
        /// </summary>
        public bool CanClone { get; }

        /// <summary>
        /// Whether this card's units may Jump at all. See <see cref="Shared.Interfaces.IMoveCapable.CanJump" /> for
        /// the capability contract.
        /// </summary>
        public bool CanJump { get; }

        /// <summary>Exact hex distance a Clone by this card's units must cover. One for every launch card.</summary>
        public int CloneDistance { get; }

        /// <summary>Exact hex distance a Jump by this card's units must cover. Two for every launch card.</summary>
        public int JumpDistance { get; }

        /// <summary>Whether this card's units require two conversion events to flip instead of one.</summary>
        public bool HasArmor { get; }

        /// <summary>Whether the card's units may land on a hex carrying a hazard. Plasmic Leaper's Hover.</summary>
        public bool CanIgnoreHazards { get; }

        /// <summary>
        /// Hex rings around the landing hex whose enemy occupants receive a conversion attempt. One for every
        /// card but Volatile Mass.
        /// </summary>
        public int ConversionRadius { get; }

        /// <summary>
        /// The impacts the card resolves on landing, in authored order. Never null; a card with no landing
        /// ability returns an empty list.
        /// </summary>
        /// <remarks>
        /// The list is owned by the implementation and must be treated as immutable. Read it with an indexed
        /// <c>for</c> loop — <c>foreach</c> over the interface boxes the backing enumerator.
        /// </remarks>
        public IReadOnlyList<ImpactEffect> LandingEffects { get; }
    }
}
