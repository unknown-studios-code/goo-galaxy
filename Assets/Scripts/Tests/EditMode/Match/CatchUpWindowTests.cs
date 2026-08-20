using GooGalaxy.Runtime.Match.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Match
{
    [TestFixture]
    public class CatchUpWindowTests
    {
        private const float ThresholdRatio = 0.4f;
        private const float RegenMultiplier = 1.15f;
        private const float DurationSeconds = 2f;
        private const float CooldownSeconds = 3f;

        private CatchUpWindow _window;
        private CatchUpConfig _config;

        [SetUp]
        public void SetUp()
        {
            _window = new CatchUpWindow();
            _config = new CatchUpConfig(ThresholdRatio, RegenMultiplier, DurationSeconds, CooldownSeconds);
        }

        [Test]
        public void Tick_AboveThresholdFromIdle_NeverActivatesAcrossRepeatedTicks()
        {
            // GIVEN

            // WHEN
            bool firstTick = _window.Tick(false, 1f, _config);
            bool secondTick = _window.Tick(false, 1f, _config);
            bool thirdTick = _window.Tick(false, 1f, _config);

            // THEN
            Assert.That((firstTick, secondTick, thirdTick), Is.EqualTo((false, false, false)));
        }

        [Test]
        public void Tick_CrossingIntoDeficit_ActivatesOnThatTickWithoutConsumingAnyOfTheWindow()
        {
            // GIVEN

            // WHEN — the huge delta on the activating tick would already exceed the whole duration if it had
            // been consumed there instead of only starting to count down from the follow-up tick.
            bool activatingTick = _window.Tick(true, 10f, _config);
            bool followUpTick = _window.Tick(false, 1f, _config);

            // THEN
            Assert.That((activatingTick, followUpTick), Is.EqualTo((true, true)));
        }

        [Test]
        public void Tick_ElapsedTimeBelowDuration_StaysActive()
        {
            // GIVEN
            _window.Tick(true, 0f, _config);

            // WHEN
            bool result = _window.Tick(true, DurationSeconds - 0.1f, _config);

            // THEN
            Assert.That(result, Is.True);
        }

        [Test]
        public void Tick_DeficitClearsMidWindow_StaysActiveWhileWithinTheDuration()
        {
            // GIVEN
            _window.Tick(true, 0f, _config);

            // WHEN — the deficit clears, but the window ignores it for the whole authored duration; the total
            // elapsed time across both ticks is still short of DurationSeconds.
            bool afterDeficitClears = _window.Tick(false, 1f, _config);
            bool stillWithinDuration = _window.Tick(false, DurationSeconds - 1.1f, _config);

            // THEN
            Assert.That((afterDeficitClears, stillWithinDuration), Is.EqualTo((true, true)));
        }

        [Test]
        public void Tick_ElapsedTimeReachesDurationExactly_ClosesTheWindow()
        {
            // GIVEN
            _window.Tick(true, 0f, _config);
            _window.Tick(true, DurationSeconds - 1f, _config);

            // WHEN — the remaining second lands exactly on the authored duration.
            bool result = _window.Tick(true, 1f, _config);

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void Tick_WindowExpiresWhileStillBelowThreshold_StaysClosedWhileTheCooldownDrains()
        {
            // GIVEN — the window opens and is driven straight through to expiry, into cooldown.
            _window.Tick(true, 0f, _config);
            _window.Tick(true, DurationSeconds, _config);

            // WHEN — the deficit persists throughout the cooldown; an unconditional cooldown must refuse to
            // re-open even though the player never stopped qualifying.
            bool midCooldown = _window.Tick(true, 1f, _config);
            bool stillMidCooldown = _window.Tick(true, CooldownSeconds - 1.1f, _config);

            // THEN
            Assert.That((midCooldown, stillMidCooldown), Is.EqualTo((false, false)));
        }

        [Test]
        public void Tick_CooldownDrainsWhileStillBelowThreshold_ReArmsOnThatSameTick()
        {
            // GIVEN — the window opens, expires into cooldown, and the deficit is still in effect throughout.
            _window.Tick(true, 0f, _config);
            _window.Tick(true, DurationSeconds, _config);

            // WHEN — this tick's delta exactly drains the cooldown.
            bool result = _window.Tick(true, CooldownSeconds, _config);

            // THEN
            Assert.That(result, Is.True);
        }

        [Test]
        public void Tick_CooldownDrainsWithNoDeficit_ReturnsToIdleAndALaterDeficitOpensAFreshWindow()
        {
            // GIVEN — the window opens, expires into cooldown, and the deficit clears before the cooldown drains.
            _window.Tick(true, 0f, _config);
            _window.Tick(true, DurationSeconds, _config);
            _window.Tick(false, CooldownSeconds, _config);

            // WHEN
            bool result = _window.Tick(true, 0f, _config);

            // THEN
            Assert.That(result, Is.True);
        }

        [Test]
        public void Tick_CooldownSecondsIsZero_ClosesForASingleTickThenReArmsOnTheVeryNextOne()
        {
            // GIVEN — MinCooldownSeconds permits zero, and the tooltip states the resulting behavior explicitly:
            // the window closes for exactly one tick and re-opens on the next while the deficit still holds.
            var zeroCooldownConfig = new CatchUpConfig(ThresholdRatio, RegenMultiplier, DurationSeconds, cooldownSeconds: 0f);
            _window.Tick(true, 0f, zeroCooldownConfig);

            // WHEN — the tick landing exactly on the authored duration closes the window, and the immediate next
            // tick, with the deficit still in effect, is what must re-open it rather than leave it locked out.
            bool expiryTick = _window.Tick(true, DurationSeconds, zeroCooldownConfig);
            bool nextTick = _window.Tick(true, 0f, zeroCooldownConfig);

            // THEN
            Assert.That((expiryTick, nextTick), Is.EqualTo((false, true)));
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        public void Tick_NonPositiveDeltaTimeWhileActive_DoesNotAdvanceTheWindow(float nonPositiveDeltaTime)
        {
            // GIVEN
            _window.Tick(true, 0f, _config);

            // WHEN — the non-positive delta must add nothing; had it advanced the window either forward or
            // backward, the follow-up's exact-duration delta would land somewhere other than precisely zero and
            // the window would not close on schedule.
            _window.Tick(true, nonPositiveDeltaTime, _config);
            bool result = _window.Tick(true, DurationSeconds, _config);

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void Reset_MidWindow_ReturnsToTheStateAFreshInstanceHolds()
        {
            // GIVEN
            _window.Tick(true, 0f, _config);
            _window.Reset();

            // WHEN — a fresh, Idle window ignores a tick reporting no deficit.
            bool result = _window.Tick(false, 1f, _config);

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void Reset_MidCooldown_ReturnsToTheStateAFreshInstanceHolds()
        {
            // GIVEN
            _window.Tick(true, 0f, _config);
            _window.Tick(true, DurationSeconds, _config);
            _window.Reset();

            // WHEN — a cooling window would refuse to re-open; a reset one, being Idle again, opens immediately.
            bool result = _window.Tick(true, 0f, _config);

            // THEN
            Assert.That(result, Is.True);
        }
    }
}
