using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode
{
    [TestFixture]
    public class StaticGameEventsTests
    {
        private bool _matchStartedFired;
        private int _phaseChangedValue;

        [SetUp]
        public void SetUp()
        {
            _matchStartedFired = false;
            _phaseChangedValue = -1;
            StaticGameEvents.ResetEvents();
        }

        [TearDown]
        public void TearDown()
        {
            StaticGameEvents.ResetEvents();
        }

        [Test]
        public void ResetEvents_ClearsSubscribers()
        {
            // GIVEN
            StaticGameEvents.MatchStarted += HandleMatchStarted;
            StaticGameEvents.PhaseChanged += HandlePhaseChanged;

            // WHEN
            StaticGameEvents.InvokeMatchStarted(new MatchConfig());
            StaticGameEvents.InvokePhaseChanged(42);

            // THEN
            Assert.IsTrue(_matchStartedFired);
            Assert.AreEqual(42, _phaseChangedValue);

            // Resetting track flags
            _matchStartedFired = false;
            _phaseChangedValue = -1;

            // WHEN
            StaticGameEvents.ResetEvents();

            // WHEN
            StaticGameEvents.InvokeMatchStarted(new MatchConfig());
            StaticGameEvents.InvokePhaseChanged(99);

            // THEN
            Assert.IsFalse(_matchStartedFired);
            Assert.AreEqual(-1, _phaseChangedValue);
        }

        private void HandleMatchStarted(MatchConfig config)
        {
            _matchStartedFired = true;
        }

        private void HandlePhaseChanged(int phase)
        {
            _phaseChangedValue = phase;
        }
    }
}
