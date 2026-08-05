using GooGalaxy.Runtime.Energy.Models;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Energy
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
    }
}
