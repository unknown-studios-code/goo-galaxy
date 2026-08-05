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
    /// A reference type on purpose: consumers hold it through <see cref="ICardData"/> and
    /// <see cref="IMoveCapable"/> — the board keeps one per live unit in an <c>IMoveCapable</c> registry — and
    /// a value type stored behind an interface boxes on every store. One definition is built per card during
    /// match setup, never per frame, so the single allocation is outside every hot path.
    /// </remarks>
    public sealed class CardDefinition : ICardData, IMoveCapable
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
