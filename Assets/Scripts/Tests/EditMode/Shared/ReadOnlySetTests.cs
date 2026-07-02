using System;
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
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Count_PopulatedSet_ReturnsCorrectCount()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });

            // THEN
            Assert.AreEqual(3, set.Count);
        }

        [Test]
        public void Contains_ExistingItem_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<string>(new HashSet<string> { "a", "b", "c" });

            // THEN
            Assert.IsTrue(set.Contains("b"));
        }

        [Test]
        public void Contains_MissingItem_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<string>(new HashSet<string> { "a", "b" });

            // THEN
            Assert.IsFalse(set.Contains("z"));
        }

        [Test]
        public void GetEnumerator_IteratesAllItems()
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
            Assert.AreEqual(3, visited.Count);
            CollectionAssert.AreEquivalent(inner, visited);
        }

        [Test]
        public void IsSubsetOf_Subset_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2 });
            var superset = new List<int> { 1, 2, 3 };

            // THEN
            Assert.IsTrue(set.IsSubsetOf(superset));
        }

        [Test]
        public void IsSubsetOf_NotSubset_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 4 });
            var other = new List<int> { 1, 2, 3 };

            // THEN
            Assert.IsFalse(set.IsSubsetOf(other));
        }

        [Test]
        public void IsSupersetOf_Superset_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var subset = new List<int> { 1, 2 };

            // THEN
            Assert.IsTrue(set.IsSupersetOf(subset));
        }

        [Test]
        public void IsSupersetOf_NotSuperset_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2 });
            var other = new List<int> { 1, 2, 3 };

            // THEN
            Assert.IsFalse(set.IsSupersetOf(other));
        }

        [Test]
        public void IsProperSubsetOf_StrictSubset_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2 });
            var superset = new List<int> { 1, 2, 3 };

            // THEN
            Assert.IsTrue(set.IsProperSubsetOf(superset));
        }

        [Test]
        public void IsProperSubsetOf_EqualSet_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var equalSet = new List<int> { 1, 2, 3 };

            // THEN
            Assert.IsFalse(set.IsProperSubsetOf(equalSet));
        }

        [Test]
        public void IsProperSupersetOf_StrictSuperset_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var subset = new List<int> { 1, 2 };

            // THEN
            Assert.IsTrue(set.IsProperSupersetOf(subset));
        }

        [Test]
        public void IsProperSupersetOf_EqualSet_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2 });
            var equalSet = new List<int> { 1, 2 };

            // THEN
            Assert.IsFalse(set.IsProperSupersetOf(equalSet));
        }

        [Test]
        public void Overlaps_SharedElement_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var other = new List<int> { 3, 4, 5 };

            // THEN
            Assert.IsTrue(set.Overlaps(other));
        }

        [Test]
        public void Overlaps_NoSharedElement_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var other = new List<int> { 4, 5, 6 };

            // THEN
            Assert.IsFalse(set.Overlaps(other));
        }

        [Test]
        public void SetEquals_SameElements_ReturnsTrue()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var other = new List<int> { 3, 1, 2 };

            // THEN
            Assert.IsTrue(set.SetEquals(other));
        }

        [Test]
        public void SetEquals_DifferentElements_ReturnsFalse()
        {
            // GIVEN
            var set = new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 });
            var other = new List<int> { 1, 2, 4 };

            // THEN
            Assert.IsFalse(set.SetEquals(other));
        }
    }
}
