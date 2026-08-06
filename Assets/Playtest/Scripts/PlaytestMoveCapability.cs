using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Shared.Interfaces;

namespace GooGalaxy.Playtest
{
    /// <summary>
    /// Presents an authored card as the movement capability the board registry stores.
    /// <c>ICardData</c> carries <c>CanClone</c> and <c>CanJump</c> but does not implement
    /// <see cref="IMoveCapable" />, so something has to bridge the two until a real deck system does it.
    /// </summary>
    /// <remarks>
    /// A class rather than a struct on purpose: <see cref="IMoveCapable" /> documents that the registry keeps
    /// capabilities behind the interface, and a value type stored that way boxes on every store.
    /// </remarks>
    internal sealed class PlaytestMoveCapability : IMoveCapable
    {
        private readonly ICardData _card;

        internal PlaytestMoveCapability(ICardData card)
        {
            _card = card;
        }

        public bool CanClone => _card.CanClone;

        public bool CanJump => _card.CanJump;
    }
}
