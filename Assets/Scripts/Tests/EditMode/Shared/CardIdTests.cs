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
        public void Empty_WithoutArguments_WrapsStringEmpty()
        {
            // GIVEN
            CardId emptyId = CardId.Empty;
            var defaultId = default(CardId);

            // WHEN
            string emptyVal = emptyId.Value;
            string defaultVal = defaultId.Value;

            // THEN
            Assert.That(emptyVal, Is.EqualTo(string.Empty));
            Assert.That(defaultVal, Is.EqualTo(string.Empty));
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
            Assert.That(equalsMethodResult, Is.True);
            Assert.That(equalityOpResult, Is.True);
            Assert.That(inequalityOpResult, Is.False);
            Assert.That(id2, Is.EqualTo(id1));
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
            Assert.That(equalsMethodResult, Is.False);
            Assert.That(equalityOpResult, Is.False);
            Assert.That(inequalityOpResult, Is.True);
            Assert.That(id2, Is.Not.EqualTo(id1));
        }

        [Test]
        public void GetHashCode_ForWrappedValue_MatchesStringHashCode()
        {
            // GIVEN
            const string rawId = "test_card";
            var cardId = new CardId(rawId);

            // WHEN
            int expectedHash = rawId.GetHashCode();
            int actualHash = cardId.GetHashCode();

            // THEN
            Assert.That(actualHash, Is.EqualTo(expectedHash));
        }

        [Test]
        public void ToString_ForWrappedValue_ReturnsRawString()
        {
            // GIVEN
            const string rawId = "test_card";
            var cardId = new CardId(rawId);

            // WHEN
            string toStringResult = cardId.ToString();

            // THEN
            Assert.That(toStringResult, Is.EqualTo(rawId));
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
            Assert.That(result, Is.True);
        }

        [Test]
        public void Equals_BoxedObject_WrongType_ReturnsFalse()
        {
            // GIVEN
            var id = new CardId("card_a");

            // WHEN
            bool result = id.Equals("card_a");

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void Equals_Empty_And_Default_AreEqual()
        {
            // GIVEN
            CardId empty = CardId.Empty;
            var defaultId = default(CardId);

            // THEN
            Assert.That(defaultId, Is.EqualTo(empty));
            Assert.That(empty == defaultId, Is.True);
        }

        [Test]
        public void Equals_CaseSensitive_DifferentCase_NotEqual()
        {
            // GIVEN
            var upper = new CardId("CardABC");
            var lower = new CardId("cardabc");

            // THEN
            Assert.That(lower, Is.Not.EqualTo(upper));
            Assert.That(upper == lower, Is.False);
        }
    }
}
