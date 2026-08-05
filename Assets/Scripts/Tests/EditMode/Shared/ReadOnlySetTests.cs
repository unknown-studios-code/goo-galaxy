using System;
using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Shared
{
    [TestFixture]
    public class ReadOnlySetTests
    {
        [Test]
        public void Constructor_NullSet_ThrowsArgumentNullException()
        {
            // GIVEN
            // WHEN
            static void constructorCall() => _ = new ReadOnlySet<int>(null);

            // THEN
            Assert.Throws<ArgumentNullException>(constructorCall);
        }

        [Test]
        public void Count_EmptySet_ReturnsZero()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int>());

            // THEN
            Assert.That(set.Count, Is.EqualTo(0));
        }

        [Test]
        public void Count_PopulatedSet_ReturnsCorrectCount()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });

            // THEN
            Assert.That(set.Count, Is.EqualTo(3));
        }

        [Test]
        public void Contains_ExistingItem_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<string>(new HashSet<string> { "a", "b", "c" });

            // THEN
            Assert.That(set.Contains("b"), Is.True);
        }

        [Test]
        public void Contains_MissingItem_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<string>(new HashSet<string> { "a", "b" });

            // THEN
            Assert.That(set.Contains("z"), Is.False);
        }

        [Test]
        public void GetEnumerator_ThroughNonGenericInterface_IteratesEveryItem()
        {
            // GIVEN
            var inner = new HashSet<int> { 10, 20, 30 };
            IEnumerable set = new ReadOnlySet<int>(inner);
            var visited = new List<int>();

            // WHEN
            foreach (object item in set)
            {
                visited.Add((int)item);
            }

            // THEN
            Assert.That(visited, Is.EquivalentTo(inner));
        }

        [Test]
        public void GetEnumerator_OverPopulatedSet_IteratesEveryItem()
        {
            // GIVEN
            var inner = new HashSet<int> { 10, 20, 30 };
            var set = new ReadOnlySet<int>(inner);
            var visited = new List<int>();

            // WHEN
            foreach (int item in set)
            {
                visited.Add(item);
            }

            // THEN
            Assert.That(visited.Count, Is.EqualTo(3));
            Assert.That(visited, Is.EquivalentTo(inner));
        }

        [Test]
        public void IsSubsetOf_Subset_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2 });
            var superset = new List<int> { 1, 2, 3 };

            // THEN
            Assert.That(set.IsSubsetOf(superset), Is.True);
        }

        [Test]
        public void IsSubsetOf_NotSubset_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 4 });
            var other = new List<int> { 1, 2, 3 };

            // THEN
            Assert.That(set.IsSubsetOf(other), Is.False);
        }

        [Test]
        public void IsSupersetOf_Superset_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var subset = new List<int> { 1, 2 };

            // THEN
            Assert.That(set.IsSupersetOf(subset), Is.True);
        }

        [Test]
        public void IsSupersetOf_NotSuperset_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2 });
            var other = new List<int> { 1, 2, 3 };

            // THEN
            Assert.That(set.IsSupersetOf(other), Is.False);
        }

        [Test]
        public void IsProperSubsetOf_StrictSubset_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2 });
            var superset = new List<int> { 1, 2, 3 };

            // THEN
            Assert.That(set.IsProperSubsetOf(superset), Is.True);
        }

        [Test]
        public void IsProperSubsetOf_EqualSet_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var equalSet = new List<int> { 1, 2, 3 };

            // THEN
            Assert.That(set.IsProperSubsetOf(equalSet), Is.False);
        }

        [Test]
        public void IsProperSupersetOf_StrictSuperset_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var subset = new List<int> { 1, 2 };

            // THEN
            Assert.That(set.IsProperSupersetOf(subset), Is.True);
        }

        [Test]
        public void IsProperSupersetOf_EqualSet_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2 });
            var equalSet = new List<int> { 1, 2 };

            // THEN
            Assert.That(set.IsProperSupersetOf(equalSet), Is.False);
        }

        [Test]
        public void Overlaps_SharedElement_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var other = new List<int> { 3, 4, 5 };

            // THEN
            Assert.That(set.Overlaps(other), Is.True);
        }

        [Test]
        public void Overlaps_NoSharedElement_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var other = new List<int> { 4, 5, 6 };

            // THEN
            Assert.That(set.Overlaps(other), Is.False);
        }

        [Test]
        public void SetEquals_SameElements_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var other = new List<int> { 3, 1, 2 };

            // THEN
            Assert.That(set.SetEquals(other), Is.True);
        }

        [Test]
        public void SetEquals_DifferentElements_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var other = new List<int> { 1, 2, 4 };

            // THEN
            Assert.That(set.SetEquals(other), Is.False);
        }
    }
}
