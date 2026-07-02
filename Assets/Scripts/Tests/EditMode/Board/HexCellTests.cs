using GooGalaxy.Runtime.Board.Models;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Board
{
    [TestFixture]
    public class HexCellTests
    {
        [Test]
        public void Constructor_Default_IsNotBlocked()
        {
            // GIVEN
            var coords = new HexCoordinates(2, -1);

            // WHEN
            var cell = new HexCell(coords);

            // THEN
            Assert.AreEqual(coords, cell.Coordinates);
            Assert.IsFalse(cell.IsBlocked);
        }

        [Test]
        public void Constructor_Blocked_IsBlockedTrue()
        {
            // GIVEN
            var coords = new HexCoordinates(1, 1);

            // WHEN
            var cell = new HexCell(coords, isBlocked: true);

            // THEN
            Assert.AreEqual(coords, cell.Coordinates);
            Assert.IsTrue(cell.IsBlocked);
        }

        [Test]
        public void IsBlocked_Setter_MutatesState()
        {
            // GIVEN
            var cell = new HexCell(new HexCoordinates(0, 0), isBlocked: false);
            Assert.IsFalse(cell.IsBlocked);

            // WHEN
            cell.IsBlocked = true;

            // THEN
            Assert.IsTrue(cell.IsBlocked);

            // WHEN
            cell.IsBlocked = false;

            // THEN
            Assert.IsFalse(cell.IsBlocked);
        }

        [Test]
        public void Coordinates_ReturnsExactValue()
        {
            // GIVEN
            var expected = new HexCoordinates(-3, 4);
            var cell = new HexCell(expected);

            // WHEN
            HexCoordinates actual = cell.Coordinates;

            // THEN
            Assert.AreEqual(expected.Q, actual.Q);
            Assert.AreEqual(expected.R, actual.R);
        }

        [Test]
        public void Constructor_OriginCoordinates_Works()
        {
            // GIVEN
            var origin = new HexCoordinates(0, 0);

            // WHEN
            var cell = new HexCell(origin);

            // THEN
            Assert.AreEqual(origin, cell.Coordinates);
            Assert.IsFalse(cell.IsBlocked);
        }
    }
}
