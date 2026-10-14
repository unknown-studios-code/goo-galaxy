using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Cards.Presenters
{
    /// <summary>
    /// Owns the registry of authored cards available to a match, keyed by <see cref="CardId" /> and built from the
    /// serialized <see cref="CardDataSO" /> array in <c>Awake</c>.
    /// </summary>
    public class CardPresenter : MonoBehaviour
    {
        [Tooltip("Authored card assets available to this match. Drop CardDataSO assets here to register them.")]
        [SerializeField]
        private CardDataSO[] _cards;

        private readonly Dictionary<CardId, CardDataSO> _cardsById = new();

        protected void Awake()
        {
            BuildRegistry();
        }

        /// <summary>
        /// Attempts to resolve the authored data for the given card id.
        /// </summary>
        /// <param name="cardId">The id of the card to look up.</param>
        /// <param name="card">The registered card, or <see langword="null"/> when <paramref name="cardId"/> was not found.</param>
        /// <returns><see langword="true"/> if a card with the given id was registered; otherwise <see langword="false"/>.</returns>
        public bool TryGetCard(CardId cardId, out ICardData card)
        {
            bool isFound = _cardsById.TryGetValue(cardId, out CardDataSO asset);
            card = asset;
            return isFound;
        }

        /// <summary>
        /// Attempts to resolve the presentation-only data for the given card id, from the same registry as
        /// <see cref="TryGetCard" />.
        /// </summary>
        /// <param name="cardId">The id of the card to look up.</param>
        /// <param name="visuals">The registered card's visuals, or <see langword="null"/> when <paramref name="cardId"/> was not found.</param>
        /// <returns><see langword="true"/> if a card with the given id was registered; otherwise <see langword="false"/>.</returns>
        /// <remarks>When two assets share an id, the first one registered wins, exactly as it does for <see cref="TryGetCard" />.</remarks>
        public bool TryGetVisuals(CardId cardId, out ICardVisuals visuals)
        {
            bool isFound = _cardsById.TryGetValue(cardId, out CardDataSO asset);
            visuals = asset;
            return isFound;
        }

        /// <remarks>The registry is not rebuilt until the next <see cref="BuildRegistry" /> call.</remarks>
        internal void SetAuthoredCards(params CardDataSO[] cards)
        {
            _cards = cards;
        }

        internal void BuildRegistry()
        {
            _cardsById.Clear();

            if (_cards == null)
            {
                return;
            }

            foreach (CardDataSO card in _cards)
            {
                if (!_cardsById.TryAdd(card.CardId, card))
                {
                    Debug.LogWarning(string.Format(CardLogMessages.DuplicateCardIdFormat, card.CardId.Value, card.name), card);
                }
            }
        }
    }
}
