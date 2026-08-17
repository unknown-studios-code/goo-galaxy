using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Deck.Services;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Deck
{
    [TestFixture]
    public class DeckShufflerTests
    {
        private const int GoldenSeed = 424242;
        private const int AlternateSeed = 99999;
        private const int ZeroSeed = 0;
        private const int PermutationSeed = 12345;
        private const int MatchSeed = 100;
        private const int FirstPlayerId = 1;
        private const int SecondPlayerId = 2;

        private static readonly CardId _alpha = new("alpha");
        private static readonly CardId _bravo = new("bravo");
        private static readonly CardId _charlie = new("charlie");
        private static readonly CardId _delta = new("delta");
        private static readonly CardId _echo = new("echo");
        private static readonly CardId _foxtrot = new("foxtrot");

        private static readonly CardId[] _kit = { _alpha, _bravo, _charlie, _delta, _echo, _foxtrot };

        [Test]
        public void Shuffle_SameSeedAndKit_ProducesIdenticalOrderAcrossRepeatedCalls()
        {
            // GIVEN
            var firstResult = new List<CardId>();
            var secondResult = new List<CardId>();

            // WHEN
            DeckShuffler.Shuffle(_kit, GoldenSeed, firstResult);
            DeckShuffler.Shuffle(_kit, GoldenSeed, secondResult);

            // THEN
            Assert.That(secondResult, Is.EqualTo(firstResult));
        }

        [Test]
        public void Shuffle_DifferentSeeds_ProducesDifferentOrders()
        {
            // GIVEN
            var firstResult = new List<CardId>();
            var secondResult = new List<CardId>();

            // WHEN
            DeckShuffler.Shuffle(_kit, GoldenSeed, firstResult);
            DeckShuffler.Shuffle(_kit, AlternateSeed, secondResult);

            // THEN
            Assert.That(secondResult, Is.Not.EqualTo(firstResult));
        }

        [Test]
        public void Shuffle_FixedSeedAndKit_MatchesThePinnedPermutation()
        {
            // GIVEN — the only thing that actually protects cross-platform determinism: a literal, not a
            // re-derivation of the algorithm under test.
            var results = new List<CardId>();

            // WHEN
            DeckShuffler.Shuffle(_kit, GoldenSeed, results);

            // THEN
            Assert.That(results, Is.EqualTo(new[] { _foxtrot, _charlie, _echo, _delta, _bravo, _alpha }));
        }

        [TestCase(GoldenSeed)]
        [TestCase(AlternateSeed)]
        [TestCase(ZeroSeed)]
        [TestCase(PermutationSeed)]
        public void Shuffle_AnySeed_ResultIsAPermutationPreservingEveryCardExactlyOnce(int seed)
        {
            // GIVEN
            var results = new List<CardId>();

            // WHEN
            DeckShuffler.Shuffle(_kit, seed, results);

            // THEN
            Assert.That(results, Is.EquivalentTo(_kit));
        }

        [Test]
        public void Shuffle_ResultsListHoldingPreviousContent_ClearsItFirst()
        {
            // GIVEN
            var results = new List<CardId> { _alpha, _bravo, _alpha, _bravo, _alpha, _bravo, _alpha, _bravo };

            // WHEN
            DeckShuffler.Shuffle(_kit, PermutationSeed, results);

            // THEN
            Assert.That(results, Is.EquivalentTo(_kit));
        }

        [Test]
        public void Shuffle_SeedZero_StillPermutesTheKit()
        {
            // GIVEN — the xorshift32 zero-state guard: a zero seed must not fall back to the identity order.
            var results = new List<CardId>();

            // WHEN
            DeckShuffler.Shuffle(_kit, ZeroSeed, results);

            // THEN
            Assert.That(results, Is.Not.EqualTo(_kit));
        }

        [Test]
        public void Shuffle_NullKit_ThrowsArgumentNullException()
        {
            // GIVEN
            var results = new List<CardId>();

            // WHEN
            void shuffleCall() => DeckShuffler.Shuffle(null, GoldenSeed, results);

            // THEN
            Assert.Throws<ArgumentNullException>(shuffleCall);
        }

        [Test]
        public void Shuffle_NullResultsBuffer_ThrowsArgumentNullException()
        {
            // GIVEN

            // WHEN
            static void shuffleCall() => DeckShuffler.Shuffle(_kit, GoldenSeed, null);

            // THEN
            Assert.Throws<ArgumentNullException>(shuffleCall);
        }

        [Test]
        public void DeriveSeed_SameArguments_IsStable()
        {
            // GIVEN

            // WHEN
            int firstSeed = DeckShuffler.DeriveSeed(MatchSeed, FirstPlayerId);
            int secondSeed = DeckShuffler.DeriveSeed(MatchSeed, FirstPlayerId);

            // THEN
            Assert.That(secondSeed, Is.EqualTo(firstSeed));
        }

        [Test]
        public void DeriveSeed_DifferentPlayerIds_ProducesDifferentSeeds()
        {
            // GIVEN

            // WHEN
            int firstPlayerSeed = DeckShuffler.DeriveSeed(MatchSeed, FirstPlayerId);
            int secondPlayerSeed = DeckShuffler.DeriveSeed(MatchSeed, SecondPlayerId);

            // THEN
            Assert.That(secondPlayerSeed, Is.Not.EqualTo(firstPlayerSeed));
        }
    }
}
