using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode
{
    [TestFixture]
    public class HexCoordinatesTests
    {
        [Test]
        public void Equals_SameCoordinates_ReturnsTrue()
        {
            // GIVEN
            var coord1 = new HexCoordinates(1, -2);
            var coord2 = new HexCoordinates(1, -2);

            // WHEN
            bool equalsResult = coord1.Equals(coord2);
            bool operatorEqualsResult = coord1 == coord2;
            bool operatorNotEqualsResult = coord1 != coord2;

            // THEN
            Assert.That(equalsResult, Is.True);
            Assert.That(operatorEqualsResult, Is.True);
            Assert.That(operatorNotEqualsResult, Is.False);
        }

        [Test]
        public void Equals_DifferentCoordinates_ReturnsFalse()
        {
            // GIVEN
            var coord1 = new HexCoordinates(1, -2);
            var coord2 = new HexCoordinates(2, -2);

            // WHEN
            bool equalsResult = coord1.Equals(coord2);
            bool operatorEqualsResult = coord1 == coord2;
            bool operatorNotEqualsResult = coord1 != coord2;

            // THEN
            Assert.That(equalsResult, Is.False);
            Assert.That(operatorEqualsResult, Is.False);
            Assert.That(operatorNotEqualsResult, Is.True);
        }

        [Test]
        public void GetHashCode_SameCoordinates_ReturnsSameHash()
        {
            // GIVEN
            var coord1 = new HexCoordinates(3, 4);
            var coord2 = new HexCoordinates(3, 4);

            // WHEN
            int hash1 = coord1.GetHashCode();
            int hash2 = coord2.GetHashCode();

            // THEN
            Assert.That(hash2, Is.EqualTo(hash1));
        }

        [Test]
        public void Distance_BetweenTwoCoordinates_ReturnsAxialHexDistance()
        {
            // GIVEN
            var origin = new HexCoordinates(0, 0);
            var border1 = new HexCoordinates(4, -4);
            var border2 = new HexCoordinates(-4, 4);
            var a = new HexCoordinates(1, 1);
            var b = new HexCoordinates(-1, -1);

            // WHEN
            int distToEast = origin.CalculateDistance(new HexCoordinates(1, 0));
            int distToNorthEast = origin.CalculateDistance(new HexCoordinates(0, 1));
            int distToNorthWest = origin.CalculateDistance(new HexCoordinates(-1, 1));
            int distToWest = origin.CalculateDistance(new HexCoordinates(-1, 0));
            int distToSouthWest = origin.CalculateDistance(new HexCoordinates(0, -1));
            int distToSouthEast = origin.CalculateDistance(new HexCoordinates(1, -1));
            int distOpposingBorders = border1.CalculateDistance(border2);
            int distDiagonal = a.CalculateDistance(b);

            // THEN
            Assert.That(distToEast, Is.EqualTo(1));
            Assert.That(distToNorthEast, Is.EqualTo(1));
            Assert.That(distToNorthWest, Is.EqualTo(1));
            Assert.That(distToWest, Is.EqualTo(1));
            Assert.That(distToSouthWest, Is.EqualTo(1));
            Assert.That(distToSouthEast, Is.EqualTo(1));
            Assert.That(distOpposingBorders, Is.EqualTo(8));
            Assert.That(distDiagonal, Is.EqualTo(4));
        }

        [Test]
        public void ToString_ForAxialPair_ReturnsFormattedCoordinates()
        {
            // GIVEN
            var coord = new HexCoordinates(3, -2);

            // WHEN
            string result = coord.ToString();

            // THEN
            Assert.That(result, Is.EqualTo("(3, -2)"));
        }

        [Test]
        public void Equals_BoxedObject_SameCoordinates_ReturnsTrue()
        {
            // GIVEN
            var coord = new HexCoordinates(1, -2);
            object boxed = new HexCoordinates(1, -2);

            // WHEN
            bool result = coord.Equals(boxed);

            // THEN
            Assert.That(result, Is.True);
        }

        [Test]
        public void Equals_BoxedObject_WrongType_ReturnsFalse()
        {
            // GIVEN
            var coord = new HexCoordinates(1, -2);

            // WHEN
            bool result = coord.Equals("not a HexCoordinates");

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void CalculateDistance_SameCoord_ReturnsZero()
        {
            // GIVEN
            var coord = new HexCoordinates(3, -1);

            // WHEN
            int distance = coord.CalculateDistance(coord);

            // THEN
            Assert.That(distance, Is.EqualTo(0));
        }

        [Test]
        public void GetHashCode_DifferentCoordinates_ReturnsDifferentHash()
        {
            // GIVEN
            var coord1 = new HexCoordinates(1, 0);
            var coord2 = new HexCoordinates(0, 1);

            // WHEN
            int hash1 = coord1.GetHashCode();
            int hash2 = coord2.GetHashCode();

            // THEN
            Assert.That(hash2, Is.Not.EqualTo(hash1), "Different coordinates should have different hash codes.");
        }
    }
}
