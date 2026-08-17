using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.EditMode.Deck
{
    // The OnEnable-dependent redeal behavior lives in GooGalaxy.Tests.PlayMode.Deck.DeckPresenterMatchLifecycleTests
    // instead — see that fixture's remarks for why the split exists.
    [TestFixture]
    public class DeckPresenterTests
    {
        private const int HandSize = 4;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;
        private const int UnknownPlayerId = 99;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();

            foreach (Object created in _spawned)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _spawned.Clear();
        }

        [Test]
        public void InitializePlayer_WithKitAssigned_DealsAHandAndPublishesHandChanged()
        {
            // GIVEN
            KitDataSO kit = BuildKit("c0", "c1", "c2", "c3", "c4", "c5");
            DeckPresenter presenter = BuildDeckPresenter(kit, HandSize);
            int publishedPlayerId = -1;
            var publishedHand = new List<CardId>();
            CardId publishedNext = default;

            // The publisher owns the list only for the dispatch — DeckState rewrites it in place on every later
            // rotation — so a subscriber that wants to inspect it afterward must copy it here, not retain the
            // reference.
            void handleHandChanged(int playerId, IReadOnlyList<CardId> hand, CardId nextCard)
            {
                publishedPlayerId = playerId;
                publishedNext = nextCard;

                for (int i = 0; i < hand.Count; i++)
                {
                    publishedHand.Add(hand[i]);
                }
            }

            MatchEvents.HandChanged += handleHandChanged;

            // WHEN
            presenter.InitializePlayer(PlayerOneId);

            // THEN
            Assert.That(publishedPlayerId, Is.EqualTo(PlayerOneId));
            Assert.That(publishedHand, Is.EqualTo(new[] { new CardId("c3"), new CardId("c5"), new CardId("c0"), new CardId("c4") }));
            Assert.That(publishedNext, Is.EqualTo(new CardId("c2")));
        }

        [Test]
        public void InitializePlayer_TwoPlayersFromTheSamePresenter_DealDifferentHandOrders()
        {
            // GIVEN
            KitDataSO kit = BuildKit("c0", "c1", "c2", "c3", "c4", "c5");
            DeckPresenter presenter = BuildDeckPresenter(kit, HandSize);

            // WHEN
            presenter.InitializePlayer(PlayerOneId);
            presenter.InitializePlayer(PlayerTwoId);

            // THEN
            presenter.TryGetHand(PlayerOneId, out IReadOnlyList<CardId> playerOneHand);
            presenter.TryGetHand(PlayerTwoId, out IReadOnlyList<CardId> playerTwoHand);
            Assert.That(playerTwoHand, Is.Not.EqualTo(playerOneHand));
        }

        [Test]
        public void InitializePlayer_KitTooSmallForHandSize_LogsErrorAndLeavesThePlayerWithoutADeck()
        {
            // GIVEN
            KitDataSO kit = BuildKit("c0", "c1");
            DeckPresenter presenter = BuildDeckPresenter(kit, HandSize);
            int minimumKitSize = DeckState.GetMinimumKitSize(HandSize);
            LogAssert.Expect(LogType.Error, string.Format(DeckLogMessages.KitTooSmallFormat, "TestKit", 2, minimumKitSize, HandSize));

            // WHEN
            presenter.InitializePlayer(PlayerOneId);

            // THEN
            Assert.That(presenter.TryGetHand(PlayerOneId, out _), Is.False);
        }

        [Test]
        public void TryGetHand_UnknownPlayer_ReturnsFalse()
        {
            // GIVEN
            KitDataSO kit = BuildKit("c0", "c1", "c2", "c3", "c4");
            DeckPresenter presenter = BuildDeckPresenter(kit, HandSize);

            // WHEN
            bool found = presenter.TryGetHand(UnknownPlayerId, out IReadOnlyList<CardId> hand);

            // THEN
            Assert.That(found, Is.False);
            Assert.That(hand, Is.Null);
        }

        private DeckPresenter BuildDeckPresenter(KitDataSO kit, int handSize)
        {
            var go = new GameObject("DeckPresenter_Test");
            go.SetActive(false);
            _spawned.Add(go);

            DeckPresenter presenter = go.AddComponent<DeckPresenter>();
            presenter.SetKit(kit, handSize);

            go.SetActive(true);

            return presenter;
        }

        private KitDataSO BuildKit(params string[] cardIds)
        {
            var cards = new CardDataSO[cardIds.Length];

            for (int i = 0; i < cardIds.Length; i++)
            {
                CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
                card.SetAuthoredData(cardIds[i], cardIds[i], "Test description.", CardType.Troop, 1, false, false, false, false, 1, null);
                _spawned.Add(card);
                cards[i] = card;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.name = "TestKit";
            kit.SetAuthoredCards(cards);
            _spawned.Add(kit);

            return kit;
        }
    }
}
