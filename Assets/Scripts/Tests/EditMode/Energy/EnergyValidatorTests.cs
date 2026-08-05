using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Services;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Energy
{
    [TestFixture]
    public class EnergyValidatorTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void CanAfford_WithNegativeCost_ReturnsFalse()
        {
            // GIVEN
            const float currentEnergy = 10f;

            // WHEN
            bool canAfford = EnergyValidator.CanAfford(currentEnergy, -1f);

            // THEN
            Assert.That(canAfford, Is.False);
        }

        [Test]
        public void CanAfford_WithSufficientEnergy_ReturnsTrue()
        {
            // GIVEN
            float current = 5.0f;
            float cost = 3.0f;

            // WHEN
            bool result = EnergyValidator.CanAfford(current, cost);

            // THEN
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanAfford_WithInsufficientEnergy_ReturnsFalse()
        {
            // GIVEN
            float current = 2.0f;
            float cost = 3.0f;

            // WHEN
            bool result = EnergyValidator.CanAfford(current, cost);

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void CanAfford_WithExactAmount_ReturnsTrue()
        {
            // GIVEN
            float current = 3.0f;
            float cost = 3.0f;

            // WHEN
            bool result = EnergyValidator.CanAfford(current, cost);

            // THEN
            Assert.That(result, Is.True);
        }

        [Test]
        public void TrySpend_WithSufficientEnergy_DeductsCorrectAmountAndReturnsSuccess()
        {
            // GIVEN
            float current = 5.0f;
            float cost = 3.0f;

            // WHEN
            SpendResult result = EnergyValidator.TrySpend(ref current, cost);

            // THEN
            Assert.That(result, Is.EqualTo(SpendResult.Success));
            Assert.That(current, Is.EqualTo(2.0f).Within(Tolerance));
        }

        [Test]
        public void TrySpend_WithInsufficientEnergy_EnergyUnchangedAndReturnsInsufficientEnergy()
        {
            // GIVEN
            float current = 2.0f;
            float cost = 3.0f;

            // WHEN
            SpendResult result = EnergyValidator.TrySpend(ref current, cost);

            // THEN
            Assert.That(result, Is.EqualTo(SpendResult.InsufficientEnergy));
            Assert.That(current, Is.EqualTo(2.0f).Within(Tolerance));
        }

        [Test]
        public void TrySpend_WithNegativeCost_EnergyUnchangedAndReturnsInsufficientEnergy()
        {
            // GIVEN
            float current = 5.0f;
            float cost = -1.0f;

            // WHEN
            SpendResult result = EnergyValidator.TrySpend(ref current, cost);

            // THEN
            Assert.That(result, Is.EqualTo(SpendResult.InsufficientEnergy));
            Assert.That(current, Is.EqualTo(5.0f).Within(Tolerance));
        }

        [Test]
        public void TrySpend_WithZeroCost_ReturnsSuccessAndEnergyUnchanged()
        {
            // GIVEN
            float current = 5.0f;
            float cost = 0f;

            // WHEN
            SpendResult result = EnergyValidator.TrySpend(ref current, cost);

            // THEN
            Assert.That(result, Is.EqualTo(SpendResult.Success));
            Assert.That(current, Is.EqualTo(5.0f).Within(Tolerance));
        }
    }
}
