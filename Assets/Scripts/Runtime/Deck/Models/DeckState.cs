using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Deck.Models
{
    /// <summary>
    /// One player's runtime card cycle: the cards currently in hand, the single card queued behind them, and the
    /// pending remainder of the Kit they rotate through. Built once per player from an already-shuffled Kit and
    /// never randomized again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Allocation:</b> the hand array, its read-only view and the pending queue are all sized in the
    /// constructor, and every rotation enqueues exactly one card before dequeuing one, so the queue's count never
    /// exceeds the capacity it was built with. Nothing on this type allocates after construction, which is what
    /// lets a rotation sit on the input path.
    /// </para>
    /// <para>
    /// <b>Zero cycle depth:</b> a Kit of exactly <c>handSize + <see cref="NextSlotCount" /></c> fills the hand and
    /// the next slot and leaves the pending queue empty. A rotation there enqueues the played card and
    /// immediately dequeues it again as the new <see cref="Next" />, which is legal and deterministic — it is the
    /// MVP's own state with a five-card roster — but it means there is no cycle to track yet and a played card
    /// reappears as the preview straight away.
    /// </para>
    /// <para>
    /// The order the cards arrive in is the whole of the randomness: shuffling belongs to
    /// <c>DeckShuffler</c> and happens once, at match start.
    /// </para>
    /// </remarks>
    public sealed class DeckState
    {
        /// <summary>Slots a Kit must fill beyond the hand itself — the single "next" preview.</summary>
        public const int NextSlotCount = 1;

        /// <summary>The smallest hand a deck can be built with. A hand of zero has no slot to rotate.</summary>
        public const int MinHandSize = 1;

        /// <summary>The largest hand the authoring surface offers, matching the GDD's eight-slot Kit.</summary>
        public const int MaxHandSize = 8;

        /// <summary>The GDD's authored hand: four cards visible, plus the next slot behind them.</summary>
        public const int DefaultHandSize = 4;

        private readonly Queue<CardId> _cycle;
        private readonly CardId[] _hand;
        private readonly ReadOnlyCollection<CardId> _handView;

        private CardId _next;

        /// <summary>Deals a hand, a next card, and the pending cycle from an already-shuffled Kit.</summary>
        /// <param name="kit">
        /// The player's Kit in the order it will be cycled through, shuffled by the caller. Copied on the way in,
        /// so the caller may reuse the buffer for the next player.
        /// </param>
        /// <param name="handSize">Cards held in hand at once, excluding the next slot.</param>
        /// <exception cref="ArgumentNullException">The kit is null.</exception>
        /// <exception cref="ArgumentException">
        /// The hand size is below <see cref="MinHandSize" />, or the kit holds fewer cards than
        /// <see cref="GetMinimumKitSize" /> requires — the hand and the next slot cannot both be filled below
        /// that, and a partially dealt hand is not a state this type is willing to represent.
        /// </exception>
        public DeckState(IReadOnlyList<CardId> kit, int handSize)
        {
            if (kit == null)
            {
                throw new ArgumentNullException(nameof(kit), "DeckState cannot be built without a kit.");
            }

            if (handSize < MinHandSize)
            {
                throw new ArgumentException("DeckState requires a hand size of at least one card.", nameof(handSize));
            }

            if (kit.Count < GetMinimumKitSize(handSize))
            {
                throw new ArgumentException("DeckState requires a kit large enough to fill the hand and the next slot.", nameof(kit));
            }

            _hand = new CardId[handSize];
            _handView = new ReadOnlyCollection<CardId>(_hand);

            // One slot wider than the pending remainder on purpose: a rotation enqueues before it dequeues, so
            // the queue momentarily holds one card more than it started with. Sized to the remainder alone, that
            // single enqueue would grow the backing array and break the no-allocation guarantee above.
            _cycle = new Queue<CardId>(kit.Count - handSize);

            for (int i = 0; i < handSize; i++)
            {
                _hand[i] = kit[i];
            }

            _next = kit[handSize];

            for (int i = handSize + NextSlotCount; i < kit.Count; i++)
            {
                _cycle.Enqueue(kit[i]);
            }
        }

        /// <summary>The cards currently in hand, in slot order.</summary>
        /// <remarks>
        /// A read-only view over this deck's own array, built once in the constructor and valid for the deck's
        /// lifetime — reading it after a rotation shows the rotated hand. Read it with an indexed <c>for</c>
        /// loop. There is no non-allocating <c>foreach</c> over this view at any static type: the backing
        /// <c>ReadOnlyCollection</c> exposes its enumerator only through <c>IEnumerable</c>, so every
        /// <c>foreach</c> allocates one enumerator, concrete type or not.
        /// </remarks>
        public IReadOnlyList<CardId> Hand => _handView;

        /// <summary>The card that will fill the next slot a rotation frees. Never one of the cards in hand.</summary>
        public CardId Next => _next;

        /// <summary>Cards held in hand at once, excluding the next slot.</summary>
        public int HandSize => _hand.Length;

        /// <summary>
        /// Cards waiting in the cycle behind the next slot. Zero for a Kit sized exactly to the hand plus its
        /// next slot, which is a legal state — see the type's remarks.
        /// </summary>
        public int CycleDepth => _cycle.Count;

        /// <summary>The smallest Kit that can fill a hand of the given size and the next slot behind it.</summary>
        /// <param name="handSize">Cards held in hand at once, excluding the next slot.</param>
        /// <returns>The minimum number of cards a Kit must author.</returns>
        public static int GetMinimumKitSize(int handSize)
        {
            return handSize + NextSlotCount;
        }

        /// <summary>Reads the card in a hand slot without changing anything.</summary>
        /// <param name="slotIndex">The zero-based hand slot to read.</param>
        /// <param name="card">The card in that slot, or a default id when the index is outside the hand.</param>
        /// <returns>True when the index names a hand slot; false when it does not.</returns>
        public bool TryGetSlot(int slotIndex, out CardId card)
        {
            if (slotIndex < 0 || slotIndex >= _hand.Length)
            {
                card = default;
                return false;
            }

            card = _hand[slotIndex];

            return true;
        }

        /// <summary>
        /// Rotates one hand slot: the card in it leaves the hand for the back of the cycle, <see cref="Next" />
        /// slides into the vacated slot, and the head of the cycle becomes the new <see cref="Next" />.
        /// </summary>
        /// <remarks>
        /// The deck's single mutation, deliberately named for what it does to the cycle rather than for playing a
        /// card: discarding a card (GOOM-10) rotates the same way and reuses this exact primitive,
        /// so nothing here may assume the card was played. Whether the rotation is allowed to happen at all is
        /// the caller's decision — <c>DeployController</c> only advances once the board has accepted the play.
        /// <para>
        /// Allocation-free, and the hand array is mutated in place, so a caller holding <see cref="Hand" /> sees
        /// the new contents through the same reference.
        /// </para>
        /// </remarks>
        /// <param name="slotIndex">The zero-based hand slot to rotate.</param>
        /// <param name="played">The card that left the hand, or a default id when the index is outside the hand.</param>
        /// <returns>True once the rotation has been applied; false when the index names no hand slot, in which
        /// case the deck is untouched.</returns>
        public bool TryAdvanceSlot(int slotIndex, out CardId played)
        {
            if (slotIndex < 0 || slotIndex >= _hand.Length)
            {
                played = default;
                return false;
            }

            played = _hand[slotIndex];

            _cycle.Enqueue(played);
            _hand[slotIndex] = _next;
            _next = _cycle.Dequeue();

            return true;
        }
    }
}
