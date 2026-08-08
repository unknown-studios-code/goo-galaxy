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
        public CardId CardId { get; }

        public string DisplayName { get; }

        /// <summary>
        /// Player-facing flavour and rules text shown beside the card. Plain text: no markup and no
        /// localization key, and empty on a card whose text has not been authored yet.
        /// </summary>
        public string Description { get; }

        public CardType Type { get; }

        public int EnergyCost { get; }

        public bool CanClone { get; }

        public bool CanJump { get; }

        /// <summary>Exact hex distance a Clone by this card's units must cover. One for every launch card.</summary>
        public int CloneDistance { get; }

        /// <summary>Exact hex distance a Jump by this card's units must cover. Two for every launch card.</summary>
        public int JumpDistance { get; }

        public bool HasArmor { get; }

        /// <summary>Whether the card's units may land on a hex carrying a hazard. Plasmic Leaper's Hover.</summary>
        public bool IgnoresHazards { get; }

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
