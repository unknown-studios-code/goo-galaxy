using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Cards.Presenters
{
    public class CardPresenter : MonoBehaviour
    {
        [Tooltip("Authored card assets available to this match. Drop CardDataSO assets here to register them.")]
        [SerializeField]
        private CardDataSO[] _cards;

        private readonly Dictionary<CardId, ICardData> _cardsById = new();

        private void Awake()
        {
            BuildRegistry();
        }

        /// <summary>
        /// Attempts to resolve the authored data for the given card id.
        /// </summary>
        /// <returns><see langword="true"/> if a card with the given id was registered; otherwise <see langword="false"/>.</returns>
        public bool TryGetCard(CardId cardId, out ICardData card)
        {
            return _cardsById.TryGetValue(cardId, out card);
        }

        /// <summary>Replaces the authored roster. The registry is rebuilt on the next <see cref="BuildRegistry" /> call.</summary>
        internal void SetAuthoredCards(params CardDataSO[] cards)
        {
            _cards = cards;
        }

        /// <summary>Indexes the authored roster by card id, discarding any previous registry.</summary>
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
