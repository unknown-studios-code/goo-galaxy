using GooGalaxy.Runtime.Energy.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Energy
{
    [TestFixture]
    public class EnergyConfigTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Constructor_SixArg_ExposesEveryAuthoredValueUnchanged()
        {
            // GIVEN
            const float maxEnergy = 12f;
            const float regenRate = 0.4f;
            const float startingEnergy = 6f;
            const float cloneCostMultiplier = 0.75f;
            const float jumpEnergyCost = 1.25f;
            const float discardEnergyCost = 0.9f;

            // WHEN
            var config = new EnergyConfig(maxEnergy, regenRate, startingEnergy, cloneCostMultiplier, jumpEnergyCost, discardEnergyCost);

            // THEN
            Assert.That(config.MaxEnergy, Is.EqualTo(maxEnergy).Within(Tolerance));
            Assert.That(config.RegenRate, Is.EqualTo(regenRate).Within(Tolerance));
            Assert.That(config.StartingEnergy, Is.EqualTo(startingEnergy).Within(Tolerance));
            Assert.That(config.CloneCostMultiplier, Is.EqualTo(cloneCostMultiplier).Within(Tolerance));
            Assert.That(config.JumpEnergyCost, Is.EqualTo(jumpEnergyCost).Within(Tolerance));
            Assert.That(config.DiscardEnergyCost, Is.EqualTo(discardEnergyCost).Within(Tolerance));
        }

        [Test]
        public void Constructor_ThreeArg_CloneCostMultiplierDefaultsToOneHalf()
        {
            // GIVEN

            // WHEN
            var config = new EnergyConfig(10f, 1f, 5f);

            // THEN
            Assert.That(config.CloneCostMultiplier, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Constructor_ThreeArg_JumpEnergyCostDefaultsToOneHalf()
        {
            // GIVEN

            // WHEN
            var config = new EnergyConfig(10f, 1f, 5f);

            // THEN
            Assert.That(config.JumpEnergyCost, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Constructor_ThreeArg_DiscardEnergyCostDefaultsToOneHalf()
        {
            // GIVEN

            // WHEN
            var config = new EnergyConfig(10f, 1f, 5f);

            // THEN
            Assert.That(config.DiscardEnergyCost, Is.EqualTo(0.5f).Within(Tolerance));
        }
    }
}
