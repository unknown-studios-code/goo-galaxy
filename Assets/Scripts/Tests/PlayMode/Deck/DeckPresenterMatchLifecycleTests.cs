using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Deck
{
    // DeckPresenter's OnEnable subscription to MatchEvents.MatchStarted is what this fixture exercises — the one
    // behavior GooGalaxy.Tests.EditMode.Deck.DeckPresenterTests cannot cover, because Awake/OnEnable never run
    // for a GameObject activated inside an EditMode test in this project (measured). PlayMode is where they
    // demonstrably do, which is why this one behavior gets its own fixture rather than folding into that one —
    // the same split EnergyLedgerTests (EditMode) and EnergyPresenterTests (PlayMode) already draw for
    // EnergyPresenter's frame-dependent half.
    [TestFixture]
    public class DeckPresenterMatchLifecycleTests
    {
        private const int HandSize = 4;
        private const int PlayerOneId = 1;
        private const int MatchSeed = 555;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();

            foreach (Object created in _spawned)
            {
                if (created != null)
                {
                    Object.Destroy(created);
                }
            }

            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator RaiseMatchStarted_AfterAPlayerIsInitialized_RedealsThatPlayerFromTheNewSeed()
        {
            // GIVEN
            KitDataSO kit = BuildKit("c0", "c1", "c2", "c3", "c4", "c5");
            DeckPresenter presenter = BuildDeckPresenter(kit, HandSize);
            yield return null;

            presenter.InitializePlayer(PlayerOneId);

            // WHEN
            MatchEvents.RaiseMatchStarted(new MatchConfiguration(MatchSeed));

            // THEN
            presenter.TryGetHand(PlayerOneId, out IReadOnlyList<CardId> handAfterRedeal);
            Assert.That(handAfterRedeal, Is.EqualTo(new[] { new CardId("c2"), new CardId("c3"), new CardId("c0"), new CardId("c5") }));
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
            kit.SetAuthoredCards(cards);
            _spawned.Add(kit);

            return kit;
        }
    }
}
