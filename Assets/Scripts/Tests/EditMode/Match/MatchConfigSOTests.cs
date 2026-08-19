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

        [Test]
        public void ValidateAuthoredData_EmptyStartingPlacements_Warns()
        {
            // GIVEN
            _config.SetAuthoredData(180f, 3f, 60f);
            LogAssert.Expect(LogType.Warning, string.Format(MatchLogMessages.MatchConfigNoPlacementsFormat, AssetName));

            // WHEN
            _config.ValidateAuthoredData();

            // THEN
            Assert.That(_config.StartingPlacements, Is.Empty);
        }
    }
}
