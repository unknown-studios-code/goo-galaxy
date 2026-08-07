using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
{
    [TestFixture]
    public class AbilityResultTests
    {
        [Test]
        public void IsEmpty_DefaultConstructedValue_IsTrue()
        {
            // GIVEN
            var result = default(AbilityResult);

            // WHEN
            bool isEmpty = result.IsEmpty;

            // THEN
            Assert.That(isEmpty, Is.True);
        }

        [Test]
        public void IsEmpty_AllListsNull_IsTrue()
        {
            // GIVEN
            var result = new AbilityResult(null, null, null);

            // WHEN
            bool isEmpty = result.IsEmpty;

            // THEN
            Assert.That(isEmpty, Is.True);
        }

        [Test]
        public void IsEmpty_AllListsEmpty_IsTrue()
        {
            // GIVEN
            var result = new AbilityResult(new List<int>(), new List<HexCoordinates>(), new List<int>());

            // WHEN
            bool isEmpty = result.IsEmpty;

            // THEN
            Assert.That(isEmpty, Is.True);
        }

        [Test]
        public void IsEmpty_OnlyAffectedUnitIdsPopulated_IsFalse()
        {
            // GIVEN
            var result = new AbilityResult(new List<int> { 1 }, null, null);

            // WHEN
            bool isEmpty = result.IsEmpty;

            // THEN
            Assert.That(isEmpty, Is.False);
        }

        [Test]
        public void IsEmpty_OnlyAffectedHexesPopulated_IsFalse()
        {
            // GIVEN
            var result = new AbilityResult(null, new List<HexCoordinates> { new(0, 0) }, null);

            // WHEN
            bool isEmpty = result.IsEmpty;

            // THEN
            Assert.That(isEmpty, Is.False);
        }

        [Test]
        public void IsEmpty_OnlyDestroyedUnitIdsPopulated_IsFalse()
        {
            // GIVEN
            var result = new AbilityResult(null, null, new List<int> { 1 });

            // WHEN
            bool isEmpty = result.IsEmpty;

            // THEN
            Assert.That(isEmpty, Is.False);
        }

        [Test]
        public void AffectedUnitIds_ReturnsTheExactInstancePassedToConstructor()
        {
            // GIVEN
            var affectedUnitIds = new List<int> { 1, 2 };
            var result = new AbilityResult(affectedUnitIds, new List<HexCoordinates>(), new List<int>());

            // WHEN
            IReadOnlyList<int> actual = result.AffectedUnitIds;

            // THEN
            Assert.That(actual, Is.SameAs(affectedUnitIds));
        }

        [Test]
        public void AffectedHexes_ReturnsTheExactInstancePassedToConstructor()
        {
            // GIVEN
            var affectedHexes = new List<HexCoordinates> { new(1, 1) };
            var result = new AbilityResult(new List<int>(), affectedHexes, new List<int>());

            // WHEN
            IReadOnlyList<HexCoordinates> actual = result.AffectedHexes;

            // THEN
            Assert.That(actual, Is.SameAs(affectedHexes));
        }

        [Test]
        public void DestroyedUnitIds_ReturnsTheExactInstancePassedToConstructor()
        {
            // GIVEN
            var destroyedUnitIds = new List<int> { 3 };
            var result = new AbilityResult(new List<int>(), new List<HexCoordinates>(), destroyedUnitIds);

            // WHEN
            IReadOnlyList<int> actual = result.DestroyedUnitIds;

            // THEN
            Assert.That(actual, Is.SameAs(destroyedUnitIds));
        }
    }
}
