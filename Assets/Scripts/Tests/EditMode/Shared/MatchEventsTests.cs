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
        private MatchPhase _phaseChangedValue;

        [SetUp]
        public void SetUp()
        {
            _hasMatchStartedFired = false;
            _phaseChangedValue = MatchPhase.None;
            MatchEvents.ResetEvents();
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();
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

        [Test]
        public void RaiseMatchStarted_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseMatchStarted(new MatchConfiguration()));
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
        public void RaiseGridInitialized_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseGridInitialized(new FakeHexGrid()));
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
        public void RaiseMatchPhaseChanged_WithSubscriber_DeliversThePhase()
        {
            // GIVEN
            MatchEvents.MatchPhaseChanged += HandlePhaseChanged;

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // THEN
            Assert.That(_phaseChangedValue, Is.EqualTo(MatchPhase.Standard));
        }

        [Test]
        public void RaiseMatchPhaseChanged_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard));
        }

        [Test]
        public void ResetEvents_WithActiveSubscribers_StopsMatchPhaseChangedFromFiring()
        {
            // GIVEN
            MatchEvents.MatchPhaseChanged += HandlePhaseChanged;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // THEN
            Assert.That(_phaseChangedValue, Is.EqualTo(MatchPhase.None));
        }

        [Test]
        public void RaiseMatchClockTicked_WithSubscriber_DeliversTheRemainingSeconds()
        {
            // GIVEN
            int receivedRemainingSeconds = -1;
            MatchEvents.MatchClockTicked += remaining => receivedRemainingSeconds = remaining;

            // WHEN
            MatchEvents.RaiseMatchClockTicked(2);

            // THEN
            Assert.That(receivedRemainingSeconds, Is.EqualTo(2));
        }

        [Test]
        public void ResetEvents_ClearsMatchClockTicked_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.MatchClockTicked += _ => hasFired = true;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseMatchClockTicked(2);

            // THEN
            Assert.That(hasFired, Is.False, "MatchClockTicked should have no subscribers after ResetEvents.");
        }

        [Test]
        public void RaiseScoreChanged_WithSubscriber_DeliversThePlayerIdAndUnitCount()
        {
            // GIVEN
            int receivedPlayerId = -1;
            int receivedUnitCount = -1;
            MatchEvents.ScoreChanged += (playerId, unitCount) =>
            {
                receivedPlayerId = playerId;
                receivedUnitCount = unitCount;
            };

            // WHEN
            MatchEvents.RaiseScoreChanged(1, 3);

            // THEN
            Assert.That((receivedPlayerId, receivedUnitCount), Is.EqualTo((1, 3)));
        }

        [Test]
        public void ResetEvents_ClearsScoreChanged_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.ScoreChanged += (_, _) => hasFired = true;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseScoreChanged(1, 3);

            // THEN
            Assert.That(hasFired, Is.False, "ScoreChanged should have no subscribers after ResetEvents.");
        }

        [Test]
        public void RaiseMatchEnded_WithSubscriber_DeliversTheOutcome()
        {
            // GIVEN
            MatchOutcome receivedOutcome = default;
            MatchEvents.MatchEnded += outcome => receivedOutcome = outcome;
            var outcome = new MatchOutcome(1, MatchEndReason.TimeLimit);

            // WHEN
            MatchEvents.RaiseMatchEnded(outcome);

            // THEN
            Assert.That(receivedOutcome, Is.EqualTo(outcome));
        }

        [Test]
        public void ResetEvents_ClearsMatchEnded_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.MatchEnded += _ => hasFired = true;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseMatchEnded(MatchOutcome.Drawn);

            // THEN
            Assert.That(hasFired, Is.False, "MatchEnded should have no subscribers after ResetEvents.");
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
        public void RaiseConversionResolved_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseConversionResolved(1, default));
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
        public void RaiseLandingResolved_WithSubscriber_DeliversTheCommandIntact()
        {
            // GIVEN
            MoveCommand receivedCommand = default;
            MatchEvents.LandingResolved += (command, conversions) => receivedCommand = command;
            var command = new MoveCommand(MoveType.Jump, new HexCoordinates(0, 0), new HexCoordinates(2, 0), 1, 5);

            // WHEN
            MatchEvents.RaiseLandingResolved(command, default);

            // THEN
            Assert.That(receivedCommand, Is.EqualTo(command));
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
        public void RaiseHandChanged_WithSubscriber_DeliversThePlayerIdHandAndNextCard()
        {
            // GIVEN
            int receivedPlayerId = -1;
            IReadOnlyList<CardId> receivedHand = null;
            CardId receivedNextCard = default;
            MatchEvents.HandChanged += (playerId, hand, nextCard) =>
            {
                receivedPlayerId = playerId;
                receivedHand = hand;
                receivedNextCard = nextCard;
            };
            IReadOnlyList<CardId> hand = new List<CardId> { new("subject_alpha"), new("acid_crawler") };
            var nextCard = new CardId("bio_phalanx");

            // WHEN
            MatchEvents.RaiseHandChanged(3, hand, nextCard);

            // THEN
            Assert.That((receivedPlayerId, receivedHand, receivedNextCard), Is.EqualTo((3, hand, nextCard)));
        }

        [Test]
        public void RaiseHandChanged_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseHandChanged(1, Array.Empty<CardId>(), default));
        }

        [Test]
        public void ResetEvents_ClearsHandChanged_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.HandChanged += (_, _, _) => hasFired = true;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseHandChanged(1, Array.Empty<CardId>(), default);

            // THEN
            Assert.That(hasFired, Is.False, "HandChanged should have no subscribers after ResetEvents.");
        }

        [Test]
        public void RaiseCardDiscarded_WithSubscriber_DeliversThePlayerIdCardAndSlot()
        {
            // GIVEN
            int receivedPlayerId = -1;
            CardId receivedCard = default;
            int receivedSlotIndex = -1;
            MatchEvents.CardDiscarded += (playerId, card, slotIndex) =>
            {
                receivedPlayerId = playerId;
                receivedCard = card;
                receivedSlotIndex = slotIndex;
            };
            var discardedCard = new CardId("acid_crawler");

            // WHEN
            MatchEvents.RaiseCardDiscarded(2, discardedCard, 3);

            // THEN
            Assert.That((receivedPlayerId, receivedCard, receivedSlotIndex), Is.EqualTo((2, discardedCard, 3)));
        }

        [Test]
        public void RaiseCardDiscarded_NoSubscribers_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseCardDiscarded(1, default, 0));
        }

        [Test]
        public void ResetEvents_ClearsCardDiscarded_Subscriber()
        {
            // GIVEN
            bool hasFired = false;
            MatchEvents.CardDiscarded += (_, _, _) => hasFired = true;

            // WHEN
            MatchEvents.ResetEvents();
            MatchEvents.RaiseCardDiscarded(1, default, 0);

            // THEN
            Assert.That(hasFired, Is.False, "CardDiscarded should have no subscribers after ResetEvents.");
        }

        private void HandleMatchStarted(MatchConfiguration config)
        {
            _hasMatchStartedFired = true;
        }

        private void HandlePhaseChanged(MatchPhase phase)
        {
            _phaseChangedValue = phase;
        }

        private sealed class FakeHexGrid : IHexGrid
        {
            public int GridRadius => 4;
        }
    }
}
