using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Cards.Models
{
    /// <summary>
    /// Immutable runtime copy of an <see cref="ICardData"/> source (e.g. a <c>CardDataSO</c> asset).
    /// Value-copied and never boxed, since it is always constructed from a class-based <see cref="ICardData"/>
    /// implementation. This is what gameplay code (e.g. board units) should hold at runtime instead of a
    /// reference to the authored asset.
    /// </summary>
    public readonly struct CardDefinition : ICardData, IMoveCapable
    {
        public CardDefinition(ICardData cardData)
        {
            CardId = cardData.CardId;
            DisplayName = cardData.DisplayName;
            Type = cardData.Type;
            EnergyCost = cardData.EnergyCost;
            CanClone = cardData.CanClone;
            CanJump = cardData.CanJump;
            HasArmor = cardData.HasArmor;
        }

        public CardId CardId { get; }

        public string DisplayName { get; }

        public CardType Type { get; }

        public int EnergyCost { get; }

        public bool CanClone { get; }

        public bool CanJump { get; }

        public bool HasArmor { get; }
    }
}
