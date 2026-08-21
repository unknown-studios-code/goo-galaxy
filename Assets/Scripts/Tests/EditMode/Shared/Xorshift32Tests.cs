using System;
using GooGalaxy.Runtime.Shared.Utils;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
{
    [TestFixture]
    public class Xorshift32Tests
    {
        private const int KnownSeed = 1;
        private const int ZeroSeed = 0;
        private const int MatchSeed = 12345;
        private const int FirstStreamId = -1;
        private const int SecondStreamId = -2;
        private const int Bound = 1000;
        private const int SeedDerivedFromFirstStream = -1334834426;

        // Known vectors, pinned as literals. Re-deriving them from the production code would prove only that it
        // agrees with itself; the guarantee this type exists for is that an iOS peer and an Android peer draw
        // exactly these numbers from exactly this seed.
        private static readonly int[] _sequenceFromSeedOne = { 369, 689, 461, 695, 233 };
        private static readonly int[] _sequenceFromSeedZero = { 560, 100, 195, 573, 735 };

        [Test]
        public void NextIndex_SeededWithAKnownValue_MatchesThePinnedSequence()
        {
            // GIVEN
            var random = new Xorshift32(KnownSeed);

            // WHEN
            int[] drawn = { random.NextIndex(Bound), random.NextIndex(Bound), random.NextIndex(Bound), random.NextIndex(Bound), random.NextIndex(Bound) };

            // THEN
            Assert.That(drawn, Is.EqualTo(_sequenceFromSeedOne));
        }

        [Test]
        public void NextIndex_SeededWithZero_StillAdvancesOnThePinnedFallbackSequence()
        {
            // GIVEN — xorshift has no way out of a zero state, so a zero seed is substituted with a fallback
            // rather than rejected; without it every draw below would come back as index zero.
            var random = new Xorshift32(ZeroSeed);

            // WHEN
            int[] drawn = { random.NextIndex(Bound), random.NextIndex(Bound), random.NextIndex(Bound), random.NextIndex(Bound), random.NextIndex(Bound) };

            // THEN
            Assert.That(drawn, Is.EqualTo(_sequenceFromSeedZero));
        }

        [Test]
        public void NextIndex_WithABoundOfOne_AlwaysReturnsZero()
        {
            // GIVEN
            var random = new Xorshift32(KnownSeed);

            // WHEN
            int[] drawn = { random.NextIndex(1), random.NextIndex(1), random.NextIndex(1) };

            // THEN
            Assert.That(drawn, Is.EqualTo(new[] { 0, 0, 0 }));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NextIndex_WithANonPositiveBound_Throws(int bound)
        {
            // GIVEN
            var random = new Xorshift32(KnownSeed);

            // WHEN / THEN
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextIndex(bound));
        }

        [Test]
        public void NextIndex_AfterAThrowingCall_ResumesWhereTheStreamLeftOff()
        {
            // GIVEN — a tick with no legal action must not be able to desynchronize the sequence from a peer
            // replaying it, so a refused draw advances nothing.
            var random = new Xorshift32(KnownSeed);
            _ = random.NextIndex(Bound);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextIndex(0), "Test setup expects an empty range to be refused.");

            // WHEN
            int second = random.NextIndex(Bound);

            // THEN
            Assert.That(second, Is.EqualTo(_sequenceFromSeedOne[1]));
        }

        [Test]
        public void NextIndex_OnTwoStreamsOfOneMatchSeed_DrawsDifferentSequences()
        {
            // GIVEN — the whole point of the derivation: neighbouring stream ids must not start the generator in
            // the same state, or two consumers sharing one match seed would draw the same numbers.
            var first = new Xorshift32(Xorshift32.DeriveSeed(MatchSeed, FirstStreamId));
            var second = new Xorshift32(Xorshift32.DeriveSeed(MatchSeed, SecondStreamId));
            int[] firstDraws = { first.NextIndex(100), first.NextIndex(100), first.NextIndex(100), first.NextIndex(100) };

            // WHEN
            int[] secondDraws = { second.NextIndex(100), second.NextIndex(100), second.NextIndex(100), second.NextIndex(100) };

            // THEN
            Assert.That(secondDraws, Is.Not.EqualTo(firstDraws));
        }

        [Test]
        public void DeriveSeed_ForAKnownMatchSeedAndStream_MatchesThePinnedSeed()
        {
            // GIVEN

            // WHEN
            int derived = Xorshift32.DeriveSeed(MatchSeed, FirstStreamId);

            // THEN
            Assert.That(derived, Is.EqualTo(SeedDerivedFromFirstStream));
        }

        [Test]
        public void DeriveSeed_TwoStreamsOfOneMatchSeed_ProduceDifferentSeeds()
        {
            // GIVEN
            int second = Xorshift32.DeriveSeed(MatchSeed, SecondStreamId);

            // WHEN
            int first = Xorshift32.DeriveSeed(MatchSeed, FirstStreamId);

            // THEN
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void NextIndex_AfterTheStructWasCopied_DrawsFromAForkedPosition()
        {
            // GIVEN — a mutable struct, documented as such: copying copies the position, which is why every
            // holder has to draw from the one storage it keeps.
            var random = new Xorshift32(KnownSeed);
            Xorshift32 fork = random;
            _ = random.NextIndex(Bound);

            // WHEN
            int forkedDraw = fork.NextIndex(Bound);

            // THEN
            Assert.That(forkedDraw, Is.EqualTo(_sequenceFromSeedOne[0]));
        }
    }
}
