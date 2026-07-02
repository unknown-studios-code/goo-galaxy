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

            // WHEN
            void constructorCall() => _ = new CardId(nullString);

            // THEN
            Assert.Throws<ArgumentNullException>(constructorCall);
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

        [Test]
        public void Equals_BoxedObject_SameValue_ReturnsTrue()
        {
            // GIVEN
            var id1 = new CardId("card_a");
            object boxed = new CardId("card_a");

            // WHEN
            bool result = id1.Equals(boxed);

            // THEN
            Assert.IsTrue(result);
        }

        [Test]
        public void Equals_BoxedObject_WrongType_ReturnsFalse()
        {
            // GIVEN
            var id = new CardId("card_a");

            // WHEN
            bool result = id.Equals("card_a");

            // THEN
            Assert.IsFalse(result);
        }

        [Test]
        public void Equals_Empty_And_Default_AreEqual()
        {
            // GIVEN
            CardId empty = CardId.Empty;
            var defaultId = default(CardId);

            // THEN
            Assert.AreEqual(empty, defaultId);
            Assert.IsTrue(empty == defaultId);
        }

        [Test]
        public void Equals_CaseSensitive_DifferentCase_NotEqual()
        {
            // GIVEN
            var upper = new CardId("CardABC");
            var lower = new CardId("cardabc");

            // THEN
            Assert.AreNotEqual(upper, lower);
            Assert.IsFalse(upper == lower);
        }
    }
}
