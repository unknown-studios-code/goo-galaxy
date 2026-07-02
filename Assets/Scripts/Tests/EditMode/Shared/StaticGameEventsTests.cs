using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode
{
    [TestFixture]
    public class StaticGameEventsTests
    {
        private bool _hasMatchStartedFired;
        private int _phaseChangedValue;

        [SetUp]
        public void SetUp()
        {
            _hasMatchStartedFired = false;
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
            StaticGameEvents.GamePhaseChanged += HandlePhaseChanged;

            // WHEN
            StaticGameEvents.OnMatchStarted(new MatchConfiguration());
            StaticGameEvents.OnGamePhaseChanged(42);

            // THEN
            Assert.IsTrue(_hasMatchStartedFired);
            Assert.AreEqual(42, _phaseChangedValue);

            _hasMatchStartedFired = false;
            _phaseChangedValue = -1;

            // WHEN
            StaticGameEvents.ResetEvents();

            // WHEN
            StaticGameEvents.OnMatchStarted(new MatchConfiguration());
            StaticGameEvents.OnGamePhaseChanged(99);

            // THEN
            Assert.IsFalse(_hasMatchStartedFired);
            Assert.AreEqual(-1, _phaseChangedValue);
        }

        private void HandleMatchStarted(MatchConfiguration config)
        {
            _hasMatchStartedFired = true;
        }

        private void HandlePhaseChanged(int phase)
        {
            _phaseChangedValue = phase;
        }

        [Test]
        public void GridInitialized_Subscribe_ReceivesEvent()
        {
            // GIVEN
            IHexGrid receivedGrid = null;
            StaticGameEvents.GridInitialized += grid => receivedGrid = grid;
            var mockGrid = new MockHexGrid();

            // WHEN
            StaticGameEvents.OnGridInitialized(mockGrid);

            // THEN
            Assert.AreSame(mockGrid, receivedGrid);
        }

        [Test]
        public void ResetEvents_ClearsGridInitialized_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            StaticGameEvents.GridInitialized += _ => hasFired = true;
            StaticGameEvents.ResetEvents();

            // WHEN
            StaticGameEvents.OnGridInitialized(new MockHexGrid());

            // THEN
            Assert.IsFalse(hasFired, "GridInitialized should have no subscribers after ResetEvents.");
        }

        [Test]
        public void OnMatchStarted_NoSubscribers_DoesNotThrow()
        {
            // GIVEN
            // WHEN
            // THEN
            Assert.DoesNotThrow(() => StaticGameEvents.OnMatchStarted(new MatchConfiguration()));
        }

        [Test]
        public void OnGridInitialized_NoSubscribers_DoesNotThrow()
        {
            // GIVEN
            // WHEN
            // THEN
            Assert.DoesNotThrow(() => StaticGameEvents.OnGridInitialized(new MockHexGrid()));
        }

        [Test]
        public void OnGamePhaseChanged_NoSubscribers_DoesNotThrow()
        {
            // GIVEN
            // WHEN
            // THEN
            Assert.DoesNotThrow(() => StaticGameEvents.OnGamePhaseChanged(1));
        }

        [Test]
        public void OnMatchStarted_MultipleSubscribers_AllReceiveEvent()
        {
            // GIVEN
            int callCount = 0;
            StaticGameEvents.MatchStarted += _ => callCount++;
            StaticGameEvents.MatchStarted += _ => callCount++;

            // WHEN
            StaticGameEvents.OnMatchStarted(new MatchConfiguration());

            // THEN
            Assert.AreEqual(2, callCount);
        }

        private class MockHexGrid : IHexGrid
        {
            public int GridRadius => 4;
        }
    }
}
