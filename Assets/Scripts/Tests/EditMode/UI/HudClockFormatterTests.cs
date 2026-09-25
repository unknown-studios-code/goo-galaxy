using GooGalaxy.Runtime.UI.Models;
using GooGalaxy.Tests.Utils;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.UI
{
    [TestFixture]
    public class HudClockFormatterTests
    {
        private const int AllocationIterations = 64;

        [Test]
        public void Format_ZeroSeconds_ReturnsZeroZero()
        {
            // GIVEN

            // WHEN
            string formatted = HudClockFormatter.Format(0);

            // THEN
            Assert.That(formatted, Is.EqualTo("00:00"));
        }

        [Test]
        public void Format_NegativeSeconds_ReturnsZeroZeroRatherThanThrowing()
        {
            // GIVEN

            // WHEN
            string formatted = HudClockFormatter.Format(-5);

            // THEN
            Assert.That(formatted, Is.EqualTo("00:00"));
        }

        [Test]
        public void Format_SubMinuteValue_ReturnsSecondsWithZeroMinutes()
        {
            // GIVEN

            // WHEN
            string formatted = HudClockFormatter.Format(45);

            // THEN
            Assert.That(formatted, Is.EqualTo("00:45"));
        }

        [Test]
        public void Format_ExactMinuteValue_ReturnsMinutesWithZeroSeconds()
        {
            // GIVEN

            // WHEN
            string formatted = HudClockFormatter.Format(120);

            // THEN
            Assert.That(formatted, Is.EqualTo("02:00"));
        }

        [Test]
        public void Format_AboveCachedRange_ComposesTheValueRatherThanThrowing()
        {
            // GIVEN — 599 is the highest cached second (9:59); this is one past it.

            // WHEN
            string formatted = HudClockFormatter.Format(600);

            // THEN
            Assert.That(formatted, Is.EqualTo("10:00"));
        }

        [Test]
        public void Format_RepeatedCallsForTheSameCachedValue_ReturnsTheSameStringInstance()
        {
            // GIVEN
            string first = HudClockFormatter.Format(45);

            // WHEN
            string second = HudClockFormatter.Format(45);

            // THEN
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        [Category("Allocation")]
        public void Format_RepeatedCallsForTheSameCachedValue_AllocatesNothing()
        {
            // GIVEN — warmed once outside the measured delegate, so the constraint sees only the repeated
            // cached lookups it exists to prove are free.
            HudClockFormatter.Format(45);

            // WHEN / THEN
            Assert.That(
                () =>
                {
                    for (int i = 0; i < AllocationIterations; i++)
                    {
                        HudClockFormatter.Format(45);
                    }
                },
                new AllocatesNothingConstraint()
            );
        }
    }
}
