using GooGalaxy.Runtime.AI.Data;
using GooGalaxy.Runtime.AI.Models;
using GooGalaxy.Runtime.Shared.Constants;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.EditMode.AI
{
    [TestFixture]
    public class AiConfigSOTests
    {
        private const string AssetName = "TestAiConfig";
        private const float AuthoredMinThinkSeconds = 1.5f;
        private const float AuthoredMaxThinkSeconds = 3f;
        private const float AuthoredEnergyCeiling = 8f;
        private const int AuthoredSeed = 4242;
        private const float Tolerance = 0.0001f;

        private AiConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<AiConfigSO>();
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

        [Test]
        public void Config_AfterAuthoring_RoundTripsEveryAuthoredValue()
        {
            // GIVEN
            _config.SetAuthoredData(AuthoredMinThinkSeconds, AuthoredMaxThinkSeconds, AuthoredEnergyCeiling, isDiscardEnabled: false, AuthoredSeed);

            // WHEN
            AiConfig tuning = _config.Config;

            // THEN
            Assert.That(
                (tuning.MinThinkSeconds, tuning.MaxThinkSeconds, tuning.EnergyCeilingThreshold, tuning.IsDiscardEnabled, tuning.Seed),
                Is.EqualTo((AuthoredMinThinkSeconds, AuthoredMaxThinkSeconds, AuthoredEnergyCeiling, false, AuthoredSeed))
            );
        }

        [Test]
        public void Config_AfterAuthoringDiscardEnabled_ReportsItEnabled()
        {
            // GIVEN
            _config.SetAuthoredData(AuthoredMinThinkSeconds, AuthoredMaxThinkSeconds, AuthoredEnergyCeiling, isDiscardEnabled: true, AuthoredSeed);

            // WHEN
            AiConfig tuning = _config.Config;

            // THEN
            Assert.That(tuning.IsDiscardEnabled, Is.True);
        }

        [Test]
        public void Config_WithAnUnauthoredSeed_ReportsTheDerivedSeedSentinel()
        {
            // GIVEN — zero is what an unauthored asset deserializes to, and it means "derive from the match seed".
            _config.SetAuthoredData(AuthoredMinThinkSeconds, AuthoredMaxThinkSeconds, AuthoredEnergyCeiling, isDiscardEnabled: true, AiConfig.DerivedSeed);

            // WHEN
            AiConfig tuning = _config.Config;

            // THEN
            Assert.That(tuning.Seed, Is.EqualTo(AiConfig.DerivedSeed));
        }

        [Test]
        public void ValidateAuthoredData_MaximumAboveMinimum_LeavesTheMaximumUntouched()
        {
            // GIVEN
            _config.SetAuthoredData(AuthoredMinThinkSeconds, AuthoredMaxThinkSeconds, AuthoredEnergyCeiling, isDiscardEnabled: true, AuthoredSeed);

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.Config.MaxThinkSeconds, Is.EqualTo(AuthoredMaxThinkSeconds).Within(Tolerance));
        }

        [Test]
        public void ValidateAuthoredData_MaximumEqualToMinimum_LeavesTheMaximumUntouched()
        {
            // GIVEN — the boundary the warning is written around: equal is authored, not a fault.
            _config.SetAuthoredData(AuthoredMinThinkSeconds, AuthoredMinThinkSeconds, AuthoredEnergyCeiling, isDiscardEnabled: true, AuthoredSeed);

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.Config.MaxThinkSeconds, Is.EqualTo(AuthoredMinThinkSeconds).Within(Tolerance));
        }

        [Test]
        public void ValidateAuthoredData_MaximumBelowMinimum_RaisesTheMaximumToTheMinimum()
        {
            // GIVEN
            const float invalidMaxThinkSeconds = 0.5f;
            _config.SetAuthoredData(AuthoredMinThinkSeconds, invalidMaxThinkSeconds, AuthoredEnergyCeiling, isDiscardEnabled: true, AuthoredSeed);
            LogAssert.Expect(
                LogType.Warning,
                string.Format(AiLogMessages.AiConfigThinkRangeInvalidFormat, AssetName, invalidMaxThinkSeconds, AuthoredMinThinkSeconds)
            );

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.Config.MaxThinkSeconds, Is.EqualTo(AuthoredMinThinkSeconds).Within(Tolerance));
        }

        [Test]
        public void ValidateAuthoredData_MaximumBelowMinimum_LeavesTheMinimumUntouched()
        {
            // GIVEN
            const float invalidMaxThinkSeconds = 0.5f;
            _config.SetAuthoredData(AuthoredMinThinkSeconds, invalidMaxThinkSeconds, AuthoredEnergyCeiling, isDiscardEnabled: true, AuthoredSeed);
            LogAssert.Expect(
                LogType.Warning,
                string.Format(AiLogMessages.AiConfigThinkRangeInvalidFormat, AssetName, invalidMaxThinkSeconds, AuthoredMinThinkSeconds)
            );

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.Config.MinThinkSeconds, Is.EqualTo(AuthoredMinThinkSeconds).Within(Tolerance));
        }
    }
}
