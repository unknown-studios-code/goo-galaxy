using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
{
    [TestFixture]
    public class ConversionResultTests
    {
        [Test]
        public void IsEmpty_DefaultConstructedValue_IsTrue()
        {
            // GIVEN
            var result = default(ConversionResult);

            // WHEN
            bool isEmpty = result.IsEmpty;

            // THEN
            Assert.That(isEmpty, Is.True);
        }

        [Test]
        public void IsEmpty_OnlyConvertedUnitIdsPopulated_IsFalse()
        {
            // GIVEN
            var result = new ConversionResult(new List<int> { 1 }, null);

            // WHEN
            bool isEmpty = result.IsEmpty;

            // THEN
            Assert.That(isEmpty, Is.False);
        }

        [Test]
        public void ConvertedUnitIds_ReturnsTheExactInstancePassedToConstructor()
        {
            // GIVEN
            var convertedUnitIds = new List<int> { 1, 2 };
            var result = new ConversionResult(convertedUnitIds, new List<int>());

            // WHEN
            IReadOnlyList<int> actual = result.ConvertedUnitIds;

            // THEN
            Assert.That(actual, Is.SameAs(convertedUnitIds));
        }

        [Test]
        public void ArmorStrippedUnitIds_ReturnsTheExactInstancePassedToConstructor()
        {
            // GIVEN
            var armorStrippedUnitIds = new List<int> { 3 };
            var result = new ConversionResult(new List<int>(), armorStrippedUnitIds);

            // WHEN
            IReadOnlyList<int> actual = result.ArmorStrippedUnitIds;

            // THEN
            Assert.That(actual, Is.SameAs(armorStrippedUnitIds));
        }
    }
}
