using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
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
            var mockGrid = new FakeHexGrid();

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

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseGridInitialized(new FakeHexGrid());

            // THEN
            Assert.That(hasFired, Is.False, "GridInitialized should have no subscribers after ResetEvents.");
        }

        [Test]
        public void ResetEvents_ClearsMoveExecuted_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.MoveExecuted += (_, _) => hasFired = true;

            // WHEN
            MatchEvents.ResetEvents();
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

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseConversionResolved(1, default);

            // THEN
            Assert.That(hasFired, Is.False, "ConversionResolved should have no subscribers after ResetEvents.");
        }

        [Test]
        public void RaiseConversionResolved_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseConversionResolved(1, default));
        }

        [Test]
        public void RaiseMatchStarted_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseMatchStarted(new MatchConfiguration()));
        }

        [Test]
        public void RaiseGridInitialized_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseGridInitialized(new FakeHexGrid()));
        }

        [Test]
        public void RaiseGamePhaseChanged_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseGamePhaseChanged(1));
        }

        [Test]
        public void RaiseLandingResolved_WithSubscriber_DeliversTheCommandIntact()
        {
            // GIVEN
            MoveCommand receivedCommand = default;
            MatchEvents.LandingResolved += (command, conversions) => receivedCommand = command;
            var command = new MoveCommand(MoveType.Jump, new HexCoordinates(0, 0), new HexCoordinates(2, 0), 1, 5);

            // WHEN
            MatchEvents.RaiseLandingResolved(command, default);

            // THEN
            Assert.That(receivedCommand.Source, Is.EqualTo(command.Source));
            Assert.That(receivedCommand.Target, Is.EqualTo(command.Target));
        }

        [Test]
        public void RaiseLandingResolved_WithSubscriber_DeliversTheConversionsIntact()
        {
            // GIVEN
            ConversionResult receivedConversions = default;
            MatchEvents.LandingResolved += (command, conversions) => receivedConversions = conversions;
            var convertedUnitIds = new List<int> { 4 };
            var conversions = new ConversionResult(convertedUnitIds, null);

            // WHEN
            MatchEvents.RaiseLandingResolved(default, conversions);

            // THEN
            Assert.That(receivedConversions.ConvertedUnitIds, Is.EqualTo(convertedUnitIds));
        }

        [Test]
        public void RaiseLandingResolved_NoSubscribers_DoesNotThrow()
        {
            // GIVEN
            // no subscriber registered

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseLandingResolved(default, default));
        }

        [Test]
        public void ResetEvents_ClearsLandingResolved_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.LandingResolved += (_, _) => hasFired = true;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseLandingResolved(default, default);

            // THEN
            Assert.That(hasFired, Is.False, "LandingResolved should have no subscribers after ResetEvents.");
        }

        [Test]
        public void RaiseAbilityResolved_WithSubscriber_DeliversThePlayerIdAndResultIntact()
        {
            // GIVEN
            int receivedPlayerId = -1;
            AbilityResult receivedResult = default;
            MatchEvents.AbilityResolved += (playerId, result) =>
            {
                receivedPlayerId = playerId;
                receivedResult = result;
            };
            var affectedUnitIds = new List<int> { 7 };
            var result = new AbilityResult(affectedUnitIds, null, null);

            // WHEN
            MatchEvents.RaiseAbilityResolved(3, result);

            // THEN
            Assert.That(receivedPlayerId, Is.EqualTo(3));
            Assert.That(receivedResult.AffectedUnitIds, Is.EqualTo(affectedUnitIds));
        }

        [Test]
        public void RaiseAbilityResolved_NoSubscribers_DoesNotThrow()
        {
            // GIVEN
            // no subscriber registered

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseAbilityResolved(1, default));
        }

        [Test]
        public void ResetEvents_ClearsAbilityResolved_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.AbilityResolved += (_, _) => hasFired = true;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseAbilityResolved(1, default);

            // THEN
            Assert.That(hasFired, Is.False, "AbilityResolved should have no subscribers after ResetEvents.");
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

        private sealed class FakeHexGrid : IHexGrid
        {
            public int GridRadius => 4;
        }
    }
}
