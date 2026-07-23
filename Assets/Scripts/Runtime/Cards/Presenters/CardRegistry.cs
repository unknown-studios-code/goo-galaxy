using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Cards.Presenters
{
    public class CardRegistry : MonoBehaviour
    {
        [Tooltip("Authored card assets available to this match. Drop CardDataSO assets here to register them.")]
        [SerializeField]
        private CardDataSO[] _cards;

        private readonly Dictionary<CardId, ICardData> _cardsById = new();

        private void Awake()
        {
            if (_cards == null)
            {
                return;
            }

            foreach (CardDataSO card in _cards)
            {
                if (!_cardsById.TryAdd(card.CardId, card))
                {
                    Debug.LogWarning($"CardRegistry: duplicate CardId '{card.CardId}' on '{card.name}' was skipped.", card);
                }
            }
        }

        /// <summary>
        /// Attempts to resolve the authored data for the given card id.
        /// </summary>
        /// <returns><see langword="true"/> if a card with the given id was registered; otherwise <see langword="false"/>.</returns>
        public bool TryGetCard(CardId cardId, out ICardData card)
        {
            return _cardsById.TryGetValue(cardId, out card);
        }
    }
}
