using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.EditMode.Cards
{
    [TestFixture]
    public class CardPresenterTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
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
        public void TryGetCard_WithRegisteredId_ReturnsTrueAndCorrectCard()
        {
            // GIVEN
            CardDataSO card = CreateCard("subject_alpha", "Subject Alpha", CardType.Troop, 1, canClone: true, canJump: true, hasArmor: false);
            CardPresenter presenter = CreatePresenter(card);

            // WHEN
            bool found = presenter.TryGetCard(new CardId("subject_alpha"), out ICardData resolved);

            // THEN
            Assert.That(found, Is.True);
            Assert.That(resolved.CardId, Is.EqualTo(card.CardId));
            Assert.That(resolved.DisplayName, Is.EqualTo(card.DisplayName));
        }

        [Test]
        public void TryGetCard_WithUnregisteredId_ReturnsFalse()
        {
            // GIVEN
            CardDataSO card = CreateCard("subject_alpha", "Subject Alpha", CardType.Troop, 1, canClone: true, canJump: true, hasArmor: false);
            CardPresenter presenter = CreatePresenter(card);

            // WHEN
            bool found = presenter.TryGetCard(new CardId("unknown_card"), out ICardData resolved);

            // THEN
            Assert.That(found, Is.False);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void Awake_DuplicateIds_SecondIsSkippedFirstRetained()
        {
            // GIVEN
            CardDataSO first = CreateCard("acid_crawler", "Acid Crawler (First)", CardType.Troop, 2, canClone: true, canJump: true, hasArmor: false);
            CardDataSO duplicate = CreateCard("acid_crawler", "Acid Crawler (Duplicate)", CardType.Troop, 2, canClone: true, canJump: true, hasArmor: false);
            duplicate.name = "AcidCrawlerDuplicate";
            LogAssert.Expect(LogType.Warning, string.Format(CardLogMessages.DuplicateCardIdFormat, "acid_crawler", "AcidCrawlerDuplicate"));
            CardPresenter presenter = CreatePresenter(first, duplicate);

            // WHEN
            bool found = presenter.TryGetCard(new CardId("acid_crawler"), out ICardData resolved);

            // THEN
            Assert.That(found, Is.True);
            Assert.That(resolved.DisplayName, Is.EqualTo(first.DisplayName));
        }

        [Test]
        public void TryGetCard_EmptyPresenter_ReturnsFalse()
        {
            // GIVEN
            CardPresenter presenter = CreatePresenter();

            // WHEN
            bool found = presenter.TryGetCard(new CardId("anything"), out ICardData resolved);

            // THEN
            Assert.That(found, Is.False);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void Awake_NullCardsArray_DoesNotThrowAndTryGetCardReturnsFalse()
        {
            // GIVEN
            CardPresenter presenter = new GameObject("CardPresenter").AddComponent<CardPresenter>();
            presenter.SetAuthoredCards(null);

            // WHEN / THEN
            Assert.DoesNotThrow(presenter.BuildRegistry);

            bool found = presenter.TryGetCard(new CardId("anything"), out ICardData resolved);
            Assert.That(found, Is.False);
            Assert.That(resolved, Is.Null);
        }

        private CardDataSO CreateCard(string cardId, string displayName, CardType type, int energyCost, bool canClone, bool canJump, bool hasArmor)
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(
                cardId,
                displayName,
                "Test description.",
                type,
                energyCost,
                canClone,
                canJump,
                hasArmor,
                canIgnoreHazards: false,
                conversionRadius: 1,
                landingEffects: null
            );
            _spawned.Add(card);

            return card;
        }

        private CardPresenter CreatePresenter(params CardDataSO[] cards)
        {
            CardPresenter presenter = new GameObject("CardPresenter").AddComponent<CardPresenter>();
            _spawned.Add(presenter.gameObject);
            presenter.SetAuthoredCards(cards);
            presenter.BuildRegistry();

            return presenter;
        }
    }
}
