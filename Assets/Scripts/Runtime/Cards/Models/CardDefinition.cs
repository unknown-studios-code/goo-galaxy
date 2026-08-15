using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Cards.Models
{
    /// <summary>
    /// Immutable runtime copy of an <see cref="ICardData"/> source (e.g. a <c>CardDataSO</c> asset).
    /// This is what gameplay code (e.g. board units) should hold at runtime instead of a reference to the
    /// authored asset.
    /// </summary>
    /// <remarks>
    /// A reference type on purpose: consumers hold it through <see cref="ICardData"/>,
    /// <see cref="IMoveCapable"/>, <see cref="IConversionCapable"/>, <see cref="IAbilityCapable"/>, and
    /// <see cref="IEnergyPriced"/> — the board keeps one per live unit in an <c>IMoveCapable</c> registry and
    /// tests it for the other three — and a value type stored behind an interface boxes on every store. One
    /// definition is built per card during match setup, never per frame, so the single allocation is outside
    /// every hot path.
    /// The landing impacts are copied into an array this instance owns, so a later edit to the authored asset
    /// cannot change the rules of a match already in progress.
    /// </remarks>
    public sealed class CardDefinition : ICardData, IMoveCapable, IConversionCapable, IAbilityCapable, IEnergyPriced
    {
        private static readonly ImpactEffect[] _noLandingEffects = Array.Empty<ImpactEffect>();

        public CardDefinition(ICardData cardData)
        {
            CardId = cardData.CardId;
            DisplayName = cardData.DisplayName;
            Description = cardData.Description;
            Type = cardData.Type;
            EnergyCost = cardData.EnergyCost;
            CanClone = cardData.CanClone;
            CanJump = cardData.CanJump;
            CloneDistance = cardData.CloneDistance;
            JumpDistance = cardData.JumpDistance;
            HasArmor = cardData.HasArmor;
            CanIgnoreHazards = cardData.CanIgnoreHazards;
            ConversionRadius = cardData.ConversionRadius;
            LandingEffects = CopyLandingEffects(cardData.LandingEffects);
        }

        public CardId CardId { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public CardType Type { get; }

        public int EnergyCost { get; }

        public bool CanClone { get; }

        public bool CanJump { get; }

        public int CloneDistance { get; }

        public int JumpDistance { get; }

        public bool HasArmor { get; }

        public bool CanIgnoreHazards { get; }

        public int ConversionRadius { get; }

        public IReadOnlyList<ImpactEffect> LandingEffects { get; }

        private static ImpactEffect[] CopyLandingEffects(IReadOnlyList<ImpactEffect> source)
        {
            if (source == null || source.Count == 0)
            {
                return _noLandingEffects;
            }

            var effects = new ImpactEffect[source.Count];

            for (int i = 0; i < source.Count; i++)
            {
                effects[i] = source[i];
            }

            return effects;
        }
    }
}
