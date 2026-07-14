using GooGalaxy.Runtime.Energy.Models;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Energy
{
    [TestFixture]
    public class EnergyStateTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Constructor_SetsCurrentEnergyToStartingEnergy()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.0f, 4.5f);

            // WHEN
            var state = new EnergyState(config);

            // THEN
            Assert.AreEqual(4.5f, state.CurrentEnergy, Tolerance);
        }

        [Test]
        public void SetEnergy_ClampsWithinBounds()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.0f, 5.0f);
            var state = new EnergyState(config);

            // WHEN
            state.SetEnergy(15.0f);

            // THEN
            Assert.AreEqual(10.0f, state.CurrentEnergy, Tolerance);

            // WHEN
            state.SetEnergy(-2.0f);

            // THEN
            Assert.AreEqual(0f, state.CurrentEnergy, Tolerance);
        }

        [Test]
        public void IsOvertime_WhenTrue_DoublesEffectiveRegenRate()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.5f, 5.0f);
            var state = new EnergyState(config);

            // WHEN
            state.IsOvertime = true;

            // THEN
            Assert.AreEqual(3.0f, state.EffectiveRegenRate, Tolerance);
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
            Assert.AreEqual(1.5f, state.EffectiveRegenRate, Tolerance);
        }

        [Test]
        public void KomiStartingAsymmetry_PlayerConfigsSetCorrectStartingValues()
        {
            // GIVEN
            var p1Config = new EnergyConfig(10.0f, 1f / 2.8f, 5.0f);
            var p2Config = new EnergyConfig(10.0f, 1f / 2.8f, 5.5f);

            // WHEN
            var p1State = new EnergyState(p1Config);
            var p2State = new EnergyState(p2Config);

            // THEN
            Assert.AreEqual(5.0f, p1State.CurrentEnergy, Tolerance);
            Assert.AreEqual(5.5f, p2State.CurrentEnergy, Tolerance);
        }
    }
}
