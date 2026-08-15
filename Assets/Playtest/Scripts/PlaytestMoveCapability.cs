using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Playtest
{
    /// <summary>
    /// Presents an authored card as the capability object the board registry stores.
    /// <c>ICardData</c> carries the authored values but implements none of the board-facing capability
    /// contracts, so something has to bridge the two until a real deck system does it.
    /// </summary>
    /// <remarks>
    /// A class rather than a struct on purpose: <see cref="IMoveCapable" /> documents that the registry keeps
    /// capabilities behind the interface, and a value type stored that way boxes on every store.
    /// Every board-facing contract is implemented on the one object, because the board looks the registered
    /// <see cref="IMoveCapable" /> up and tests it for the rest — a bridge that implemented only movement would
    /// silently give every playtest card a one-ring conversion, no landing ability, and the fallback Energy
    /// price, each of which reads as a balance bug rather than a missing interface. Add the contract here
    /// whenever the board grows one.
    /// </remarks>
    internal sealed class PlaytestMoveCapability : IMoveCapable, IConversionCapable, IAbilityCapable, IEnergyPriced
    {
        private readonly ICardData _card;

        internal PlaytestMoveCapability(ICardData card)
        {
            _card = card;
        }

        public bool CanClone => _card.CanClone;

        public bool CanJump => _card.CanJump;

        public int CloneDistance => _card.CloneDistance;

        public int JumpDistance => _card.JumpDistance;

        public bool CanIgnoreHazards => _card.CanIgnoreHazards;

        public int ConversionRadius => _card.ConversionRadius;

        public IReadOnlyList<ImpactEffect> LandingEffects => _card.LandingEffects;

        public int EnergyCost => _card.EnergyCost;
    }
}
