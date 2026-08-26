using GooGalaxy.Runtime.UI.Models;
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
            // cached lookups it exists to prove are free. GC.GetAllocatedBytesForCurrentThread(), the primitive
            // this test used before, was verified against the live editor to return a constant 0 in this
            // runtime — before and after allocating 200,000 objects, in both Edit Mode and Play Mode — which
            // made "after - before, Is.EqualTo(0)" pass unconditionally. NotAllocatingGCMemory() measures
            // allocation on the calling thread during the delegate only, so it would not see one from a
            // scheduled callback or another thread; neither applies to this synchronous static call.
            HudClockFormatter.Format(45);

            // WHEN / THEN — the act is the delegate itself, which the constraint both runs and measures.
            Assert.That(
                () =>
                {
                    for (int i = 0; i < AllocationIterations; i++)
                    {
                        HudClockFormatter.Format(45);
                    }
                },
                NotAllocatingGCMemory()
            );
        }

        // Fully qualified rather than reached through a `using UnityEngine.TestTools.Constraints;`, which would
        // shadow NUnit.Framework.Is (already used unqualified throughout this fixture) and force every other
        // Is.* call here to disambiguate. The static form also sidesteps a real bug this file's author found
        // live: capturing Is.Not.AllocatingGCMemory() into a variable and calling .ApplyTo() on it directly
        // evaluates the un-negated constraint, because .ApplyTo() bypasses the IResolveConstraint.Resolve() step
        // that folds the pending Not into the tree — confirmed by probing both the raw and the resolved form
        // against the same allocating and non-allocating delegates. Routed through Assert.That(), as here, the
        // resolution happens correctly.
        private static UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint NotAllocatingGCMemory()
        {
            return UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not);
        }
    }
}
