using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
{
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
        [TestCase(CardPlayResult.MatchNotInPlay, ExpectedResult = 9)]
        public int CardPlayResult_ExplicitValue_MatchesTheAuthoredWireNumber(CardPlayResult result)
        {
            // THEN
            return (int)result;
        }
    }
}
