using UnityEngine;

namespace GooGalaxy.Runtime.Cards.Interfaces
{
    /// <summary>
    /// Presentation-only contract of a card, implemented by <c>CardDataSO</c>.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="ICardData" /> on purpose (Interface Segregation): gameplay, AI, board and network
    /// code identify cards by <c>CardId</c> and must never see an engine type, so anything that needs
    /// <c>UnityEngine</c> to be expressed lives here instead.
    /// </remarks>
    public interface ICardVisuals
    {
        /// <summary>
        /// The card face shown in the hand slot, or null when no artwork has been authored.
        /// </summary>
        /// <remarks>
        /// A <see cref="Object" />, so compare with <c>!= null</c> and never <c>is null</c> or <c>?.</c> — those
        /// treat a destroyed sprite as alive.
        /// </remarks>
        public Sprite Artwork { get; }
    }
}
