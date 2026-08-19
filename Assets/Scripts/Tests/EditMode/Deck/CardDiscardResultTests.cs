using GooGalaxy.Runtime.Deck.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Deck
{
    // CardDiscardResult is destined to become a network wire enum, so its explicit numeric values must never
    // drift — a renumbering silently changes what an older peer reads. This fixture pins every value.
    [TestFixture]
    public class CardDiscardResultTests
    {
        [TestCase(CardDiscardResult.Success, ExpectedResult = 0)]
        [TestCase(CardDiscardResult.UnknownPlayer, ExpectedResult = 1)]
        [TestCase(CardDiscardResult.SlotOutOfRange, ExpectedResult = 2)]
        [TestCase(CardDiscardResult.InsufficientEnergy, ExpectedResult = 3)]
        [TestCase(CardDiscardResult.DeckBusy, ExpectedResult = 4)]
        [TestCase(CardDiscardResult.DeckUnavailable, ExpectedResult = 5)]
        public int CardDiscardResult_ExplicitValue_MatchesTheAuthoredWireNumber(CardDiscardResult result)
        {
            // GIVEN

            // WHEN / THEN — the act is the returned value; a parameterized failure names the offending member.
            return (int)result;
        }
    }
}
