using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Audio.Services;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Audio
{
    [TestFixture]
    public class MatchIntensityResolverTests
    {
        private const float IdleIntensity = 0f;
        private const float EarlyIntensity = 0.2f;
        private const float EarlyUntilElapsedSeconds = 60f;
        private const float MidIntensity = 0.5f;
        private const float MidUntilElapsedSeconds = 120f;
        private const float LateIntensity = 0.7f;
        private const float FinalWindowIntensity = 0.85f;
        private const float FinalWindowSeconds = 15f;
        private const float OvertimeIntensity = 1f;
        private const float ArbitraryLargeRemainingSeconds = 999f;
        private const float Tolerance = 0.0001f;

        [TestCase(MatchPhase.None)]
        [TestCase(MatchPhase.Loading)]
        [TestCase(MatchPhase.Countdown)]
        public void TryResolve_IdlePhase_ResolvesIdleIntensity(MatchPhase phase)
        {
            // GIVEN
            IntensityBands bands = BuildBands();

            // WHEN
            bool didResolve = MatchIntensityResolver.TryResolve(phase, 0f, ArbitraryLargeRemainingSeconds, in bands, out float intensity);

            // THEN
            Assert.That(didResolve, Is.True);
            Assert.That(intensity, Is.EqualTo(IdleIntensity).Within(Tolerance));
        }

        [TestCase(0f)]
        [TestCase(59f)]
        public void TryResolve_StandardEarlyBand_ResolvesEarlyIntensity(float elapsedSeconds)
        {
            // GIVEN
            IntensityBands bands = BuildBands();

            // WHEN
            bool didResolve = MatchIntensityResolver.TryResolve(
                MatchPhase.Standard,
                elapsedSeconds,
                ArbitraryLargeRemainingSeconds,
                in bands,
                out float intensity
            );

            // THEN
            Assert.That(didResolve, Is.True);
            Assert.That(intensity, Is.EqualTo(EarlyIntensity).Within(Tolerance));
        }

        [Test]
        public void TryResolve_StandardAtEarlyBandBoundary_ResolvesMidIntensity()
        {
            // GIVEN
            IntensityBands bands = BuildBands();

            // WHEN
            bool didResolve = MatchIntensityResolver.TryResolve(
                MatchPhase.Standard,
                EarlyUntilElapsedSeconds,
                ArbitraryLargeRemainingSeconds,
                in bands,
                out float intensity
            );

            // THEN
            Assert.That(didResolve, Is.True);
            Assert.That(intensity, Is.EqualTo(MidIntensity).Within(Tolerance));
        }

        [TestCase(120f)]
        [TestCase(150f)]
        public void TryResolve_StandardLateBand_ResolvesLateIntensity(float elapsedSeconds)
        {
            // GIVEN
            IntensityBands bands = BuildBands();

            // WHEN
            bool didResolve = MatchIntensityResolver.TryResolve(
                MatchPhase.Standard,
                elapsedSeconds,
                ArbitraryLargeRemainingSeconds,
                in bands,
                out float intensity
            );

            // THEN
            Assert.That(didResolve, Is.True);
            Assert.That(intensity, Is.EqualTo(LateIntensity).Within(Tolerance));
        }

        [Test]
        public void TryResolve_Overtime_ResolvesOvertimeIntensity()
        {
            // GIVEN
            IntensityBands bands = BuildBands();

            // WHEN
            bool didResolve = MatchIntensityResolver.TryResolve(MatchPhase.Overtime, 0f, 0f, in bands, out float intensity);

            // THEN
            Assert.That(didResolve, Is.True);
            Assert.That(intensity, Is.EqualTo(OvertimeIntensity).Within(Tolerance));
        }

        [TestCase(15f)]
        [TestCase(10f)]
        public void TryResolve_StandardFinalWindow_WinsOverTheElapsedBand(float remainingSeconds)
        {
            // GIVEN — elapsed sits in the early band, so a resolver that checked elapsed time first would answer early.
            IntensityBands bands = BuildBands();
            const float earlyBandElapsedSeconds = 10f;

            // WHEN
            bool didResolve = MatchIntensityResolver.TryResolve(MatchPhase.Standard, earlyBandElapsedSeconds, remainingSeconds, in bands, out float intensity);

            // THEN
            Assert.That(didResolve, Is.True);
            Assert.That(intensity, Is.EqualTo(FinalWindowIntensity).Within(Tolerance));
        }

        [Test]
        public void TryResolve_ShortenedStandardDurationNearItsFinalWindow_StillResolvesFinalWindowIntensity()
        {
            // GIVEN — a 90-second Standard phase with 15 authored for the final window: 75 elapsed sits in the mid
            // band by elapsed time alone, but only 15 seconds remain, which is what the final window is for.
            IntensityBands bands = BuildBands();
            const float elapsedSecondsOfANinetySecondMatch = 75f;
            const float remainingSecondsOfANinetySecondMatch = 15f;

            // WHEN
            bool didResolve = MatchIntensityResolver.TryResolve(
                MatchPhase.Standard,
                elapsedSecondsOfANinetySecondMatch,
                remainingSecondsOfANinetySecondMatch,
                in bands,
                out float intensity
            );

            // THEN
            Assert.That(didResolve, Is.True);
            Assert.That(intensity, Is.EqualTo(FinalWindowIntensity).Within(Tolerance));
        }

        [TestCase(MatchPhase.OvertimeCheck)]
        [TestCase(MatchPhase.Ended)]
        [TestCase(MatchPhase.Results)]
        public void TryResolve_HoldingPhases_ReturnsFalseAndZero(MatchPhase phase)
        {
            // GIVEN
            IntensityBands bands = BuildBands();

            // WHEN
            bool didResolve = MatchIntensityResolver.TryResolve(phase, 0f, 0f, in bands, out float intensity);

            // THEN
            Assert.That(didResolve, Is.False);
            Assert.That(intensity, Is.EqualTo(0f).Within(Tolerance));
        }

        private static IntensityBands BuildBands()
        {
            return new IntensityBands(
                IdleIntensity,
                EarlyIntensity,
                EarlyUntilElapsedSeconds,
                MidIntensity,
                MidUntilElapsedSeconds,
                LateIntensity,
                FinalWindowIntensity,
                FinalWindowSeconds,
                OvertimeIntensity
            );
        }
    }
}
