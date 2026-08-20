using GooGalaxy.Runtime.Match.Data;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Constants;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.EditMode.Match
{
    [TestFixture]
    public class MatchConfigSOTests
    {
        private const float MinimumPhaseDurationSeconds = 1f;
        private const string AssetName = "TestMatchConfig";
        private const float Tolerance = 0.0001f;

        // The authored defaults CatchUpConfig's field initializer carries on MatchConfigSO, restated here as
        // literals so the clamp tests below build an out-of-band value around a known in-band baseline for the
        // other three fields, per Rule 3's ban on re-deriving an expectation from production data.
        private const float DefaultThresholdRatio = 0.4f;
        private const float DefaultRegenMultiplier = 1.15f;
        private const float DefaultDurationSeconds = 20f;
        private const float DefaultCooldownSeconds = 60f;

        private MatchConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<MatchConfigSO>();
            _config.name = AssetName;
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        public void ValidateAuthoredData_NonPositiveStandardDuration_ClampsToMinimumAndWarns(float authoredDuration)
        {
            // GIVEN
            _config.SetAuthoredData(
                authoredDuration,
                3f,
                60f,
                3f,
                new StartingPlacement
                {
                    CardId = "unit_alpha",
                    UnitId = 1,
                    PlayerId = 1,
                    Q = 0,
                    R = 0,
                }
            );
            LogAssert.Expect(
                LogType.Warning,
                string.Format(
                    MatchLogMessages.MatchConfigPhaseDurationInvalidFormat,
                    AssetName,
                    nameof(MatchConfigSO.StandardDurationSeconds),
                    authoredDuration,
                    MinimumPhaseDurationSeconds
                )
            );

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.StandardDurationSeconds, Is.EqualTo(MinimumPhaseDurationSeconds));
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        public void ValidateAuthoredData_NonPositiveOvertimeLeadHoldSeconds_ClampsToMinimumAndWarns(float authoredHoldSeconds)
        {
            // GIVEN
            _config.SetAuthoredData(
                180f,
                3f,
                60f,
                authoredHoldSeconds,
                new StartingPlacement
                {
                    CardId = "unit_alpha",
                    UnitId = 1,
                    PlayerId = 1,
                    Q = 0,
                    R = 0,
                }
            );
            LogAssert.Expect(
                LogType.Warning,
                string.Format(
                    MatchLogMessages.MatchConfigPhaseDurationInvalidFormat,
                    AssetName,
                    nameof(MatchConfigSO.OvertimeLeadHoldSeconds),
                    authoredHoldSeconds,
                    MinimumPhaseDurationSeconds
                )
            );

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.OvertimeLeadHoldSeconds, Is.EqualTo(MinimumPhaseDurationSeconds));
        }

        [TestCase(0.05f, 0.1f)]
        [TestCase(0.9f, 0.49f)]
        public void ValidateAuthoredData_ThresholdRatioOutOfBand_ClampsToTheNearestBound(float authoredThresholdRatio, float expectedThresholdRatio)
        {
            // GIVEN
            _config.SetAuthoredData(180f, 3f, 60f, 3f, ValidPlacement());
            SetCatchUp(_config, new CatchUpConfig(authoredThresholdRatio, DefaultRegenMultiplier, DefaultDurationSeconds, DefaultCooldownSeconds));

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.CatchUp.ThresholdRatio, Is.EqualTo(expectedThresholdRatio).Within(Tolerance));
        }

        [Test]
        public void ValidateAuthoredData_ThresholdRatioOutOfBand_WarnsWithTheAuthoredBandAndTheClampedValue()
        {
            // GIVEN — the clamp tests above pin the clamped value; this pins the designer-facing warning text
            // that goes with it, using the same six-placeholder message the production code formats.
            const float authoredThresholdRatio = 0.9f;
            _config.SetAuthoredData(180f, 3f, 60f, 3f, ValidPlacement());
            SetCatchUp(_config, new CatchUpConfig(authoredThresholdRatio, DefaultRegenMultiplier, DefaultDurationSeconds, DefaultCooldownSeconds));
            LogAssert.Expect(
                LogType.Warning,
                string.Format(
                    MatchLogMessages.MatchConfigCatchUpFieldInvalidFormat,
                    AssetName,
                    nameof(CatchUpConfig.ThresholdRatio),
                    authoredThresholdRatio,
                    0.1f,
                    0.49f,
                    0.49f
                )
            );

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.CatchUp.ThresholdRatio, Is.EqualTo(0.49f).Within(Tolerance));
        }

        [TestCase(0.5f, 1f)]
        [TestCase(2f, 1.5f)]
        public void ValidateAuthoredData_RegenMultiplierOutOfBand_ClampsToTheNearestBound(float authoredRegenMultiplier, float expectedRegenMultiplier)
        {
            // GIVEN
            _config.SetAuthoredData(180f, 3f, 60f, 3f, ValidPlacement());
            SetCatchUp(_config, new CatchUpConfig(DefaultThresholdRatio, authoredRegenMultiplier, DefaultDurationSeconds, DefaultCooldownSeconds));

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.CatchUp.RegenMultiplier, Is.EqualTo(expectedRegenMultiplier).Within(Tolerance));
        }

        [TestCase(1f, 5f)]
        [TestCase(90f, 60f)]
        public void ValidateAuthoredData_DurationSecondsOutOfBand_ClampsToTheNearestBound(float authoredDurationSeconds, float expectedDurationSeconds)
        {
            // GIVEN
            _config.SetAuthoredData(180f, 3f, 60f, 3f, ValidPlacement());
            SetCatchUp(_config, new CatchUpConfig(DefaultThresholdRatio, DefaultRegenMultiplier, authoredDurationSeconds, DefaultCooldownSeconds));

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.CatchUp.DurationSeconds, Is.EqualTo(expectedDurationSeconds).Within(Tolerance));
        }

        [TestCase(-10f, 0f)]
        [TestCase(240f, 180f)]
        public void ValidateAuthoredData_CooldownSecondsOutOfBand_ClampsToTheNearestBound(float authoredCooldownSeconds, float expectedCooldownSeconds)
        {
            // GIVEN
            _config.SetAuthoredData(180f, 3f, 60f, 3f, ValidPlacement());
            SetCatchUp(_config, new CatchUpConfig(DefaultThresholdRatio, DefaultRegenMultiplier, DefaultDurationSeconds, authoredCooldownSeconds));

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.CatchUp.CooldownSeconds, Is.EqualTo(expectedCooldownSeconds).Within(Tolerance));
        }

        [Test]
        public void ValidateAuthoredData_TwoCatchUpFieldsOutOfBandAtOnce_ClampsAndWarnsForBothRatherThanStoppingAtTheFirst()
        {
            // GIVEN — ValidateCatchUp accumulates with |= rather than short-circuiting specifically so every
            // field is still checked once an earlier one has already failed; this pins that both actually fire
            // together instead of the second silently riding along on an accumulator nothing exercises.
            const float authoredThresholdRatio = 0.9f;
            const float authoredRegenMultiplier = 2f;
            _config.SetAuthoredData(180f, 3f, 60f, 3f, ValidPlacement());
            SetCatchUp(_config, new CatchUpConfig(authoredThresholdRatio, authoredRegenMultiplier, DefaultDurationSeconds, DefaultCooldownSeconds));
            LogAssert.Expect(
                LogType.Warning,
                string.Format(
                    MatchLogMessages.MatchConfigCatchUpFieldInvalidFormat,
                    AssetName,
                    nameof(CatchUpConfig.ThresholdRatio),
                    authoredThresholdRatio,
                    0.1f,
                    0.49f,
                    0.49f
                )
            );
            LogAssert.Expect(
                LogType.Warning,
                string.Format(
                    MatchLogMessages.MatchConfigCatchUpFieldInvalidFormat,
                    AssetName,
                    nameof(CatchUpConfig.RegenMultiplier),
                    authoredRegenMultiplier,
                    1f,
                    1.5f,
                    1.5f
                )
            );

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.CatchUp.ThresholdRatio, Is.EqualTo(0.49f).Within(Tolerance));
            Assert.That(_config.CatchUp.RegenMultiplier, Is.EqualTo(1.5f).Within(Tolerance));
        }

        [Test]
        public void ValidateAuthoredData_EmptyStartingPlacements_Warns()
        {
            // GIVEN
            _config.SetAuthoredData(180f, 3f, 60f, 3f);
            LogAssert.Expect(LogType.Warning, string.Format(MatchLogMessages.MatchConfigNoPlacementsFormat, AssetName));

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.StartingPlacements, Is.Empty);
        }

        [Test]
        public void SetAuthoredData_OvertimeLeadHoldSecondsAuthored_ReadsBackTheAuthoredValue()
        {
            // GIVEN
            const float authoredHoldSeconds = 4.5f;

            // WHEN
            _config.SetAuthoredData(180f, 3f, 60f, authoredHoldSeconds);

            // THEN
            Assert.That(_config.OvertimeLeadHoldSeconds, Is.EqualTo(authoredHoldSeconds).Within(0.0001f));
        }

        [Test]
        public void CatchUp_UnauthoredAsset_DefaultsToTheAuthoredBaseline()
        {
            // GIVEN

            // WHEN
            CatchUpConfig catchUp = _config.CatchUp;

            // THEN
            Assert.That(catchUp.ThresholdRatio, Is.EqualTo(DefaultThresholdRatio).Within(Tolerance));
            Assert.That(catchUp.RegenMultiplier, Is.EqualTo(DefaultRegenMultiplier).Within(Tolerance));
            Assert.That(catchUp.DurationSeconds, Is.EqualTo(DefaultDurationSeconds).Within(Tolerance));
            Assert.That(catchUp.CooldownSeconds, Is.EqualTo(DefaultCooldownSeconds).Within(Tolerance));
        }

        private static StartingPlacement ValidPlacement()
        {
            return new StartingPlacement
            {
                CardId = "unit_alpha",
                UnitId = 1,
                PlayerId = 1,
                Q = 0,
                R = 0,
            };
        }

        // Goes through the same authoring seam SetAuthoredData is, rather than driving the serialized field
        // directly: it skips validation by design, which is exactly what these tests need in order to watch
        // ValidateAuthoredData act on a value the Inspector's [Range] would never have let through.
        private static void SetCatchUp(MatchConfigSO config, CatchUpConfig catchUp)
        {
            config.SetAuthoredCatchUp(catchUp);
        }
    }
}
