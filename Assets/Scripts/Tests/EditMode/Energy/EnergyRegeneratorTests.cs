using GooGalaxy.Runtime.Energy.Services;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Energy
{
    [TestFixture]
    public class EnergyRegeneratorTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Tick_WithDeltaTimeProducingOneEnergy_CorrectlyAdded()
        {
            // GIVEN
            float current = 5.0f;
            float regenRate = 1.0f;
            float dt = 1.0f;
            float max = 10.0f;

            // WHEN
            float result = EnergyRegenerator.Tick(current, dt, regenRate, max);

            // THEN
            Assert.AreEqual(6.0f, result, Tolerance);
        }

        [Test]
        public void Tick_AtCap_ReturnsMaxEnergy()
        {
            // GIVEN
            float current = 10.0f;
            float regenRate = 1.0f;
            float dt = 5.0f;
            float max = 10.0f;

            // WHEN
            float result = EnergyRegenerator.Tick(current, dt, regenRate, max);

            // THEN
            Assert.AreEqual(10.0f, result, Tolerance);
        }

        [Test]
        public void Tick_WithExcess_ClampsToMaxEnergy()
        {
            // GIVEN
            float current = 9.5f;
            float regenRate = 1.0f;
            float dt = 2.0f;
            float max = 10.0f;

            // WHEN
            float result = EnergyRegenerator.Tick(current, dt, regenRate, max);

            // THEN
            Assert.AreEqual(10.0f, result, Tolerance);
        }

        [Test]
        public void Tick_WithZeroDeltaTime_EnergyUnchanged()
        {
            // GIVEN
            float current = 5.0f;
            float regenRate = 1.0f;
            float dt = 0f;
            float max = 10.0f;

            // WHEN
            float result = EnergyRegenerator.Tick(current, dt, regenRate, max);

            // THEN
            Assert.AreEqual(5.0f, result, Tolerance);
        }

        [Test]
        public void Tick_WithNegativeDeltaTime_EnergyUnchanged()
        {
            // GIVEN
            float current = 5.0f;
            float regenRate = 1.0f;
            float dt = -1.0f;
            float max = 10.0f;

            // WHEN
            float result = EnergyRegenerator.Tick(current, dt, regenRate, max);

            // THEN
            Assert.AreEqual(5.0f, result, Tolerance);
        }

        [Test]
        public void Tick_OverLongSimulatedTime_NeverExceedsMaxEnergy()
        {
            // GIVEN
            float current = 0f;
            float regenRate = 10f;
            float dt = 100.0f;
            float max = 10.0f;

            // WHEN
            float result = EnergyRegenerator.Tick(current, dt, regenRate, max);

            // THEN
            Assert.AreEqual(10.0f, result, Tolerance);
        }
    }
}
