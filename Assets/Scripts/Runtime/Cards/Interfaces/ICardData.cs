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

        public CardType Type { get; }

        public int EnergyCost { get; }

        public bool CanClone { get; }

        public bool CanJump { get; }

        public bool HasArmor { get; }
    }
}
