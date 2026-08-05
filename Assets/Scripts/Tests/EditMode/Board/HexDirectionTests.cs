using System;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Board
{
    [TestFixture]
    public class HexDirectionTests
    {
        [Test]
        public void Directions_ForEachCardinal_ExposeExpectedAxialOffset()
        {
            // GIVEN
            var expectedEast = new HexCoordinates(1, 0);
            var expectedNorthEast = new HexCoordinates(1, -1);
            var expectedNorthWest = new HexCoordinates(0, -1);
            var expectedWest = new HexCoordinates(-1, 0);
            var expectedSouthWest = new HexCoordinates(-1, 1);
            var expectedSouthEast = new HexCoordinates(0, 1);

            // WHEN
            HexCoordinates east = HexDirection.E;
            HexCoordinates northEast = HexDirection.NE;
            HexCoordinates northWest = HexDirection.NW;
            HexCoordinates west = HexDirection.W;
            HexCoordinates southWest = HexDirection.SW;
            HexCoordinates southEast = HexDirection.SE;

            // THEN
            Assert.That(east, Is.EqualTo(expectedEast));
            Assert.That(northEast, Is.EqualTo(expectedNorthEast));
            Assert.That(northWest, Is.EqualTo(expectedNorthWest));
            Assert.That(west, Is.EqualTo(expectedWest));
            Assert.That(southWest, Is.EqualTo(expectedSouthWest));
            Assert.That(southEast, Is.EqualTo(expectedSouthEast));
        }

        [Test]
        public void HexDirection_All_HasSixElements()
        {
            // GIVEN
            const int expectedDirectionCount = 6;

            // WHEN
            int directionCount = HexDirection.All.Length;

            // THEN
            Assert.That(directionCount, Is.EqualTo(expectedDirectionCount));
        }

        [Test]
        public void HexDirection_All_ContainsAllDirectionsInClockwiseOrder()
        {
            // GIVEN
            // WHEN
            ReadOnlySpan<HexCoordinates> allDirections = HexDirection.All;

            // THEN
            Assert.That(allDirections[0], Is.EqualTo(HexDirection.E));
            Assert.That(allDirections[1], Is.EqualTo(HexDirection.NE));
            Assert.That(allDirections[2], Is.EqualTo(HexDirection.NW));
            Assert.That(allDirections[3], Is.EqualTo(HexDirection.W));
            Assert.That(allDirections[4], Is.EqualTo(HexDirection.SW));
            Assert.That(allDirections[5], Is.EqualTo(HexDirection.SE));
        }

        [Test]
        public void GetNeighbor_ForEachDirection_MatchesThatDirectionOffset()
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
            Assert.That(neighborE, Is.EqualTo(new HexCoordinates(1, 0)));
            Assert.That(neighborNE, Is.EqualTo(new HexCoordinates(1, -1)));
            Assert.That(neighborNW, Is.EqualTo(new HexCoordinates(0, -1)));
            Assert.That(neighborW, Is.EqualTo(new HexCoordinates(-1, 0)));
            Assert.That(neighborSW, Is.EqualTo(new HexCoordinates(-1, 1)));
            Assert.That(neighborSE, Is.EqualTo(new HexCoordinates(0, 1)));
        }
    }
}
