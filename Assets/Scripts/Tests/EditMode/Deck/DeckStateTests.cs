using System;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Deck
{
    [TestFixture]
    public class DeckStateTests
    {
        private const int HandSize = 4;

        private static readonly CardId _card0 = new("kit_card_0");
        private static readonly CardId _card1 = new("kit_card_1");
        private static readonly CardId _card2 = new("kit_card_2");
        private static readonly CardId _card3 = new("kit_card_3");
        private static readonly CardId _card4 = new("kit_card_4");
        private static readonly CardId _card5 = new("kit_card_5");
        private static readonly CardId _card6 = new("kit_card_6");

        [Test]
        public void Constructor_KitLargerThanTheHand_FillsTheHandFromTheFrontOfTheKit()
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };

            // WHEN
            var deck = new DeckState(kit, HandSize);

            // THEN
            Assert.That(deck.Hand, Is.EqualTo(new[] { _card0, _card1, _card2, _card3 }));
        }

        [Test]
        public void Constructor_KitLargerThanTheHand_SetsNextFromTheCardAfterTheHand()
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };

            // WHEN
            var deck = new DeckState(kit, HandSize);

            // THEN
            Assert.That(deck.Next, Is.EqualTo(_card4));
        }

        [Test]
        public void Constructor_KitExactlyHandSizePlusOne_CycleDepthIsZero()
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };

            // WHEN
            var deck = new DeckState(kit, HandSize);

            // THEN
            Assert.That(deck.CycleDepth, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_KitShorterThanHandSizePlusOne_ThrowsArgumentException()
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3 };

            // WHEN
            void buildDeck() => new DeckState(kit, HandSize);

            // THEN
            Assert.Throws<ArgumentException>(buildDeck);
        }

        [Test]
        public void Constructor_NullKit_ThrowsArgumentNullException()
        {
            // GIVEN

            // WHEN
            static void buildDeck() => new DeckState(null, HandSize);

            // THEN
            Assert.Throws<ArgumentNullException>(buildDeck);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_NonPositiveHandSize_ThrowsArgumentException(int handSize)
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };

            // WHEN
            void buildDeck() => new DeckState(kit, handSize);

            // THEN
            Assert.Throws<ArgumentException>(buildDeck);
        }

        [TestCase(1, ExpectedResult = 2)]
        [TestCase(4, ExpectedResult = 5)]
        [TestCase(8, ExpectedResult = 9)]
        public int GetMinimumKitSize_HandSize_ReturnsHandSizePlusTheNextSlot(int handSize)
        {
            // GIVEN

            // WHEN / THEN — a parameterized failure names the offending hand size.
            return DeckState.GetMinimumKitSize(handSize);
        }

        [Test]
        public void TryGetSlot_ValidIndex_ReturnsTheHeldCard()
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };
            var deck = new DeckState(kit, HandSize);

            // WHEN
            bool wasFound = deck.TryGetSlot(2, out CardId card);

            // THEN
            Assert.That((wasFound, card), Is.EqualTo((true, _card2)));
        }

        [Test]
        public void TryGetSlot_ValidIndex_DoesNotMutateTheDeck()
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4, _card5 };
            var deck = new DeckState(kit, HandSize);
            int cycleDepthBefore = deck.CycleDepth;

            // WHEN
            deck.TryGetSlot(0, out _);

            // THEN
            Assert.That((deck.CycleDepth, deck.Next), Is.EqualTo((cycleDepthBefore, _card4)));
        }

        [TestCase(-1)]
        [TestCase(HandSize)]
        public void TryGetSlot_OutOfRangeIndex_ReturnsFalseAndDefaultCard(int slotIndex)
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };
            var deck = new DeckState(kit, HandSize);

            // WHEN
            bool wasFound = deck.TryGetSlot(slotIndex, out CardId card);

            // THEN
            Assert.That((wasFound, card), Is.EqualTo((false, default(CardId))));
        }

        [Test]
        public void TryAdvanceSlot_ValidIndex_MovesTheQueuedNextCardIntoThePlayedSlot()
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4, _card5 };
            var deck = new DeckState(kit, HandSize);

            // WHEN
            deck.TryAdvanceSlot(0, out CardId played);

            // THEN
            Assert.That((played, deck.Hand[0]), Is.EqualTo((_card0, _card4)));
        }

        [Test]
        public void TryAdvanceSlot_ZeroCycleDepthKit_ReturnsThePlayedCardAsTheNewNext()
        {
            // GIVEN — the MVP's own state with a five-card roster: pinned deliberately, see DeckState's remarks.
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };
            var deck = new DeckState(kit, HandSize);

            // WHEN
            deck.TryAdvanceSlot(0, out CardId played);

            // THEN
            Assert.That(deck.Next, Is.EqualTo(played));
        }

        [Test]
        public void TryAdvanceSlot_ZeroCycleDepthKit_LeavesHandSizeAndCycleDepthUnchanged()
        {
            // GIVEN — the same zero-depth roster the discard path runs against: nothing to draw from means the
            // rotation must not grow the hand or the cycle, it can only rewrite the one slot in place.
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };
            var deck = new DeckState(kit, HandSize);
            (int handSizeBefore, int cycleDepthBefore) = (deck.HandSize, deck.CycleDepth);

            // WHEN
            deck.TryAdvanceSlot(0, out _);

            // THEN
            Assert.That((deck.HandSize, deck.CycleDepth), Is.EqualTo((handSizeBefore, cycleDepthBefore)));
        }

        [Test]
        public void TryAdvanceSlot_RepeatedAcrossAFullCycle_ReturnsToTheOpeningOrder()
        {
            // GIVEN — one card in hand, one in Next, one in the cycle: three plays of the same slot is a full lap.
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4, _card5 };
            var deck = new DeckState(kit, HandSize);
            CardId openingSlot0 = deck.Hand[0];
            CardId openingNext = deck.Next;

            // WHEN
            deck.TryAdvanceSlot(0, out _);
            deck.TryAdvanceSlot(0, out _);
            deck.TryAdvanceSlot(0, out _);

            // THEN
            Assert.That((deck.Hand[0], deck.Next), Is.EqualTo((openingSlot0, openingNext)));
        }

        [Test]
        public void TryAdvanceSlot_RotatedCard_DoesNotReappearBeforeTheRestOfTheCycleIsDrawn()
        {
            // GIVEN — a seven-card kit against a hand of four leaves a two-deep cycle behind the rotated card.
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4, _card5, _card6 };
            var deck = new DeckState(kit, HandSize);
            deck.TryAdvanceSlot(0, out CardId rotated);

            // WHEN — exactly as many further rotations as cards were queued behind the rotated one.
            deck.TryAdvanceSlot(1, out _);
            deck.TryAdvanceSlot(2, out _);

            // THEN
            Assert.That(deck.Hand, Has.None.EqualTo(rotated));
        }

        [Test]
        public void TryAdvanceSlot_RotatedCard_ReappearsOnceTheRestOfTheCycleIsDrawn()
        {
            // GIVEN — the same two-deep cycle as above.
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4, _card5, _card6 };
            var deck = new DeckState(kit, HandSize);
            deck.TryAdvanceSlot(0, out CardId rotated);

            // WHEN — one more rotation than the cycle is deep draws the rotated card back into the hand.
            deck.TryAdvanceSlot(1, out _);
            deck.TryAdvanceSlot(2, out _);
            deck.TryAdvanceSlot(3, out _);

            // THEN
            Assert.That(deck.Hand, Does.Contain(rotated));
        }

        [Test]
        public void TryAdvanceSlot_OutOfRangeIndex_ReturnsFalseAndLeavesTheDeckUnchanged()
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };
            var deck = new DeckState(kit, HandSize);

            // WHEN
            bool wasAdvanced = deck.TryAdvanceSlot(HandSize, out CardId played);

            // THEN
            Assert.That((wasAdvanced, played, deck.Hand[0], deck.Next), Is.EqualTo((false, default(CardId), _card0, _card4)));
        }

        [Test]
        [Category("Allocation")]
        public void TryAdvanceSlot_RepeatedCalls_AllocatesNoManagedMemory()
        {
            // GIVEN
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4, _card5 };
            var deck = new DeckState(kit, HandSize);
            deck.TryAdvanceSlot(0, out _); // Warm-up to exclude JIT allocation from the measurement.

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                deck.TryAdvanceSlot(0, out _);
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(allocatedAfter - allocatedBefore, Is.EqualTo(0), "TryAdvanceSlot allocated memory on a hot path!");
        }

        [Test]
        [Category("Allocation")]
        public void TryAdvanceSlot_ZeroCycleDepthKit_RepeatedCalls_AllocatesNoManagedMemory()
        {
            // GIVEN — the configuration the MVP actually runs: a five-card kit against a hand of four leaves the
            // cycle queue capacity at exactly one, starting empty. A six-card kit here would still pass even if
            // the constructor sized the queue one short, since it starts with a full slot of headroom; this is
            // the one case where an off-by-one grows the backing array on the very first rotation.
            CardId[] kit = { _card0, _card1, _card2, _card3, _card4 };
            var deck = new DeckState(kit, HandSize);
            deck.TryAdvanceSlot(0, out _); // Warm-up to exclude JIT allocation from the measurement.

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                deck.TryAdvanceSlot(0, out _);
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(allocatedAfter - allocatedBefore, Is.EqualTo(0), "TryAdvanceSlot allocated memory on a hot path!");
        }
    }
}
