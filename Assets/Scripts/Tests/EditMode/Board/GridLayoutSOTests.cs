using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Runtime.Tests.EditMode.Board
{
    [TestFixture]
    public class GridLayoutSOTests
    {
        private GridLayoutSO _gridLayout;

        [SetUp]
        public void SetUp()
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gridLayout != null)
            {
                Object.DestroyImmediate(_gridLayout);
            }
        }

        [Test]
        public void GridRadius_AfterAuthoring_ReturnsAuthoredValue()
        {
            // GIVEN
            _gridLayout.SetAuthoredData(gridRadius: 4);

            // WHEN
            int radius = _gridLayout.GridRadius;

            // THEN
            Assert.That(radius, Is.EqualTo(4));
        }

        [Test]
        public void BlockedCoordinates_AfterAuthoring_ContainsEveryAuthoredCoordinate()
        {
            // GIVEN
            _gridLayout.SetAuthoredData(4, new Vector2Int(1, -1), new Vector2Int(2, -2));

            // WHEN
            IReadOnlySet<HexCoordinates> blocked = _gridLayout.BlockedCoordinates;

            // THEN
            Assert.That(blocked.Count, Is.EqualTo(2));
            Assert.That(blocked.Contains(new HexCoordinates(1, -1)), Is.True);
            Assert.That(blocked.Contains(new HexCoordinates(2, -2)), Is.True);
        }

        [Test]
        public void BlockedCoordinates_NeverAuthored_ReturnsEmptySetWithoutExplicitInitialization()
        {
            // GIVEN
            // a freshly created asset, with no authored data at all

            // WHEN
            IReadOnlySet<HexCoordinates> blocked = _gridLayout.BlockedCoordinates;

            // THEN
            Assert.That(blocked, Is.Not.Null);
            Assert.That(blocked.Count, Is.EqualTo(0));
        }

        [Test]
        public void BlockedCoordinates_AuthoredEmpty_ReturnsEmptySet()
        {
            // GIVEN
            _gridLayout.SetAuthoredData(gridRadius: 4);

            // WHEN
            IReadOnlySet<HexCoordinates> blocked = _gridLayout.BlockedCoordinates;

            // THEN
            Assert.That(blocked.Count, Is.EqualTo(0));
        }

        [Test]
        public void ValidateAuthoredData_DuplicateCoordinates_KeepsFirstOccurrenceOnly()
        {
            // GIVEN
            _gridLayout.SetAuthoredData(4, new Vector2Int(1, 0), new Vector2Int(1, 0), new Vector2Int(0, 2));

            // WHEN
            _gridLayout.ValidateAuthoredData();

            // THEN
            Assert.That(_gridLayout.AuthoredBlockedCoordinates, Is.EqualTo(new[] { new Vector2Int(1, 0), new Vector2Int(0, 2) }));
        }

        [Test]
        public void ValidateAuthoredData_DuplicateCoordinates_RebuildsLookupSetWithoutDuplicates()
        {
            // GIVEN
            _gridLayout.SetAuthoredData(4, new Vector2Int(1, 0), new Vector2Int(1, 0), new Vector2Int(0, 2));

            // WHEN
            _gridLayout.ValidateAuthoredData();

            // THEN
            Assert.That(_gridLayout.BlockedCoordinates.Count, Is.EqualTo(2));
            Assert.That(_gridLayout.BlockedCoordinates.Contains(new HexCoordinates(1, 0)), Is.True);
            Assert.That(_gridLayout.BlockedCoordinates.Contains(new HexCoordinates(0, 2)), Is.True);
        }

        [Test]
        public void ValidateAuthoredData_DuplicateZeroAppendedByInspector_DropsTheExtraEntry()
        {
            // GIVEN
            _gridLayout.name = "TestLayout";
            _gridLayout.SetAuthoredData(4, new Vector2Int(1, 0), Vector2Int.zero, Vector2Int.zero);
            LogAssert.Expect(LogType.Warning, string.Format(BoardLogMessages.CannotAddBlockedCoordinateFormat, "TestLayout"));

            // WHEN
            _gridLayout.ValidateAuthoredData();

            // THEN
            Assert.That(_gridLayout.AuthoredBlockedCoordinates, Is.EqualTo(new[] { new Vector2Int(1, 0), Vector2Int.zero }));
        }

        [Test]
        public void ValidateAuthoredData_DuplicateNonZeroAppendedByInspector_ResetsTheEntryAndDeduplicates()
        {
            // GIVEN
            _gridLayout.name = "TestLayout";
            _gridLayout.SetAuthoredData(4, new Vector2Int(1, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(2, 0));
            LogAssert.Expect(LogType.Warning, string.Format(BoardLogMessages.DuplicateBlockedCoordinatesFormat, "TestLayout"));

            // WHEN
            _gridLayout.ValidateAuthoredData();

            // THEN
            Assert.That(_gridLayout.AuthoredBlockedCoordinates, Is.EqualTo(new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), Vector2Int.zero }));
        }

        [TestCase(-5, ExpectedResult = 0)]
        [TestCase(-1, ExpectedResult = 0)]
        [TestCase(0, ExpectedResult = 0)]
        [TestCase(7, ExpectedResult = 7)]
        public int ValidateAuthoredData_ForAuthoredRadius_ClampsNegativeValuesToZero(int authoredRadius)
        {
            // GIVEN
            _gridLayout.SetAuthoredData(authoredRadius);

            // WHEN
            _gridLayout.ValidateAuthoredData();

            // THEN
            return _gridLayout.GridRadius;
        }
    }
}
