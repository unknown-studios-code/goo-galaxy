using System.Collections.Generic;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Services;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Deck.Presenters
{
    /// <summary>
    /// Presenter owning one <see cref="DeckState" /> per player: it shuffles each player's Kit from the match
    /// seed, answers what is in their hand, and publishes <c>MatchEvents.HandChanged</c> whenever that changes.
    /// </summary>
    /// <remarks>
    /// Players are named by whoever bootstraps the match, never invented here — nothing in
    /// <c>MatchConfiguration</c> says who is playing, so a presenter that dealt to "player 1 and player 2" would
    /// be guessing at the one fact it is not told. <see cref="InitializePlayer" /> is therefore the only way a
    /// deck comes into existence.
    /// <para>
    /// The seed is captured from <c>MatchEvents.MatchStarted</c>, which also re-deals every player already
    /// initialized. That is what makes a rematch in the same session start clean: domain reload is disabled, so
    /// this component survives play sessions with the previous match's decks still in it.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class DeckPresenter : MonoBehaviour
    {
        // The GDD's Kit is eight slots and a match is two players; both are sizing hints rather than rules, and
        // both buffers grow rather than break if a Kit or a match ever exceeds them.
        private const int KitCapacity = 8;

        private const int PlayersPerMatch = 2;

        [Header("Match Setup")]
        [SerializeField]
        private KitDataSO _kit;

        [Tooltip(
            "Cards a player holds at once, excluding the 'next' preview slot behind them. The GDD authors 4. "
                + "A Kit must hold at least this many cards plus one, or no hand can be dealt from it."
        )]
        [Range(DeckState.MinHandSize, DeckState.MaxHandSize)]
        [SerializeField]
        private int _handSize = DeckState.DefaultHandSize;

        private readonly Dictionary<int, DeckState> _playerDecks = new(PlayersPerMatch);
        private readonly List<CardId> _shuffleBuffer = new(KitCapacity);
        private readonly List<int> _initializedPlayerIds = new(PlayersPerMatch);

        private int _matchSeed;

        protected void Awake()
        {
            Debug.Assert(_kit != null, DeckLogMessages.KitDataMissing, this);
        }

        protected void OnEnable()
        {
            MatchEvents.MatchStarted += HandleMatchStarted;
        }

        protected void OnDisable()
        {
            MatchEvents.MatchStarted -= HandleMatchStarted;
        }

        /// <summary>
        /// Deals a player their opening hand: the serialized Kit, shuffled with a seed derived from the match
        /// seed and the player id, then split into a hand, a next card, and the pending cycle.
        /// </summary>
        /// <remarks>
        /// Must be called explicitly by match bootstrap for every player taking part; not invoked automatically.
        /// Calling it again replaces that player's deck outright, which is what a rematch does. A player dealt
        /// before <c>MatchStarted</c> arrives is dealt from a zero seed and re-dealt from the real one when it
        /// does, so bootstrap order does not have to be defended against.
        /// <para>
        /// Publishes <c>MatchEvents.HandChanged</c> on success. A Kit that is missing or too small is reported
        /// and leaves the player without a deck rather than throwing.
        /// </para>
        /// </remarks>
        /// <param name="playerId">The player to deal to.</param>
        public void InitializePlayer(int playerId)
        {
            if (_kit == null)
            {
                Debug.LogError(DeckLogMessages.KitDataMissing, this);
                return;
            }

            if (_handSize < DeckState.MinHandSize)
            {
                Debug.LogError(string.Format(DeckLogMessages.HandSizeTooSmallFormat, _handSize, DeckState.MinHandSize), this);
                return;
            }

            IReadOnlyList<CardId> kitCardIds = _kit.CardIds;
            int minimumKitSize = DeckState.GetMinimumKitSize(_handSize);

            if (kitCardIds.Count < minimumKitSize)
            {
                Debug.LogError(string.Format(DeckLogMessages.KitTooSmallFormat, _kit.name, kitCardIds.Count, minimumKitSize, _handSize), this);
                return;
            }

            DeckShuffler.Shuffle(kitCardIds, DeckShuffler.DeriveSeed(_matchSeed, playerId), _shuffleBuffer);

            var deck = new DeckState(_shuffleBuffer, _handSize);
            _playerDecks[playerId] = deck;

            PublishHandChanged(playerId, deck);
        }

        /// <summary>Reads a player's current hand.</summary>
        /// <param name="playerId">The player to read.</param>
        /// <param name="hand">
        /// The cards in hand, in slot order, or null when the player has no deck. The list belongs to the deck
        /// and reflects every later rotation; read it with an indexed <c>for</c> loop.
        /// </param>
        /// <returns>True when the player has a deck; false when they do not.</returns>
        public bool TryGetHand(int playerId, out IReadOnlyList<CardId> hand)
        {
            if (!_playerDecks.TryGetValue(playerId, out DeckState deck))
            {
                hand = null;
                return false;
            }

            hand = deck.Hand;

            return true;
        }

        /// <summary>Reads the card queued behind a player's hand — the "next" slot the HUD previews.</summary>
        /// <param name="playerId">The player to read.</param>
        /// <param name="nextCard">The queued card, or a default id when the player has no deck.</param>
        /// <returns>True when the player has a deck; false when they do not.</returns>
        public bool TryGetNextCard(int playerId, out CardId nextCard)
        {
            if (!_playerDecks.TryGetValue(playerId, out DeckState deck))
            {
                nextCard = default;
                return false;
            }

            nextCard = deck.Next;

            return true;
        }

        /// <summary>Reads a single hand slot without changing anything.</summary>
        /// <param name="playerId">The player to read.</param>
        /// <param name="slotIndex">The zero-based hand slot to read.</param>
        /// <param name="card">The card in that slot, or a default id when the player or the slot is unknown.</param>
        /// <returns>True when the player has a deck and the index names one of its hand slots.</returns>
        /// <remarks>
        /// Answers false both for an unknown player and for an out-of-range slot; a caller that has to tell the
        /// two apart asks <see cref="TryGetHand" /> first.
        /// </remarks>
        public bool TryGetSlot(int playerId, int slotIndex, out CardId card)
        {
            if (!_playerDecks.TryGetValue(playerId, out DeckState deck))
            {
                card = default;
                return false;
            }

            return deck.TryGetSlot(slotIndex, out card);
        }

        /// <remarks>
        /// Replaces what the Inspector authored, for a caller that has no asset to assign — the same seam
        /// <c>CardPresenter.SetAuthoredCards</c> exists for. Decks already dealt are left alone; the next
        /// <see cref="InitializePlayer" /> is what picks the new Kit up.
        /// </remarks>
        internal void SetKit(KitDataSO kit, int handSize)
        {
            _kit = kit;
            _handSize = handSize;
        }

        /// <remarks>
        /// Called only once the action consuming the slot has been accepted — a play the board resolved
        /// (<c>DeployController.TryPlayCard</c>) or a discard the ledger has already charged
        /// (<c>CardDiscardController.TryDiscardCard</c>) — which is what makes the cycle advance a consequence of
        /// a resolved action rather than of an attempted one. Publishes <c>MatchEvents.HandChanged</c> with the
        /// rotated hand; nothing is published when the rotation is refused.
        /// </remarks>
        internal bool TryAdvanceSlot(int playerId, int slotIndex, out CardId played)
        {
            if (!_playerDecks.TryGetValue(playerId, out DeckState deck))
            {
                played = default;
                return false;
            }

            if (!deck.TryAdvanceSlot(slotIndex, out played))
            {
                return false;
            }

            PublishHandChanged(playerId, deck);

            return true;
        }

        private static void PublishHandChanged(int playerId, DeckState deck)
        {
            MatchEvents.RaiseHandChanged(playerId, deck.Hand, deck.Next);
        }

        // The player ids are copied out before the re-deal because InitializePlayer writes into the dictionary
        // and publishes an event from inside it — a subscriber that initialized another player would otherwise
        // mutate the collection this loop is walking.
        private void HandleMatchStarted(MatchConfiguration config)
        {
            _matchSeed = config.Seed;

            _initializedPlayerIds.Clear();

            foreach (int playerId in _playerDecks.Keys)
            {
                _initializedPlayerIds.Add(playerId);
            }

            for (int i = 0; i < _initializedPlayerIds.Count; i++)
            {
                InitializePlayer(_initializedPlayerIds[i]);
            }
        }
    }
}
