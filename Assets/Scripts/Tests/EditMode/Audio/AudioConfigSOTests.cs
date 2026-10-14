using System.Collections.Generic;
using System.Text;
using FMODUnity;
using GooGalaxy.Runtime.Audio.Data;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Constants;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.EditMode.Audio
{
    [TestFixture]
    public class AudioConfigSOTests
    {
        private const float Tolerance = 0.0001f;
        private const string ConfigName = "TestAudioConfig";
        private const string AcidCrawlerId = "acid_crawler";
        private const string CryoStasisId = "cryo_stasis";
        private const string FirstEventPath = "event:/test/first";
        private const string SecondEventPath = "event:/test/second";

        private AudioConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            _config.name = ConfigName;
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

        [Test]
        public void CardCues_RowsAuthored_ExposesEveryRowInAuthoredOrder()
        {
            // GIVEN
            SetCardCues((AcidCrawlerId, FirstEventPath), (CryoStasisId, SecondEventPath));

            // WHEN
            IReadOnlyList<CardCueEntry> rows = _config.CardCues;

            // THEN
            Assert.That((rows.Count, rows[0].CardId, rows[1].CardId), Is.EqualTo((2, AcidCrawlerId, CryoStasisId)));
        }

        [Test]
        public void CardCues_RowsAuthored_ExposesEachRowsEventPath()
        {
            // GIVEN
            SetCardCues((AcidCrawlerId, FirstEventPath));

            // WHEN
            IReadOnlyList<CardCueEntry> rows = _config.CardCues;

            // THEN
            Assert.That(rows[0].Event.Path, Is.EqualTo(FirstEventPath));
        }

        [Test]
        public void CardCues_NoRowsAuthored_IsEmpty()
        {
            // GIVEN

            // WHEN
            IReadOnlyList<CardCueEntry> rows = _config.CardCues;

            // THEN
            Assert.That(rows, Is.Empty);
        }

        [Test]
        public void ValidateAuthoredData_CardListedTwice_WarnsOnceNamingTheCard()
        {
            // GIVEN
            SetCardCues((AcidCrawlerId, FirstEventPath), (AcidCrawlerId, SecondEventPath));
            LogAssert.Expect(LogType.Warning, string.Format(AudioLogMessages.ConfigCardCueDuplicateFormat, ConfigName, AcidCrawlerId));

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValidateAuthoredData_RowWithABlankId_WarnsNamingTheRowIndex()
        {
            // GIVEN
            SetCardCues((AcidCrawlerId, FirstEventPath), ("", SecondEventPath));
            LogAssert.Expect(LogType.Warning, string.Format(AudioLogMessages.ConfigCardCueIdMissingFormat, ConfigName, 1));

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValidateAuthoredData_DistinctCardRows_LogsNothing()
        {
            // GIVEN
            SetCardCues((AcidCrawlerId, FirstEventPath), (CryoStasisId, SecondEventPath));

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        private void SetCardCues(params (string CardId, string EventPath)[] rows)
        {
            var json = new StringBuilder("{\"_cardCues\":[");

            for (int i = 0; i < rows.Length; i++)
            {
                json.Append(i == 0 ? "" : ",");
                json.Append("{\"CardId\":\"").Append(rows[i].CardId).Append("\",\"Event\":{\"Path\":\"").Append(rows[i].EventPath).Append("\"}}");
            }

            json.Append("]}");
            JsonUtility.FromJsonOverwrite(json.ToString(), _config);
        }
    }
}
