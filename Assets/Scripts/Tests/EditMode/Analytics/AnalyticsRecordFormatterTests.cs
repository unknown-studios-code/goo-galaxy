using System;
using System.Globalization;
using System.Text;
using System.Threading;
using GooGalaxy.Runtime.Analytics.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Analytics
{
    [TestFixture]
    public class AnalyticsRecordFormatterTests
    {
        private static readonly AnalyticsSession _emptySession = default;

        private CultureInfo _originalCulture;

        [SetUp]
        public void SetUp()
        {
            _originalCulture = Thread.CurrentThread.CurrentCulture;
        }

        [TearDown]
        public void TearDown()
        {
            Thread.CurrentThread.CurrentCulture = _originalCulture;
        }

        [Test]
        public void TryAppendLine_SessionStart_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var session = new AnalyticsSession("session-id", "Pixel 9", "Android 15", "1.0.0");
            var record = AnalyticsRecord.ForSessionStart();

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in session);

            // THEN
            Assert.That(
                builder.ToString(),
                Is.EqualTo("{\"v\":1,\"t\":0,\"m\":0,\"e\":\"session_start\",\"device\":\"Pixel 9\",\"os\":\"Android 15\",\"app\":\"1.0.0\"}\n")
            );
        }

        [Test]
        public void TryAppendLine_SessionEnd_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForSessionEnd(5000L, 120000L, 3);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(builder.ToString(), Is.EqualTo("{\"v\":1,\"t\":5000,\"m\":3,\"e\":\"session_end\",\"dur_ms\":120000,\"matches\":3}\n"));
        }

        [Test]
        public void TryAppendLine_MatchStart_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var configuration = new MatchConfiguration(42, new PlayerSlot(1, PlayerControl.LocalHuman), new PlayerSlot(2, PlayerControl.Machine), 90f, 5f, 30f);
            var record = AnalyticsRecord.ForMatchStart(1000L, 1, in configuration);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(
                builder.ToString(),
                Is.EqualTo(
                    "{\"v\":1,\"t\":1000,\"m\":1,\"e\":\"match_start\",\"seed\":42,\"p1_ctrl\":\"LocalHuman\",\"p2_ctrl\":\"Machine\",\"std_s\":90,\"ot_s\":30}\n"
                )
            );
        }

        [Test]
        public void TryAppendLine_MatchEnd_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var outcome = new MatchOutcome(1, MatchEndReason.TimeLimit);
            var record = AnalyticsRecord.ForMatchEnd(20000L, 2, in outcome, 10, 7, 180000L, true);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(
                builder.ToString(),
                Is.EqualTo(
                    "{\"v\":1,\"t\":20000,\"m\":2,\"e\":\"match_end\",\"winner\":1,\"reason\":\"TimeLimit\",\"p1_score\":10,\"p2_score\":7,\"dur_ms\":180000,\"overtime\":true}\n"
                )
            );
        }

        [Test]
        public void TryAppendLine_CardDeployed_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForCardDeployed(3000L, 1, 2, new CardId("acid_crawler"), new HexCoordinates(2, -1), 4);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(
                builder.ToString(),
                Is.EqualTo("{\"v\":1,\"t\":3000,\"m\":1,\"e\":\"card_deployed\",\"p\":2,\"card\":\"acid_crawler\",\"q\":2,\"r\":-1,\"cost\":4}\n")
            );
        }

        [Test]
        public void TryAppendLine_CardPlayRejected_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForCardPlayRejected(4000L, 1, 2, new CardId("bio_phalanx"), CardPlayResult.InsufficientEnergy);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(
                builder.ToString(),
                Is.EqualTo("{\"v\":1,\"t\":4000,\"m\":1,\"e\":\"card_play_rejected\",\"p\":2,\"card\":\"bio_phalanx\",\"reason\":\"InsufficientEnergy\"}\n")
            );
        }

        [Test]
        public void TryAppendLine_EnergySpent_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForEnergySpent(6000L, 1, 2, 7.5f, true);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(builder.ToString(), Is.EqualTo("{\"v\":1,\"t\":6000,\"m\":1,\"e\":\"energy_spent\",\"p\":2,\"after\":7.5,\"ok\":true}\n"));
        }

        [Test]
        public void TryAppendLine_ConversionEvent_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForConversion(7000L, 1, 2, 3, 1);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(builder.ToString(), Is.EqualTo("{\"v\":1,\"t\":7000,\"m\":1,\"e\":\"conversion_event\",\"p\":2,\"converted\":3,\"stripped\":1}\n"));
        }

        [Test]
        public void TryAppendLine_AbilityResolved_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForAbilityResolved(8000L, 1, 2, 5, 6, 2);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(
                builder.ToString(),
                Is.EqualTo("{\"v\":1,\"t\":8000,\"m\":1,\"e\":\"ability_resolved\",\"p\":2,\"affected\":5,\"hexes\":6,\"destroyed\":2}\n")
            );
        }

        [Test]
        public void TryAppendLine_CardDiscarded_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForCardDiscarded(9000L, 1, 2, new CardId("subject_alpha"), 3);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(
                builder.ToString(),
                Is.EqualTo("{\"v\":1,\"t\":9000,\"m\":1,\"e\":\"card_discarded\",\"p\":2,\"card\":\"subject_alpha\",\"slot\":3}\n")
            );
        }

        [Test]
        public void TryAppendLine_PhaseChanged_WritesExactGoldenLine()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForPhaseChanged(10000L, 1, MatchPhase.Overtime);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(builder.ToString(), Is.EqualTo("{\"v\":1,\"t\":10000,\"m\":1,\"e\":\"phase_changed\",\"phase\":\"Overtime\"}\n"));
        }

        [Test]
        public void TryAppendLine_CardValueWithSpecialCharacters_EscapesQuotesBackslashesAndControlCharacters()
        {
            // GIVEN
            var builder = new StringBuilder();
            var card = new CardId("a\"b\\c\nd\te\u0001f");
            var record = AnalyticsRecord.ForCardDeployed(0L, 0, 0, card, default, 0);
            string expectedCardField = "a\\\"b\\\\c\\nd\\te\\u0001f";

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(
                builder.ToString(),
                Is.EqualTo("{\"v\":1,\"t\":0,\"m\":0,\"e\":\"card_deployed\",\"p\":0,\"card\":\"" + expectedCardField + "\",\"q\":0,\"r\":0,\"cost\":0}\n")
            );
        }

        [Test]
        public void TryAppendLine_DeDeThreadCulture_WritesFloatsWithInvariantDecimalPoint()
        {
            // GIVEN
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForEnergySpent(0L, 0, 0, 3.5f, true);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(builder.ToString(), Is.EqualTo("{\"v\":1,\"t\":0,\"m\":0,\"e\":\"energy_spent\",\"p\":0,\"after\":3.5,\"ok\":true}\n"));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TryAppendLine_NonFiniteFloatField_WritesJsonNull(float energyAfter)
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForEnergySpent(0L, 0, 0, energyAfter, false);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(builder.ToString(), Is.EqualTo("{\"v\":1,\"t\":0,\"m\":0,\"e\":\"energy_spent\",\"p\":0,\"after\":null,\"ok\":false}\n"));
        }

        [Test]
        public void TryAppendLine_Int64MinValueField_WritesTheFullNegativeDigitString()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForSessionEnd(long.MinValue, long.MinValue, 0);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(
                builder.ToString(),
                Is.EqualTo("{\"v\":1,\"t\":-9223372036854775808,\"m\":0,\"e\":\"session_end\",\"dur_ms\":-9223372036854775808,\"matches\":0}\n")
            );
        }

        [Test]
        public void TryAppendLine_OutOfRangeEnumValue_WritesTheBareNumber()
        {
            // GIVEN
            var builder = new StringBuilder();
            var record = AnalyticsRecord.ForCardPlayRejected(0L, 0, 0, CardId.Empty, (CardPlayResult)999);

            // WHEN
            AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That(builder.ToString(), Is.EqualTo("{\"v\":1,\"t\":0,\"m\":0,\"e\":\"card_play_rejected\",\"p\":0,\"card\":\"\",\"reason\":999}\n"));
        }

        [Test]
        public void TryAppendLine_NoneEventType_ReturnsFalseAndLeavesBuilderUntouched()
        {
            // GIVEN
            var builder = new StringBuilder();
            AnalyticsRecord record = default;

            // WHEN
            bool wasAppended = AnalyticsRecordFormatter.TryAppendLine(builder, in record, in _emptySession);

            // THEN
            Assert.That((wasAppended, builder.Length), Is.EqualTo((false, 0)));
        }

        [Test]
        public void TryAppendLine_NullBuilder_ThrowsArgumentNullException()
        {
            // GIVEN
            var record = AnalyticsRecord.ForSessionStart();

            // WHEN / THEN
            Assert.Throws<ArgumentNullException>(() => AnalyticsRecordFormatter.TryAppendLine(null, in record, in _emptySession));
        }
    }
}
