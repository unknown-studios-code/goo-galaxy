using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Deck.Data
{
    /// <summary>
    /// The authored Kit a player brings into a match: an ordered list of cards, shuffled once at match start and
    /// then cycled through for the rest of the match.
    /// </summary>
    /// <remarks>
    /// The GDD's Kit <i>composition</i> rules — eight slots, a minimum of four Specimens, no duplicates — are
    /// deliberately not enforced here. They belong to a Kit builder the MVP does not have, and the MVP roster
    /// authors five cards in total, so enforcing them now would reject every Kit the project can currently build.
    /// The only rule validated here is the one the runtime actually depends on: enough cards to deal a hand and
    /// the next slot behind it. Do not add the composition checks to this asset when the builder lands — add
    /// them to the builder.
    /// </remarks>
    [CreateAssetMenu(menuName = "Goo Galaxy/Deck/Kit Data", fileName = "NewKitData")]
    public class KitDataSO : ScriptableObject
    {
        private static readonly CardId[] _noCardIds = Array.Empty<CardId>();

        private static readonly CardDataSO[] _noCards = Array.Empty<CardDataSO>();

        [Header("Composition")]
        [Tooltip(
            "The cards this Kit brings into a match, in authored order. The order is only a starting point — DeckShuffler reorders a copy per player "
                + "at match start. Needs at least Hand Size + 1 entries or no hand can be dealt; the too-small warning names the exact number for the "
                + "Hand Size in use."
        )]
        [SerializeField]
        private CardDataSO[] _cards;

        private CardId[] _cachedCardIds;

        /// <summary>The authored card assets, in authoring order. Never null; an unauthored Kit reads as empty.</summary>
        public IReadOnlyList<CardDataSO> Cards => _cards ?? _noCards;

        /// <summary>
        /// The Kit's card ids in authored order, which is what <c>DeckShuffler</c> shuffles. Empty slots are
        /// skipped, so this can be shorter than <see cref="Cards" />.
        /// </summary>
        /// <remarks>
        /// Built once and memoized, because a deck is dealt per player and per rematch and projecting the
        /// authoring array each time would allocate an array per deal. The cache is a derived view of authored
        /// data, never runtime state, and <c>OnValidate</c> drops it so an Inspector edit is picked up
        /// immediately.
        /// </remarks>
        public IReadOnlyList<CardId> CardIds => _cachedCardIds ??= BuildCardIds();

#if UNITY_EDITOR
        protected void OnValidate()
        {
            _cachedCardIds = null;
            ValidateAuthoredData();
        }
#endif

        /// <remarks>
        /// Reports rather than repairs: an under-sized Kit is a designer decision to reverse, not something this
        /// asset can invent cards to fix. The runtime refuses the same Kit again at deal time, so the warning is
        /// an early copy of a failure the match would otherwise only show once it started.
        /// </remarks>
        internal void ValidateAuthoredData()
        {
            int authoredCount = _cards == null ? 0 : _cards.Length;

            for (int i = 0; i < authoredCount; i++)
            {
                if (_cards[i] == null)
                {
                    Debug.LogWarning(string.Format(DeckLogMessages.KitCardMissingFormat, name, i), this);
                }
            }

            int minimumKitSize = DeckState.GetMinimumKitSize(DeckState.DefaultHandSize);

            // Measured against the default hand rather than the presenter's, which this asset cannot see: a Kit
            // is authored once and can be dropped onto any DeckPresenter, so the authored default is the only
            // hand size knowable here. A presenter running a smaller hand re-checks against its own at deal time.
            if (CardIds.Count < minimumKitSize)
            {
                Debug.LogWarning(string.Format(DeckLogMessages.KitTooSmallFormat, name, CardIds.Count, minimumKitSize, DeckState.DefaultHandSize), this);
            }
        }

        internal void SetAuthoredCards(params CardDataSO[] cards)
        {
            _cards = cards;
            _cachedCardIds = null;
        }

        private CardId[] BuildCardIds()
        {
            if (_cards == null || _cards.Length == 0)
            {
                return _noCardIds;
            }

            int authoredCount = 0;

            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] != null)
                {
                    authoredCount++;
                }
            }

            if (authoredCount == 0)
            {
                return _noCardIds;
            }

            var cardIds = new CardId[authoredCount];
            int writeIndex = 0;

            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] != null)
                {
                    cardIds[writeIndex++] = _cards[i].CardId;
                }
            }

            return cardIds;
        }
    }
}
