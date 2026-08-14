using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Services;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Energy
{
    [TestFixture]
    public class MoveCostResolverTests
    {
        private const float Tolerance = 0.0001f;
        private const MoveType UndefinedMoveType = (MoveType)99;

        [TestCase(1, 0.5f)]
        [TestCase(2, 1.0f)]
        [TestCase(3, 1.5f)]
        [TestCase(4, 2.0f)]
        [TestCase(5, 2.5f)]
        public void GetCost_CloneAtTheDefaultMultiplier_ReturnsHalfTheUnitCost(int unitEnergyCost, float expectedCost)
        {
            // GIVEN
            var config = new EnergyConfig(10f, 1f, 5f);

            // WHEN
            float cost = MoveCostResolver.GetCost(MoveType.Clone, unitEnergyCost, config);

            // THEN
            Assert.That(cost, Is.EqualTo(expectedCost).Within(Tolerance));
        }

        [Test]
        public void GetCost_CloneWithFullMultiplier_ReturnsTheFullUnitCost()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 1f, 5f, 1.0f, 0.5f, 0.5f);
            const int unitEnergyCost = 4;

            // WHEN
            float cost = MoveCostResolver.GetCost(MoveType.Clone, unitEnergyCost, config);

            // THEN
            Assert.That(cost, Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void GetCost_CloneWithZeroMultiplier_ReturnsZero()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 1f, 5f, 0f, 0.5f, 0.5f);
            const int unitEnergyCost = 4;

            // WHEN
            float cost = MoveCostResolver.GetCost(MoveType.Clone, unitEnergyCost, config);

            // THEN
            Assert.That(cost, Is.EqualTo(0f).Within(Tolerance));
        }

        [TestCase(1)]
        [TestCase(5)]
        public void GetCost_Jump_IgnoresTheUnitCostAndChargesTheFlatCost(int unitEnergyCost)
        {
            // GIVEN
            var config = new EnergyConfig(10f, 1f, 5f);

            // WHEN
            float cost = MoveCostResolver.GetCost(MoveType.Jump, unitEnergyCost, config);

            // THEN
            Assert.That(cost, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void GetCost_Deploy_ReturnsTheFullUnitCost()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 1f, 5f);
            const int unitEnergyCost = 3;

            // WHEN
            float cost = MoveCostResolver.GetCost(MoveType.Deploy, unitEnergyCost, config);

            // THEN
            Assert.That(cost, Is.EqualTo(3f).Within(Tolerance));
        }

        [Test]
        public void GetCost_UndefinedMoveType_ReturnsZero()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 1f, 5f);
            const int unitEnergyCost = 5;

            // WHEN
            float cost = MoveCostResolver.GetCost(UndefinedMoveType, unitEnergyCost, config);

            // THEN
            Assert.That(cost, Is.EqualTo(0f).Within(Tolerance));
        }
    }
}
