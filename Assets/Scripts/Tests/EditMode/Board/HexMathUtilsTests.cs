using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class HexMathUtilsTests
    {
        private const float Size = 1f;
        private const float Tolerance = 0.0001f;

        [Test]
        public void ProjectToWorldSpace_Origin_ReturnsZero()
        {
            // GIVEN
            var origin = new HexCoordinates(0, 0);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(origin, Size);

            // THEN
            Assert.That(result, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ProjectToWorldSpace_ZCoordinate_IsAlwaysZero()
        {
            // GIVEN
            var coords = new HexCoordinates(2, -1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(coords, Size);

            // THEN
            Assert.That(result.z, Is.EqualTo(0f).Within(Tolerance), "Z axis should always be 0 (XY plane layout).");
        }

        [Test]
        public void ProjectToWorldSpace_East_CorrectPosition()
        {
            // GIVEN
            var east = new HexCoordinates(1, 0);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(east, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(1.5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(Mathf.Sqrt(3f) * 0.5f).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_West_CorrectPosition()
        {
            // GIVEN
            var west = new HexCoordinates(-1, 0);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(west, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(-1.5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(-Mathf.Sqrt(3f) * 0.5f).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_SouthEast_CorrectPosition()
        {
            // GIVEN
            var se = new HexCoordinates(0, 1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(se, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(Mathf.Sqrt(3f)).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_NorthEast_CorrectPosition()
        {
            // GIVEN
            var ne = new HexCoordinates(1, -1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(ne, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(1.5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(-Mathf.Sqrt(3f) * 0.5f).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_NorthWest_CorrectPosition()
        {
            // GIVEN
            var nw = new HexCoordinates(0, -1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(nw, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(-Mathf.Sqrt(3f)).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_SouthWest_CorrectPosition()
        {
            // GIVEN
            var sw = new HexCoordinates(-1, 1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(sw, Size);

            // THEN
            Assert.That(result.x, Is.EqualTo(-1.5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(Mathf.Sqrt(3f) * 0.5f).Within(Tolerance));
        }

        [Test]
        public void ProjectToWorldSpace_SizeZero_ReturnsZero()
        {
            // GIVEN
            var coords = new HexCoordinates(3, -1);

            // WHEN
            Vector3 result = HexMathUtils.ProjectToWorldSpace(coords, 0f);

            // THEN
            Assert.That(result, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ProjectToWorldSpace_WithLargerCellSize_ScalesPositionProportionally()
        {
            // GIVEN
            var east = new HexCoordinates(1, 0);
            float size2 = 2f;

            // WHEN
            Vector3 result1 = HexMathUtils.ProjectToWorldSpace(east, Size);
            Vector3 result2 = HexMathUtils.ProjectToWorldSpace(east, size2);

            // THEN
            Assert.That(result2.x, Is.EqualTo(result1.x * size2).Within(Tolerance));
            Assert.That(result2.y, Is.EqualTo(result1.y * size2).Within(Tolerance));
        }
    }
}
