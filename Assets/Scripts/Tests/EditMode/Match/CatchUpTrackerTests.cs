using GooGalaxy.Runtime.Match.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Match
{
    [TestFixture]
    public class CatchUpTrackerTests
    {
        private const float ThresholdRatio = 0.4f;
        private const float RegenMultiplier = 1.15f;
        private const float DurationSeconds = 2f;
        private const float CooldownSeconds = 3f;

        private CatchUpTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new CatchUpTracker();
        }

        [Test]
        public void IsBelowThreshold_TotalUnitsIsZero_ReturnsFalseWithoutDividing()
        {
            // GIVEN

            // WHEN
            bool result = CatchUpTracker.IsBelowThreshold(playerUnits: 0, totalUnits: 0, thresholdRatio: ThresholdRatio);

            // THEN
            Assert.That(result, Is.False);
        }

        [TestCase(2, 5, ExpectedResult = true)]
        [TestCase(4, 10, ExpectedResult = true)]
        [TestCase(3, 5, ExpectedResult = false)]
        [TestCase(1, 5, ExpectedResult = true)]
        public bool IsBelowThreshold_AtAndAroundTheRatioBoundary_MatchesTheAuthoredThreshold(int playerUnits, int totalUnits)
        {
            // GIVEN

            // WHEN / THEN — the act is the returned value; a parameterized failure names the offending input.
            return CatchUpTracker.IsBelowThreshold(playerUnits, totalUnits, thresholdRatio: ThresholdRatio);
        }

        [TestCase(1, 9, true, false)]
        [TestCase(9, 1, false, true)]
        public void Tick_OnePlayerBelowThreshold_ActivatesOnlyThatPlayersWindow(
            int playerOneUnits,
            int playerTwoUnits,
            bool expectedPlayerOneActive,
            bool expectedPlayerTwoActive
        )
        {
            // GIVEN
            CatchUpConfig config = BuildConfig();

            // WHEN
            _tracker.Tick(playerOneUnits, playerTwoUnits, deltaTime: 0f, config, out bool isPlayerOneActive, out bool isPlayerTwoActive);

            // THEN
            Assert.That((isPlayerOneActive, isPlayerTwoActive), Is.EqualTo((expectedPlayerOneActive, expectedPlayerTwoActive)));
        }

        [Test]
        public void Tick_LeadSwingsWhileOnePlayersWindowIsCooling_KeepsThatPlayersCooldownIndependentOfTheOthersWindow()
        {
            // GIVEN — player one falls behind, their window opens, and is driven straight through to expiry.
            CatchUpConfig config = BuildConfig();
            _tracker.Tick(playerOneUnits: 1, playerTwoUnits: 9, deltaTime: 0f, config, out _, out _);
            _tracker.Tick(playerOneUnits: 1, playerTwoUnits: 9, deltaTime: DurationSeconds, config, out _, out _);

            // WHEN — the lead swings to player two while player one is still cooling down, then swings back to
            // player one before that cooldown has drained.
            _tracker.Tick(playerOneUnits: 9, playerTwoUnits: 1, deltaTime: 0f, config, out bool isPlayerOneActiveOnSwing, out bool isPlayerTwoActiveOnSwing);
            _tracker.Tick(
                playerOneUnits: 1,
                playerTwoUnits: 9,
                deltaTime: 1f,
                config,
                out bool isPlayerOneActiveOnSwingBack,
                out bool isPlayerTwoActiveOnSwingBack
            );

            // THEN — player two opens immediately on the swing; player one, still cooling, does not re-open even
            // though the counts have swung back in its favor before its own cooldown has drained.
            Assert.That(
                (isPlayerOneActiveOnSwing, isPlayerTwoActiveOnSwing, isPlayerOneActiveOnSwingBack, isPlayerTwoActiveOnSwingBack),
                Is.EqualTo((false, true, false, true))
            );
        }

        [Test]
        public void Reset_AfterAWindowHasOpened_ReturnsBothToIdle()
        {
            // GIVEN
            CatchUpConfig config = BuildConfig();
            _tracker.Tick(playerOneUnits: 1, playerTwoUnits: 9, deltaTime: 0f, config, out _, out _);
            _tracker.Reset();

            // WHEN — a tracker with cleared windows ignores a tick reporting no deficit on either side. A window
            // still Active from before the reset would report itself active regardless, since Active ignores
            // the deficit for its own duration — the difference this asserts on.
            _tracker.Tick(playerOneUnits: 5, playerTwoUnits: 5, deltaTime: 0f, config, out bool isPlayerOneActive, out bool isPlayerTwoActive);

            // THEN
            Assert.That((isPlayerOneActive, isPlayerTwoActive), Is.EqualTo((false, false)));
        }

        private static CatchUpConfig BuildConfig()
        {
            return new CatchUpConfig(ThresholdRatio, RegenMultiplier, DurationSeconds, CooldownSeconds);
        }
    }
}
