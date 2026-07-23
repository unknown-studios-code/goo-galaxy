using System;
using System.Reflection;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Runtime.Tests.EditMode.Cards
{
    [TestFixture]
    public class CardRegistryTests
    {
        [Test]
        public void TryGetCard_WithRegisteredId_ReturnsTrueAndCorrectCard()
        {
            // GIVEN
            CardDataSO card = CreateCard("subject_alpha", "Subject Alpha", CardType.Troop, 1, canClone: true, canJump: true, hasArmor: false);
            CardRegistry registry = CreateRegistry(card);

            // WHEN
            bool found = registry.TryGetCard(new CardId("subject_alpha"), out ICardData resolved);

            // THEN
            Assert.IsTrue(found);
            Assert.AreEqual(card.CardId, resolved.CardId);
            Assert.AreEqual(card.DisplayName, resolved.DisplayName);
        }

        [Test]
        public void TryGetCard_WithUnregisteredId_ReturnsFalse()
        {
            // GIVEN
            CardDataSO card = CreateCard("subject_alpha", "Subject Alpha", CardType.Troop, 1, canClone: true, canJump: true, hasArmor: false);
            CardRegistry registry = CreateRegistry(card);

            // WHEN
            bool found = registry.TryGetCard(new CardId("unknown_card"), out ICardData resolved);

            // THEN
            Assert.IsFalse(found);
            Assert.IsNull(resolved);
        }

        [Test]
        public void Awake_DuplicateIds_SecondIsSkippedFirstRetained()
        {
            // GIVEN
            CardDataSO first = CreateCard("acid_crawler", "Acid Crawler (First)", CardType.Troop, 2, canClone: true, canJump: true, hasArmor: false);
            CardDataSO duplicate = CreateCard("acid_crawler", "Acid Crawler (Duplicate)", CardType.Troop, 2, canClone: true, canJump: true, hasArmor: false);
            CardRegistry registry = CreateRegistry(first, duplicate);

            // WHEN
            bool found = registry.TryGetCard(new CardId("acid_crawler"), out ICardData resolved);

            // THEN
            Assert.IsTrue(found);
            Assert.AreEqual(first.DisplayName, resolved.DisplayName);
        }

        [Test]
        public void TryGetCard_EmptyRegistry_ReturnsFalse()
        {
            // GIVEN
            CardRegistry registry = CreateRegistry();

            // WHEN
            bool found = registry.TryGetCard(new CardId("anything"), out ICardData resolved);

            // THEN
            Assert.IsFalse(found);
            Assert.IsNull(resolved);
        }

        [Test]
        public void Awake_NullCardsArray_DoesNotThrowAndTryGetCardReturnsFalse()
        {
            // GIVEN
            CardRegistry registry = new GameObject("CardRegistry").AddComponent<CardRegistry>();
            FieldInfo cardsField = typeof(CardRegistry).GetField("_cards", BindingFlags.NonPublic | BindingFlags.Instance);
            cardsField.SetValue(registry, null);
            MethodInfo awakeMethod = typeof(CardRegistry).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);

            // WHEN
            void awakeCall() => awakeMethod.Invoke(registry, null);

            // THEN
            Assert.DoesNotThrow(awakeCall);

            bool found = registry.TryGetCard(new CardId("anything"), out ICardData resolved);
            Assert.IsFalse(found);
            Assert.IsNull(resolved);
        }

        private static CardDataSO CreateCard(string cardId, string displayName, CardType type, int energyCost, bool canClone, bool canJump, bool hasArmor)
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            Type soType = typeof(CardDataSO);

            soType.GetField("_cardId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, cardId);
            soType.GetField("_displayName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, displayName);
            soType.GetField("_type", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, type);
            soType.GetField("_energyCost", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, energyCost);
            soType.GetField("_canClone", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, canClone);
            soType.GetField("_canJump", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, canJump);
            soType.GetField("_hasArmor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, hasArmor);

            return card;
        }

        private static CardRegistry CreateRegistry(params CardDataSO[] cards)
        {
            CardRegistry registry = new GameObject("CardRegistry").AddComponent<CardRegistry>();
            FieldInfo cardsField = typeof(CardRegistry).GetField("_cards", BindingFlags.NonPublic | BindingFlags.Instance);
            cardsField.SetValue(registry, cards);

            MethodInfo awakeMethod = typeof(CardRegistry).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            awakeMethod.Invoke(registry, null);

            return registry;
        }
    }
}
