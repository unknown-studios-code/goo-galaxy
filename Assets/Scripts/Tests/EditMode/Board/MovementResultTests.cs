using GooGalaxy.Runtime.Board.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class MovementResultTests
    {
        [Test]
        public void Success_Value_Equals0()
        {
            // GIVEN

            // WHEN
            const MovementResult result = MovementResult.Success;

            // THEN
            Assert.That((int)result, Is.EqualTo(0));
        }

        [Test]
        public void TargetHazardous_Value_Equals13()
        {
            // GIVEN

            // WHEN
            const MovementResult result = MovementResult.TargetHazardous;

            // THEN
            Assert.That((int)result, Is.EqualTo(13));
        }

        [Test]
        public void InsufficientEnergy_Value_Equals14()
        {
            // GIVEN

            // WHEN
            const MovementResult result = MovementResult.InsufficientEnergy;

            // THEN
            Assert.That((int)result, Is.EqualTo(14));
        }

        [Test]
        public void NotAdjacentToOwnedTerritory_Value_Equals15()
        {
            // GIVEN

            // WHEN
            const MovementResult result = MovementResult.NotAdjacentToOwnedTerritory;

            // THEN
            Assert.That((int)result, Is.EqualTo(15));
        }
    }
}
