using GooGalaxy.Runtime.Board.Models;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Board
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
            Assert.IsTrue(equalsResult);
            Assert.IsTrue(operatorEqualsResult);
            Assert.IsFalse(operatorNotEqualsResult);
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
            Assert.IsFalse(equalsResult);
            Assert.IsFalse(operatorEqualsResult);
            Assert.IsTrue(operatorNotEqualsResult);
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
            Assert.AreEqual(hash1, hash2);
        }

        [Test]
        public void Distance_CalculatesCorrectly()
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
            Assert.AreEqual(1, distToEast);
            Assert.AreEqual(1, distToNorthEast);
            Assert.AreEqual(1, distToNorthWest);
            Assert.AreEqual(1, distToWest);
            Assert.AreEqual(1, distToSouthWest);
            Assert.AreEqual(1, distToSouthEast);
            Assert.AreEqual(8, distOpposingBorders);
            Assert.AreEqual(4, distDiagonal);
        }

        [Test]
        public void GetNeighbor_MatchesOffsets()
        {
            // GIVEN
            var origin = new HexCoordinates(0, 0);

            // WHEN
            HexCoordinates neighborE = origin.GetNeighbor(HexDirection.E);
            HexCoordinates neighborNE = origin.GetNeighbor(HexDirection.NE);
            HexCoordinates neighborNW = origin.GetNeighbor(HexDirection.NW);
            HexCoordinates neighborW = origin.GetNeighbor(HexDirection.W);
            HexCoordinates neighborSW = origin.GetNeighbor(HexDirection.SW);
            HexCoordinates neighborSE = origin.GetNeighbor(HexDirection.SE);

            // THEN
            Assert.AreEqual(new HexCoordinates(1, 0), neighborE);
            Assert.AreEqual(new HexCoordinates(1, -1), neighborNE);
            Assert.AreEqual(new HexCoordinates(0, -1), neighborNW);
            Assert.AreEqual(new HexCoordinates(-1, 0), neighborW);
            Assert.AreEqual(new HexCoordinates(-1, 1), neighborSW);
            Assert.AreEqual(new HexCoordinates(0, 1), neighborSE);
        }

        [Test]
        public void HexDirection_ExposesCorrectOffsets()
        {
            // GIVEN
            // WHEN
            // THEN
            Assert.AreEqual(new HexCoordinates(1, 0), HexDirection.E);
            Assert.AreEqual(new HexCoordinates(1, -1), HexDirection.NE);
            Assert.AreEqual(new HexCoordinates(0, -1), HexDirection.NW);
            Assert.AreEqual(new HexCoordinates(-1, 0), HexDirection.W);
            Assert.AreEqual(new HexCoordinates(-1, 1), HexDirection.SW);
            Assert.AreEqual(new HexCoordinates(0, 1), HexDirection.SE);
        }

        [Test]
        public void ToString_ReturnsFormattedCoordinates()
        {
            // GIVEN
            var coord = new HexCoordinates(3, -2);

            // WHEN
            string result = coord.ToString();

            // THEN
            Assert.AreEqual("(3, -2)", result);
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
            Assert.IsTrue(result);
        }

        [Test]
        public void Equals_BoxedObject_WrongType_ReturnsFalse()
        {
            // GIVEN
            var coord = new HexCoordinates(1, -2);

            // WHEN
            bool result = coord.Equals("not a HexCoordinates");

            // THEN
            Assert.IsFalse(result);
        }

        [Test]
        public void CalculateDistance_SameCoord_ReturnsZero()
        {
            // GIVEN
            var coord = new HexCoordinates(3, -1);

            // WHEN
            int distance = coord.CalculateDistance(coord);

            // THEN
            Assert.AreEqual(0, distance);
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
            Assert.AreNotEqual(hash1, hash2, "Different coordinates should have different hash codes.");
        }

        [Test]
        public void HexDirection_All_HasSixElements()
        {
            // THEN
            Assert.AreEqual(6, HexDirection.All.Length);
        }

        [Test]
        public void HexDirection_All_ContainsAllDirections()
        {
            // THEN
            Assert.AreEqual(HexDirection.E, HexDirection.All[0]);
            Assert.AreEqual(HexDirection.NE, HexDirection.All[1]);
            Assert.AreEqual(HexDirection.NW, HexDirection.All[2]);
            Assert.AreEqual(HexDirection.W, HexDirection.All[3]);
            Assert.AreEqual(HexDirection.SW, HexDirection.All[4]);
            Assert.AreEqual(HexDirection.SE, HexDirection.All[5]);
        }
    }
}
