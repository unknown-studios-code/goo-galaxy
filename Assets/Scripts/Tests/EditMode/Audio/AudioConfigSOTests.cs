using FMODUnity;
using GooGalaxy.Runtime.Audio.Data;
using GooGalaxy.Runtime.Audio.Models;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.EditMode.Audio
{
    [TestFixture]
    public class AudioConfigSOTests
    {
        private const float Tolerance = 0.0001f;

        private AudioConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        [TestCase(-0.5f, 0f)]
        [TestCase(1.5f, 1f)]
        public void ValidateBands_IdleIntensityOutOfRange_ClampsIntoZeroToOneAndFlagsTheIssue(float authoredIdle, float expectedIdle)
        {
            // GIVEN
            var authored = new IntensityBands(authoredIdle, 0.2f, 60f, 0.5f, 120f, 0.7f, 0.85f, 15f, 1f);

            // WHEN
            IntensityBandIssue issues = AudioConfigSO.ValidateBands(in authored, out IntensityBands validated);

            // THEN
            Assert.That(issues.HasFlag(IntensityBandIssue.IntensityOutOfRange), Is.True);
            Assert.That(validated.Idle, Is.EqualTo(expectedIdle).Within(Tolerance));
        }

        [Test]
        public void ValidateBands_NegativeThreshold_ClampsToZeroAndFlagsTheIssue()
        {
            // GIVEN
            var authored = new IntensityBands(0f, 0.2f, -10f, 0.5f, 120f, 0.7f, 0.85f, 15f, 1f);

            // WHEN
            IntensityBandIssue issues = AudioConfigSO.ValidateBands(in authored, out IntensityBands validated);

            // THEN
            Assert.That(issues.HasFlag(IntensityBandIssue.ThresholdsOutOfOrder), Is.True);
            Assert.That(validated.EarlyUntilElapsedSeconds, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ValidateBands_MidThresholdBelowEarlyThreshold_RaisesTheMidThresholdToTheEarlyOne()
        {
            // GIVEN
            var authored = new IntensityBands(0f, 0.2f, 60f, 0.5f, 30f, 0.7f, 0.85f, 15f, 1f);

            // WHEN
            IntensityBandIssue issues = AudioConfigSO.ValidateBands(in authored, out IntensityBands validated);

            // THEN
            Assert.That(issues.HasFlag(IntensityBandIssue.ThresholdsOutOfOrder), Is.True);
            Assert.That(validated.MidUntilElapsedSeconds, Is.EqualTo(60f).Within(Tolerance));
        }

        [Test]
        public void ValidateBands_IntensitiesDescendingButInRange_FlagsTheIssueWithoutChangingAnyValue()
        {
            // GIVEN — every intensity is within 0..1 and every threshold is already ascending, so only the
            // descending-intensity rule has anything to flag.
            var authored = new IntensityBands(0.5f, 0.4f, 60f, 0.3f, 120f, 0.2f, 0.1f, 15f, 0f);

            // WHEN
            IntensityBandIssue issues = AudioConfigSO.ValidateBands(in authored, out IntensityBands validated);

            // THEN
            Assert.That(issues.HasFlag(IntensityBandIssue.IntensitiesNotAscending), Is.True);
            Assert.That(validated.Idle, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(validated.Early, Is.EqualTo(0.4f).Within(Tolerance));
            Assert.That(validated.Mid, Is.EqualTo(0.3f).Within(Tolerance));
            Assert.That(validated.Late, Is.EqualTo(0.2f).Within(Tolerance));
            Assert.That(validated.FinalWindow, Is.EqualTo(0.1f).Within(Tolerance));
            Assert.That(validated.Overtime, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void TryGetCueEvent_CueWithNoAuthoredRow_ReturnsFalse()
        {
            // GIVEN

            // WHEN
            bool wasFound = _config.TryGetCueEvent(AudioCue.ButtonPress, out EventReference _);

            // THEN
            Assert.That(wasFound, Is.False);
        }
    }
}
