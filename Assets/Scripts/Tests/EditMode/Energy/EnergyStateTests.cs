using GooGalaxy.Runtime.Energy.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Energy
{
    [TestFixture]
    public class EnergyStateTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Constructor_WithConfig_SetsCurrentEnergyToStartingEnergy()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.0f, 4.5f);

            // WHEN
            var state = new EnergyState(config);

            // THEN
            Assert.That(state.CurrentEnergy, Is.EqualTo(4.5f).Within(Tolerance));
        }

        [Test]
        public void SetEnergy_OutsideConfiguredRange_ClampsToBounds()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.0f, 5.0f);
            var state = new EnergyState(config);

            // WHEN
            state.SetEnergy(15.0f);

            // THEN
            Assert.That(state.CurrentEnergy, Is.EqualTo(10.0f).Within(Tolerance));

            // WHEN
            state.SetEnergy(-2.0f);

            // THEN
            Assert.That(state.CurrentEnergy, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void IsOvertime_WhenTrue_DoublesEffectiveRegenRate()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.5f, 5.0f);
            var state = new EnergyState(config)
            {
                // WHEN
                IsOvertime = true,
            };

            // THEN
            Assert.That(state.EffectiveRegenRate, Is.EqualTo(3.0f).Within(Tolerance));
        }

        [Test]
        public void IsOvertime_WhenFalse_RestoresEffectiveRegenRateToBase()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.5f, 5.0f);
            var state = new EnergyState(config) { IsOvertime = true };

            // WHEN
            state.IsOvertime = false;

            // THEN
            Assert.That(state.EffectiveRegenRate, Is.EqualTo(1.5f).Within(Tolerance));
        }

        [Test]
        public void Constructor_WithAsymmetricKomiConfigs_SetsPerPlayerStartingEnergy()
        {
            // GIVEN
            var p1Config = new EnergyConfig(10.0f, 1f / 2.8f, 5.0f);
            var p2Config = new EnergyConfig(10.0f, 1f / 2.8f, 5.5f);

            // WHEN
            var p1State = new EnergyState(p1Config);
            var p2State = new EnergyState(p2Config);

            // THEN
            Assert.That(p1State.CurrentEnergy, Is.EqualTo(5.0f).Within(Tolerance));
            Assert.That(p2State.CurrentEnergy, Is.EqualTo(5.5f).Within(Tolerance));
        }

        [Test]
        public void EffectiveRegenRate_CatchUpActiveWithoutOvertime_MatchesOneEnergyPerApproximately2Point43Seconds()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1f / 2.8f, 5.0f);
            var state = new EnergyState(config) { CatchUpMultiplier = 1.15f };

            // WHEN
            float rate = state.EffectiveRegenRate;

            // THEN — 2.8 / 1.15 is approximately 2.4348 seconds per whole Energy point.
            Assert.That(rate, Is.EqualTo(0.410714f).Within(Tolerance));
        }

        [Test]
        public void EffectiveRegenRate_CatchUpActiveDuringOvertime_ComposesMultiplicativelyToOneEnergyPerApproximately1Point22Seconds()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1f / 2.8f, 5.0f);
            var state = new EnergyState(config) { IsOvertime = true, CatchUpMultiplier = 1.15f };

            // WHEN
            float rate = state.EffectiveRegenRate;

            // THEN — 2.8 / (2 x 1.15) is approximately 1.2174 seconds per whole Energy point.
            Assert.That(rate, Is.EqualTo(0.821429f).Within(Tolerance));
        }
    }
}
