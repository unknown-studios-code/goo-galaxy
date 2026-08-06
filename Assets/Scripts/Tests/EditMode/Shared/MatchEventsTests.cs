using System;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode
{
    [TestFixture]
    public class MatchEventsTests
    {
        private bool _hasMatchStartedFired;
        private int _phaseChangedValue;

        [SetUp]
        public void SetUp()
        {
            _hasMatchStartedFired = false;
            _phaseChangedValue = -1;
            MatchEvents.ResetEvents();
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();
        }

        [Test]
        public void RaiseGamePhaseChanged_WithSubscriber_DeliversThePhase()
        {
            // GIVEN
            MatchEvents.GamePhaseChanged += HandlePhaseChanged;

            // WHEN
            MatchEvents.RaiseGamePhaseChanged(42);

            // THEN
            Assert.That(_phaseChangedValue, Is.EqualTo(42));
        }

        [Test]
        public void ResetEvents_WithActiveSubscribers_StopsMatchStartedFromFiring()
        {
            // GIVEN
            MatchEvents.MatchStarted += HandleMatchStarted;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseMatchStarted(new MatchConfiguration());

            // THEN
            Assert.That(_hasMatchStartedFired, Is.False);
        }

        [Test]
        public void ResetEvents_WithActiveSubscribers_StopsGamePhaseChangedFromFiring()
        {
            // GIVEN
            MatchEvents.GamePhaseChanged += HandlePhaseChanged;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseGamePhaseChanged(99);

            // THEN
            Assert.That(_phaseChangedValue, Is.EqualTo(-1));
        }

        [Test]
        public void GridInitialized_Subscribe_ReceivesEvent()
        {
            // GIVEN
            IHexGrid receivedGrid = null;
            MatchEvents.GridInitialized += grid => receivedGrid = grid;
            var mockGrid = new MockHexGrid();

            // WHEN
            MatchEvents.RaiseGridInitialized(mockGrid);

            // THEN
            Assert.That(receivedGrid, Is.SameAs(mockGrid));
        }

        [Test]
        public void ResetEvents_ClearsGridInitialized_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.GridInitialized += _ => hasFired = true;
            MatchEvents.ResetEvents();

            // WHEN
            MatchEvents.RaiseGridInitialized(new MockHexGrid());

            // THEN
            Assert.That(hasFired, Is.False, "GridInitialized should have no subscribers after ResetEvents.");
        }

        [Test]
        public void ResetEvents_ClearsMoveExecuted_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.MoveExecuted += (_, _) => hasFired = true;
            MatchEvents.ResetEvents();

            // WHEN
            MatchEvents.RaiseMoveExecuted(default, Array.Empty<HexCoordinates>());

            // THEN
            Assert.That(hasFired, Is.False, "MoveExecuted should have no subscribers after ResetEvents.");
        }

        [Test]
        public void ResetEvents_ClearsConversionResolved_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.ConversionResolved += (_, _) => hasFired = true;
            MatchEvents.ResetEvents();

            // WHEN
            MatchEvents.RaiseConversionResolved(1, default);

            // THEN
            Assert.That(hasFired, Is.False, "ConversionResolved should have no subscribers after ResetEvents.");
        }

        [Test]
        public void RaiseConversionResolved_NoSubscribers_DoesNotThrow()
        {
            // GIVEN
            MatchEvents.ResetEvents();

            // WHEN
            // THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseConversionResolved(1, default));
        }

        [Test]
        public void RaiseMatchStarted_NoSubscribers_DoesNotThrow()
        {
            // GIVEN
            // WHEN
            // THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseMatchStarted(new MatchConfiguration()));
        }

        [Test]
        public void HandleGridInitialized_NoSubscribers_DoesNotThrow()
        {
            // GIVEN
            // WHEN
            // THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseGridInitialized(new MockHexGrid()));
        }

        [Test]
        public void RaiseGamePhaseChanged_NoSubscribers_DoesNotThrow()
        {
            // GIVEN
            // WHEN
            // THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseGamePhaseChanged(1));
        }

        [Test]
        public void RaiseMatchStarted_MultipleSubscribers_AllReceiveEvent()
        {
            // GIVEN
            int callCount = 0;
            MatchEvents.MatchStarted += _ => callCount++;
            MatchEvents.MatchStarted += _ => callCount++;

            // WHEN
            MatchEvents.RaiseMatchStarted(new MatchConfiguration());

            // THEN
            Assert.That(callCount, Is.EqualTo(2));
        }

        private void HandleMatchStarted(MatchConfiguration config)
        {
            _hasMatchStartedFired = true;
        }

        private void HandlePhaseChanged(int phase)
        {
            _phaseChangedValue = phase;
        }

        private sealed class MockHexGrid : IHexGrid
        {
            public int GridRadius => 4;
        }
    }
}
