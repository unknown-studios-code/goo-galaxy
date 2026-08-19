using GooGalaxy.Runtime.Deck.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Deck
{
    // CardPlayResult travels to the client as a rejection reason, so its explicit numeric values must never
    // drift — a renumbering silently changes what an older peer reads. This fixture pins every value.
    [TestFixture]
    public class CardPlayResultTests
    {
        [TestCase(CardPlayResult.Success, ExpectedResult = 0)]
        [TestCase(CardPlayResult.UnknownPlayer, ExpectedResult = 1)]
        [TestCase(CardPlayResult.SlotOutOfRange, ExpectedResult = 2)]
        [TestCase(CardPlayResult.CardNotFound, ExpectedResult = 3)]
        [TestCase(CardPlayResult.InvalidTargetCount, ExpectedResult = 4)]
        [TestCase(CardPlayResult.InsufficientEnergy, ExpectedResult = 5)]
        [TestCase(CardPlayResult.IllegalPlacement, ExpectedResult = 6)]
        [TestCase(CardPlayResult.BoardUnavailable, ExpectedResult = 7)]
        [TestCase(CardPlayResult.ResolverBusy, ExpectedResult = 8)]
        public int CardPlayResult_ExplicitValue_MatchesTheAuthoredWireNumber(CardPlayResult result)
        {
            // GIVEN

            // WHEN / THEN — the act is the returned value; a parameterized failure names the offending member.
            return (int)result;
        }
    }
}
