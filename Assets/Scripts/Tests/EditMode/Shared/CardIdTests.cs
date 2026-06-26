using System;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode
{
    [TestFixture]
    public class CardIdTests
    {
        [Test]
        public void Constructor_WithNull_ThrowsArgumentNullException()
        {
            // GIVEN
            string nullString = null;

            // WHEN // THEN
            Assert.Throws<ArgumentNullException>(() => _ = new CardId(nullString));
        }

        [Test]
        public void Empty_WrapsStringEmpty()
        {
            // GIVEN
            CardId emptyId = CardId.Empty;
            var defaultId = default(CardId);

            // WHEN
            string emptyVal = emptyId.Value;
            string defaultVal = defaultId.Value;

            // THEN
            Assert.AreEqual(string.Empty, emptyVal);
            Assert.AreEqual(string.Empty, defaultVal);
        }

        [Test]
        public void Equality_SameString_IsEqual()
        {
            // GIVEN
            var id1 = new CardId("test_card");
            var id2 = new CardId("test_card");

            // WHEN
            bool equalsMethodResult = id1.Equals(id2);
            bool equalityOpResult = id1 == id2;
            bool inequalityOpResult = id1 != id2;

            // THEN
            Assert.IsTrue(equalsMethodResult);
            Assert.IsTrue(equalityOpResult);
            Assert.IsFalse(inequalityOpResult);
            Assert.AreEqual(id1, id2);
        }

        [Test]
        public void Equality_DifferentString_IsNotEqual()
        {
            // GIVEN
            var id1 = new CardId("test_card_1");
            var id2 = new CardId("test_card_2");

            // WHEN
            bool equalsMethodResult = id1.Equals(id2);
            bool equalityOpResult = id1 == id2;
            bool inequalityOpResult = id1 != id2;

            // THEN
            Assert.IsFalse(equalsMethodResult);
            Assert.IsFalse(equalityOpResult);
            Assert.IsTrue(inequalityOpResult);
            Assert.AreNotEqual(id1, id2);
        }

        [Test]
        public void GetHashCode_MatchesStringGetHashCode()
        {
            // GIVEN
            const string rawId = "test_card";
            var cardId = new CardId(rawId);

            // WHEN
            int expectedHash = rawId.GetHashCode();
            int actualHash = cardId.GetHashCode();

            // THEN
            Assert.AreEqual(expectedHash, actualHash);
        }

        [Test]
        public void ToString_ReturnsWrappedString()
        {
            // GIVEN
            const string rawId = "test_card";
            var cardId = new CardId(rawId);

            // WHEN
            string toStringResult = cardId.ToString();

            // THEN
            Assert.AreEqual(rawId, toStringResult);
        }
    }
}
